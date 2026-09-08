using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class
        Migration012ObservationDeviceBindings :
        IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE observation_device_bindings
(
    observation_id TEXT NOT NULL PRIMARY KEY,
    device_id TEXT NOT NULL,

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);"
                };

        public int Version
        {
            get { return 12; }
        }

        public string Name
        {
            get
            {
                return
                    "Stable observation device bindings";
            }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}