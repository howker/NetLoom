using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Lldp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Protocols.Snmp.Lldp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class LldpCollectorTests
    {
        [TestMethod]
        public void CollectorWalksLldpTablesAndStoresSameObservation()
        {
            var transport = new FakeTransport();
            var rawStore = new FakeRawStore();
            var lldpStore = new FakeLldpStore();

            var collector =
                new LldpCollector(
                    transport,
                    rawStore,
                    lldpStore,
                    new LldpObservationParser());

            var result = collector.Collect(
                new LldpCollectionRequest(
                    IPAddress.Parse("192.0.2.50"),
                    161,
                    SnmpVersion.V2C,
                    new SnmpCommunityCredentials(
                        Encoding.ASCII.GetBytes("public")),
                    1000,
                    0,
                    20));

            Assert.AreEqual(2, transport.Roots.Count);

            Assert.AreEqual(
                "1.0.8802.1.1.2.1.3.7.1",
                transport.Roots[0]);

            Assert.AreEqual(
                "1.0.8802.1.1.2.1.4.1.1",
                transport.Roots[1]);

            Assert.IsNotNull(rawStore.Saved);
            Assert.IsNotNull(lldpStore.Saved);

            Assert.AreEqual(
                rawStore.Saved.Observation.Id,
                lldpStore.Saved.Observation.Id);

            Assert.AreEqual(
                result.Observation.Id,
                rawStore.Saved.Observation.Id);

            Assert.AreEqual(
                "192.0.2.50",
                result.Observation.SourceAddress);

            Assert.AreEqual(
                1,
                result.Neighbors.Count);

            Assert.AreEqual(
                "remote-switch",
                result.Neighbors[0].SystemName);
        }

        private sealed class FakeTransport
            : ISnmpTransport
        {
            public List<string> Roots { get; } =
                new List<string>();

            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new SnmpVariable[0];
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                Roots.Add(request.RootOid);

                if (request.RootOid ==
                    "1.0.8802.1.1.2.1.3.7.1")
                {
                    return new[]
                    {
                        V(
                            "1.0.8802.1.1.2.1.3.7.1.3.7",
                            "Gi1/0/7")
                    };
                }

                return new[]
                {
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.5.100.7.1",
                        "00:11:22:33:44:55"),
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.9.100.7.1",
                        "remote-switch")
                };
            }

            private static SnmpVariable V(
                string oid,
                string value)
            {
                return new SnmpVariable(
                    oid,
                    4,
                    value,
                    new byte[0]);
            }
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

        private sealed class FakeLldpStore
            : ILldpObservationStore
        {
            public LldpObservation Saved { get; private set; }

            public void Save(
                LldpObservation observation)
            {
                Saved = observation;
            }

            public LldpObservation Get(
                Guid observationId)
            {
                return null;
            }
        }
    }
}
