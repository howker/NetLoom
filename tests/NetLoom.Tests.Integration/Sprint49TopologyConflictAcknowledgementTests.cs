using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Migrations;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint49TopologyConflictAcknowledgementTests
    {
        [TestMethod]
        public void Migration026AddsAcknowledgementsToExistingDatabaseWithoutChangingDevices()
        {
            WithDatabase(factory =>
            {
                var deviceId = Guid.NewGuid();
                using (var connection = factory.OpenConnection())
                {
                    new MigrationRunner(new IMigration[]
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
                        new Migration012ObservationDeviceBindings(),
                        new Migration013LldpTopologyIdentity(),
                        new Migration014InterfaceCounterBaselines(),
                        new Migration015InterfaceDegradationStates(),
                        new Migration016InterfaceDegradationOutbox(),
                        new Migration017InterfaceDegradationDelivery(),
                        new Migration018InterfaceDegradationDeliveryRetry(),
                        new Migration019MapLayout(),
                        new Migration020InterfaceIdentityAndManagementAddress(),
                        new Migration021MapLocationLayout(),
                        new Migration022DeviceSystemIdentity(),
                        new Migration023DiscoveryRuns(),
                        new Migration024DeviceConfirmation(),
                        new Migration025DeviceIgnore()
                    }).ApplyPending(connection);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"INSERT INTO devices (id, custom_name, created_at_utc, updated_at_utc)
VALUES (@id, 'conflict-sw-old', @utc, @utc);";
                        command.Parameters.AddWithValue("@id", deviceId.ToString("D"));
                        command.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                        command.ExecuteNonQuery();
                    }
                }
                var initializer = new DatabaseInitializer(factory);
                initializer.Initialize();
                initializer.Initialize();
                Assert.AreEqual(0, new SqliteTopologyConflictAcknowledgementStore(factory).List().Count);
                Assert.AreEqual("conflict-sw-old", new SqliteMaterializedTopologyRepository(factory).GetDevice(deviceId).CustomName);
                using (var connection = factory.OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = 26;";
                    Assert.AreEqual(1L, Convert.ToInt64(command.ExecuteScalar()));
                }
            });
        }

        [TestMethod]
        public void AcknowledgeIsIdempotentAndListSurvivesReopeningStore()
        {
            WithDatabase(factory =>
            {
                new DatabaseInitializer(factory).Initialize();
                var store = new SqliteTopologyConflictAcknowledgementStore(factory);
                var manual = Guid.NewGuid();
                var observed = Guid.NewGuid();
                var another = Guid.NewGuid();
                var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
                store.Acknowledge(manual, observed, now);
                store.Acknowledge(manual, observed, now.AddMinutes(1));
                store.Acknowledge(manual, another, now);
                var pairs = new SqliteTopologyConflictAcknowledgementStore(factory).List();
                Assert.AreEqual(2, pairs.Count);
                CollectionAssert.AreEquivalent(new[] { new TopologyConflictKey(manual, observed),
                    new TopologyConflictKey(manual, another) }, pairs.ToArray());
                using (var connection = factory.OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT acknowledged_utc FROM topology_conflict_acknowledgements
WHERE manual_link_id = @manual AND observed_link_id = @observed;";
                    command.Parameters.AddWithValue("@manual", manual.ToString("D"));
                    command.Parameters.AddWithValue("@observed", observed.ToString("D"));
                    Assert.AreEqual(now.ToString("o", CultureInfo.InvariantCulture), (string)command.ExecuteScalar());
                }
                Assert.ThrowsExactly<ArgumentException>(() => store.Acknowledge(Guid.Empty, observed, now));
                Assert.ThrowsExactly<ArgumentException>(() => store.Acknowledge(manual, Guid.Empty, now));
                Assert.ThrowsExactly<ArgumentException>(() => store.Acknowledge(manual, observed, DateTime.SpecifyKind(now, DateTimeKind.Unspecified)));
                Assert.AreEqual(2, store.List().Count);
            });
        }

        private static void WithDatabase(Action<SqliteConnectionFactory> action)
        {
            var path = Path.Combine(Path.GetTempPath(), "netloom-s49-conflict-" + Guid.NewGuid().ToString("N") + ".db");
            try { action(new SqliteConnectionFactory(path)); }
            finally
            {
                SQLiteConnection.ClearAllPools();
                foreach (var candidate in new[] { path, path + "-wal", path + "-shm" })
                    if (File.Exists(candidate)) File.Delete(candidate);
            }
        }
    }
}
