using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    // Sprint 48 (Г2): sysDescr и sysObjectID, полученные при обнаружении или от соседа по LLDP.
    public sealed class Migration022DeviceSystemIdentity : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
ALTER TABLE devices
ADD COLUMN sys_description TEXT NULL;",
                    @"
ALTER TABLE devices
ADD COLUMN sys_object_id TEXT NULL;"
                };

        public int Version
        {
            get { return 22; }
        }

        public string Name
        {
            get { return "Device system identity"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
