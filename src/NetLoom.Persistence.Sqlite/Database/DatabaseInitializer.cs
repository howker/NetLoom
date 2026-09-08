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
                    new Migration003Observations(),
                    new Migration004LldpObservations(),
                    new Migration005CdpObservations(),
                    new Migration006FdbObservations(),
                    new Migration007ArpObservations(),
                    new Migration008Locations(),
                    new Migration009MaterializedTopology(),
                    new Migration010PhysicalLinkEvidenceCurrent(),
                    new Migration011StpObservations(),
                    new Migration012ObservationDeviceBindings()
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
