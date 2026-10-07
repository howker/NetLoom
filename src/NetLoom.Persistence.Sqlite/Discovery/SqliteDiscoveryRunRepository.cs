using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Snmp;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Discovery
{
    public sealed class SqliteDiscoveryRunRepository :
        IDiscoveryRunRepository
    {
        private const string RunColumns = @"
id, started_utc, finished_utc, state,
access_profile_id, access_profile_name, scope_text,
total_addresses, processed_addresses, found_candidates,
snmp_responded, error_count, known_unchanged_count, fault_message";

        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteDiscoveryRunRepository(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public void SaveRun(
            DiscoveryRunRecord run)
        {
            if (run == null)
            {
                throw new ArgumentNullException(
                    nameof(run));
            }

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO discovery_runs
(" + RunColumns + @")
VALUES
(
    @id, @startedUtc, @finishedUtc, @state,
    @profileId, @profileName, @scope,
    @total, @processed, @found,
    @snmp, @errors, @known, @fault
)
ON CONFLICT(id) DO UPDATE SET
    started_utc = excluded.started_utc,
    finished_utc = excluded.finished_utc,
    state = excluded.state,
    access_profile_id = excluded.access_profile_id,
    access_profile_name = excluded.access_profile_name,
    scope_text = excluded.scope_text,
    total_addresses = excluded.total_addresses,
    processed_addresses = excluded.processed_addresses,
    found_candidates = excluded.found_candidates,
    snmp_responded = excluded.snmp_responded,
    error_count = excluded.error_count,
    known_unchanged_count = excluded.known_unchanged_count,
    fault_message = excluded.fault_message;";

                Add(command, "@id", run.Id.ToString("D"));
                Add(command, "@startedUtc", UtcText(run.StartedUtc));
                Add(command, "@finishedUtc", UtcText(run.FinishedUtc));
                Add(command, "@state", run.State.ToString());
                Add(command, "@profileId", run.AccessProfileId?.ToString("D"));
                Add(command, "@profileName", run.AccessProfileName);
                Add(command, "@scope", run.ScopeText);
                Add(command, "@total", run.TotalAddresses);
                Add(command, "@processed", run.ProcessedAddresses);
                Add(command, "@found", run.FoundCandidates);
                Add(command, "@snmp", run.SnmpResponded);
                Add(command, "@errors", run.ErrorCount);
                Add(command, "@known", run.KnownUnchangedCount);
                Add(command, "@fault", run.FaultMessage);
                command.ExecuteNonQuery();
            }
        }

        public DiscoveryRunRecord GetRun(
            Guid id)
        {
            using (var connection = _connectionFactory.OpenReadOnlyConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT " + RunColumns +
                    " FROM discovery_runs WHERE id = @id;";
                Add(command, "@id", id.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read()
                        ? ReadRun(reader)
                        : null;
                }
            }
        }

        public DiscoveryRunRecord GetLatestRun()
        {
            using (var connection = _connectionFactory.OpenReadOnlyConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT " + RunColumns +
                    " FROM discovery_runs ORDER BY started_utc DESC, id DESC LIMIT 1;";

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read()
                        ? ReadRun(reader)
                        : null;
                }
            }
        }

        public void SaveResult(
            DiscoveryRunResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
INSERT OR REPLACE INTO discovery_run_results
(
    run_id, address, result_group, device_id, observed_utc,
    icmp_reachable, open_tcp_ports, snmp_responded, snmp_error,
    sys_name, sys_description, sys_object_id, interface_count,
    completeness, partial_reason, reason, reason_detail,
    resolution, resolved_utc
)
VALUES
(
    @runId, @address, @group, @deviceId, @observed,
    @icmp, @ports, @snmp, @snmpError,
    @name, @description, @objectId, @interfaces,
    @completeness, @partialReason, @reason, @detail,
    @resolution, @resolved
);";
                            Add(command, "@runId", result.RunId.ToString("D"));
                            Add(command, "@address", result.Address);
                            Add(command, "@group", result.Group.ToString());
                            Add(command, "@deviceId", result.DeviceId?.ToString("D"));
                            Add(command, "@observed", UtcText(result.ObservedUtc));
                            Add(command, "@icmp", result.IcmpReachable ? 1 : 0);
                            Add(command, "@ports", result.OpenTcpPorts.Count == 0
                                ? null
                                : string.Join(
                                    ",",
                                    result.OpenTcpPorts.Select(
                                        port => port.ToString(CultureInfo.InvariantCulture))));
                            Add(command, "@snmp", result.SnmpResponded ? 1 : 0);
                            Add(command, "@snmpError", result.SnmpError?.ToString());
                            Add(command, "@name", result.SysName);
                            Add(command, "@description", result.SysDescription);
                            Add(command, "@objectId", result.SysObjectId);
                            Add(command, "@interfaces", result.InterfaceCount);
                            Add(command, "@completeness", result.Completeness.ToString());
                            Add(command, "@partialReason", result.PartialReason.ToString());
                            Add(command, "@reason", result.Reason.ToString());
                            Add(command, "@detail", result.ReasonDetail);
                            Add(command, "@resolution", result.Resolution.ToString());
                            Add(command, "@resolved", UtcText(result.ResolvedUtc));
                            command.ExecuteNonQuery();
                        }

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
DELETE FROM discovery_run_result_changes
WHERE run_id = @runId AND address = @address;";
                            Add(command, "@runId", result.RunId.ToString("D"));
                            Add(command, "@address", result.Address);
                            command.ExecuteNonQuery();
                        }

                        foreach (var change in result.Changes)
                        {
                            using (var command = connection.CreateCommand())
                            {
                                command.CommandText = @"
INSERT INTO discovery_run_result_changes
(run_id, address, field, old_value, new_value)
VALUES (@runId, @address, @field, @old, @new);";
                                Add(command, "@runId", result.RunId.ToString("D"));
                                Add(command, "@address", result.Address);
                                Add(command, "@field", change.Field);
                                Add(command, "@old", change.OldValue);
                                Add(command, "@new", change.NewValue);
                                command.ExecuteNonQuery();
                            }
                        }
                    });
            }
        }

        public IReadOnlyList<DiscoveryRunResult> GetResults(
            Guid runId)
        {
            var results = new List<DiscoveryRunResult>();

            using (var connection = _connectionFactory.OpenReadOnlyConnection())
            using (var transaction = connection.BeginTransaction(
                System.Data.IsolationLevel.ReadCommitted))
            {
                var changes = ReadChanges(connection, transaction, runId);

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
SELECT
    address, result_group, device_id, observed_utc,
    icmp_reachable, open_tcp_ports, snmp_responded, snmp_error,
    sys_name, sys_description, sys_object_id, interface_count,
    completeness, partial_reason, reason, reason_detail,
    resolution, resolved_utc
FROM discovery_run_results
WHERE run_id = @runId
ORDER BY address;";
                    Add(command, "@runId", runId.ToString("D"));

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var address = reader.GetString(0);
                            var ports = Text(reader, 5);
                            List<DiscoveryFieldChange> fieldChanges;
                            changes.TryGetValue(address, out fieldChanges);

                            results.Add(
                                new DiscoveryRunResult(
                                    runId,
                                    address,
                                    ParseEnum<DiscoveryResultGroup>(reader.GetString(1)),
                                    ReadGuid(reader, 2),
                                    ReadUtc(reader.GetString(3)),
                                    reader.GetInt32(4) != 0,
                                    string.IsNullOrEmpty(ports)
                                        ? new int[0]
                                        : ports.Split(',').Select(
                                            port => int.Parse(port, CultureInfo.InvariantCulture)).ToArray(),
                                    reader.GetInt32(6) != 0,
                                    reader.IsDBNull(7)
                                        ? (SnmpTransportFailure?)null
                                        : ParseEnum<SnmpTransportFailure>(reader.GetString(7)),
                                    Text(reader, 8),
                                    Text(reader, 9),
                                    Text(reader, 10),
                                    reader.GetInt32(11),
                                    ParseEnum<DiscoveryResultCompleteness>(reader.GetString(12)),
                                    ParseEnum<DiscoveryPartialReason>(reader.GetString(13)),
                                    ParseEnum<DiscoveryResultReason>(reader.GetString(14)),
                                    Text(reader, 15),
                                    fieldChanges != null
                                        ? (IReadOnlyList<DiscoveryFieldChange>)fieldChanges
                                        : new DiscoveryFieldChange[0],
                                    ParseEnum<DiscoveryResultResolution>(reader.GetString(16)),
                                    ReadNullableUtc(reader, 17)));
                        }
                    }
                }

                transaction.Commit();
            }

            return results.AsReadOnly();
        }

        public void DeleteResult(Guid runId, string address)
        {
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
DELETE FROM discovery_run_results
WHERE run_id = @runId AND address = @address;";
                Add(command, "@runId", runId.ToString("D"));
                Add(command, "@address", address);
                command.ExecuteNonQuery();
            }
        }

        public void PruneRuns(
            int keepLatest)
        {
            if (keepLatest < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(keepLatest));
            }

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
DELETE FROM discovery_runs
WHERE id IN
(
    SELECT id FROM discovery_runs
    ORDER BY started_utc DESC, id DESC
    LIMIT -1 OFFSET @keep
);";
                Add(command, "@keep", keepLatest);
                command.ExecuteNonQuery();
            }
        }

        private static Dictionary<string, List<DiscoveryFieldChange>> ReadChanges(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            Guid runId)
        {
            var changes = new Dictionary<string, List<DiscoveryFieldChange>>(
                StringComparer.Ordinal);

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT address, field, old_value, new_value
FROM discovery_run_result_changes
WHERE run_id = @runId
ORDER BY address, field;";
                Add(command, "@runId", runId.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var address = reader.GetString(0);
                        List<DiscoveryFieldChange> fieldChanges;

                        if (!changes.TryGetValue(address, out fieldChanges))
                        {
                            fieldChanges = new List<DiscoveryFieldChange>();
                            changes.Add(address, fieldChanges);
                        }

                        fieldChanges.Add(
                            new DiscoveryFieldChange(
                                reader.GetString(1),
                                Text(reader, 2),
                                Text(reader, 3)));
                    }
                }
            }

            return changes;
        }

        private static DiscoveryRunRecord ReadRun(
            SQLiteDataReader reader)
        {
            return new DiscoveryRunRecord(
                Guid.Parse(reader.GetString(0)),
                ReadUtc(reader.GetString(1)),
                ReadNullableUtc(reader, 2),
                ParseEnum<DiscoveryControlState>(reader.GetString(3)),
                ReadGuid(reader, 4),
                Text(reader, 5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                Text(reader, 13));
        }

        private static void Add(
            SQLiteCommand command,
            string name,
            object value)
        {
            command.Parameters.AddWithValue(
                name,
                value ?? DBNull.Value);
        }

        private static string UtcText(
            DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("o", CultureInfo.InvariantCulture)
                : null;
        }

        private static DateTime ReadUtc(
            string value)
        {
            return DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        private static DateTime? ReadNullableUtc(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (DateTime?)null
                : ReadUtc(reader.GetString(ordinal));
        }

        private static Guid? ReadGuid(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (Guid?)null
                : Guid.Parse(reader.GetString(ordinal));
        }

        private static string Text(
            SQLiteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : reader.GetString(ordinal);
        }

        private static T ParseEnum<T>(
            string value)
            where T : struct
        {
            return (T)Enum.Parse(typeof(T), value);
        }
    }
}
