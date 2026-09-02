using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration004LldpObservations : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE lldp_observations
(
    observation_id TEXT NOT NULL,
    time_mark INTEGER NOT NULL,
    local_port_number INTEGER NOT NULL,
    remote_index INTEGER NOT NULL,

    chassis_id_subtype INTEGER NULL,
    chassis_id TEXT NULL,

    port_id_subtype INTEGER NULL,
    port_id TEXT NULL,
    port_description TEXT NULL,

    system_name TEXT NULL,
    system_description TEXT NULL,
    system_capabilities_supported TEXT NULL,
    system_capabilities_enabled TEXT NULL,

    local_port_present INTEGER NOT NULL
        CHECK (local_port_present IN (0, 1)),
    local_port_id_subtype INTEGER NULL,
    local_port_id TEXT NULL,
    local_port_description TEXT NULL,

    PRIMARY KEY
    (
        observation_id,
        time_mark,
        local_port_number,
        remote_index
    ),

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                @"
CREATE INDEX ix_lldp_observations_chassis_id
ON lldp_observations(chassis_id);",

                @"
CREATE INDEX ix_lldp_observations_system_name
ON lldp_observations(system_name);"
            };

        public int Version
        {
            get { return 4; }
        }

        public string Name
        {
            get { return "Normalized LLDP observations"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
