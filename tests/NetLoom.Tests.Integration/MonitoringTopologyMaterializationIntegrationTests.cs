using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Map;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class
        MonitoringTopologyMaterializationIntegrationTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026,
                9,
                10,
                8,
                0,
                0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddMinutes(5);

        [TestMethod]
        public void
            ExplicitPolledDeviceAndIfIndexFeedMaterializedMapWithoutDuplicates()
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
                            "monitoring-topology.db"));

                new DatabaseInitializer(
                    factory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var deviceId =
                    new Guid(
                        "11111111-1111-1111-1111-111111111111");

                materializer.MaterializeDevice(
                    deviceId,
                    T1);

                materializer.MaterializeInterface(
                    deviceId,
                    17,
                    T1);

                materializer.MaterializeDevice(
                    deviceId,
                    T2);

                materializer.MaterializeInterface(
                    deviceId,
                    17,
                    T2);

                Assert.AreEqual(
                    1,
                    repository.GetDevices().Count);

                Assert.AreEqual(
                    1,
                    repository.GetInterfaces().Count);

                Assert.AreEqual(
                    0,
                    repository.GetPhysicalLinks().Count);

                var networkInterface =
                    repository
                        .GetInterfaces()
                        .Single();

                Assert.AreEqual(
                    deviceId,
                    networkInterface.DeviceId);

                Assert.AreEqual(
                    17,
                    networkInterface.IfIndex);

                Assert.AreEqual(
                    T1,
                    networkInterface.FirstSeenUtc);

                Assert.AreEqual(
                    T2,
                    networkInterface.LastSeenUtc);

                var provider =
                    new MaterializedMapSnapshotProvider(
                        repository,
                        new SqliteLocationRepository(
                            factory),
                        new MaterializedTopologyMapProjector(),
                        () => T2);

                var snapshot =
                    provider.GetSnapshot();

                Assert.AreEqual(
                    1,
                    snapshot.Nodes.Count);

                Assert.AreEqual(
                    deviceId,
                    snapshot.Nodes[0].DeviceId);

                Assert.AreEqual(
                    0,
                    snapshot.Links.Count);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        [TestMethod]
        public void
            ReciprocalLldpMaterializesNamedDevicesPortsAndSinglePhysicalLink()
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
                            "lldp-topology.db"));

                new DatabaseInitializer(factory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var deviceA =
                    new Guid(
                        "11111111-1111-1111-1111-111111111111");

                var deviceB =
                    new Guid(
                        "22222222-2222-2222-2222-222222222222");

                materializer.MaterializeLldp(
                    deviceA,
                    Lldp(
                        "192.0.2.10",
                        T1,
                        "02:00:00:00:00:01",
                        "stand-sw-01",
                        1,
                        "Gi1/0/1",
                        "02:00:00:00:00:02",
                        "stand-sw-02",
                        "Gi1/0/24"));

                Assert.AreEqual(
                    0,
                    repository.GetPhysicalLinks().Count);

                materializer.MaterializeLldp(
                    deviceB,
                    Lldp(
                        "192.0.2.20",
                        T2,
                        "02:00:00:00:00:02",
                        "stand-sw-02",
                        24,
                        "Gi1/0/24",
                        "02:00:00:00:00:01",
                        "stand-sw-01",
                        "Gi1/0/1"));

                Assert.AreEqual(
                    PhysicalLinkStrength.Observed,
                    repository
                        .GetPhysicalLinks()
                        .Single()
                        .Strength);

                materializer.MaterializeLldp(
                    deviceA,
                    Lldp(
                        "192.0.2.10",
                        T2,
                        "02:00:00:00:00:01",
                        "stand-sw-01",
                        1,
                        "Gi1/0/1",
                        "02:00:00:00:00:02",
                        "stand-sw-02",
                        "Gi1/0/24"));

                Assert.AreEqual(
                    2,
                    repository.GetDevices().Count);

                Assert.AreEqual(
                    2,
                    repository.GetInterfaces().Count);

                Assert.AreEqual(
                    1,
                    repository.GetPhysicalLinks().Count);

                Assert.AreEqual(
                    PhysicalLinkStrength.Confirmed,
                    repository
                        .GetPhysicalLinks()
                        .Single()
                        .Strength);

                var storedA =
                    repository.GetDevice(deviceA);

                var storedB =
                    repository.GetDevice(deviceB);

                Assert.AreEqual(
                    "stand-sw-01",
                    storedA.DiscoveredName);

                Assert.AreEqual(
                    "02:00:00:00:00:01",
                    storedA.LldpChassisId);

                Assert.AreEqual(
                    "stand-sw-02",
                    storedB.DiscoveredName);

                Assert.AreEqual(
                    2,
                    repository
                        .GetPhysicalLinkEvidence()
                        .Count);

                var readSet =
                    new SqliteMaterializedTopologyReadSetReader(
                        factory)
                    .Read("cist");

                var snapshot =
                    new MaterializedMapSnapshotProvider(
                        repository,
                        new SqliteLocationRepository(factory),
                        new MaterializedTopologyMapProjector(),
                        () => T2)
                    .GetSnapshot(readSet);

                Assert.AreEqual(
                    2,
                    snapshot.Nodes.Count);

                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "stand-sw-01",
                        "stand-sw-02"
                    },
                    snapshot.Nodes
                        .Select(item => item.Label)
                        .ToArray());

                Assert.AreEqual(
                    1,
                    snapshot.Links.Count);

                var link = snapshot.Links.Single();

                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "Gi1/0/1",
                        "Gi1/0/24"
                    },
                    new[]
                    {
                        link.SourcePortLabel,
                        link.TargetPortLabel
                    });
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        [TestMethod]
        public void
            AmbiguousRemoteChassisDoesNotInventPhysicalLink()
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
                            "ambiguous-lldp.db"));

                new DatabaseInitializer(factory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var source = Guid.NewGuid();
                var duplicateA = Guid.NewGuid();
                var duplicateB = Guid.NewGuid();

                materializer.MaterializeLldp(
                    duplicateA,
                    EmptyLldp(
                        "192.0.2.11",
                        T1,
                        "02:00:00:00:00:AA",
                        "duplicate-a"));

                materializer.MaterializeLldp(
                    duplicateB,
                    EmptyLldp(
                        "192.0.2.12",
                        T1,
                        "02-00-00-00-00-AA",
                        "duplicate-b"));

                materializer.MaterializeLldp(
                    source,
                    Lldp(
                        "192.0.2.10",
                        T2,
                        "02:00:00:00:00:10",
                        "source",
                        1,
                        "Gi1/0/1",
                        "02:00:00:00:00:AA",
                        "duplicate",
                        "Gi1/0/2"));

                Assert.AreEqual(
                    0,
                    repository.GetPhysicalLinks().Count);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }



        [TestMethod]
        public void
            ParallelLldpPortsBetweenSameDevicesRemainDistinct()
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
                            "parallel-lldp.db"));

                new DatabaseInitializer(factory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var deviceA = Guid.NewGuid();
                var deviceB = Guid.NewGuid();

                materializer.MaterializeLldp(
                    deviceB,
                    EmptyLldp(
                        "192.0.2.20",
                        T1,
                        "02:00:00:00:00:02",
                        "stand-sw-02"));

                var observation =
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Lldp,
                        "192.0.2.10",
                        T2);

                materializer.MaterializeLldp(
                    deviceA,
                    new LldpObservation(
                        observation,
                        new[]
                        {
                            Neighbor(
                                1,
                                1,
                                "Gi1/0/1",
                                "02:00:00:00:00:02",
                                "stand-sw-02",
                                "Gi1/0/23"),
                            Neighbor(
                                2,
                                2,
                                "Gi1/0/2",
                                "02:00:00:00:00:02",
                                "stand-sw-02",
                                "Gi1/0/24")
                        },
                        new LldpLocalSystem(
                            4,
                            "02:00:00:00:00:01",
                            "stand-sw-01")));

                Assert.AreEqual(
                    2,
                    repository.GetPhysicalLinks().Count);

                Assert.AreEqual(
                    4,
                    repository.GetInterfaces().Count);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        [TestMethod]
        public void
            LldpIdentityEnrichesManualDeviceWithoutReplacingCustomName()
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
                            "manual-name.db"));

                new DatabaseInitializer(factory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        factory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var deviceId = Guid.NewGuid();

                repository.SaveDevice(
                    new TopologyDevice(
                        deviceId,
                        null,
                        "Operator name",
                        DeviceCategory.Unknown,
                        DeviceDiscoveryOrigin.Manual,
                        MonitoringCapability.Unknown,
                        null,
                        null,
                        null,
                        false,
                        false,
                        T1,
                        T1,
                        T1));

                materializer.MaterializeLldp(
                    deviceId,
                    EmptyLldp(
                        "192.0.2.10",
                        T2,
                        "02:00:00:00:00:01",
                        "discovered-name"));

                var stored =
                    repository.GetDevice(deviceId);

                Assert.AreEqual(
                    "Operator name",
                    stored.CustomName);

                Assert.AreEqual(
                    DeviceDiscoveryOrigin.Manual,
                    stored.DiscoveryOrigin);

                Assert.AreEqual(
                    "discovered-name",
                    stored.DiscoveredName);

                repository.SaveDevice(
                    new TopologyDevice(
                        deviceId,
                        null,
                        "Operator renamed",
                        DeviceCategory.Unknown,
                        DeviceDiscoveryOrigin.Manual,
                        MonitoringCapability.Unknown,
                        null,
                        null,
                        null,
                        false,
                        false,
                        T1,
                        T2,
                        T2));

                stored =
                    repository.GetDevice(deviceId);

                Assert.AreEqual(
                    "discovered-name",
                    stored.DiscoveredName);

                Assert.AreEqual(
                    "02:00:00:00:00:01",
                    stored.LldpChassisId);

                var snapshot =
                    new MaterializedMapSnapshotProvider(
                        repository,
                        new SqliteLocationRepository(factory),
                        new MaterializedTopologyMapProjector(),
                        () => T2)
                    .GetSnapshot();

                Assert.AreEqual(
                    "Operator renamed",
                    snapshot.Nodes.Single().Label);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection
                    .ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        private static LldpRemoteNeighbor Neighbor(
            int localPortNumber,
            int remoteIndex,
            string localPortId,
            string remoteChassisId,
            string remoteSystemName,
            string remotePortId)
        {
            return new LldpRemoteNeighbor(
                100,
                localPortNumber,
                remoteIndex,
                4,
                remoteChassisId,
                5,
                remotePortId,
                null,
                remoteSystemName,
                null,
                null,
                null,
                new LldpLocalPort(
                    localPortNumber,
                    5,
                    localPortId,
                    null));
        }

        private static LldpObservation Lldp(
            string sourceAddress,
            DateTime capturedUtc,
            string localChassisId,
            string localSystemName,
            int localPortNumber,
            string localPortId,
            string remoteChassisId,
            string remoteSystemName,
            string remotePortId)
        {
            var observation =
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Lldp,
                    sourceAddress,
                    capturedUtc);

            return new LldpObservation(
                observation,
                new[]
                {
                    new LldpRemoteNeighbor(
                        100,
                        localPortNumber,
                        1,
                        4,
                        remoteChassisId,
                        5,
                        remotePortId,
                        null,
                        remoteSystemName,
                        null,
                        null,
                        null,
                        new LldpLocalPort(
                            localPortNumber,
                            5,
                            localPortId,
                            null))
                },
                new LldpLocalSystem(
                    4,
                    localChassisId,
                    localSystemName));
        }

        private static LldpObservation EmptyLldp(
            string sourceAddress,
            DateTime capturedUtc,
            string localChassisId,
            string localSystemName)
        {
            return new LldpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Lldp,
                    sourceAddress,
                    capturedUtc),
                new LldpRemoteNeighbor[0],
                new LldpLocalSystem(
                    4,
                    localChassisId,
                    localSystemName));
        }

    }
}
