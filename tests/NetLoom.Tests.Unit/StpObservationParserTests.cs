using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Protocols.Snmp.Stp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class StpObservationParserTests
    {
        [TestMethod]
        public void ParsesCommonRootAndPortStateWithExplicitBridgeMapping()
        {
            var raw =
                Observation(
                    V(
                        "1.3.6.1.2.1.17.2.1.0",
                        "ieee8021d(3)"),
                    V(
                        "1.3.6.1.2.1.17.2.5.0",
                        "8000.001122334455"),
                    V(
                        "1.3.6.1.2.1.17.2.6.0",
                        "20000"),
                    V(
                        "1.3.6.1.2.1.17.2.7.0",
                        "5"),
                    V(
                        "1.3.6.1.2.1.17.1.4.1.2.5",
                        "205"),
                    V(
                        "1.3.6.1.2.1.17.2.15.1.3.5",
                        "forwarding(5)"),
                    V(
                        "1.3.6.1.2.1.17.2.15.1.11.5",
                        "200000"));

            var parsed =
                new StpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                "cist",
                parsed.InstanceId);

            Assert.AreEqual(
                3,
                parsed.ProtocolSpecification);

            Assert.AreEqual(
                5,
                parsed.RootPortBridgePortIndex);

            Assert.AreEqual(
                205,
                parsed.RootPortIfIndex);

            Assert.AreEqual(
                1,
                parsed.Ports.Count);

            Assert.AreEqual(
                5,
                parsed.Ports[0].BridgePortIndex);

            Assert.AreEqual(
                205,
                parsed.Ports[0].IfIndex);

            Assert.AreEqual(
                5,
                parsed.Ports[0].State);

            Assert.AreEqual(
                200000L,
                parsed.Ports[0].PathCost);
        }

        [TestMethod]
        public void BridgePortIndexIsNeverUsedAsIfIndexFallback()
        {
            var parsed =
                new StpObservationParser()
                    .Parse(
                        Observation(
                            V(
                                "1.3.6.1.2.1.17.2.7.0",
                                "7"),
                            V(
                                "1.3.6.1.2.1.17.2.15.1.3.7",
                                "blocking(2)")));

            Assert.AreEqual(
                7,
                parsed.RootPortBridgePortIndex);

            Assert.IsNull(
                parsed.RootPortIfIndex);

            Assert.AreEqual(
                7,
                parsed.Ports[0].BridgePortIndex);

            Assert.IsNull(
                parsed.Ports[0].IfIndex);
        }

        [TestMethod]
        public void AmbiguousBridgeMappingDoesNotGuessIfIndex()
        {
            var parsed =
                new StpObservationParser()
                    .Parse(
                        Observation(
                            V(
                                "1.3.6.1.2.1.17.1.4.1.2.9",
                                "301"),
                            V(
                                "1.3.6.1.2.1.17.1.4.1.2.9",
                                "302"),
                            V(
                                "1.3.6.1.2.1.17.2.15.1.3.9",
                                "2")));

            Assert.AreEqual(
                9,
                parsed.Ports[0].BridgePortIndex);

            Assert.IsNull(
                parsed.Ports[0].IfIndex);
        }

        [TestMethod]
        public void ThirtyTwoBitPathCostWinsOverLegacyPathCost()
        {
            var parsed =
                new StpObservationParser()
                    .Parse(
                        Observation(
                            V(
                                "1.3.6.1.2.1.17.2.15.1.5.3",
                                "65535"),
                            V(
                                "1.3.6.1.2.1.17.2.15.1.11.3",
                                "200000000")));

            Assert.AreEqual(
                200000000L,
                parsed.Ports[0].PathCost);
        }

        private static SnmpObservation Observation(
            params SnmpVariable[] variables)
        {
            return new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Stp,
                    "192.0.2.23",
                    new DateTime(
                        2026, 1, 5, 12, 0, 0,
                        DateTimeKind.Utc)),
                variables);
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
}
