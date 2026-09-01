using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration001Initial : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE IF NOT EXISTS app_settings
(
    setting_key TEXT NOT NULL PRIMARY KEY,
    setting_value TEXT NULL,
    updated_utc TEXT NOT NULL
);"
            };

        public int Version
        {
            get { return 1; }
        }

        public string Name
        {
            get { return "Initial application settings"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
