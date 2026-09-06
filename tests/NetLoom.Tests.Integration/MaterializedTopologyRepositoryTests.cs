using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class MaterializedTopologyRepositoryTests
    {
        [TestMethod]
        public void ManualTopologyRoundTripUsesSharedGraph()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        Path.Combine(
                            directory,
                            "topology.db"));

                new DatabaseInitializer(factory).Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var manual = new ManualTopologyFactory();

                var a =
                    manual.CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "Converter",
                        DeviceCategory.MediaConverter,
                        null);

                var b =
                    manual.CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "Unmanaged",
                        DeviceCategory.UnmanagedSwitch,
                        null);

                var portA =
                    manual.CreateInterface(
                        Guid.NewGuid(),
                        a.Id,
                        "FX",
                        "Fiber");

                var portB =
                    manual.CreateInterface(
                        Guid.NewGuid(),
                        b.Id,
                        "Port 1",
                        "Ethernet");

                repository.SaveDevice(a);
                repository.SaveDevice(b);
                repository.SaveInterface(portA);
                repository.SaveInterface(portB);

                var now =
                    new DateTime(
                        2026,
                        1,
                        1,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc);

                var link =
                    manual.CreateLink(
                        Guid.NewGuid(),
                        "manual-a-b",
                        a.Id,
                        portA.Id,
                        b.Id,
                        portB.Id,
                        "Fiber",
                        null,
                        now);

                repository.SavePhysicalLink(link);

                Assert.AreEqual(2, repository.GetDevices().Count);
                Assert.AreEqual(2, repository.GetInterfaces().Count);
                Assert.AreEqual(1, repository.GetPhysicalLinks().Count);

                Assert.AreEqual(
                    DeviceDiscoveryOrigin.Manual,
                    repository.GetDevice(a.Id).DiscoveryOrigin);

                AssertInvalidOperation(
                    () => repository.DeleteManualDevice(a.Id));

                repository.DeleteManualPhysicalLink(link.Id);
                repository.DeleteManualInterface(portA.Id);
                repository.DeleteManualDevice(a.Id);

                Assert.IsNull(repository.GetDevice(a.Id));
                Assert.AreEqual(1, repository.GetDevices().Count);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [TestMethod]
        public void AutomaticSaveCannotReplaceManualTopology()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        Path.Combine(
                            directory,
                            "protection.db"));

                new DatabaseInitializer(factory).Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var manual =
                    new ManualTopologyFactory().CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "Manual",
                        DeviceCategory.PassiveNetworkEquipment,
                        null);

                repository.SaveDevice(manual);

                var automatic =
                    new TopologyDevice(
                        manual.Id,
                        null,
                        "Automatic replacement",
                        DeviceCategory.Unknown,
                        DeviceDiscoveryOrigin.Automatic,
                        MonitoringCapability.Unknown,
                        null,
                        null,
                        null,
                        false,
                        false,
                        null,
                        null,
                        null);

                AssertInvalidOperation(
                    () => repository.SaveDevice(automatic));

                Assert.AreEqual(
                    DeviceDiscoveryOrigin.Manual,
                    repository
                        .GetDevice(manual.Id)
                        .DiscoveryOrigin);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [TestMethod]
        public void LinkRejectsInterfaceFromAnotherDevice()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom.Tests",
                    Guid.NewGuid().ToString("N"));

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        Path.Combine(
                            directory,
                            "endpoints.db"));

                new DatabaseInitializer(factory).Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var manual = new ManualTopologyFactory();

                var a = manual.CreateDevice(
                    Guid.NewGuid(),
                    null,
                    "A",
                    DeviceCategory.MediaConverter,
                    null);

                var b = manual.CreateDevice(
                    Guid.NewGuid(),
                    null,
                    "B",
                    DeviceCategory.MediaConverter,
                    null);

                repository.SaveDevice(a);
                repository.SaveDevice(b);

                var portB =
                    manual.CreateInterface(
                        Guid.NewGuid(),
                        b.Id,
                        "B1",
                        null);

                repository.SaveInterface(portB);

                var link =
                    manual.CreateLink(
                        Guid.NewGuid(),
                        "invalid-endpoint",
                        a.Id,
                        portB.Id,
                        b.Id,
                        null,
                        null,
                        null,
                        new DateTime(
                            2026,
                            1,
                            1,
                            12,
                            0,
                            0,
                            DateTimeKind.Utc));

                AssertInvalidOperation(
                    () => repository.SavePhysicalLink(link));
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void AssertInvalidOperation(Action action)
        {
            try
            {
                action();
                Assert.Fail(
                    "InvalidOperationException was expected.");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
