using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Cdp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Protocols.Snmp.Cdp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class CdpCollectorTests
    {
        [TestMethod]
        public void CollectorStoresRawBeforeNormalizedObservation()
        {
            var order = new List<string>();
            var transport = new FakeTransport();
            var rawStore = new FakeRawStore(order);
            var cdpStore = new FakeCdpStore(order);

            var collector =
                new CdpCollector(
                    transport,
                    rawStore,
                    cdpStore,
                    new CdpObservationParser());

            var result = collector.Collect(
                new CdpCollectionRequest(
                    IPAddress.Parse("192.0.2.60"),
                    161,
                    SnmpVersion.V2C,
                    new SnmpCommunityCredentials(
                        Encoding.ASCII.GetBytes("public")),
                    1000,
                    0,
                    20));

            Assert.AreEqual(
                "1.3.6.1.4.1.9.9.23.1.2.1.1",
                transport.RootOid);

            CollectionAssert.AreEqual(
                new[] { "raw", "normalized" },
                order);

            Assert.IsNotNull(rawStore.Saved);
            Assert.IsNotNull(cdpStore.Saved);

            Assert.AreEqual(
                rawStore.Saved.Observation.Id,
                cdpStore.Saved.Observation.Id);

            Assert.AreEqual(
                result.Observation.Id,
                rawStore.Saved.Observation.Id);

            Assert.AreEqual(
                "192.0.2.60",
                result.Observation.SourceAddress);

            Assert.AreEqual(
                "remote-switch",
                result.Neighbors[0].DeviceId);
        }

        private sealed class FakeTransport
            : ISnmpTransport
        {
            public string RootOid { get; private set; }

            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new SnmpVariable[0];
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                RootOid = request.RootOid;

                return new[]
                {
                    new SnmpVariable(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.6.17.1",
                        4,
                        "remote-switch",
                        new byte[0]),

                    new SnmpVariable(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.7.17.1",
                        4,
                        "Gi0/1",
                        new byte[0])
                };
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

        private sealed class FakeCdpStore
            : ICdpObservationStore
        {
            private readonly IList<string> _order;

            public FakeCdpStore(IList<string> order)
            {
                _order = order;
            }

            public CdpObservation Saved { get; private set; }

            public void Save(
                CdpObservation observation)
            {
                Saved = observation;
                _order.Add("normalized");
            }

            public CdpObservation Get(
                Guid observationId)
            {
                return null;
            }
        }
    }
}
