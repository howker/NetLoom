using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Topology;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Topology
{
    public sealed class SqliteTopologyConflictAcknowledgementStore : ITopologyConflictAcknowledgementStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteTopologyConflictAcknowledgementStore(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyCollection<TopologyConflictKey> List()
        {
            var result = new List<TopologyConflictKey>();
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT manual_link_id, observed_link_id
FROM topology_conflict_acknowledgements ORDER BY manual_link_id, observed_link_id;";
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Add(new TopologyConflictKey(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1))));
            }
            return result.AsReadOnly();
        }

        public void Acknowledge(Guid manualLinkId, Guid observedLinkId, DateTime acknowledgedUtc)
        {
            if (manualLinkId == Guid.Empty) throw new ArgumentException("Manual link id is required.", nameof(manualLinkId));
            if (observedLinkId == Guid.Empty) throw new ArgumentException("Observed link id is required.", nameof(observedLinkId));
            if (acknowledgedUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Timestamp must be UTC.", nameof(acknowledgedUtc));

            using (var connection = _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(connection, () =>
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"INSERT OR IGNORE INTO topology_conflict_acknowledgements
(manual_link_id, observed_link_id, acknowledged_utc) VALUES (@manual, @observed, @utc);";
                        command.Parameters.AddWithValue("@manual", manualLinkId.ToString("D"));
                        command.Parameters.AddWithValue("@observed", observedLinkId.ToString("D"));
                        command.Parameters.AddWithValue("@utc", acknowledgedUtc.ToString("o", CultureInfo.InvariantCulture));
                        command.ExecuteNonQuery();
                    }
                });
            }
        }
    }
}
