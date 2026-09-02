using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration007ArpObservations : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE arp_observations
(
    observation_id TEXT NOT NULL,
    if_index INTEGER NOT NULL,
    address_type INTEGER NOT NULL,
    ip_address TEXT NOT NULL,
    physical_address TEXT NULL,
    entry_type INTEGER NULL,
    entry_state INTEGER NULL,
    table_kind INTEGER NOT NULL,

    PRIMARY KEY
    (
        observation_id,
        if_index,
        address_type,
        ip_address,
        table_kind
    ),

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                @"
CREATE INDEX ix_arp_observations_ip
ON arp_observations(ip_address);",

                @"
CREATE INDEX ix_arp_observations_physical_address
ON arp_observations(physical_address);",

                @"
CREATE INDEX ix_arp_observations_if_index
ON arp_observations(if_index);"
            };

        public int Version
        {
            get { return 7; }
        }

        public string Name
        {
            get { return "ARP and neighbor observations"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
