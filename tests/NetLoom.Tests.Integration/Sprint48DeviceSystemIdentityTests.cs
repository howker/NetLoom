using System;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Topology;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Integration
{
    // Sprint 48: Г2 — sysDescr из обнаружения сохраняется; Г1 — категория по возможностям LLDP.
    [TestClass]
    public sealed class Sprint48DeviceSystemIdentityTests
    {
        private static readonly DateTime T0 =
            new DateTime(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void DiscoveredSysDescrIsStoredSurvivesUnawareSavesAndShowsAsModel()
        {
            WithRepository(
                repository =>
                {
                    var deviceId =
                        new DiscoveryCandidateTopologyMaterializer(
                                repository)
                            .Materialize(
                                Candidate(
                                    "192.0.2.31",
                                    "MOXA EDS-518A\r\nFirmware 4.1 build 19"),
                                T0);

                    var stored =
                        repository.GetDevice(deviceId);

                    Assert.AreEqual("MOXA EDS-518A\r\nFirmware 4.1 build 19", stored.SystemDescription);
                    Assert.AreEqual("1.3.6.1.4.1.8691.7.84", stored.SystemObjectId);

                    // Сохранение, которое о sysDescr не знает (как старые места копирования), его не стирает.
                    repository.SaveDevice(
                        new TopologyDevice(
                            stored.Id,
                            stored.LocationId,
                            stored.CustomName,
                            stored.Category,
                            stored.DiscoveryOrigin,
                            stored.MonitoringCapability,
                            null,
                            null,
                            null,
                            false,
                            false,
                            stored.FirstSeenUtc,
                            stored.LastSeenUtc,
                            stored.LastResolvedUtc,
                            stored.DiscoveredName,
                            null,
                            stored.ManagementAddress));

                    Assert.AreEqual(
                        "MOXA EDS-518A\r\nFirmware 4.1 build 19",
                        repository.GetDevice(deviceId).SystemDescription);

                    var node =
                        new MaterializedTopologyMapProjector()
                            .Project(
                                repository.GetDevices(),
                                repository.GetInterfaces(),
                                repository.GetPhysicalLinks(),
                                new Location[0],
                                T0)
                            .Nodes
                            .Single();

                    Assert.AreEqual(
                        "MOXA EDS-518A",
                        node.SecondaryText,
                        "The model column shows the first line of sysDescr.");
                });
        }

        [TestMethod]
        public void LldpCapabilitiesSetCategoryOfAutomaticDevicesButNeverOfManualOnes()
        {
            WithRepository(
                repository =>
                {
                    var materializer =
                        new MonitoringTopologyMaterializer(
                            repository);

                    var localId =
                        Guid.NewGuid();

                    var routerId =
                        Guid.NewGuid();

                    // Сосед уже известен по своему опросу; его время опроса — T0.
                    materializer.MaterializeLldp(
                        routerId,
                        Lldp(
                            "192.0.2.2",
                            T0,
                            "02:00:00:00:00:02",
                            "edge-router",
                            null,
                            new LldpRemoteNeighbor[0]));

                    var routerBefore =
                        repository.GetDevice(routerId);

                    materializer.MaterializeLldp(
                        localId,
                        Lldp(
                            "192.0.2.1",
                            T0.AddMinutes(5),
                            "02:00:00:00:00:01",
                            "core-switch",
                            "28:00",
                            new[]
                            {
                                Neighbor(
                                    "02:00:00:00:00:02",
                                    "edge-router",
                                    "RouterOS RB4011",
                                    "08:00")
                            }));

                    Assert.AreEqual(
                        DeviceCategory.Switch,
                        repository.GetDevice(localId).Category,
                        "Own lldpLocSysCapEnabled: bridge + router is an L3 switch.");

                    var router =
                        repository.GetDevice(routerId);

                    Assert.AreEqual(DeviceCategory.Router, router.Category, "The neighbour reports the router capability.");
                    Assert.AreEqual("RouterOS RB4011", router.SystemDescription, "The neighbour reports sysDescr.");
                    Assert.AreEqual(
                        routerBefore.LastSeenUtc,
                        router.LastSeenUtc,
                        "A neighbour report is not a poll of the router.");

                    var manualId =
                        Guid.NewGuid();

                    repository.SaveDevice(
                        new TopologyDevice(
                            manualId,
                            null,
                            "Media converter",
                            DeviceCategory.MediaConverter,
                            DeviceDiscoveryOrigin.Manual,
                            MonitoringCapability.None,
                            null,
                            null,
                            null,
                            false,
                            false,
                            T0,
                            T0,
                            null,
                            null,
                            "02:00:00:00:00:03"));

                    materializer.MaterializeLldp(
                        localId,
                        Lldp(
                            "192.0.2.1",
                            T0.AddMinutes(10),
                            "02:00:00:00:00:01",
                            "core-switch",
                            "28:00",
                            new[]
                            {
                                Neighbor(
                                    "02:00:00:00:00:03",
                                    "converter",
                                    "Some description",
                                    "20:00")
                            }));

                    var manual =
                        repository.GetDevice(manualId);

                    Assert.AreEqual(DeviceCategory.MediaConverter, manual.Category, "Manual category is the operator's.");
                    Assert.IsNull(manual.SystemDescription);
                });
        }

        private static DiscoveryCandidateSnapshot Candidate(
            string address,
            string sysDescription)
        {
            return new DiscoveryCandidateSnapshot(
                IPAddress.Parse(address),
                Guid.NewGuid(),
                true,
                true,
                new[] { 22 },
                "stand-sw-31",
                sysDescription,
                "1.3.6.1.4.1.8691.7.84",
                null,
                8);
        }

        private static LldpObservation Lldp(
            string sourceAddress,
            DateTime capturedUtc,
            string chassisId,
            string systemName,
            string capabilitiesEnabled,
            LldpRemoteNeighbor[] neighbors)
        {
            return new LldpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Lldp,
                    sourceAddress,
                    capturedUtc),
                neighbors,
                new LldpLocalSystem(
                    4,
                    chassisId,
                    systemName,
                    capabilitiesEnabled));
        }

        private static LldpRemoteNeighbor Neighbor(
            string chassisId,
            string systemName,
            string systemDescription,
            string capabilitiesEnabled)
        {
            return new LldpRemoteNeighbor(
                100,
                1,
                1,
                4,
                chassisId,
                5,
                "Gi1",
                null,
                systemName,
                systemDescription,
                capabilitiesEnabled,
                capabilitiesEnabled,
                new LldpLocalPort(
                    1,
                    5,
                    "Gi1/0/1",
                    null));
        }

        private static void WithRepository(
            Action<SqliteMaterializedTopologyRepository> action)
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s48-system-identity-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        path);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                action(
                    new SqliteMaterializedTopologyRepository(
                        connectionFactory));
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

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
