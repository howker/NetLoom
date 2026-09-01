using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public interface IMigration
    {
        int Version { get; }

        string Name { get; }

        IReadOnlyList<string> Statements { get; }
    }
}
