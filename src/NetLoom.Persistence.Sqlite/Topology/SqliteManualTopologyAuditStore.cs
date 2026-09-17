using System;
using System.Globalization;
using NetLoom.Application.Topology;
using NetLoom.Domain.Observations;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Topology
{
    public sealed class SqliteManualTopologyAuditStore :
        IManualTopologyAuditStore
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteManualTopologyAuditStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public void Record(
            Observation observation)
        {
            if (observation == null)
            {
                throw new ArgumentNullException(
                    nameof(observation));
            }

            if (observation.Kind !=
                ObservationKind.Manual)
            {
                throw new InvalidOperationException(
                    "Manual topology audit requires a Manual observation.");
            }

            if (!string.Equals(
                observation.SourceAddress,
                "User",
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Manual topology audit source must be User.");
            }

            if (observation.CapturedUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Manual topology audit timestamp must be UTC.");
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO observations
(
    observation_id,
    observation_kind,
    source_address,
    captured_utc
)
VALUES
(
    @id,
    @kind,
    @sourceAddress,
    @capturedUtc
);";

                command.Parameters.AddWithValue(
                    "@id",
                    observation.Id.ToString("D"));

                command.Parameters.AddWithValue(
                    "@kind",
                    observation.Kind.ToString());

                command.Parameters.AddWithValue(
                    "@sourceAddress",
                    observation.SourceAddress);

                command.Parameters.AddWithValue(
                    "@capturedUtc",
                    observation.CapturedUtc.ToString(
                        "o",
                        CultureInfo.InvariantCulture));

                command.ExecuteNonQuery();
            }
        }
    }
}
