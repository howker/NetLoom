using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    // Sprint 48: отметка неподтверждённого результата обнаружения.
    public sealed class Migration024DeviceConfirmation : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
ALTER TABLE devices
ADD COLUMN is_unconfirmed INTEGER NOT NULL DEFAULT 0 CHECK (is_unconfirmed IN (0, 1));"
                };

        public int Version
        {
            get { return 24; }
        }

        public string Name
        {
            get { return "Device confirmation"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
