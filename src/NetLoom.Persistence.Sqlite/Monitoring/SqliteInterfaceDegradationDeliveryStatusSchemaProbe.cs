using System;
using System.Collections.Generic;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Monitoring
{
    public sealed class
        SqliteInterfaceDegradationDeliveryStatusSchemaProbe
    {
        private static readonly string[]
            RequiredColumns =
            {
                "event_key",
                "device_id",
                "if_index",
                "captured_utc",
                "transition_kind",
                "previous_status",
                "previous_evidence_fingerprint",
                "current_status",
                "current_evidence_fingerprint",
                "error_rate_per_minute",
                "discard_rate_per_minute",
                "reason_codes",
                "delivery_failure_count",
                "last_delivery_failure_utc",
                "next_delivery_attempt_utc",
                "delivered_utc"
            };

        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteInterfaceDegradationDeliveryStatusSchemaProbe(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public bool IsCompatible()
        {
            var columns =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            using (var connection =
                _connectionFactory.OpenReadOnlyConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText =
                    "PRAGMA table_info(interface_degradation_outbox);";

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            columns.Add(
                                reader.GetString(1));
                        }
                    }
                }
            }

            foreach (var requiredColumn in
                RequiredColumns)
            {
                if (!columns.Contains(
                    requiredColumn))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
