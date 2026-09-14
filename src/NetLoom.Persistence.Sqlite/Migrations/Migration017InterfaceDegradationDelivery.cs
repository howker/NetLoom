using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration017InterfaceDegradationDelivery : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
ALTER TABLE interface_degradation_outbox
ADD COLUMN delivered_utc TEXT NULL;",
                    @"
CREATE INDEX ix_interface_degradation_outbox_pending
ON interface_degradation_outbox
(
    delivered_utc,
    captured_utc,
    event_key
);"
                };

        public int Version
        {
            get { return 17; }
        }

        public string Name
        {
            get
            {
                return
                    "Interface degradation delivery acknowledgement";
            }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
