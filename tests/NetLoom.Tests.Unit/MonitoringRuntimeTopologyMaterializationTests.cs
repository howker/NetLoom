using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        MonitoringRuntimeTopologyMaterializationTests
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

        private static readonly Guid DeviceId =
            new Guid(
                "11111111-1111-1111-1111-111111111111");

        [TestMethod]
        public void
            SuccessfulBoundLldpPollMaterializesExplicitDeviceId()
        {
            var observation =
                new LldpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Lldp,
                        "192.0.2.10",
                        T1),
                    new LldpRemoteNeighbor[0]);

            var materializer =
                new RecordingTopologyMaterializer();

            var runtime =
                new MonitoringRuntime(
                    new FixedLldpCollector(
                        observation),
                    new NullCdpCollector(),
                    new NullFdbCollector(),
                    new NullArpCollector(),
                    null,
                    null,
                    () => T1,
                    topologyMaterializer:
                        materializer);

            var result =
                runtime.PollOnce(
                    Request(
                        DeviceId));

            Assert.IsTrue(
                result.AllSucceeded);

            Assert.AreEqual(
                0,
                materializer.DeviceCalls);

            Assert.AreEqual(
                1,
                materializer.LldpCalls);

            Assert.AreEqual(
                DeviceId,
                materializer.LastDeviceId);

            Assert.AreEqual(
                T1,
                materializer.LastObservedUtc);
        }

        [TestMethod]
        public void
            UnboundLldpPollDoesNotInventMaterializedDeviceIdentity()
        {
            var observation =
                new LldpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Lldp,
                        "192.0.2.10",
                        T1),
                    new LldpRemoteNeighbor[0]);

            var materializer =
                new RecordingTopologyMaterializer();

            var runtime =
                new MonitoringRuntime(
                    new FixedLldpCollector(
                        observation),
                    new NullCdpCollector(),
                    new NullFdbCollector(),
                    new NullArpCollector(),
                    null,
                    null,
                    () => T1,
                    topologyMaterializer:
                        materializer);

            var result =
                runtime.PollOnce(
                    Request(
                        null));

            Assert.IsTrue(
                result.AllSucceeded);

            Assert.AreEqual(
                0,
                materializer.DeviceCalls);

            Assert.AreEqual(
                0,
                materializer.InterfaceCalls);

            Assert.AreEqual(
                0,
                materializer.LldpCalls);
        }


        [TestMethod]
        public void
            BoundInterfacePollCarriesRealIdentityAndManagementAddressIntoMaterialization()
        {
            var materializer =
                new RecordingTopologyMaterializer();

            var runtime =
                new MonitoringRuntime(
                    new FixedLldpCollector(null),
                    new NullCdpCollector(),
                    new NullFdbCollector(),
                    new NullArpCollector(),
                    null,
                    new FixedInterfaceCollector(),
                    () => T1,
                    topologyMaterializer:
                        materializer);

            var result =
                runtime.PollOnce(
                    new MonitoringPollRequest(
                        IPAddress.Parse("192.0.2.10"),
                        161,
                        SnmpVersion.V2C,
                        new SnmpCommunityCredentials(
                            new byte[] { 1, 2, 3 }),
                        1000,
                        1,
                        10,
                        new[]
                        {
                            MonitoringPollKind.Interface
                        },
                        DeviceId));

            Assert.IsTrue(result.AllSucceeded);
            Assert.AreEqual(1, materializer.DeviceCalls);
            Assert.AreEqual(1, materializer.InterfaceCalls);
            Assert.AreEqual("192.0.2.10", materializer.LastManagementAddress);
            Assert.AreEqual(10107, materializer.LastIfIndex);
            Assert.AreEqual("ge-1/0/1", materializer.LastIfName);
            Assert.AreEqual("uplink", materializer.LastIfAlias);
            Assert.AreEqual(6, materializer.LastIfType);
        }

        private static MonitoringPollRequest Request(
            Guid? deviceId)
        {
            return new MonitoringPollRequest(
                IPAddress.Parse(
                    "192.0.2.10"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[] { 1, 2, 3 }),
                1000,
                1,
                10,
                new[]
                {
                    MonitoringPollKind.Lldp
                },
                deviceId);
        }

        private sealed class FixedLldpCollector :
            ILldpCollector
        {
            private readonly LldpObservation
                _observation;

            public FixedLldpCollector(
                LldpObservation observation)
            {
                _observation = observation;
            }

            public LldpObservation Collect(
                LldpCollectionRequest request)
            {
                return _observation;
            }
        }

        private sealed class NullCdpCollector :
            ICdpCollector
        {
            public CdpObservation Collect(
                CdpCollectionRequest request)
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
            public ArpObservation Collect(
                ArpCollectionRequest request)
            {
                return null;
            }
        }

        private sealed class FixedInterfaceCollector :
            IInterfaceCollector
        {
            public System.Collections.Generic.IReadOnlyList<InterfaceMonitoringSnapshot> Collect(
                InterfaceCollectionRequest request)
            {
                return new[]
                {
                    new InterfaceMonitoringSnapshot(
                        request.DeviceId,
                        10107,
                        1,
                        1,
                        T1,
                        ifName: "ge-1/0/1",
                        ifDescription: "ge-1/0/1",
                        ifAlias: "uplink",
                        ifType: 6)
                };
            }
        }

        private sealed class RecordingTopologyMaterializer :
            IMonitoringTopologyMaterializer
        {
            public int DeviceCalls
            {
                get;
                private set;
            }

            public int InterfaceCalls
            {
                get;
                private set;
            }

            public int LldpCalls
            {
                get;
                private set;
            }

            public Guid LastDeviceId
            {
                get;
                private set;
            }

            public DateTime LastObservedUtc
            {
                get;
                private set;
            }

            public string LastManagementAddress
            {
                get;
                private set;
            }

            public int LastIfIndex
            {
                get;
                private set;
            }

            public string LastIfName
            {
                get;
                private set;
            }

            public string LastIfAlias
            {
                get;
                private set;
            }

            public int? LastIfType
            {
                get;
                private set;
            }

            public void MaterializeDevice(
                Guid deviceId,
                DateTime observedUtc,
                string managementAddress = null)
            {
                DeviceCalls++;
                LastDeviceId = deviceId;
                LastObservedUtc = observedUtc;
                LastManagementAddress = managementAddress;
            }

            public void MaterializeLldp(
                Guid deviceId,
                LldpObservation observation)
            {
                LldpCalls++;
                LastDeviceId = deviceId;
                LastObservedUtc =
                    observation.Observation.CapturedUtc;
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
                InterfaceCalls++;
                LastIfIndex = ifIndex;
                LastIfName = ifName;
                LastIfAlias = ifAlias;
                LastIfType = ifType;
            }
        }
    }
}
