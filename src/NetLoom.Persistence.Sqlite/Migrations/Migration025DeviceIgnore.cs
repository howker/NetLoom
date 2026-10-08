using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration025DeviceIgnore : IMigration
    {
        public int Version => 25;
        public string Name => "Device ignore";
        public IReadOnlyList<string> Statements { get; } = new[]
        {
            "ALTER TABLE devices ADD COLUMN ignored_utc TEXT NULL;"
        };
    }
}
