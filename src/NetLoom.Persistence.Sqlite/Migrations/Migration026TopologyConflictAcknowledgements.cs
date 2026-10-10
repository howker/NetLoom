using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration026TopologyConflictAcknowledgements : IMigration
    {
        public int Version => 26;
        public string Name => "Topology conflict acknowledgements";
        public IReadOnlyList<string> Statements { get; } = new[]
        {
            @"CREATE TABLE topology_conflict_acknowledgements (
    manual_link_id TEXT NOT NULL,
    observed_link_id TEXT NOT NULL,
    acknowledged_utc TEXT NOT NULL,
    PRIMARY KEY (manual_link_id, observed_link_id)
);"
        };
    }
}
