using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Protocols.Snmp.Arp;
using NetLoom.Topology.Correlation;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class ArpObservationTests
    {
        [TestMethod]
        public void ParsesModernIpv4Neighbor()
        {
            var raw =
                Observation(
                    new[]
                    {
                        V(
                            "1.3.6.1.2.1.4.35.1.4.101.1.4.192.0.2.50",
                            "ignored",
                            Ber(
                                0x04,
                                0x00, 0x11, 0x22,
                                0x33, 0x44, 0x55)),

                        V(
                            "1.3.6.1.2.1.4.35.1.6.101.1.4.192.0.2.50",
                            "3",
                            new byte[0]),

                        V(
                            "1.3.6.1.2.1.4.35.1.7.101.1.4.192.0.2.50",
                            "1",
                            new byte[0])
                    });

            var parsed =
                new ArpObservationParser().Parse(raw);

            Assert.AreEqual(1, parsed.Entries.Count);

            var entry = parsed.Entries[0];

            Assert.AreEqual(101, entry.IfIndex);
            Assert.AreEqual(1, entry.AddressType);
            Assert.AreEqual("192.0.2.50", entry.IpAddress);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                entry.PhysicalAddress);

            Assert.AreEqual(3, entry.Type);
            Assert.AreEqual(1, entry.State);

            Assert.AreEqual(
                ArpTableKind.IpNetToPhysical,
                entry.TableKind);
        }

        [TestMethod]
        public void ParsesLegacyIpv4Neighbor()
        {
            var raw =
                Observation(
                    new[]
                    {
                        V(
                            "1.3.6.1.2.1.4.22.1.2.17.198.51.100.7",
                            "AA-BB-CC-DD-EE-FF",
                            new byte[0]),

                        V(
                            "1.3.6.1.2.1.4.22.1.4.17.198.51.100.7",
                            "3",
                            new byte[0])
                    });

            var parsed =
                new ArpObservationParser().Parse(raw);

            Assert.AreEqual(1, parsed.Entries.Count);
            Assert.AreEqual(17, parsed.Entries[0].IfIndex);

            Assert.AreEqual(
                "198.51.100.7",
                parsed.Entries[0].IpAddress);

            Assert.AreEqual(
                "AA:BB:CC:DD:EE:FF",
                parsed.Entries[0].PhysicalAddress);

            Assert.AreEqual(
                ArpTableKind.IpNetToMedia,
                parsed.Entries[0].TableKind);
        }

        [TestMethod]
        public void ProtocolFailureFallsBackToLegacyTable()
        {
            var transport =
                new FallbackTransport(
                    SnmpTransportFailure.Protocol);

            var rawStore =
                new FakeRawStore();

            var collector =
                new ArpCollector(
                    transport,
                    rawStore,
                    new FakeArpStore(),
                    new ArpObservationParser());

            var result =
                collector.Collect(Request());

            CollectionAssert.AreEqual(
                new[]
                {
                    "1.3.6.1.2.1.4.35.1",
                    "1.3.6.1.2.1.4.22.1"
                },
                transport.RootOids);

            Assert.AreEqual(1, result.Entries.Count);

            Assert.AreEqual(
                ArpTableKind.IpNetToMedia,
                result.Entries[0].TableKind);

            Assert.IsNotNull(rawStore.Saved);
            Assert.AreEqual(
                result.Observation.Id,
                rawStore.Saved.Observation.Id);
        }

        [TestMethod]
        public void TimeoutDoesNotFallBack()
        {
            var transport =
                new FallbackTransport(
                    SnmpTransportFailure.Timeout);

            var collector =
                new ArpCollector(
                    transport,
                    new FakeRawStore(),
                    new FakeArpStore(),
                    new ArpObservationParser());

            try
            {
                collector.Collect(Request());

                Assert.Fail(
                    "Timeout exception was expected.");
            }
            catch (SnmpTransportException exception)
            {
                Assert.AreEqual(
                    SnmpTransportFailure.Timeout,
                    exception.Failure);
            }

            Assert.AreEqual(
                1,
                transport.RootOids.Count);
        }

        [TestMethod]
        public void CorrelatesIpMacAndExplicitFdbInterface()
        {
            var arpObservation =
                new ArpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Arp,
                        "192.0.2.1",
                        DateTime.UtcNow),
                    new[]
                    {
                        new ArpEntry(
                            10,
                            1,
                            "192.0.2.50",
                            "00:11:22:33:44:55",
                            3,
                            1,
                            ArpTableKind.IpNetToPhysical)
                    });

            var fdbObservation =
                new FdbObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Fdb,
                        "192.0.2.2",
                        DateTime.UtcNow),
                    new[]
                    {
                        new BridgePortMapping(5, 205)
                    },
                    new[]
                    {
                        new FdbEntry(
                            "00:11:22:33:44:55",
                            5,
                            3)
                    });

            var result =
                new MacCorrelationResolver()
                    .Correlate(
                        arpObservation,
                        fdbObservation);

            Assert.AreEqual(1, result.Count);

            Assert.AreEqual(
                "192.0.2.50",
                result[0].IpAddress);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                result[0].MacAddress);

            Assert.AreEqual(10, result[0].ArpIfIndex);
            Assert.AreEqual(5, result[0].BridgePortIndex);
            Assert.AreEqual(205, result[0].FdbIfIndex);
        }

        [TestMethod]
        public void InvalidOrLocalNeighborIsNotCorrelated()
        {
            var arpObservation =
                new ArpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Arp,
                        "192.0.2.1",
                        DateTime.UtcNow),
                    new[]
                    {
                        new ArpEntry(
                            1,
                            1,
                            "192.0.2.10",
                            "00:11:22:33:44:55",
                            2,
                            1,
                            ArpTableKind.IpNetToPhysical),

                        new ArpEntry(
                            1,
                            1,
                            "192.0.2.11",
                            "00:11:22:33:44:55",
                            5,
                            1,
                            ArpTableKind.IpNetToPhysical)
                    });

            var fdbObservation =
                new FdbObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Fdb,
                        "192.0.2.2",
                        DateTime.UtcNow),
                    new[]
                    {
                        new BridgePortMapping(5, 20)
                    },
                    new[]
                    {
                        new FdbEntry(
                            "00:11:22:33:44:55",
                            5,
                            3)
                    });

            Assert.AreEqual(
                0,
                new MacCorrelationResolver()
                    .Correlate(
                        arpObservation,
                        fdbObservation)
                    .Count);
        }

        private static SnmpObservation Observation(
            IEnumerable<SnmpVariable> variables)
        {
            return new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Arp,
                    "192.0.2.1",
                    DateTime.UtcNow),
                variables);
        }

        private static SnmpVariable V(
            string oid,
            string displayValue,
            byte[] encodedValue)
        {
            return new SnmpVariable(
                oid,
                4,
                displayValue,
                encodedValue);
        }

        private static byte[] Ber(
            byte tag,
            params byte[] value)
        {
            var result =
                new byte[value.Length + 2];

            result[0] = tag;
            result[1] = (byte)value.Length;

            Buffer.BlockCopy(
                value,
                0,
                result,
                2,
                value.Length);

            return result;
        }

        private static ArpCollectionRequest Request()
        {
            return new ArpCollectionRequest(
                IPAddress.Parse("192.0.2.1"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    Encoding.ASCII.GetBytes("public")),
                1000,
                0,
                20);
        }

        private sealed class FakeRawStore
            : IObservationStore
        {
            public SnmpObservation Saved { get; private set; }

            public void SaveSnmp(
                SnmpObservation observation)
            {
                Saved = observation;
            }

            public SnmpObservation GetSnmp(
                Guid observationId)
            {
                return null;
            }
        }

        private sealed class FakeArpStore
            : IArpObservationStore
        {
            public ArpObservation Saved { get; private set; }

            public void Save(
                ArpObservation observation)
            {
                Saved = observation;
            }

            public ArpObservation Get(
                Guid observationId)
            {
                return null;
            }
        }

        private sealed class FallbackTransport
            : ISnmpTransport
        {
            private readonly SnmpTransportFailure _failure;

            public FallbackTransport(
                SnmpTransportFailure failure)
            {
                _failure = failure;
            }

            public List<string> RootOids { get; } =
                new List<string>();

            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new SnmpVariable[0];
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                RootOids.Add(request.RootOid);

                if (request.RootOid ==
                    "1.3.6.1.2.1.4.35.1")
                {
                    throw new SnmpTransportException(
                        _failure,
                        "test",
                        null);
                }

                return new[]
                {
                    V(
                        "1.3.6.1.2.1.4.22.1.2.17.198.51.100.7",
                        "AA:BB:CC:DD:EE:FF",
                        new byte[0]),

                    V(
                        "1.3.6.1.2.1.4.22.1.4.17.198.51.100.7",
                        "3",
                        new byte[0])
                };
            }
        }
    }
}
