using System;
using NetLoom.Persistence.Sqlite.Migrations;

namespace NetLoom.Persistence.Sqlite.Database
{
    public sealed class DatabaseInitializer
    {
        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly MigrationRunner _migrationRunner;

        public DatabaseInitializer(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));

            _migrationRunner = new MigrationRunner(
                new IMigration[]
                {
                    new Migration001Initial(),
                    new Migration002AccessProfiles(),
                    new Migration003Observations()
                });
        }

        public void Initialize()
        {
            using (var connection = _connectionFactory.OpenConnection())
            {
                _migrationRunner.ApplyPending(connection);
            }
        }
    }
}
