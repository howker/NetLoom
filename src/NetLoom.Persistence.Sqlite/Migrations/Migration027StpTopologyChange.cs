using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration027StpTopologyChange : IMigration
    {
        public int Version => 27;
        public string Name => "STP topology change";
        public IReadOnlyList<string> Statements { get; } = new[]
        {
            "ALTER TABLE stp_observations ADD COLUMN time_since_topology_change INTEGER NULL;",
            "ALTER TABLE stp_observations ADD COLUMN topology_changes INTEGER NULL;"
        };
    }
}
