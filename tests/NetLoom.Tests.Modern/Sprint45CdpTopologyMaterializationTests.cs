using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint45CdpTopologyMaterializationTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 9, 26, 15, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void MonitoringRuntimeForwardsCdpObservationToTopologyMaterializer()
        {
            var localDeviceId =
                Guid.Parse(
                    "71717171-7171-7171-7171-717171717171");

            var observation =
                new CdpObservation(
                    new Observation(
                        Guid.Parse(
                            "72727272-7272-7272-7272-727272727272"),
                        ObservationKind.Cdp,
                        "192.0.2.71",
                        T1),
                    new CdpRemoteNeighbor[0]);

            var topology =
                new RecordingTopologyMaterializer();

            var runtime =
                new MonitoringRuntime(
                    new NullLldpCollector(),
                    new FixedCdpCollector(
                        observation),
                    new NullFdbCollector(),
                    new NullArpCollector(),
                    null,
                    null,
                    () => T1,
                    null,
                    null,
                    topology);

            var result =
                runtime.PollOnce(
                    new MonitoringPollRequest(
                        IPAddress.Parse(
                            "192.0.2.71"),
                        161,
                        SnmpVersion.V1,
                        new SnmpCommunityCredentials(
                            new byte[]
                            {
                                1,
                                2,
                                3
                            }),
                        1000,
                        0,
                        10,
                        new[]
                        {
                            MonitoringPollKind.Cdp
                        },
                        localDeviceId));

            Assert.IsTrue(
                result.AllSucceeded);

            Assert.AreEqual(
                1,
                topology.CdpCalls);

            Assert.AreEqual(
                localDeviceId,
                topology.LastCdpDeviceId);

            Assert.AreSame(
                observation,
                topology.LastCdpObservation);
        }

        [TestMethod]
        public void CdpNeighborCreatesObservedPhysicalLinkByManagementAddress()
        {
            var databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s45-cdp-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        databasePath);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                var repository =
                    new SqliteMaterializedTopologyRepository(
                        connectionFactory);

                var materializer =
                    new MonitoringTopologyMaterializer(
                        repository);

                var localDeviceId =
                    Guid.Parse(
                        "73737373-7373-7373-7373-737373737373");

                var remoteDeviceId =
                    Guid.Parse(
                        "74747474-7474-7474-7474-747474747474");

                materializer.MaterializeDevice(
                    localDeviceId,
                    T1,
                    "192.0.2.73");

                materializer.MaterializeDevice(
                    remoteDeviceId,
                    T1,
                    "192.0.2.74");

                materializer.MaterializeInterface(
                    localDeviceId,
                    5,
                    T1,
                    "ether5");

                materializer.MaterializeInterface(
                    remoteDeviceId,
                    7,
                    T1,
                    "ether7");

                var observation =
                    new CdpObservation(
                        new Observation(
                            Guid.Parse(
                                "75757575-7575-7575-7575-757575757575"),
                            ObservationKind.Cdp,
                            "192.0.2.73",
                            T1),
                        new[]
                        {
                            new CdpRemoteNeighbor(
                                cacheIfIndex: 5,
                                deviceIndex: 1,
                                addressType: null,
                                address: "192.0.2.74",
                                version: null,
                                deviceId: "remote-switch",
                                devicePort: "ether7",
                                platform: null,
                                capabilities: null,
                                nativeVlan: null,
                                duplex: null,
                                systemName: "remote-switch",
                                systemObjectId: null,
                                primaryManagementAddressType: null,
                                primaryManagementAddress: "192.0.2.74",
                                physicalLocation: null,
                                lastChange: null)
                        });

                materializer.MaterializeCdp(
                    localDeviceId,
                    observation);

                var links =
                    repository
                        .GetPhysicalLinks()
                        .ToArray();

                Assert.AreEqual(
                    1,
                    links.Length);

                var link =
                    links[0];

                var devices =
                    new HashSet<Guid>
                    {
                        link.DeviceAId,
                        link.DeviceBId
                    };

                Assert.IsTrue(
                    devices.Contains(
                        localDeviceId));

                Assert.IsTrue(
                    devices.Contains(
                        remoteDeviceId));

                Assert.AreEqual(
                    "CDP",
                    link.SourceSummary);

                Assert.AreEqual(
                    NetLoom.Domain.Topology.PhysicalLinkStrength.Observed,
                    link.Strength);

                var evidence =
                    repository
                        .GetPhysicalLinkEvidence(
                            link.Id);

                Assert.IsTrue(
                    evidence.Any(
                        item =>
                            item.Kind ==
                            NetLoom.Domain.Topology
                                .PhysicalLinkEvidenceKind.Cdp));
            }
            finally
            {
                DeleteDatabaseFamily(
                    databasePath);
            }
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

        private sealed class FixedCdpCollector :
            ICdpCollector
        {
            private readonly CdpObservation
                _observation;

            public FixedCdpCollector(
                CdpObservation observation)
            {
                _observation =
                    observation;
            }

            public CdpObservation Collect(
                CdpCollectionRequest request)
            {
                return _observation;
            }
        }

        private sealed class NullLldpCollector :
            ILldpCollector
        {
            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NullFdbCollector :
            IFdbCollector
        {
            public FdbObservation Collect(
                FdbCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class NullArpCollector :
            IArpCollector
        {
            public NetLoom.Domain.Observations.Arp.ArpObservation
                Collect(
                    ArpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class RecordingTopologyMaterializer :
            IMonitoringTopologyMaterializer,
            IMonitoringCdpTopologyMaterializer
        {
            public int CdpCalls { get; private set; }

            public Guid LastCdpDeviceId { get; private set; }

            public CdpObservation LastCdpObservation { get; private set; }

            public void MaterializeDevice(
                Guid deviceId,
                DateTime observedUtc,
                string managementAddress = null)
            {
            }

            public void MaterializeInterface(
                Guid deviceId,
                int ifIndex,
                DateTime observedUtc,
                string ifName = null,
                string ifDescription = null,
                string ifAlias = null,
                int? ifType = null)
            {
            }

            public void MaterializeLldp(
                Guid deviceId,
                LldpObservation observation)
            {
            }

            public void MaterializeCdp(
                Guid deviceId,
                CdpObservation observation)
            {
                CdpCalls++;
                LastCdpDeviceId =
                    deviceId;
                LastCdpObservation =
                    observation;
            }
        }
    }
}
