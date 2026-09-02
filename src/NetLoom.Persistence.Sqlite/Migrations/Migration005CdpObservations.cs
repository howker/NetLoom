using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration005CdpObservations : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE cdp_observations
(
    observation_id TEXT NOT NULL,
    cache_if_index INTEGER NOT NULL,
    device_index INTEGER NOT NULL,

    address_type INTEGER NULL,
    address TEXT NULL,
    version TEXT NULL,
    device_id TEXT NULL,
    device_port TEXT NULL,
    platform TEXT NULL,
    capabilities TEXT NULL,
    native_vlan INTEGER NULL,
    duplex INTEGER NULL,

    system_name TEXT NULL,
    system_object_id TEXT NULL,

    primary_management_address_type INTEGER NULL,
    primary_management_address TEXT NULL,

    physical_location TEXT NULL,
    last_change INTEGER NULL,

    PRIMARY KEY
    (
        observation_id,
        cache_if_index,
        device_index
    ),

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                @"
CREATE INDEX ix_cdp_observations_device_id
ON cdp_observations(device_id);",

                @"
CREATE INDEX ix_cdp_observations_system_name
ON cdp_observations(system_name);",

                @"
CREATE INDEX ix_cdp_observations_address
ON cdp_observations(address);"
            };

        public int Version
        {
            get { return 5; }
        }

        public string Name
        {
            get { return "Normalized CDP observations"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
