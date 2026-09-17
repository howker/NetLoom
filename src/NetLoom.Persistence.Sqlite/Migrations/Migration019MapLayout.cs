using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration019MapLayout : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE maps
(
    id TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    root_location_id TEXT NULL,
    description TEXT NULL,
    default_zoom REAL NOT NULL DEFAULT 1.0,
    default_pan_x REAL NOT NULL DEFAULT 0.0,
    default_pan_y REAL NOT NULL DEFAULT 0.0,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,

    CHECK (length(trim(name)) > 0),
    CHECK (default_zoom > 0.0),

    FOREIGN KEY(root_location_id)
        REFERENCES locations(location_id)
        ON DELETE SET NULL
);",
                    @"
CREATE TABLE map_device_layout
(
    map_id TEXT NOT NULL,
    device_id TEXT NOT NULL,
    x REAL NOT NULL,
    y REAL NOT NULL,
    width REAL NOT NULL DEFAULT 140.0,
    height REAL NOT NULL DEFAULT 70.0,
    shape TEXT NOT NULL DEFAULT 'Rectangle',
    is_locked INTEGER NOT NULL DEFAULT 0,
    is_hidden INTEGER NOT NULL DEFAULT 0,
    z_index INTEGER NOT NULL DEFAULT 0,

    PRIMARY KEY(map_id, device_id),

    CHECK (width > 0.0),
    CHECK (height > 0.0),
    CHECK (length(trim(shape)) > 0),
    CHECK (is_locked IN (0, 1)),
    CHECK (is_hidden IN (0, 1)),

    FOREIGN KEY(map_id)
        REFERENCES maps(id)
        ON DELETE CASCADE,

    FOREIGN KEY(device_id)
        REFERENCES devices(id)
        ON DELETE CASCADE
);",
                    @"
CREATE INDEX ix_map_device_layout_device
ON map_device_layout(device_id);"
                };

        public int Version
        {
            get { return 19; }
        }

        public string Name
        {
            get { return "Persistent map layout"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
