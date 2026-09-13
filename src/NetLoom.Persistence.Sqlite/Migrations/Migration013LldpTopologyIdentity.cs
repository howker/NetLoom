using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration013LldpTopologyIdentity : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE lldp_local_system
(
    observation_id TEXT NOT NULL PRIMARY KEY,
    chassis_id_subtype INTEGER NULL,
    chassis_id TEXT NULL,
    system_name TEXT NULL,

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                    @"
CREATE INDEX ix_lldp_local_system_chassis_id
ON lldp_local_system(chassis_id);",

                    @"
ALTER TABLE interfaces
ADD COLUMN lldp_port_id TEXT NULL;",

                    @"
ALTER TABLE interfaces
ADD COLUMN lldp_port_description TEXT NULL;",

                    @"
ALTER TABLE devices
ADD COLUMN discovered_name TEXT NULL;",

                    @"
ALTER TABLE devices
ADD COLUMN lldp_chassis_id TEXT NULL;",

                    @"
CREATE INDEX ix_devices_lldp_chassis_id
ON devices(lldp_chassis_id);"
                };

        public int Version
        {
            get { return 13; }
        }

        public string Name
        {
            get
            {
                return
                    "LLDP topology identity";
            }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
