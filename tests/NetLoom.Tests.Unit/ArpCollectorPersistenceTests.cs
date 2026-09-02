using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Protocols.Snmp.Arp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class ArpCollectorPersistenceTests
    {
        [TestMethod]
        public void CollectorStoresRawBeforeNormalized()
        {
            var order =
                new List<string>();

            var rawStore =
                new RawStore(order);

            var arpStore =
                new ArpStore(order);

            var collector =
                new ArpCollector(
                    new Transport(),
                    rawStore,
                    arpStore,
                    new ArpObservationParser());

            var result =
                collector.Collect(
                    new ArpCollectionRequest(
                        IPAddress.Parse("192.0.2.1"),
                        161,
                        SnmpVersion.V2C,
                        new SnmpCommunityCredentials(
                            Encoding.ASCII.GetBytes("public")),
                        1000,
                        0,
                        20));

            CollectionAssert.AreEqual(
                new[] { "raw", "normalized" },
                order);

            Assert.IsNotNull(rawStore.Saved);
            Assert.IsNotNull(arpStore.Saved);

            Assert.AreEqual(
                rawStore.Saved.Observation.Id,
                arpStore.Saved.Observation.Id);

            Assert.AreEqual(
                result.Observation.Id,
                rawStore.Saved.Observation.Id);
        }

        private sealed class Transport
            : ISnmpTransport
        {
            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new SnmpVariable[0];
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                return new[]
                {
                    new SnmpVariable(
                        "1.3.6.1.2.1.4.35.1.4.10.1.4.192.0.2.50",
                        4,
                        "00:11:22:33:44:55",
                        new byte[0])
                };
            }
        }

        private sealed class RawStore
            : IObservationStore
        {
            private readonly IList<string> _order;

            public RawStore(IList<string> order)
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

        private sealed class ArpStore
            : IArpObservationStore
        {
            private readonly IList<string> _order;

            public ArpStore(IList<string> order)
            {
                _order = order;
            }

            public ArpObservation Saved { get; private set; }

            public void Save(
                ArpObservation observation)
            {
                Saved = observation;
                _order.Add("normalized");
            }

            public ArpObservation Get(
                Guid observationId)
            {
                return null;
            }
        }
    }
}
