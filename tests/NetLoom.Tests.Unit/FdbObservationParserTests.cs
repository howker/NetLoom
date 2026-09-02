using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Protocols.Snmp.Fdb;
using NetLoom.Topology.Fdb;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class FdbObservationParserTests
    {
        [TestMethod]
        public void ParsesBridgePortMappingAndFdbEntry()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "192.0.2.10",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.2.1.17.1.4.1.2.5",
                        "101"),

                    V(
                        "1.3.6.1.2.1.17.4.3.1.1.0.17.34.51.68.85",
                        "ignored"),

                    V(
                        "1.3.6.1.2.1.17.4.3.1.2.0.17.34.51.68.85",
                        "5"),

                    V(
                        "1.3.6.1.2.1.17.4.3.1.3.0.17.34.51.68.85",
                        "3")
                });

            var parsed =
                new FdbObservationParser().Parse(raw);

            Assert.AreEqual(
                1,
                parsed.BridgePortMappings.Count);

            Assert.AreEqual(
                5,
                parsed.BridgePortMappings[0].BridgePortIndex);

            Assert.AreEqual(
                101,
                parsed.BridgePortMappings[0].IfIndex);

            Assert.AreEqual(1, parsed.Entries.Count);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                parsed.Entries[0].MacAddress);

            Assert.AreEqual(
                5,
                parsed.Entries[0].BridgePortIndex);

            Assert.AreEqual(
                3,
                parsed.Entries[0].Status);
        }

        [TestMethod]
        public void ResolverUsesExplicitBridgePortMapping()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "192.0.2.20",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.2.1.17.1.4.1.2.7",
                        "205"),

                    V(
                        "1.3.6.1.2.1.17.4.3.1.2.170.187.204.221.238.255",
                        "7")
                });

            var parsed =
                new FdbObservationParser().Parse(raw);

            var resolver =
                new BridgePortResolver(
                    parsed.BridgePortMappings);

            var ifIndex =
                resolver.ResolveIfIndex(
                    parsed.Entries[0]);

            Assert.AreEqual(205, ifIndex);

            Assert.AreNotEqual(
                parsed.Entries[0].BridgePortIndex,
                ifIndex);
        }

        [TestMethod]
        public void ResolverDoesNotGuessIfIndex()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "192.0.2.30",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.2.1.17.4.3.1.2.1.2.3.4.5.6",
                        "77")
                });

            var parsed =
                new FdbObservationParser().Parse(raw);

            var resolver =
                new BridgePortResolver(
                    parsed.BridgePortMappings);

            Assert.IsNull(
                resolver.ResolveIfIndex(
                    parsed.Entries[0]));
        }

        [TestMethod]
        public void ZeroFdbPortIsNotResolved()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "192.0.2.40",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.2.1.17.4.3.1.2.10.20.30.40.50.60",
                        "0")
                });

            var parsed =
                new FdbObservationParser().Parse(raw);

            var resolver =
                new BridgePortResolver(
                    parsed.BridgePortMappings);

            Assert.AreEqual(
                0,
                parsed.Entries[0].BridgePortIndex);

            Assert.IsNull(
                resolver.ResolveIfIndex(
                    parsed.Entries[0]));
        }

        [TestMethod]
        public void AmbiguousBridgeMappingIsNotResolved()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "192.0.2.50",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.2.1.17.1.4.1.2.9",
                        "301"),

                    V(
                        "1.3.6.1.2.1.17.1.4.1.2.9",
                        "302")
                });

            var parsed =
                new FdbObservationParser().Parse(raw);

            var resolver =
                new BridgePortResolver(
                    parsed.BridgePortMappings);

            Assert.IsNull(
                resolver.ResolveIfIndex(9));
        }

        [TestMethod]
        public void MalformedMacIndexIsIgnored()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Fdb,
                    "192.0.2.60",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.2.1.17.4.3.1.2.1.2.3",
                        "5"),

                    V(
                        "1.3.6.1.2.1.17.4.3.1.2.1.2.3.4.5.999",
                        "5")
                });

            var parsed =
                new FdbObservationParser().Parse(raw);

            Assert.AreEqual(
                0,
                parsed.Entries.Count);
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
}
