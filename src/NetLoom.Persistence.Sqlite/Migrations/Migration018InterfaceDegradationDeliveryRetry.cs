using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration018InterfaceDegradationDeliveryRetry : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
ALTER TABLE interface_degradation_outbox
ADD COLUMN delivery_failure_count INTEGER NOT NULL DEFAULT 0
CHECK (delivery_failure_count >= 0);",
                    @"
ALTER TABLE interface_degradation_outbox
ADD COLUMN last_delivery_failure_utc TEXT NULL;",
                    @"
ALTER TABLE interface_degradation_outbox
ADD COLUMN next_delivery_attempt_utc TEXT NULL;",
                    @"
CREATE INDEX ix_interface_degradation_outbox_delivery_ready
ON interface_degradation_outbox
(
    delivered_utc,
    next_delivery_attempt_utc,
    captured_utc,
    event_key
);"
                };

        public int Version
        {
            get { return 18; }
        }

        public string Name
        {
            get
            {
                return
                    "Interface degradation delivery retry";
            }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
