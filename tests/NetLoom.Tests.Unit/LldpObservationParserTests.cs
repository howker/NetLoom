using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Protocols.Snmp.Lldp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class LldpObservationParserTests
    {
        [TestMethod]
        public void ParsesCompositeRemoteIndexAndLocalPort()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Lldp,
                    "192.0.2.10",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.0.8802.1.1.2.1.3.7.1.2.17",
                        "5"),
                    V(
                        "1.0.8802.1.1.2.1.3.7.1.3.17",
                        "Gi1/0/17"),
                    V(
                        "1.0.8802.1.1.2.1.3.7.1.4.17",
                        "Uplink"),

                    V(
                        "1.0.8802.1.1.2.1.4.1.1.4.100.17.1",
                        "4"),
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.5.100.17.1",
                        "00:11:22:33:44:55"),
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.6.100.17.1",
                        "5"),
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.7.100.17.1",
                        "Gi0/1"),
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.9.100.17.1",
                        "edge-switch"),

                    V(
                        "1.0.8802.1.1.2.1.4.1.1.4.100.17.2",
                        "7"),
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.5.100.17.2",
                        "remote-chassis-2"),
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.7.100.17.2",
                        "port-2")
                });

            var parsed =
                new LldpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                2,
                parsed.Neighbors.Count);

            var first =
                parsed.Neighbors[0];

            Assert.AreEqual(
                100L,
                first.TimeMark);

            Assert.AreEqual(
                17,
                first.LocalPortNumber);

            Assert.AreEqual(
                1,
                first.RemoteIndex);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                first.ChassisId);

            Assert.AreEqual(
                "edge-switch",
                first.SystemName);

            Assert.IsNotNull(
                first.LocalPort);

            Assert.AreEqual(
                17,
                first.LocalPort.LocalPortNumber);

            Assert.AreEqual(
                "Gi1/0/17",
                first.LocalPort.PortId);

            Assert.AreEqual(
                2,
                parsed.Neighbors[1].RemoteIndex);
        }

        [TestMethod]
        public void MalformedRemoteIndexesAreIgnored()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Lldp,
                    "192.0.2.20",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.0.8802.1.1.2.1.4.1.1.5.bad",
                        "bad"),

                    V(
                        "1.0.8802.1.1.2.1.4.1.1.5.1.2",
                        "bad"),

                    V(
                        "1.0.8802.1.1.2.1.4.1.1.5.1.0.1",
                        "bad")
                });

            var parsed =
                new LldpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                0,
                parsed.Neighbors.Count);
        }

        [TestMethod]
        public void LocalPortNumberIsNotTreatedAsIfIndex()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Lldp,
                    "192.0.2.30",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.0.8802.1.1.2.1.3.7.1.3.900",
                        "Port-A"),

                    V(
                        "1.0.8802.1.1.2.1.4.1.1.5.10.900.1",
                        "remote-a")
                });

            var parsed =
                new LldpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                900,
                parsed.Neighbors[0]
                    .LocalPortNumber);

            Assert.AreEqual(
                "Port-A",
                parsed.Neighbors[0]
                    .LocalPort.PortId);
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
}
