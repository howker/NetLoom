using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Locations;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint38LocationTopologyServiceTests
    {
        [TestMethod]
        public void LocationHierarchyAndDeviceAssignmentRoundTripWithoutChangingDeviceIdentity()
        {
            WithDatabase(
                factory =>
                {
                    var topology =
                        new SqliteMaterializedTopologyRepository(
                            factory);

                    var locations =
                        new SqliteLocationRepository(
                            factory);

                    var service =
                        new LocationTopologyService(
                            locations,
                            topology);

                    var deviceId =
                        Guid.NewGuid();

                    var observedUtc =
                        new DateTime(
                            2026,
                            9,
                            17,
                            16,
                            0,
                            0,
                            DateTimeKind.Utc);

                    topology.SaveDevice(
                        new TopologyDevice(
                            deviceId,
                            null,
                            "Core switch",
                            DeviceCategory.Unknown,
                            DeviceDiscoveryOrigin.Automatic,
                            MonitoringCapability.Unknown,
                            "Vendor",
                            "Model",
                            "Keep notes",
                            false,
                            false,
                            observedUtc,
                            observedUtc,
                            observedUtc,
                            "core-1",
                            "chassis-1",
                            "192.0.2.44"));

                    var siteId =
                        service.CreateLocation(
                            null,
                            "Site A",
                            "Primary site");

                    var rackId =
                        service.CreateLocation(
                            siteId,
                            "Rack 17",
                            null);

                    service.AssignDevice(
                        deviceId,
                        rackId);

                    var snapshot =
                        service.GetSnapshot();

                    Assert.AreEqual(
                        2,
                        snapshot.Locations.Count);

                    var rack =
                        snapshot.Locations.Single(
                            item => item.Id == rackId);

                    Assert.AreEqual(
                        siteId,
                        rack.ParentLocationId);

                    var device =
                        snapshot.Devices.Single(
                            item =>
                                item.DeviceId ==
                                deviceId);

                    Assert.AreEqual(
                        rackId,
                        device.LocationId);

                    Assert.AreEqual(
                        "Core switch",
                        device.DisplayName);

                    var persisted =
                        topology.GetDevice(
                            deviceId);

                    Assert.AreEqual(
                        deviceId,
                        persisted.Id);

                    Assert.AreEqual(
                        rackId,
                        persisted.LocationId);

                    Assert.AreEqual(
                        DeviceDiscoveryOrigin.Automatic,
                        persisted.DiscoveryOrigin);

                    Assert.AreEqual(
                        "192.0.2.44",
                        persisted.ManagementAddress);

                    Assert.AreEqual(
                        "Keep notes",
                        persisted.Notes);

                    service.UpdateLocation(
                        rackId,
                        siteId,
                        "Rack 17A",
                        "Renamed");

                    Assert.AreEqual(
                        "Rack 17A",
                        locations.Get(rackId).Name);

                    AssertInvalidOperation(
                        () =>
                            service.DeleteLocation(
                                rackId));

                    service.AssignDevice(
                        deviceId,
                        null);

                    Assert.IsNull(
                        topology
                            .GetDevice(deviceId)
                            .LocationId);

                    service.DeleteLocation(
                        rackId);

                    Assert.IsNull(
                        locations.Get(rackId));
                });
        }

        [TestMethod]
        public void HierarchyRejectsMissingParentCyclesAndNonLeafDeletion()
        {
            WithDatabase(
                factory =>
                {
                    var locations =
                        new SqliteLocationRepository(
                            factory);

                    var service =
                        new LocationTopologyService(
                            locations,
                            new SqliteMaterializedTopologyRepository(
                                factory));

                    AssertInvalidOperation(
                        () =>
                            service.CreateLocation(
                                Guid.NewGuid(),
                                "Orphan",
                                null));

                    var siteId =
                        service.CreateLocation(
                            null,
                            "Site",
                            null);

                    var buildingId =
                        service.CreateLocation(
                            siteId,
                            "Building",
                            null);

                    var roomId =
                        service.CreateLocation(
                            buildingId,
                            "Room",
                            null);

                    var rackId =
                        service.CreateLocation(
                            roomId,
                            "Rack",
                            null);

                    AssertInvalidOperation(
                        () =>
                            service.UpdateLocation(
                                siteId,
                                rackId,
                                "Site",
                                null));

                    Assert.AreEqual(
                        (Guid?)null,
                        locations
                            .Get(siteId)
                            .ParentLocationId);

                    AssertInvalidOperation(
                        () =>
                            service.DeleteLocation(
                                buildingId));

                    Assert.IsNotNull(
                        locations.Get(
                            buildingId));

                    service.UpdateLocation(
                        rackId,
                        roomId,
                        "Rack 01",
                        "Renamed without identity change");

                    var renamed =
                        locations.Get(
                            rackId);

                    Assert.AreEqual(
                        rackId,
                        renamed.Id);

                    Assert.AreEqual(
                        "Rack 01",
                        renamed.Name);

                    service.DeleteLocation(
                        rackId);

                    Assert.IsNull(
                        locations.Get(
                            rackId));
                });
        }

        [TestMethod]
        public void AssigningUnknownLocationDoesNotMutateDevice()
        {
            WithDatabase(
                factory =>
                {
                    var topology =
                        new SqliteMaterializedTopologyRepository(
                            factory);

                    var service =
                        new LocationTopologyService(
                            new SqliteLocationRepository(
                                factory),
                            topology);

                    var deviceId =
                        Guid.NewGuid();

                    topology.SaveDevice(
                        new TopologyDevice(
                            deviceId,
                            null,
                            "Manual endpoint",
                            DeviceCategory.UnmanagedSwitch,
                            DeviceDiscoveryOrigin.Manual,
                            MonitoringCapability.None,
                            null,
                            null,
                            null,
                            false,
                            false,
                            null,
                            null,
                            null));

                    AssertInvalidOperation(
                        () =>
                            service.AssignDevice(
                                deviceId,
                                Guid.NewGuid()));

                    Assert.IsNull(
                        topology
                            .GetDevice(deviceId)
                            .LocationId);
                });
        }

        private static void AssertInvalidOperation(
            Action action)
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

        private static void WithDatabase(
            Action<SqliteConnectionFactory> action)
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s38-location-service-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var factory =
                    new SqliteConnectionFactory(
                        path);

                new DatabaseInitializer(factory)
                    .Initialize();

                action(factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();

                foreach (var suffix in
                    new[]
                    {
                        string.Empty,
                        "-wal",
                        "-shm"
                    })
                {
                    var candidate =
                        path + suffix;

                    if (File.Exists(candidate))
                    {
                        File.Delete(candidate);
                    }
                }
            }
        }
    }
}
