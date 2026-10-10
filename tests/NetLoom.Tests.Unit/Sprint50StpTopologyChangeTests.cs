using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Domain.Topology;
using NetLoom.Protocols.Snmp.Stp;
using NetLoom.Topology.Stp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint50StpTopologyChangeTests
    {
        private const string TimeSinceOid = "1.3.6.1.2.1.17.2.3.0";
        private const string ChangesOid = "1.3.6.1.2.1.17.2.4.0";

        private static readonly DateTime Captured =
            new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void ParserReadsBothScalars()
        {
            var parsed =
                Parse(
                    Var(TimeSinceOid, 67, "240000"),
                    Var(ChangesOid, 65, "5"));

            Assert.AreEqual(240000L, parsed.TimeSinceTopologyChangeCentiseconds);
            Assert.AreEqual(5L, parsed.TopologyChangeCount);
        }

        [TestMethod]
        public void ParserReadsTimeTicksWithParenthesesForm()
        {
            var parsed =
                Parse(
                    Var(TimeSinceOid, 67, "00:40:00.00 (240000)"));

            Assert.AreEqual(240000L, parsed.TimeSinceTopologyChangeCentiseconds);
        }

        // Настоящий транспорт SharpSnmp: TimeTicks показан интервалом, значение — в BER (0x43).
        [TestMethod]
        public void ParserReadsSharpSnmpTimeTicksFromEncodedValue()
        {
            var parsed =
                Parse(
                    new SnmpVariable(
                        TimeSinceOid,
                        67,
                        "00:40:00",
                        new byte[] { 0x43, 0x03, 0x03, 0xA9, 0x80 }),
                    new SnmpVariable(
                        ChangesOid,
                        65,
                        "5",
                        new byte[] { 0x41, 0x01, 0x05 }));

            Assert.AreEqual(240000L, parsed.TimeSinceTopologyChangeCentiseconds);
            Assert.AreEqual(5L, parsed.TopologyChangeCount);
        }

        [TestMethod]
        public void ParserReadsTimeTicksIntervalTextWithoutEncodedValue()
        {
            var parsed =
                Parse(
                    Var(TimeSinceOid, 67, "6.00:00:00"));

            Assert.AreEqual(51840000L, parsed.TimeSinceTopologyChangeCentiseconds);
        }

        [TestMethod]
        public void ParserReturnsNullsWhenScalarsAreMissing()
        {
            var parsed =
                Parse(
                    Var("1.3.6.1.2.1.17.2.1.0", 2, "3"));

            Assert.IsNull(parsed.TimeSinceTopologyChangeCentiseconds);
            Assert.IsNull(parsed.TopologyChangeCount);
        }

        [TestMethod]
        public void ParserReturnsNullForNonNumericValue()
        {
            var parsed =
                Parse(
                    Var(TimeSinceOid, 67, "noSuchObject"),
                    Var(ChangesOid, 65, "abc"));

            Assert.IsNull(parsed.TimeSinceTopologyChangeCentiseconds);
            Assert.IsNull(parsed.TopologyChangeCount);
        }

        [TestMethod]
        public void ObservationRejectsNegativeValues()
        {
            var observation = NewObservation();

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () =>
                    new StpObservation(
                        observation, "cist", null, null, null, null, null,
                        new StpPortState[0], -1L, null));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () =>
                    new StpObservation(
                        observation, "cist", null, null, null, null, null,
                        new StpPortState[0], null, -1L));
        }

        [TestMethod]
        public void ProjectorComputesLastTopologyChange()
        {
            var observation =
                new StpObservation(
                    NewObservation(), "cist", null, null, null, null, null,
                    new StpPortState[0], 240000L, 6L);

            var snapshot =
                new StpTreeProjector()
                    .Project(
                        Guid.NewGuid(),
                        new DeviceInterface[0],
                        observation);

            Assert.AreEqual(
                Captured.AddMinutes(-40),
                snapshot.LastTopologyChangeUtc);
            Assert.AreEqual(
                DateTimeKind.Utc,
                snapshot.LastTopologyChangeUtc.Value.Kind);
            Assert.AreEqual(6L, snapshot.TopologyChangeCount);
        }

        [TestMethod]
        public void ProjectorLeavesNullsWithoutValues()
        {
            var observation =
                new StpObservation(
                    NewObservation(), "cist", null, null, null, null, null,
                    new StpPortState[0]);

            var snapshot =
                new StpTreeProjector()
                    .Project(
                        Guid.NewGuid(),
                        new DeviceInterface[0],
                        observation);

            Assert.IsNull(snapshot.LastTopologyChangeUtc);
            Assert.IsNull(snapshot.TopologyChangeCount);
        }

        [TestMethod]
        public void CollectorRequestsBothScalars()
        {
            var transport = new RecordingTransport();

            var collector =
                new StpCollector(
                    transport,
                    new NullRawStore(),
                    new NullStpStore(),
                    new StpObservationParser(),
                    () => Captured);

            collector.Collect(
                new StpCollectionRequest(
                    IPAddress.Parse("192.0.2.23"),
                    161,
                    SnmpVersion.V2C,
                    new SnmpCommunityCredentials(new byte[] { 1, 2, 3 }),
                    1000,
                    1,
                    10));

            CollectionAssert.Contains(transport.RequestedOids, TimeSinceOid);
            CollectionAssert.Contains(transport.RequestedOids, ChangesOid);
        }

        private static Observation NewObservation()
        {
            return new Observation(
                Guid.NewGuid(),
                ObservationKind.Stp,
                "192.0.2.23",
                Captured);
        }

        private static StpObservation Parse(
            params SnmpVariable[] variables)
        {
            return new StpObservationParser()
                .Parse(
                    new SnmpObservation(
                        NewObservation(),
                        variables));
        }

        private static SnmpVariable Var(
            string oid,
            int typeCode,
            string displayValue)
        {
            return new SnmpVariable(
                oid,
                typeCode,
                displayValue,
                new byte[0]);
        }

        private sealed class RecordingTransport :
            ISnmpTransport
        {
            public List<string> RequestedOids =
                new List<string>();

            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                RequestedOids.AddRange(request.Oids);

                return new SnmpVariable[0];
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                return new SnmpVariable[0];
            }
        }

        private sealed class NullRawStore :
            IObservationStore
        {
            public void SaveSnmp(
                SnmpObservation observation)
            {
            }

            public SnmpObservation GetSnmp(
                Guid observationId)
            {
                return null;
            }
        }

        private sealed class NullStpStore :
            IStpObservationStore
        {
            public void Save(
                StpObservation observation)
            {
            }

            public StpObservation Get(
                Guid observationId)
            {
                return null;
            }
        }
    }
}
