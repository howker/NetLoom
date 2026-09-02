using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Protocols.Snmp.Cdp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class CdpObservationParserTests
    {
        [TestMethod]
        public void ParsesCompositeCdpCacheIndex()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Cdp,
                    "192.0.2.10",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.3.17.1",
                        "1"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.4.17.1",
                        "192.0.2.50"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.6.17.1",
                        "edge-switch"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.7.17.1",
                        "GigabitEthernet0/1"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.8.17.1",
                        "Cisco IOS"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.11.17.1",
                        "100"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.17.17.1",
                        "edge-01"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.18.17.1",
                        "1.3.6.1.4.1.9.1.516"),

                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.6.17.2",
                        "phone-01"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.7.17.2",
                        "Port 1")
                });

            var parsed =
                new CdpObservationParser().Parse(raw);

            Assert.AreEqual(2, parsed.Neighbors.Count);

            var first = parsed.Neighbors[0];

            Assert.AreEqual(17, first.CacheIfIndex);
            Assert.AreEqual(1, first.DeviceIndex);
            Assert.AreEqual("edge-switch", first.DeviceId);
            Assert.AreEqual(
                "GigabitEthernet0/1",
                first.DevicePort);
            Assert.AreEqual(100, first.NativeVlan);
            Assert.AreEqual(
                "1.3.6.1.4.1.9.1.516",
                first.SystemObjectId);

            Assert.AreEqual(
                2,
                parsed.Neighbors[1].DeviceIndex);
        }

        [TestMethod]
        public void MalformedCdpIndexesAreIgnored()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Cdp,
                    "192.0.2.20",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.6.bad",
                        "bad"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.6.1",
                        "bad"),
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.6.-1.1",
                        "bad")
                });

            var parsed =
                new CdpObservationParser().Parse(raw);

            Assert.AreEqual(0, parsed.Neighbors.Count);
        }

        [TestMethod]
        public void CacheIfIndexIsPreservedAsObservedValue()
        {
            var raw = new SnmpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Cdp,
                    "192.0.2.30",
                    DateTime.UtcNow),
                new[]
                {
                    V(
                        "1.3.6.1.4.1.9.9.23.1.2.1.1.6.90000.1",
                        "repeater-neighbor")
                });

            var parsed =
                new CdpObservationParser().Parse(raw);

            Assert.AreEqual(
                90000,
                parsed.Neighbors[0].CacheIfIndex);

            Assert.AreEqual(
                "repeater-neighbor",
                parsed.Neighbors[0].DeviceId);
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
