using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration020InterfaceIdentityAndManagementAddress : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
ALTER TABLE devices
ADD COLUMN management_address TEXT NULL;",
                    @"
ALTER TABLE interfaces
ADD COLUMN if_type INTEGER NULL;"
                };

        public int Version
        {
            get { return 20; }
        }

        public string Name
        {
            get { return "Interface identity and management address"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
