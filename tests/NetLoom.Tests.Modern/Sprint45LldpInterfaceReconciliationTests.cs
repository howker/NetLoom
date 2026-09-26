using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Topology;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint45LldpInterfaceReconciliationTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 9, 26, 16, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddSeconds(10);

        private static readonly DateTime T3 =
            T1.AddSeconds(20);

        [TestMethod]
        public void RepositoryRebindsAutomaticLinkAndDeletesSyntheticInterface()
        {
            var databasePath =
                TempDatabasePath(
                    "repository");

            try
            {
                var repository =
                    CreateRepository(
                        databasePath);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var localDeviceId =
                    Guid.Parse(
                        "81818181-8181-8181-8181-818181818181");

                var remoteDeviceId =
                    Guid.Parse(
                        "82828282-8282-8282-8282-828282828282");

                materializer.MaterializeDevice(
                    localDeviceId,
                    T1,
                    "192.0.2.81");

                materializer.MaterializeDevice(
                    remoteDeviceId,
                    T1,
                    "192.0.2.82");

                materializer.MaterializeInterface(
                    localDeviceId,
                    5,
                    T1,
                    "Ethernet Port 5");

                materializer.MaterializeInterface(
                    remoteDeviceId,
                    2,
                    T1,
                    "Ethernet Port 2");

                var canonicalLocalInterfaceId =
                    InterfaceId(
                        repository,
                        localDeviceId,
                        5);

                var canonicalRemoteInterfaceId =
                    InterfaceId(
                        repository,
                        remoteDeviceId,
                        2);

                var syntheticInterfaceId =
                    Guid.Parse(
                        "83838383-8383-8383-8383-838383838383");

                repository.SaveInterface(
                    new DeviceInterface(
                        syntheticInterfaceId,
                        localDeviceId,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        false,
                        T1,
                        T1,
                        "5",
                        "LLDP synthetic port"));

                var linkId =
                    Guid.Parse(
                        "84848484-8484-8484-8484-848484848484");

                repository.SavePhysicalLink(
                    new PhysicalLink(
                        linkId,
                        localDeviceId,
                        syntheticInterfaceId,
                        remoteDeviceId,
                        canonicalRemoteInterfaceId,
                        PhysicalLinkStrength.Observed,
                        PhysicalLinkFreshness.Fresh,
                        null,
                        null,
                        "LLDP",
                        T1,
                        T1,
                        null,
                        "monitoring-lldp-v1",
                        false,
                        false,
                        null));

                repository.ReplacePhysicalLinkEvidence(
                    linkId,
                    new[]
                    {
                        new PhysicalLinkEvidence(
                            linkId,
                            PhysicalLinkEvidenceKind.Lldp,
                            PhysicalLinkEvidenceStrength.Strong,
                            "192.0.2.81",
                            "device:81818181818181818181818181818181|lldp-local:index:5",
                            Guid.Parse(
                                "85858585-8585-8585-8585-858585858585"),
                            T1,
                            "LLDP adjacency")
                    });

                var reconciler =
                    repository
                        as IAutomaticInterfaceReferenceReconciler;

                Assert.IsNotNull(
                    reconciler);

                reconciler
                    .ReconcileAutomaticInterfaceReferences(
                        syntheticInterfaceId,
                        canonicalLocalInterfaceId);

                var links =
                    repository
                        .GetPhysicalLinks()
                        .ToArray();

                Assert.AreEqual(
                    1,
                    links.Length);

                Assert.AreEqual(
                    linkId,
                    links[0].Id);

                Assert.AreEqual(
                    canonicalLocalInterfaceId,
                    InterfaceForDevice(
                        links[0],
                        localDeviceId));

                Assert.AreEqual(
                    canonicalRemoteInterfaceId,
                    InterfaceForDevice(
                        links[0],
                        remoteDeviceId));

                Assert.IsFalse(
                    repository
                        .GetInterfaces()
                        .Any(
                            item =>
                                item.Id ==
                                syntheticInterfaceId));

                Assert.AreEqual(
                    1,
                    repository
                        .GetPhysicalLinkEvidence(
                            linkId)
                        .Count);
            }
            finally
            {
                DeleteDatabaseFamily(
                    databasePath);
            }
        }

        [TestMethod]
        public void ReciprocalNumericLldpUsesExistingIfIndexInterfaces()
        {
            var databasePath =
                TempDatabasePath(
                    "lldp");

            try
            {
                var repository =
                    CreateRepository(
                        databasePath);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var localDeviceId =
                    Guid.Parse(
                        "86868686-8686-8686-8686-868686868686");

                var remoteDeviceId =
                    Guid.Parse(
                        "87878787-8787-8787-8787-878787878787");

                materializer.MaterializeDevice(
                    localDeviceId,
                    T1,
                    "192.0.2.86");

                materializer.MaterializeDevice(
                    remoteDeviceId,
                    T1,
                    "192.0.2.87");

                materializer.MaterializeLldp(
                    localDeviceId,
                    IdentityObservation(
                        Guid.Parse(
                            "88888888-8888-8888-8888-888888888888"),
                        "192.0.2.86",
                        "local-chassis",
                        "Local switch"));

                materializer.MaterializeLldp(
                    remoteDeviceId,
                    IdentityObservation(
                        Guid.Parse(
                            "89898989-8989-8989-8989-898989898989"),
                        "192.0.2.87",
                        "remote-chassis",
                        "Remote switch"));

                materializer.MaterializeInterface(
                    localDeviceId,
                    5,
                    T1,
                    "Ethernet Port 5");

                materializer.MaterializeInterface(
                    remoteDeviceId,
                    2,
                    T1,
                    "Ethernet Port 2");

                var localInterfaceId =
                    InterfaceId(
                        repository,
                        localDeviceId,
                        5);

                var remoteInterfaceId =
                    InterfaceId(
                        repository,
                        remoteDeviceId,
                        2);

                materializer.MaterializeLldp(
                    localDeviceId,
                    AdjacencyObservation(
                        Guid.Parse(
                            "90909090-9090-9090-9090-909090909090"),
                        "192.0.2.86",
                        T2,
                        5,
                        "5",
                        "remote-chassis",
                        "Remote switch",
                        "2"));

                materializer.MaterializeLldp(
                    remoteDeviceId,
                    AdjacencyObservation(
                        Guid.Parse(
                            "91919191-9191-9191-9191-919191919191"),
                        "192.0.2.87",
                        T3,
                        2,
                        "2",
                        "local-chassis",
                        "Local switch",
                        "5"));

                var links =
                    repository
                        .GetPhysicalLinks()
                        .ToArray();

                Assert.AreEqual(
                    1,
                    links.Length);

                Assert.AreEqual(
                    localInterfaceId,
                    InterfaceForDevice(
                        links[0],
                        localDeviceId));

                Assert.AreEqual(
                    remoteInterfaceId,
                    InterfaceForDevice(
                        links[0],
                        remoteDeviceId));

                Assert.AreEqual(
                    PhysicalLinkStrength.Confirmed,
                    links[0].Strength);

                var interfaces =
                    repository
                        .GetInterfaces()
                        .Where(
                            item =>
                                item.DeviceId ==
                                    localDeviceId ||
                                item.DeviceId ==
                                    remoteDeviceId)
                        .ToArray();

                Assert.AreEqual(
                    2,
                    interfaces.Length);

                Assert.IsFalse(
                    interfaces.Any(
                        item =>
                            !item.IfIndex.HasValue));
            }
            finally
            {
                DeleteDatabaseFamily(
                    databasePath);
            }
        }

        private static SqliteMaterializedTopologyRepository
            CreateRepository(
                string databasePath)
        {
            var connectionFactory =
                new SqliteConnectionFactory(
                    databasePath);

            new DatabaseInitializer(
                connectionFactory)
                .Initialize();

            return
                new SqliteMaterializedTopologyRepository(
                    connectionFactory);
        }

        private static LldpObservation IdentityObservation(
            Guid observationId,
            string sourceAddress,
            string chassisId,
            string systemName)
        {
            return
                new LldpObservation(
                    new Observation(
                        observationId,
                        ObservationKind.Lldp,
                        sourceAddress,
                        T1),
                    new LldpRemoteNeighbor[0],
                    new LldpLocalSystem(
                        4,
                        chassisId,
                        systemName));
        }

        private static LldpObservation AdjacencyObservation(
            Guid observationId,
            string sourceAddress,
            DateTime capturedUtc,
            int localPortNumber,
            string localPortId,
            string remoteChassisId,
            string remoteSystemName,
            string remotePortId)
        {
            return
                new LldpObservation(
                    new Observation(
                        observationId,
                        ObservationKind.Lldp,
                        sourceAddress,
                        capturedUtc),
                    new[]
                    {
                        new LldpRemoteNeighbor(
                            1,
                            localPortNumber,
                            1,
                            4,
                            remoteChassisId,
                            7,
                            remotePortId,
                            null,
                            remoteSystemName,
                            null,
                            null,
                            null,
                            new LldpLocalPort(
                                localPortNumber,
                                7,
                                localPortId,
                                null))
                    });
        }

        private static Guid InterfaceId(
            SqliteMaterializedTopologyRepository repository,
            Guid deviceId,
            int ifIndex)
        {
            return
                repository
                    .GetInterfaces()
                    .Single(
                        item =>
                            item.DeviceId ==
                                deviceId &&
                            item.IfIndex ==
                                ifIndex)
                    .Id;
        }

        private static Guid? InterfaceForDevice(
            PhysicalLink link,
            Guid deviceId)
        {
            if (link.DeviceAId ==
                deviceId)
            {
                return link.InterfaceAId;
            }

            if (link.DeviceBId ==
                deviceId)
            {
                return link.InterfaceBId;
            }

            Assert.Fail(
                "Link does not contain expected device.");

            return null;
        }

        private static string TempDatabasePath(
            string suffix)
        {
            return
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s45-lldp-reconcile-" +
                    suffix +
                    "-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");
        }

        private static void DeleteDatabaseFamily(
            string databasePath)
        {
            foreach (var path in new[]
            {
                databasePath,
                databasePath + "-wal",
                databasePath + "-shm"
            })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
