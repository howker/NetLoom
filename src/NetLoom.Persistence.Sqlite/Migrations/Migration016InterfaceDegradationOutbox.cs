using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration016InterfaceDegradationOutbox : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE interface_degradation_outbox
(
    event_key TEXT NOT NULL PRIMARY KEY,
    device_id TEXT NOT NULL,
    if_index INTEGER NOT NULL,
    captured_utc TEXT NOT NULL,
    transition_kind INTEGER NOT NULL,
    previous_status INTEGER NULL,
    previous_evidence_fingerprint TEXT NULL,
    current_status INTEGER NOT NULL,
    current_evidence_fingerprint TEXT NOT NULL,
    error_rate_per_minute REAL NULL,
    discard_rate_per_minute REAL NULL,
    reason_codes TEXT NOT NULL,

    CHECK (if_index >= 1),
    CHECK (transition_kind IN (1, 2, 3)),
    CHECK (
        previous_status IS NULL OR
        previous_status IN (1, 2)
    ),
    CHECK (current_status IN (1, 2))
);"
                };

        public int Version
        {
            get { return 16; }
        }

        public string Name
        {
            get
            {
                return
                    "Interface degradation outbox";
            }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
