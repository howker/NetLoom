using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration029AccessProfileSnmpParameters : IMigration
    {
        public int Version => 29;
        public string Name => "Access profile SNMP parameters";
        public IReadOnlyList<string> Statements { get; } = new[]
        {
            "ALTER TABLE access_profiles ADD COLUMN snmp_auth_protocol TEXT NULL;",
            "ALTER TABLE access_profiles ADD COLUMN snmp_privacy_protocol TEXT NULL;",
            "ALTER TABLE access_profiles ADD COLUMN snmp_timeout_ms INTEGER NULL CHECK (snmp_timeout_ms IS NULL OR snmp_timeout_ms > 0);",
            "ALTER TABLE access_profiles ADD COLUMN snmp_retry_count INTEGER NULL CHECK (snmp_retry_count IS NULL OR snmp_retry_count >= 0);"
        };
    }
}
