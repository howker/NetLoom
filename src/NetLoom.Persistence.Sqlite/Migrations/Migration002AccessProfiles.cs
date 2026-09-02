using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration002AccessProfiles : IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE access_profiles
(
    access_profile_id TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
    snmp_version TEXT NOT NULL,
    snmp_username TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);",

                @"
CREATE TABLE secrets
(
    secret_id TEXT NOT NULL PRIMARY KEY,
    access_profile_id TEXT NOT NULL,
    secret_kind TEXT NOT NULL,
    protected_value BLOB NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    FOREIGN KEY (access_profile_id)
        REFERENCES access_profiles(access_profile_id)
        ON DELETE CASCADE,
    UNIQUE (access_profile_id, secret_kind)
);",

                @"
CREATE TABLE access_profile_targets
(
    target_id TEXT NOT NULL PRIMARY KEY,
    access_profile_id TEXT NOT NULL,
    target_kind TEXT NOT NULL,
    target_value TEXT NOT NULL,
    priority INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (access_profile_id)
        REFERENCES access_profiles(access_profile_id)
        ON DELETE CASCADE,
    UNIQUE (access_profile_id, target_kind, target_value)
);",

                @"
CREATE TABLE access_profile_exclusions
(
    exclusion_id TEXT NOT NULL PRIMARY KEY,
    access_profile_id TEXT NOT NULL,
    target_kind TEXT NOT NULL,
    target_value TEXT NOT NULL,
    FOREIGN KEY (access_profile_id)
        REFERENCES access_profiles(access_profile_id)
        ON DELETE CASCADE,
    UNIQUE (access_profile_id, target_kind, target_value)
);",

                @"
CREATE TABLE access_profile_tcp_ports
(
    access_profile_id TEXT NOT NULL,
    port INTEGER NOT NULL CHECK (port BETWEEN 1 AND 65535),
    PRIMARY KEY (access_profile_id, port),
    FOREIGN KEY (access_profile_id)
        REFERENCES access_profiles(access_profile_id)
        ON DELETE CASCADE
);"
            };

        public int Version
        {
            get { return 2; }
        }

        public string Name
        {
            get { return "Access profiles and protected secrets"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
