using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Fdb;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Protocols.Snmp.Fdb;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class FdbCollectorTests
    {
        [TestMethod]
        public void CollectorWalksMappingAndFdbThenStoresRawFirst()
        {
            var order = new List<string>();
            var transport = new FakeTransport();
            var rawStore = new FakeRawStore(order);
            var fdbStore = new FakeFdbStore(order);

            var collector =
                new FdbCollector(
                    transport,
                    rawStore,
                    fdbStore,
                    new FdbObservationParser());

            var result =
                collector.Collect(
                    new FdbCollectionRequest(
                        IPAddress.Parse("192.0.2.70"),
                        161,
                        SnmpVersion.V2C,
                        new SnmpCommunityCredentials(
                            Encoding.ASCII.GetBytes("public")),
                        1000,
                        0,
                        20));

            Assert.AreEqual(
                2,
                transport.RootOids.Count);

            Assert.AreEqual(
                "1.3.6.1.2.1.17.1.4.1.2",
                transport.RootOids[0]);

            Assert.AreEqual(
                "1.3.6.1.2.1.17.4.3.1",
                transport.RootOids[1]);

            CollectionAssert.AreEqual(
                new[] { "raw", "normalized" },
                order);

            Assert.AreEqual(
                rawStore.Saved.Observation.Id,
                fdbStore.Saved.Observation.Id);

            Assert.AreEqual(
                rawStore.Saved.Observation.Id,
                result.Observation.Id);

            Assert.AreEqual(
                101,
                result.BridgePortMappings[0].IfIndex);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                result.Entries[0].MacAddress);
        }

        private sealed class FakeTransport
            : ISnmpTransport
        {
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
                    "1.3.6.1.2.1.17.1.4.1.2")
                {
                    return new[]
                    {
                        V(
                            "1.3.6.1.2.1.17.1.4.1.2.5",
                            "101")
                    };
                }

                return new[]
                {
                    V(
                        "1.3.6.1.2.1.17.4.3.1.1.0.17.34.51.68.85",
                        "00:11:22:33:44:55"),
                    V(
                        "1.3.6.1.2.1.17.4.3.1.2.0.17.34.51.68.85",
                        "5"),
                    V(
                        "1.3.6.1.2.1.17.4.3.1.3.0.17.34.51.68.85",
                        "3")
                };
            }

            private static SnmpVariable V(
                string oid,
                string value)
            {
                return new SnmpVariable(
                    oid,
                    2,
                    value,
                    new byte[0]);
            }
        }

        private sealed class FakeRawStore
            : IObservationStore
        {
            private readonly IList<string> _order;

            public FakeRawStore(IList<string> order)
            {
                _order = order;
            }

            public SnmpObservation Saved { get; private set; }

            public void SaveSnmp(
                SnmpObservation observation)
            {
                Saved = observation;
                _order.Add("raw");
            }

            public SnmpObservation GetSnmp(
                Guid observationId)
            {
                return null;
            }
        }

        private sealed class FakeFdbStore
            : IFdbObservationStore
        {
            private readonly IList<string> _order;

            public FakeFdbStore(IList<string> order)
            {
                _order = order;
            }

            public FdbObservation Saved { get; private set; }

            public void Save(
                FdbObservation observation)
            {
                Saved = observation;
                _order.Add("normalized");
            }

            public FdbObservation Get(
                Guid observationId)
            {
                return null;
            }
        }
    }
}
