using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint37ManualTopologyIntegrationTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                17,
                10,
                30,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void CreateEditAndRestartRoundTripUsesSharedTablesAndManualAudit()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var service =
                    CreateService(factory);

                var deviceId =
                    service.CreateDevice(
                        "Converter",
                        ManualTopologyDeviceCategory.MediaConverter,
                        "Before");

                service.UpdateDevice(
                    deviceId,
                    "Converter 2",
                    ManualTopologyDeviceCategory.UnmanagedSwitch,
                    "After");

                var portId =
                    service.CreatePort(
                        deviceId,
                        "FX",
                        "Fiber");

                service.UpdatePort(
                    portId,
                    "TX",
                    "Copper");

                var reopened =
                    CreateService(
                        new SqliteConnectionFactory(
                            path));

                var snapshot =
                    reopened.GetSnapshot();

                var device =
                    FindDevice(
                        snapshot,
                        deviceId);

                var port =
                    FindPort(
                        snapshot,
                        portId);

                Assert.IsNotNull(device);
                Assert.IsNotNull(port);

                Assert.IsTrue(device.IsManual);
                Assert.IsTrue(device.CanEdit);

                Assert.AreEqual(
                    "Converter 2",
                    device.DisplayName);

                Assert.AreEqual(
                    ManualTopologyDeviceCategory.UnmanagedSwitch,
                    device.Category);

                Assert.AreEqual(
                    "After",
                    device.Notes);

                Assert.AreEqual(
                    "TX",
                    port.DisplayName);

                Assert.AreEqual(
                    "Copper",
                    port.MediaType);

                Assert.AreEqual(
                    4L,
                    CountManualAuditObservations(
                        factory));
            }
            finally
            {
                DeleteDatabase(path);
            }
        }

        [TestMethod]
        public void ManualLinkBetweenAutomaticAndManualEndpointRoundTripsAndKeepsIdentityOnMetadataEdit()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var automatic =
                    CreateAutomaticDevice(
                        Guid.NewGuid(),
                        "Switch");

                repository.SaveDevice(
                    automatic);

                var automaticPort =
                    CreateAutomaticPort(
                        Guid.NewGuid(),
                        automatic.Id,
                        10,
                        "if10");

                repository.SaveInterface(
                    automaticPort);

                var service =
                    CreateService(factory);

                var manualId =
                    service.CreateDevice(
                        "Converter",
                        ManualTopologyDeviceCategory.MediaConverter,
                        null);

                var manualPortId =
                    service.CreatePort(
                        manualId,
                        "FX",
                        "Fiber");

                var linkId =
                    service.CreateLink(
                        automatic.Id,
                        automaticPort.Id,
                        manualId,
                        manualPortId,
                        "Fiber",
                        "Before");

                service.UpdateLink(
                    linkId,
                    "Copper",
                    "After");

                var reopened =
                    CreateService(
                        new SqliteConnectionFactory(
                            path));

                var link =
                    FindLink(
                        reopened.GetSnapshot(),
                        linkId);

                Assert.IsNotNull(link);

                Assert.AreEqual(
                    linkId,
                    link.PhysicalLinkId);

                Assert.IsTrue(
                    link.IsManual);

                Assert.AreEqual(
                    "Copper",
                    link.MediaType);

                Assert.AreEqual(
                    "After",
                    link.Notes);

                var stored =
                    FindPhysicalLink(
                        new SqliteMaterializedTopologyRepository(
                            new SqliteConnectionFactory(
                                path)),
                        linkId);

                Assert.AreEqual(
                    PhysicalLinkStrength.Manual,
                    stored.Strength);

                Assert.AreEqual(
                    automaticPort.Id,
                    InterfaceForDevice(
                        stored,
                        automatic.Id));

                Assert.AreEqual(
                    manualPortId,
                    InterfaceForDevice(
                        stored,
                        manualId));
            }
            finally
            {
                DeleteDatabase(path);
            }
        }

        [TestMethod]
        public void ConnectedManualPortAndDeviceCannotBeDeletedBeforeCable()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var service =
                    CreateService(factory);

                var first =
                    service.CreateDevice(
                        "First",
                        ManualTopologyDeviceCategory.MediaConverter,
                        null);

                var second =
                    service.CreateDevice(
                        "Second",
                        ManualTopologyDeviceCategory.MediaConverter,
                        null);

                var firstPort =
                    service.CreatePort(
                        first,
                        "P1",
                        "Fiber");

                var secondPort =
                    service.CreatePort(
                        second,
                        "P1",
                        "Fiber");

                var link =
                    service.CreateLink(
                        first,
                        firstPort,
                        second,
                        secondPort,
                        "Fiber",
                        null);

                AssertThrows<InvalidOperationException>(
                    () =>
                        service.DeletePort(
                            firstPort));

                AssertThrows<InvalidOperationException>(
                    () =>
                        service.DeleteDevice(
                            first));

                service.DeleteLink(
                    link);

                service.DeletePort(
                    firstPort);

                service.DeleteDevice(
                    first);

                var snapshot =
                    service.GetSnapshot();

                Assert.IsNull(
                    FindDevice(
                        snapshot,
                        first));

                Assert.IsNull(
                    FindPort(
                        snapshot,
                        firstPort));
            }
            finally
            {
                DeleteDatabase(path);
            }
        }

        [TestMethod]
        public void AutomaticElementsAreReadOnlyToManualService()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var first =
                    CreateAutomaticDevice(
                        Guid.NewGuid(),
                        "First");

                var second =
                    CreateAutomaticDevice(
                        Guid.NewGuid(),
                        "Second");

                repository.SaveDevice(first);
                repository.SaveDevice(second);

                var port =
                    CreateAutomaticPort(
                        Guid.NewGuid(),
                        first.Id,
                        1,
                        "if1");

                repository.SaveInterface(port);

                var link =
                    new PhysicalLink(
                        Guid.NewGuid(),
                        first.Id,
                        port.Id,
                        second.Id,
                        null,
                        PhysicalLinkStrength.Confirmed,
                        PhysicalLinkFreshness.Fresh,
                        "Copper",
                        null,
                        "LLDP",
                        Now,
                        Now,
                        Now,
                        null,
                        false,
                        false,
                        null);

                repository.SavePhysicalLink(
                    link);

                var service =
                    CreateService(factory);

                AssertThrows<InvalidOperationException>(
                    () =>
                        service.UpdateDevice(
                            first.Id,
                            "Edited",
                            ManualTopologyDeviceCategory.Unknown,
                            null));

                AssertThrows<InvalidOperationException>(
                    () =>
                        service.UpdatePort(
                            port.Id,
                            "Edited",
                            "Fiber"));

                AssertThrows<InvalidOperationException>(
                    () =>
                        service.UpdateLink(
                            link.Id,
                            "Fiber",
                            null));

                AssertThrows<InvalidOperationException>(
                    () =>
                        service.DeleteLink(
                            link.Id));

                Assert.AreEqual(
                    0L,
                    CountManualAuditObservations(
                        factory));
            }
            finally
            {
                DeleteDatabase(path);
            }
        }

        [TestMethod]
        public void CompatibleAutomaticCableCannotBeConvertedToManual()
        {
            var path =
                NewDatabasePath();

            try
            {
                var factory =
                    Initialize(path);

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var first =
                    CreateAutomaticDevice(
                        Guid.NewGuid(),
                        "First");

                var second =
                    CreateAutomaticDevice(
                        Guid.NewGuid(),
                        "Second");

                repository.SaveDevice(first);
                repository.SaveDevice(second);

                var existing =
                    new PhysicalLink(
                        Guid.NewGuid(),
                        first.Id,
                        null,
                        second.Id,
                        null,
                        PhysicalLinkStrength.Observed,
                        PhysicalLinkFreshness.Fresh,
                        "Copper",
                        null,
                        "LLDP",
                        Now,
                        Now,
                        null,
                        null,
                        false,
                        false,
                        null);

                repository.SavePhysicalLink(
                    existing);

                var service =
                    CreateService(factory);

                AssertThrows<InvalidOperationException>(
                    () =>
                        service.CreateLink(
                            first.Id,
                            null,
                            second.Id,
                            null,
                            "Fiber",
                            "Manual"));

                var stored =
                    FindPhysicalLink(
                        repository,
                        existing.Id);

                Assert.AreEqual(
                    PhysicalLinkStrength.Observed,
                    stored.Strength);

                Assert.AreEqual(
                    "Copper",
                    stored.MediaTypeResolved);

                Assert.AreEqual(
                    0L,
                    CountManualAuditObservations(
                        factory));
            }
            finally
            {
                DeleteDatabase(path);
            }
        }

        private static ManualTopologyService
            CreateService(
                SqliteConnectionFactory factory)
        {
            return new ManualTopologyService(
                new SqliteMaterializedTopologyRepository(
                    factory),
                new SqliteManualTopologyAuditStore(
                    factory),
                () => Now);
        }

        private static SqliteConnectionFactory
            Initialize(
                string path)
        {
            var factory =
                new SqliteConnectionFactory(
                    path);

            new DatabaseInitializer(
                factory)
                .Initialize();

            return factory;
        }

        private static TopologyDevice
            CreateAutomaticDevice(
                Guid id,
                string name)
        {
            return new TopologyDevice(
                id,
                null,
                name,
                DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic,
                MonitoringCapability.Unknown,
                null,
                null,
                null,
                false,
                false,
                Now,
                Now,
                Now);
        }

        private static DeviceInterface
            CreateAutomaticPort(
                Guid id,
                Guid deviceId,
                int ifIndex,
                string ifName)
        {
            return new DeviceInterface(
                id,
                deviceId,
                ifIndex,
                ifName,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "Copper",
                null,
                false,
                false,
                Now,
                Now);
        }

        private static long
            CountManualAuditObservations(
                SqliteConnectionFactory factory)
        {
            using (var connection =
                factory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT COUNT(*)
FROM observations
WHERE observation_kind = @kind
  AND source_address = @source;";

                command.Parameters.AddWithValue(
                    "@kind",
                    "Manual");

                command.Parameters.AddWithValue(
                    "@source",
                    "User");

                return Convert.ToInt64(
                    command.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }
        }

        private static ManualTopologyDeviceItem
            FindDevice(
                ManualTopologyEditorSnapshot snapshot,
                Guid id)
        {
            foreach (var item in snapshot.Devices)
            {
                if (item.DeviceId == id)
                {
                    return item;
                }
            }

            return null;
        }

        private static ManualTopologyPortItem
            FindPort(
                ManualTopologyEditorSnapshot snapshot,
                Guid id)
        {
            foreach (var item in snapshot.Ports)
            {
                if (item.InterfaceId == id)
                {
                    return item;
                }
            }

            return null;
        }

        private static ManualTopologyLinkItem
            FindLink(
                ManualTopologyEditorSnapshot snapshot,
                Guid id)
        {
            foreach (var item in snapshot.Links)
            {
                if (item.PhysicalLinkId == id)
                {
                    return item;
                }
            }

            return null;
        }

        private static PhysicalLink
            FindPhysicalLink(
                SqliteMaterializedTopologyRepository repository,
                Guid id)
        {
            foreach (var item in
                repository.GetPhysicalLinks())
            {
                if (item.Id == id)
                {
                    return item;
                }
            }

            return null;
        }

        private static Guid?
            InterfaceForDevice(
                PhysicalLink link,
                Guid deviceId)
        {
            if (link.DeviceAId == deviceId)
            {
                return link.InterfaceAId;
            }

            if (link.DeviceBId == deviceId)
            {
                return link.InterfaceBId;
            }

            Assert.Fail(
                "Device is not an endpoint.");

            return null;
        }

        private static string
            NewDatabasePath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "NetLoom-Sprint37-" +
                Guid.NewGuid().ToString("N") +
                ".db");
        }

        private static void
            DeleteDatabase(
                string path)
        {
            foreach (var suffix in
                new[]
                {
                    string.Empty,
                    "-wal",
                    "-shm"
                })
            {
                try
                {
                    File.Delete(
                        path + suffix);
                }
                catch
                {
                }
            }
        }

        private static void AssertThrows<T>(
            Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            Assert.Fail(
                "Expected exception: " +
                typeof(T).FullName);
        }
    }
}
