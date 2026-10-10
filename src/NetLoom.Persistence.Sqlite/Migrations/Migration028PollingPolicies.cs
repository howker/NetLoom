using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration028PollingPolicies : IMigration
    {
        public int Version => 28;
        public string Name => "Polling policies";
        public IReadOnlyList<string> Statements { get; } = new[]
        {
            @"CREATE TABLE polling_policies (
    polling_policy_id TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    is_default INTEGER NOT NULL DEFAULT 0 CHECK (is_default IN (0, 1)),
    active_polling INTEGER NOT NULL CHECK (active_polling IN (0, 1)),
    state_mode TEXT NOT NULL CHECK (state_mode IN ('General', 'Interval', 'Once', 'Off')),
    state_interval_seconds INTEGER NULL CHECK (state_interval_seconds IS NULL OR state_interval_seconds > 0),
    topology_mode TEXT NOT NULL CHECK (topology_mode IN ('General', 'Interval', 'Once', 'Off')),
    topology_interval_seconds INTEGER NULL CHECK (topology_interval_seconds IS NULL OR topology_interval_seconds > 0),
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL);",
            "CREATE UNIQUE INDEX ux_polling_policies_name ON polling_policies(name COLLATE NOCASE);",
            "CREATE UNIQUE INDEX ux_polling_policies_default ON polling_policies(is_default) WHERE is_default = 1;",
            @"CREATE TABLE polling_policy_tcp_ports (
    polling_policy_id TEXT NOT NULL,
    port INTEGER NOT NULL CHECK (port BETWEEN 1 AND 65535),
    PRIMARY KEY (polling_policy_id, port),
    FOREIGN KEY (polling_policy_id) REFERENCES polling_policies(polling_policy_id) ON DELETE CASCADE);",
            @"CREATE TABLE device_polling_policies (
    device_id TEXT NOT NULL PRIMARY KEY,
    polling_policy_id TEXT NOT NULL,
    assigned_utc TEXT NOT NULL,
    FOREIGN KEY (device_id) REFERENCES devices(id) ON DELETE CASCADE,
    FOREIGN KEY (polling_policy_id) REFERENCES polling_policies(polling_policy_id) ON DELETE RESTRICT);",
            "CREATE INDEX ix_device_polling_policies_policy ON device_polling_policies(polling_policy_id);",
            @"CREATE TABLE location_polling_policies (
    location_id TEXT NOT NULL PRIMARY KEY,
    polling_policy_id TEXT NOT NULL,
    assigned_utc TEXT NOT NULL,
    FOREIGN KEY (location_id) REFERENCES locations(location_id) ON DELETE CASCADE,
    FOREIGN KEY (polling_policy_id) REFERENCES polling_policies(polling_policy_id) ON DELETE RESTRICT);",
            "CREATE INDEX ix_location_polling_policies_policy ON location_polling_policies(polling_policy_id);",
            @"INSERT INTO polling_policies
(polling_policy_id, name, is_default, active_polling, state_mode, state_interval_seconds, topology_mode, topology_interval_seconds, created_utc, updated_utc)
VALUES ('5d0f5051-0000-4000-8000-000000000051', 'Default', 1, 1, 'General', NULL, 'General', NULL, '2026-10-10T00:00:00.0000000Z', '2026-10-10T00:00:00.0000000Z');",
            "INSERT INTO polling_policy_tcp_ports (polling_policy_id, port) VALUES ('5d0f5051-0000-4000-8000-000000000051', 22), ('5d0f5051-0000-4000-8000-000000000051', 80), ('5d0f5051-0000-4000-8000-000000000051', 443);"
        };
    }
}
