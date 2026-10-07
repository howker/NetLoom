using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Topology;
using NetLoom.Protocols.Snmp.Lldp;
using NetLoom.Topology.Materialization;

namespace NetLoom.Tests.Unit
{
    // Sprint 48 (Г1): возможности LLDP — биты, из которых получается категория устройства.
    [TestClass]
    public sealed class Sprint48SystemIdentityTests
    {
        [TestMethod]
        public void CapabilityBitsGiveSwitchOrRouterAndNothingElse()
        {
            Assert.AreEqual(DeviceCategory.Switch, LldpCapabilityCategory.FromEnabled("20:00"), "bridge");
            Assert.AreEqual(DeviceCategory.Switch, LldpCapabilityCategory.FromEnabled("28:00"), "bridge + router is an L3 switch");
            Assert.AreEqual(DeviceCategory.Router, LldpCapabilityCategory.FromEnabled("08:00"), "router");
            Assert.IsNull(LldpCapabilityCategory.FromEnabled("80:00"), "other");
            Assert.IsNull(LldpCapabilityCategory.FromEnabled("01:00"), "station only");
            Assert.IsNull(LldpCapabilityCategory.FromEnabled(null));
            Assert.IsNull(LldpCapabilityCategory.FromEnabled("( "), "the old text form is not evidence");
        }

        [TestMethod]
        public void ParserKeepsCapabilityBitsThatTheTextFormLost()
        {
            var raw =
                new SnmpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Lldp,
                        "192.0.2.10",
                        DateTime.UtcNow),
                    new[]
                    {
                        Text("1.0.8802.1.1.2.1.3.1.0", "4"),
                        Text("1.0.8802.1.1.2.1.3.3.0", "core-switch"),
                        // 0x20 0x00 в текстовом виде — пробел и NUL: бит bridge терялся.
                        Bits("1.0.8802.1.1.2.1.3.6.0", 0x20, 0x00),
                        Text("1.0.8802.1.1.2.1.4.1.1.4.100.17.1", "4"),
                        Text("1.0.8802.1.1.2.1.4.1.1.9.100.17.1", "edge-router"),
                        Bits("1.0.8802.1.1.2.1.4.1.1.11.100.17.1", 0x28, 0x00),
                        Bits("1.0.8802.1.1.2.1.4.1.1.12.100.17.1", 0x08, 0x00)
                    });

            var parsed =
                new LldpObservationParser()
                    .Parse(raw);

            Assert.AreEqual("20:00", parsed.LocalSystem.SystemCapabilitiesEnabled);
            Assert.AreEqual("28:00", parsed.Neighbors[0].SystemCapabilitiesSupported);
            Assert.AreEqual("08:00", parsed.Neighbors[0].SystemCapabilitiesEnabled);
        }

        private static SnmpVariable Text(
            string oid,
            string value)
        {
            return new SnmpVariable(
                oid,
                4,
                value,
                new byte[0]);
        }

        private static SnmpVariable Bits(
            string oid,
            params byte[] payload)
        {
            var encoded =
                new byte[payload.Length + 2];

            encoded[0] = 0x04;
            encoded[1] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, encoded, 2, payload.Length);

            return new SnmpVariable(
                oid,
                4,
                new string(Array.ConvertAll(payload, item => (char)item)),
                encoded);
        }
    }
}
