using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration009MaterializedTopology : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE devices
(
    id TEXT NOT NULL PRIMARY KEY,
    location_id TEXT NULL,

    custom_name TEXT NULL,
    category TEXT NOT NULL DEFAULT 'Unknown',
    discovery_origin TEXT NOT NULL DEFAULT 'Automatic',
    monitoring_capability TEXT NOT NULL DEFAULT 'Unknown',

    vendor_override TEXT NULL,
    model_override TEXT NULL,
    notes TEXT NULL,

    is_hidden INTEGER NOT NULL DEFAULT 0,
    is_archived INTEGER NOT NULL DEFAULT 0,

    first_seen_utc TEXT NULL,
    last_seen_utc TEXT NULL,
    last_resolved_utc TEXT NULL,

    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,

    FOREIGN KEY(location_id)
        REFERENCES locations(location_id)
        ON DELETE SET NULL
);",

                @"
CREATE INDEX ix_devices_location
ON devices(location_id);",

                @"
CREATE INDEX ix_devices_origin
ON devices(discovery_origin);",

                @"
CREATE TABLE interfaces
(
    id TEXT NOT NULL PRIMARY KEY,
    device_id TEXT NOT NULL,

    if_index INTEGER NULL,

    if_name TEXT NULL,
    if_descr TEXT NULL,
    if_alias TEXT NULL,
    custom_name TEXT NULL,

    mac_address TEXT NULL,

    admin_status TEXT NULL,
    oper_status TEXT NULL,

    speed_bps INTEGER NULL,

    media_type_auto TEXT NULL,
    media_type_override TEXT NULL,

    is_manual INTEGER NOT NULL DEFAULT 0,
    is_hidden INTEGER NOT NULL DEFAULT 0,

    first_seen_utc TEXT NULL,
    last_seen_utc TEXT NULL,

    FOREIGN KEY(device_id)
        REFERENCES devices(id)
        ON DELETE CASCADE
);",

                @"
CREATE UNIQUE INDEX ux_interfaces_device_if_index
ON interfaces(device_id, if_index)
WHERE if_index IS NOT NULL;",

                @"
CREATE INDEX ix_interfaces_device
ON interfaces(device_id);",

                @"
CREATE TABLE physical_links
(
    id TEXT NOT NULL PRIMARY KEY,
    link_key TEXT NOT NULL UNIQUE,

    device_a_id TEXT NOT NULL,
    interface_a_id TEXT NULL,

    device_b_id TEXT NOT NULL,
    interface_b_id TEXT NULL,

    strength TEXT NOT NULL,
    freshness TEXT NOT NULL,

    media_type_resolved TEXT NULL,
    speed_bps_resolved INTEGER NULL,

    source_summary TEXT NULL,

    first_seen_utc TEXT NOT NULL,
    last_seen_utc TEXT NOT NULL,
    last_confirmed_utc TEXT NULL,

    resolver_version TEXT NULL,

    is_hidden INTEGER NOT NULL DEFAULT 0,
    is_archived INTEGER NOT NULL DEFAULT 0,

    notes TEXT NULL,

    FOREIGN KEY(device_a_id)
        REFERENCES devices(id)
        ON DELETE RESTRICT,

    FOREIGN KEY(interface_a_id)
        REFERENCES interfaces(id)
        ON DELETE RESTRICT,

    FOREIGN KEY(device_b_id)
        REFERENCES devices(id)
        ON DELETE RESTRICT,

    FOREIGN KEY(interface_b_id)
        REFERENCES interfaces(id)
        ON DELETE RESTRICT
);",

                @"
CREATE INDEX ix_physical_links_device_a
ON physical_links(device_a_id);",

                @"
CREATE INDEX ix_physical_links_device_b
ON physical_links(device_b_id);",

                @"
CREATE INDEX ix_physical_links_strength
ON physical_links(strength);"
            };

        public int Version
        {
            get { return 9; }
        }

        public string Name
        {
            get { return "Materialized physical topology"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
