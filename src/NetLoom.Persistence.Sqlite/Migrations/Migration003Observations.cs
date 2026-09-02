using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration003Observations : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE observations
(
    observation_id TEXT NOT NULL PRIMARY KEY,
    observation_kind TEXT NOT NULL,
    source_address TEXT NOT NULL,
    captured_utc TEXT NOT NULL
);",

                @"
CREATE INDEX ix_observations_captured_utc
ON observations(captured_utc);",

                @"
CREATE INDEX ix_observations_source_kind
ON observations(source_address, observation_kind);",

                @"
CREATE TABLE snmp_varbinds
(
    observation_id TEXT NOT NULL,
    sequence_no INTEGER NOT NULL,
    oid TEXT NOT NULL,
    type_code INTEGER NOT NULL,
    display_value TEXT NULL,
    encoded_value BLOB NOT NULL,
    PRIMARY KEY (observation_id, sequence_no),
    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                @"
CREATE INDEX ix_snmp_varbinds_oid
ON snmp_varbinds(oid);"
            };

        public int Version
        {
            get { return 3; }
        }

        public string Name
        {
            get { return "Observations and raw SNMP varbinds"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
