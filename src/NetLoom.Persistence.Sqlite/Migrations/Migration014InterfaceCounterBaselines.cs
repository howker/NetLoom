using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration014InterfaceCounterBaselines : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE interface_counter_baselines
(
    device_id TEXT NOT NULL,
    if_index INTEGER NOT NULL,
    captured_utc TEXT NOT NULL,
    in_errors INTEGER NULL,
    out_errors INTEGER NULL,
    in_discards INTEGER NULL,
    out_discards INTEGER NULL,
    counter_discontinuity_time_ticks INTEGER NULL,

    PRIMARY KEY
    (
        device_id,
        if_index
    ),

    CHECK (if_index >= 1)
);"
                };

        public int Version
        {
            get { return 14; }
        }

        public string Name
        {
            get
            {
                return
                    "Interface counter baselines";
            }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
