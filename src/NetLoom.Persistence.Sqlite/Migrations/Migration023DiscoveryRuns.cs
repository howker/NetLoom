using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration023DiscoveryRuns : IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE discovery_runs
(
    id TEXT NOT NULL PRIMARY KEY,
    started_utc TEXT NOT NULL,
    finished_utc TEXT NULL,
    state TEXT NOT NULL,
    access_profile_id TEXT NULL,
    access_profile_name TEXT NULL,
    scope_text TEXT NOT NULL,
    total_addresses INTEGER NOT NULL DEFAULT 0,
    processed_addresses INTEGER NOT NULL DEFAULT 0,
    found_candidates INTEGER NOT NULL DEFAULT 0,
    snmp_responded INTEGER NOT NULL DEFAULT 0,
    error_count INTEGER NOT NULL DEFAULT 0,
    known_unchanged_count INTEGER NOT NULL DEFAULT 0,
    fault_message TEXT NULL,

    CHECK (total_addresses >= 0),
    CHECK (processed_addresses >= 0),
    CHECK (found_candidates >= 0),
    CHECK (snmp_responded >= 0),
    CHECK (error_count >= 0),
    CHECK (known_unchanged_count >= 0)
);",
                    @"
CREATE INDEX ix_discovery_runs_started
ON discovery_runs(started_utc);",
                    @"
CREATE TABLE discovery_run_results
(
    run_id TEXT NOT NULL,
    address TEXT NOT NULL,
    result_group TEXT NOT NULL,
    device_id TEXT NULL,
    observed_utc TEXT NOT NULL,
    icmp_reachable INTEGER NOT NULL DEFAULT 0,
    open_tcp_ports TEXT NULL,
    snmp_responded INTEGER NOT NULL DEFAULT 0,
    snmp_error TEXT NULL,
    sys_name TEXT NULL,
    sys_description TEXT NULL,
    sys_object_id TEXT NULL,
    interface_count INTEGER NOT NULL DEFAULT 0,
    completeness TEXT NOT NULL DEFAULT 'NotApplicable',
    partial_reason TEXT NOT NULL DEFAULT 'None',
    reason TEXT NOT NULL DEFAULT 'None',
    reason_detail TEXT NULL,
    resolution TEXT NOT NULL DEFAULT 'Pending',
    resolved_utc TEXT NULL,

    PRIMARY KEY(run_id, address),

    FOREIGN KEY(run_id)
        REFERENCES discovery_runs(id)
        ON DELETE CASCADE,

    FOREIGN KEY(device_id)
        REFERENCES devices(id)
        ON DELETE SET NULL
);",
                    @"
CREATE INDEX ix_discovery_run_results_device
ON discovery_run_results(device_id);",
                    @"
CREATE TABLE discovery_run_result_changes
(
    run_id TEXT NOT NULL,
    address TEXT NOT NULL,
    field TEXT NOT NULL,
    old_value TEXT NULL,
    new_value TEXT NULL,

    PRIMARY KEY(run_id, address, field),

    FOREIGN KEY(run_id, address)
        REFERENCES discovery_run_results(run_id, address)
        ON DELETE CASCADE
);"
                };

        public int Version
        {
            get { return 23; }
        }

        public string Name
        {
            get { return "Discovery runs and results"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
