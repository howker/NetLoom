using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration021MapLocationLayout : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE map_location_layout
(
    map_id TEXT NOT NULL,
    location_id TEXT NOT NULL,
    x REAL NOT NULL,
    y REAL NOT NULL,
    width REAL NOT NULL,
    height REAL NOT NULL,
    is_collapsed INTEGER NOT NULL DEFAULT 0,
    is_locked INTEGER NOT NULL DEFAULT 0,

    PRIMARY KEY(map_id, location_id),

    CHECK (width > 0.0),
    CHECK (height > 0.0),
    CHECK (is_collapsed IN (0, 1)),
    CHECK (is_locked IN (0, 1)),

    FOREIGN KEY(map_id)
        REFERENCES maps(id)
        ON DELETE CASCADE,

    FOREIGN KEY(location_id)
        REFERENCES locations(location_id)
        ON DELETE CASCADE
);",
                    @"
CREATE INDEX ix_map_location_layout_location
ON map_location_layout(location_id);"
                };

        public int Version
        {
            get { return 21; }
        }

        public string Name
        {
            get { return "Persistent location container layout"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
