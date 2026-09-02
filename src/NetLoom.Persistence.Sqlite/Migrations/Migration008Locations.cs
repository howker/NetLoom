using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration008Locations : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE locations
(
    location_id TEXT NOT NULL PRIMARY KEY,
    parent_location_id TEXT NULL,
    name TEXT NOT NULL,
    description TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,

    CHECK (length(trim(name)) > 0),
    CHECK
    (
        parent_location_id IS NULL OR
        parent_location_id <> location_id
    ),

    FOREIGN KEY (parent_location_id)
        REFERENCES locations(location_id)
        ON DELETE RESTRICT
);",

                @"
CREATE INDEX ix_locations_parent
ON locations(parent_location_id);",

                @"
CREATE INDEX ix_locations_name
ON locations(name);"
            };

        public int Version
        {
            get { return 8; }
        }

        public string Name
        {
            get { return "Locations"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
