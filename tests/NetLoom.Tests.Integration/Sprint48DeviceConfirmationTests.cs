using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Migrations;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint48DeviceConfirmationTests
    {
        private static readonly DateTime Now =
            new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void MigrationConfirmsDevicesFromAnOlderDatabase()
        {
            WithDatabase(factory =>
            {
                var id = Guid.NewGuid();
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
                        new Migration023DiscoveryRuns()
                    }).ApplyPending(connection);

                    // Старая схема ещё не содержит отметки подтверждения.
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
INSERT INTO devices (id, custom_name, created_at_utc, updated_at_utc)
VALUES (@id, 'kb-sw-01', @now, @now);";
                        command.Parameters.AddWithValue("@id", id.ToString("D"));
                        command.Parameters.AddWithValue("@now", Now.ToString("O"));
                        command.ExecuteNonQuery();
                    }
                }

                new DatabaseInitializer(factory).Initialize();
                var repository = new SqliteMaterializedTopologyRepository(factory);
                Assert.IsFalse(repository.GetDevice(id).IsUnconfirmed);
                Assert.IsFalse(repository.GetDevices().Single().IsUnconfirmed);
                Assert.IsFalse(new SqliteMaterializedTopologyReadSetReader(factory)
                    .Read("cist").Devices.Single().IsUnconfirmed);
            }, initialize: false);
        }

        [TestMethod]
        public void DiscoveryMarkSurvivesUpdatesUntilExplicitConfirmation()
        {
            WithDatabase(factory =>
            {
                var repository = new SqliteMaterializedTopologyRepository(factory);
                var materializer = new DiscoveryCandidateTopologyMaterializer(repository);
                var candidate = Candidate();
                var id = materializer.Materialize(candidate, Now);
                Assert.IsTrue(repository.GetDevice(id).IsUnconfirmed);

                Assert.AreEqual(id, materializer.Materialize(candidate, Now.AddMinutes(1)));
                Assert.IsTrue(repository.GetDevice(id).IsUnconfirmed);
                repository.SaveDevice(Device(id, DeviceDiscoveryOrigin.Automatic));
                Assert.IsTrue(new SqliteMaterializedTopologyRepository(factory)
                    .GetDevice(id).IsUnconfirmed);
                Assert.IsTrue(repository.GetDevices().Single().IsUnconfirmed);

                IDeviceConfirmationStore confirmationStore = repository;
                confirmationStore.SetUnconfirmed(id, false);
                Assert.IsFalse(new SqliteMaterializedTopologyRepository(factory)
                    .GetDevice(id).IsUnconfirmed);
                Assert.AreEqual(id, materializer.Materialize(candidate, Now.AddMinutes(2)));
                Assert.IsFalse(repository.GetDevice(id).IsUnconfirmed);
            });
        }

        [TestMethod]
        public void ManualDeviceIsConfirmed()
        {
            WithDatabase(factory =>
            {
                var repository = new SqliteMaterializedTopologyRepository(factory);
                var id = Guid.NewGuid();
                repository.SaveDevice(Device(id, DeviceDiscoveryOrigin.Manual));
                Assert.IsFalse(repository.GetDevice(id).IsUnconfirmed);
                Assert.AreEqual(id, new DiscoveryCandidateTopologyMaterializer(repository)
                    .Materialize(Candidate(), Now));
                Assert.IsFalse(repository.GetDevice(id).IsUnconfirmed);
            });
        }

        [TestMethod]
        public void ReadSetAndMapProjectionPreserveDiscoveryMark()
        {
            WithDatabase(factory =>
            {
                var repository = new SqliteMaterializedTopologyRepository(factory);
                var id = new DiscoveryCandidateTopologyMaterializer(repository)
                    .Materialize(Candidate(), Now);
                var readSet = new SqliteMaterializedTopologyReadSetReader(factory).Read("cist");
                Assert.IsTrue(readSet.Devices.Single().IsUnconfirmed);
                var map = new MaterializedTopologyMapProjector().Project(
                    readSet.Devices, readSet.Interfaces, readSet.PhysicalLinks,
                    readSet.PhysicalLinkEvidence, readSet.Locations, Now);
                Assert.AreEqual(id, map.Nodes.Single().DeviceId);
                Assert.IsTrue(map.Nodes.Single().IsUnconfirmed);
            });
        }

        private static TopologyDevice Device(Guid id, DeviceDiscoveryOrigin origin)
        {
            return new TopologyDevice(
                id, null, "kb-sw-07", DeviceCategory.Switch,
                origin, MonitoringCapability.Unknown,
                null, null, null, false, false, Now, Now, Now,
                managementAddress: "10.48.228.57", isUnconfirmed: false);
        }

        private static DiscoveryCandidateSnapshot Candidate()
        {
            return new DiscoveryCandidateSnapshot(
                IPAddress.Parse("10.48.228.57"), Guid.NewGuid(), true, true,
                new[] { 22 }, "kb-sw-07", "MOXA EDS-518A", "1.3.6.1.4.1.8691",
                null, 8);
        }

        private static void WithDatabase(Action<SqliteConnectionFactory> action, bool initialize = true)
        {
            var path = Path.Combine(Path.GetTempPath(),
                "netloom-s48-confirmation-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var factory = new SqliteConnectionFactory(path);
                if (initialize)
                {
                    new DatabaseInitializer(factory).Initialize();
                }

                action(factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
            }
        }
    }
}
