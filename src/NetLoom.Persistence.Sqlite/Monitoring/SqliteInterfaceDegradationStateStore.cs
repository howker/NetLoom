using System;
using System.Data.SQLite;
using System.Globalization;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Monitoring
{
    public sealed class SqliteInterfaceDegradationStateStore :
        IInterfaceDegradationStateStore
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteInterfaceDegradationStateStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public InterfaceDegradationState Load(
            Guid deviceId,
            int ifIndex)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface degradation state requires a stable DeviceId.",
                    nameof(deviceId));
            }

            if (ifIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                return Load(
                    connection,
                    deviceId,
                    ifIndex);
            }
        }

        public InterfaceDegradationState ReplaceAndGetPrevious(
            InterfaceDegradationState current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(
                    nameof(current));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                return SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        var previous =
                            Load(
                                connection,
                                current.DeviceId,
                                current.IfIndex);

                        if (previous != null &&
                            current.CapturedUtc <=
                                previous.CapturedUtc)
                        {
                            throw new InvalidOperationException(
                                "Interface degradation state must move forward in time.");
                        }

                        Save(
                            connection,
                            current);

                        return previous;
                    });
            }
        }

        private static InterfaceDegradationState Load(
            SQLiteConnection connection,
            Guid deviceId,
            int ifIndex)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    captured_utc,
    status,
    evidence_fingerprint
FROM interface_degradation_states
WHERE
    device_id = @deviceId AND
    if_index = @ifIndex;";

                command.Parameters.AddWithValue(
                    "@deviceId",
                    deviceId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@ifIndex",
                    ifIndex);

                using (var reader =
                    command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new InterfaceDegradationState(
                        deviceId,
                        ifIndex,
                        ParseUtc(
                            reader.GetString(0)),
                        (InterfaceDegradationStatus)
                            Convert.ToInt32(
                                reader.GetValue(1),
                                CultureInfo.InvariantCulture),
                        reader.GetString(2));
                }
            }
        }

        private static void Save(
            SQLiteConnection connection,
            InterfaceDegradationState current)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO interface_degradation_states
(
    device_id,
    if_index,
    captured_utc,
    status,
    evidence_fingerprint
)
VALUES
(
    @deviceId,
    @ifIndex,
    @capturedUtc,
    @status,
    @evidenceFingerprint
)
ON CONFLICT(device_id, if_index) DO UPDATE SET
    captured_utc = excluded.captured_utc,
    status = excluded.status,
    evidence_fingerprint = excluded.evidence_fingerprint;";

                command.Parameters.AddWithValue(
                    "@deviceId",
                    current.DeviceId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@ifIndex",
                    current.IfIndex);

                command.Parameters.AddWithValue(
                    "@capturedUtc",
                    FormatUtc(
                        current.CapturedUtc));

                command.Parameters.AddWithValue(
                    "@status",
                    (int)current.Status);

                command.Parameters.AddWithValue(
                    "@evidenceFingerprint",
                    current.EvidenceFingerprint);

                command.ExecuteNonQuery();
            }
        }

        private static string FormatUtc(
            DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation state timestamp must be UTC.",
                    nameof(value));
            }

            return value.ToString(
                "o",
                CultureInfo.InvariantCulture);
        }

        private static DateTime ParseUtc(
            string value)
        {
            var parsed =
                DateTime.Parse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);

            if (parsed.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Stored interface degradation state timestamp must be UTC.");
            }

            return parsed;
        }
    }
}
