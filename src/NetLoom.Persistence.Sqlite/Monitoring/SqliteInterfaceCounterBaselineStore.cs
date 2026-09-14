using System;
using System.Data.SQLite;
using System.Globalization;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Monitoring
{
    public sealed class SqliteInterfaceCounterBaselineStore :
        IInterfaceCounterBaselineStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteInterfaceCounterBaselineStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public InterfaceMonitoringSnapshot ReplaceAndGetPrevious(
            InterfaceMonitoringSnapshot current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(
                    nameof(current));
            }

            if (!current.DeviceId.HasValue)
            {
                throw new ArgumentException(
                    "Interface counter baseline requires a stable DeviceId.",
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
                                current.DeviceId.Value,
                                current.IfIndex);

                        if (previous != null &&
                            current.CapturedUtc <=
                                previous.CapturedUtc)
                        {
                            throw new InvalidOperationException(
                                "Interface counter baseline must move forward in time.");
                        }

                        Save(
                            connection,
                            current);

                        return previous;
                    });
            }
        }

        private static InterfaceMonitoringSnapshot Load(
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
    in_errors,
    out_errors,
    in_discards,
    out_discards,
    counter_discontinuity_time_ticks
FROM interface_counter_baselines
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

                    return new InterfaceMonitoringSnapshot(
                        deviceId,
                        ifIndex,
                        null,
                        null,
                        ParseUtc(
                            reader.GetString(0)),
                        ReadUInt32(
                            reader,
                            1),
                        ReadUInt32(
                            reader,
                            2),
                        ReadUInt32(
                            reader,
                            3),
                        ReadUInt32(
                            reader,
                            4),
                        ReadUInt32(
                            reader,
                            5));
                }
            }
        }

        private static void Save(
            SQLiteConnection connection,
            InterfaceMonitoringSnapshot current)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO interface_counter_baselines
(
    device_id,
    if_index,
    captured_utc,
    in_errors,
    out_errors,
    in_discards,
    out_discards,
    counter_discontinuity_time_ticks
)
VALUES
(
    @deviceId,
    @ifIndex,
    @capturedUtc,
    @inErrors,
    @outErrors,
    @inDiscards,
    @outDiscards,
    @counterDiscontinuityTimeTicks
)
ON CONFLICT(device_id, if_index) DO UPDATE SET
    captured_utc = excluded.captured_utc,
    in_errors = excluded.in_errors,
    out_errors = excluded.out_errors,
    in_discards = excluded.in_discards,
    out_discards = excluded.out_discards,
    counter_discontinuity_time_ticks =
        excluded.counter_discontinuity_time_ticks;";

                command.Parameters.AddWithValue(
                    "@deviceId",
                    current.DeviceId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "@ifIndex",
                    current.IfIndex);

                command.Parameters.AddWithValue(
                    "@capturedUtc",
                    FormatUtc(
                        current.CapturedUtc));

                AddUInt32(
                    command,
                    "@inErrors",
                    current.InErrors);

                AddUInt32(
                    command,
                    "@outErrors",
                    current.OutErrors);

                AddUInt32(
                    command,
                    "@inDiscards",
                    current.InDiscards);

                AddUInt32(
                    command,
                    "@outDiscards",
                    current.OutDiscards);

                AddUInt32(
                    command,
                    "@counterDiscontinuityTimeTicks",
                    current.CounterDiscontinuityTimeTicks);

                command.ExecuteNonQuery();
            }
        }

        private static string FormatUtc(
            DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface counter baseline timestamp must be UTC.",
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
                    "Stored interface counter baseline timestamp must be UTC.");
            }

            return parsed;
        }

        private static uint? ReadUInt32(
            SQLiteDataReader reader,
            int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToUInt32(
                reader.GetValue(ordinal),
                CultureInfo.InvariantCulture);
        }

        private static void AddUInt32(
            SQLiteCommand command,
            string name,
            uint? value)
        {
            command.Parameters.AddWithValue(
                name,
                value.HasValue
                    ? (object)(long)value.Value
                    : DBNull.Value);
        }
    }
}
