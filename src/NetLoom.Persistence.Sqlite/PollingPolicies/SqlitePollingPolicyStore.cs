using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using NetLoom.Application.PollingPolicies;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.PollingPolicies
{
    public sealed class SqlitePollingPolicyStore : IPollingPolicyStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqlitePollingPolicyStore(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<PollingPolicy> GetPolicies()
        {
            using (var connection = _connectionFactory.OpenConnection()) return ReadPolicies(connection).AsReadOnly();
        }

        public PollingPolicy GetPolicy(Guid policyId)
        {
            using (var connection = _connectionFactory.OpenConnection())
            {
                foreach (var policy in ReadPolicies(connection))
                    if (policy.Id == policyId) return policy;
            }
            return null;
        }

        public void SavePolicy(PollingPolicy policy, DateTime nowUtc)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            RequireUtc(nowUtc);
            using (var connection = _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(connection, () =>
                {
                    var existingDefaultId = ScalarString(connection, "SELECT polling_policy_id FROM polling_policies WHERE is_default = 1;");
                    if (existingDefaultId != null &&
                        ((policy.Id.ToString("D") == existingDefaultId && !policy.IsDefault) ||
                         (policy.Id.ToString("D") != existingDefaultId && policy.IsDefault)))
                        throw new InvalidOperationException("POLLING_POLICY_DEFAULT_IMMUTABLE");
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT polling_policy_id FROM polling_policies WHERE name = @name COLLATE NOCASE;";
                        command.Parameters.AddWithValue("@name", policy.Name);
                        var owner = command.ExecuteScalar() as string;
                        if (owner != null && owner != policy.Id.ToString("D"))
                            throw new InvalidOperationException("POLLING_POLICY_NAME_TAKEN");
                    }
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"INSERT INTO polling_policies
(polling_policy_id, name, is_default, active_polling, state_mode, state_interval_seconds, topology_mode, topology_interval_seconds, created_utc, updated_utc)
VALUES (@id, @name, @default, @active, @state, @stateSeconds, @topology, @topologySeconds, @now, @now)
ON CONFLICT(polling_policy_id) DO UPDATE SET
name = excluded.name, is_default = excluded.is_default, active_polling = excluded.active_polling,
state_mode = excluded.state_mode, state_interval_seconds = excluded.state_interval_seconds,
topology_mode = excluded.topology_mode, topology_interval_seconds = excluded.topology_interval_seconds,
updated_utc = excluded.updated_utc;";
                        command.Parameters.AddWithValue("@id", policy.Id.ToString("D"));
                        command.Parameters.AddWithValue("@name", policy.Name);
                        command.Parameters.AddWithValue("@default", policy.IsDefault ? 1 : 0);
                        command.Parameters.AddWithValue("@active", policy.ActivePolling ? 1 : 0);
                        command.Parameters.AddWithValue("@state", policy.StateSchedule.Mode.ToString());
                        command.Parameters.AddWithValue("@stateSeconds", (object)policy.StateSchedule.IntervalSeconds ?? DBNull.Value);
                        command.Parameters.AddWithValue("@topology", policy.TopologySchedule.Mode.ToString());
                        command.Parameters.AddWithValue("@topologySeconds", (object)policy.TopologySchedule.IntervalSeconds ?? DBNull.Value);
                        command.Parameters.AddWithValue("@now", nowUtc.ToString("o", CultureInfo.InvariantCulture));
                        command.ExecuteNonQuery();
                    }
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM polling_policy_tcp_ports WHERE polling_policy_id = @id;";
                        command.Parameters.AddWithValue("@id", policy.Id.ToString("D"));
                        command.ExecuteNonQuery();
                    }
                    foreach (var port in policy.TcpPorts)
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO polling_policy_tcp_ports (polling_policy_id, port) VALUES (@id, @port);";
                            command.Parameters.AddWithValue("@id", policy.Id.ToString("D"));
                            command.Parameters.AddWithValue("@port", port);
                            command.ExecuteNonQuery();
                        }
                    }
                });
            }
        }

        public PollingPolicyUsage GetUsage(Guid policyId)
        {
            using (var connection = _connectionFactory.OpenConnection()) return ReadUsage(connection, policyId);
        }

        public PollingPolicyDeleteOutcome DeletePolicy(Guid policyId)
        {
            using (var connection = _connectionFactory.OpenConnection())
            {
                return SqliteImmediateWrite.Execute(connection, () =>
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT is_default FROM polling_policies WHERE polling_policy_id = @id;";
                        command.Parameters.AddWithValue("@id", policyId.ToString("D"));
                        var value = command.ExecuteScalar();
                        if (value == null) return PollingPolicyDeleteOutcome.NotFound;
                        if (Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0) return PollingPolicyDeleteOutcome.DefaultPolicy;
                    }
                    if (ReadUsage(connection, policyId).IsUsed) return PollingPolicyDeleteOutcome.InUse;
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM polling_policies WHERE polling_policy_id = @id;";
                        command.Parameters.AddWithValue("@id", policyId.ToString("D"));
                        command.ExecuteNonQuery();
                    }
                    return PollingPolicyDeleteOutcome.Deleted;
                });
            }
        }

        public IReadOnlyList<PollingPolicyAssignment> GetAssignments()
        {
            using (var connection = _connectionFactory.OpenConnection()) return ReadAssignments(connection).AsReadOnly();
        }

        public void Assign(PollingPolicySubjectKind kind, Guid subjectId, Guid? policyId, DateTime nowUtc)
        {
            if (subjectId == Guid.Empty) throw new ArgumentException("Subject id is required.", nameof(subjectId));
            if (policyId == Guid.Empty) throw new ArgumentException("Policy id is required.", nameof(policyId));
            RequireUtc(nowUtc);
            var table = kind == PollingPolicySubjectKind.Device ? "device_polling_policies" :
                kind == PollingPolicySubjectKind.Location ? "location_polling_policies" :
                throw new ArgumentOutOfRangeException(nameof(kind));
            var column = kind == PollingPolicySubjectKind.Device ? "device_id" : "location_id";
            using (var connection = _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(connection, () =>
                {
                    if (policyId.HasValue)
                    {
                        using (var check = connection.CreateCommand())
                        {
                            check.CommandText = "SELECT COUNT(*) FROM polling_policies WHERE polling_policy_id = @id;";
                            check.Parameters.AddWithValue("@id", policyId.Value.ToString("D"));
                            if (Convert.ToInt32(check.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                                throw new InvalidOperationException("POLLING_POLICY_NOT_FOUND");
                        }
                    }
                    using (var command = connection.CreateCommand())
                    {
                        if (policyId.HasValue)
                            command.CommandText = "INSERT INTO " + table + " (" + column + ", polling_policy_id, assigned_utc) VALUES (@subject, @policy, @now) " +
                                "ON CONFLICT(" + column + ") DO UPDATE SET polling_policy_id = excluded.polling_policy_id, assigned_utc = excluded.assigned_utc;";
                        else
                            command.CommandText = "DELETE FROM " + table + " WHERE " + column + " = @subject;";
                        command.Parameters.AddWithValue("@subject", subjectId.ToString("D"));
                        if (policyId.HasValue)
                        {
                            command.Parameters.AddWithValue("@policy", policyId.Value.ToString("D"));
                            command.Parameters.AddWithValue("@now", nowUtc.ToString("o", CultureInfo.InvariantCulture));
                        }
                        command.ExecuteNonQuery();
                    }
                });
            }
        }

        public PollingPolicyResolver LoadResolver()
        {
            using (var connection = _connectionFactory.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                var policies = ReadPolicies(connection, transaction);
                var assignments = ReadAssignments(connection, transaction);
                var parents = new Dictionary<Guid, Guid?>();
                var locations = new Dictionary<Guid, Guid?>();
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT location_id, parent_location_id FROM locations;";
                    using (var reader = command.ExecuteReader())
                        while (reader.Read()) parents.Add(Guid.Parse(reader.GetString(0)), reader.IsDBNull(1) ? (Guid?)null : Guid.Parse(reader.GetString(1)));
                }
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT id, location_id FROM devices;";
                    using (var reader = command.ExecuteReader())
                        while (reader.Read()) locations.Add(Guid.Parse(reader.GetString(0)), reader.IsDBNull(1) ? (Guid?)null : Guid.Parse(reader.GetString(1)));
                }
                transaction.Commit();
                return new PollingPolicyResolver(policies, assignments, parents, locations);
            }
        }

        private static List<PollingPolicy> ReadPolicies(SQLiteConnection connection, SQLiteTransaction transaction = null)
        {
            var ports = new Dictionary<Guid, List<int>>();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT polling_policy_id, port FROM polling_policy_tcp_ports ORDER BY port;";
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        var id = Guid.Parse(reader.GetString(0));
                        List<int> list;
                        if (!ports.TryGetValue(id, out list)) ports[id] = list = new List<int>();
                        list.Add(reader.GetInt32(1));
                    }
            }
            var result = new List<PollingPolicy>();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"SELECT polling_policy_id, name, is_default, active_polling, state_mode, state_interval_seconds, topology_mode, topology_interval_seconds
FROM polling_policies ORDER BY is_default DESC, name COLLATE NOCASE, polling_policy_id;";
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        var id = Guid.Parse(reader.GetString(0));
                        List<int> list;
                        if (!ports.TryGetValue(id, out list)) list = new List<int>();
                        result.Add(new PollingPolicy(id, reader.GetString(1), reader.GetInt32(2) != 0, reader.GetInt32(3) != 0,
                            ReadSchedule(reader.GetString(4), reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5)),
                            ReadSchedule(reader.GetString(6), reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)), list));
                    }
            }
            return result;
        }

        private static PollingSchedule ReadSchedule(string mode, int? seconds)
        {
            switch (mode)
            {
                case "General": return PollingSchedule.General;
                case "Interval": return PollingSchedule.Every(seconds.Value);
                case "Once": return PollingSchedule.Once;
                case "Off": return PollingSchedule.Off;
                default: throw new InvalidOperationException("Unknown polling schedule mode.");
            }
        }

        private static List<PollingPolicyAssignment> ReadAssignments(SQLiteConnection connection, SQLiteTransaction transaction = null)
        {
            var result = new List<PollingPolicyAssignment>();
            ReadAssignmentTable(connection, transaction, "device_polling_policies", "device_id", PollingPolicySubjectKind.Device, result);
            ReadAssignmentTable(connection, transaction, "location_polling_policies", "location_id", PollingPolicySubjectKind.Location, result);
            return result;
        }

        private static void ReadAssignmentTable(SQLiteConnection connection, SQLiteTransaction transaction, string table, string column,
            PollingPolicySubjectKind kind, List<PollingPolicyAssignment> result)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT " + column + ", polling_policy_id FROM " + table + " ORDER BY " + column + ";";
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(new PollingPolicyAssignment(kind, Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1))));
            }
        }

        private static PollingPolicyUsage ReadUsage(SQLiteConnection connection, Guid policyId)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT
(SELECT COUNT(*) FROM device_polling_policies WHERE polling_policy_id = @id),
(SELECT COUNT(*) FROM location_polling_policies WHERE polling_policy_id = @id);";
                command.Parameters.AddWithValue("@id", policyId.ToString("D"));
                using (var reader = command.ExecuteReader())
                {
                    reader.Read();
                    return new PollingPolicyUsage(reader.GetInt32(0), reader.GetInt32(1));
                }
            }
        }

        private static string ScalarString(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return command.ExecuteScalar() as string;
            }
        }

        private static void RequireUtc(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(nowUtc));
        }
    }
}
