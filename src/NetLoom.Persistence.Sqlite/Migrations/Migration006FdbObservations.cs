using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration006FdbObservations : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE bridge_port_mappings
(
    observation_id TEXT NOT NULL,
    bridge_port_index INTEGER NOT NULL,
    if_index INTEGER NOT NULL,

    PRIMARY KEY
    (
        observation_id,
        bridge_port_index,
        if_index
    ),

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                @"
CREATE INDEX ix_bridge_port_mappings_if_index
ON bridge_port_mappings(if_index);",

                @"
CREATE TABLE fdb_observations
(
    observation_id TEXT NOT NULL,
    mac_address TEXT NOT NULL,
    bridge_port_index INTEGER NULL,
    status INTEGER NULL,

    PRIMARY KEY
    (
        observation_id,
        mac_address
    ),

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                @"
CREATE INDEX ix_fdb_observations_mac
ON fdb_observations(mac_address);",

                @"
CREATE INDEX ix_fdb_observations_bridge_port
ON fdb_observations(bridge_port_index);"
            };

        public int Version
        {
            get { return 6; }
        }

        public string Name
        {
            get { return "FDB observations"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
