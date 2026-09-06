using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Protocols.Snmp.Stp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class StpCollectorTests
    {
        [TestMethod]
        public void CollectorPersistsRawBeforeNormalizedObservation()
        {
            var order =
                new List<string>();

            var rawStore =
                new RecordingRawStore(order);

            var normalizedStore =
                new RecordingStpStore(order);

            var collector =
                new StpCollector(
                    new FakeTransport(),
                    rawStore,
                    normalizedStore,
                    new StpObservationParser(),
                    () =>
                        new DateTime(
                            2026, 1, 5, 13, 0, 0,
                            DateTimeKind.Utc));

            var parsed =
                collector.Collect(
                    new StpCollectionRequest(
                        IPAddress.Parse(
                            "192.0.2.23"),
                        161,
                        SnmpVersion.V2C,
                        new SnmpCommunityCredentials(
                            new byte[] { 1, 2, 3 }),
                        1000,
                        1,
                        10));

            CollectionAssert.AreEqual(
                new[]
                {
                    "raw",
                    "normalized"
                },
                order);

            Assert.AreEqual(
                rawStore.Saved.Observation.Id,
                normalizedStore.Saved.Observation.Id);

            Assert.AreEqual(
                "cist",
                parsed.InstanceId);

            Assert.AreEqual(
                17,
                parsed.Ports[0].IfIndex);
        }

        private sealed class FakeTransport :
            ISnmpTransport
        {
            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new[]
                {
                    V(
                        "1.3.6.1.2.1.17.2.1.0",
                        "3"),
                    V(
                        "1.3.6.1.2.1.17.2.7.0",
                        "5")
                };
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                if (request.RootOid ==
                    "1.3.6.1.2.1.17.1.4.1.2")
                {
                    return new[]
                    {
                        V(
                            "1.3.6.1.2.1.17.1.4.1.2.5",
                            "17")
                    };
                }

                return new[]
                {
                    V(
                        "1.3.6.1.2.1.17.2.15.1.3.5",
                        "5")
                };
            }

            private static SnmpVariable V(
                string oid,
                string displayValue)
            {
                return new SnmpVariable(
                    oid,
                    2,
                    displayValue,
                    new byte[0]);
            }
        }

        private sealed class RecordingRawStore :
            IObservationStore
        {
            private readonly IList<string> _order;

            public RecordingRawStore(
                IList<string> order)
            {
                _order = order;
            }

            public SnmpObservation Saved
            {
                get;
                private set;
            }

            public void SaveSnmp(
                SnmpObservation observation)
            {
                Saved = observation;
                _order.Add("raw");
            }

            public SnmpObservation GetSnmp(
                Guid observationId)
            {
                return Saved != null &&
                    Saved.Observation.Id ==
                    observationId
                    ? Saved
                    : null;
            }
        }

        private sealed class RecordingStpStore :
            IStpObservationStore
        {
            private readonly IList<string> _order;

            public RecordingStpStore(
                IList<string> order)
            {
                _order = order;
            }

            public StpObservation Saved
            {
                get;
                private set;
            }

            public void Save(
                StpObservation observation)
            {
                Saved = observation;
                _order.Add("normalized");
            }

            public StpObservation Get(
                Guid observationId)
            {
                return Saved != null &&
                    Saved.Observation.Id ==
                    observationId
                    ? Saved
                    : null;
            }
        }
    }
}
