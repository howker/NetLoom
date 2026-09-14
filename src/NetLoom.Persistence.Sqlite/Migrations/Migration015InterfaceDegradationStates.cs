using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration015InterfaceDegradationStates : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE interface_degradation_states
(
    device_id TEXT NOT NULL,
    if_index INTEGER NOT NULL,
    captured_utc TEXT NOT NULL,
    status INTEGER NOT NULL,
    evidence_fingerprint TEXT NOT NULL,

    PRIMARY KEY
    (
        device_id,
        if_index
    ),

    CHECK (if_index >= 1),
    CHECK (status IN (1, 2))
);"
                };

        public int Version
        {
            get { return 15; }
        }

        public string Name
        {
            get
            {
                return
                    "Interface degradation states";
            }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
