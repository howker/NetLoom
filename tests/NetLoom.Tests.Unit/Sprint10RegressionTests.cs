using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Protocols.Snmp.Arp;
using NetLoom.Topology.Correlation;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint10RegressionTests
    {
        [TestMethod]
        public void ModernNeighborTableParsesIpv6()
        {
            var raw =
                new SnmpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Arp,
                        "192.0.2.1",
                        DateTime.UtcNow),
                    new[]
                    {
                        new SnmpVariable(
                            "1.3.6.1.2.1.4.35.1.4.42.2.16.32.1.13.184.0.0.0.0.0.0.0.0.0.0.0.1",
                            4,
                            "00:11:22:33:44:55",
                            new byte[0])
                    });

            var parsed =
                new ArpObservationParser().Parse(raw);

            Assert.AreEqual(
                1,
                parsed.Entries.Count);

            Assert.AreEqual(
                42,
                parsed.Entries[0].IfIndex);

            Assert.AreEqual(
                2,
                parsed.Entries[0].AddressType);

            Assert.AreEqual(
                "2001:db8::1",
                parsed.Entries[0].IpAddress);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                parsed.Entries[0].PhysicalAddress);
        }

        [TestMethod]
        public void AmbiguousBridgeMappingNeverGuessesFdbIfIndex()
        {
            var arp =
                new ArpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Arp,
                        "192.0.2.1",
                        DateTime.UtcNow),
                    new[]
                    {
                        new ArpEntry(
                            10,
                            1,
                            "192.0.2.50",
                            "00:11:22:33:44:55",
                            3,
                            1,
                            ArpTableKind.IpNetToPhysical)
                    });

            var fdb =
                new FdbObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Fdb,
                        "192.0.2.2",
                        DateTime.UtcNow),
                    new[]
                    {
                        new BridgePortMapping(9, 301),
                        new BridgePortMapping(9, 302)
                    },
                    new[]
                    {
                        new FdbEntry(
                            "00:11:22:33:44:55",
                            9,
                            3)
                    });

            var result =
                new MacCorrelationResolver()
                    .Correlate(arp, fdb);

            Assert.AreEqual(
                1,
                result.Count);

            Assert.AreEqual(
                9,
                result[0].BridgePortIndex);

            Assert.IsNull(
                result[0].FdbIfIndex);
        }

        [TestMethod]
        public void MissingMacInFdbDoesNotCreateCorrelation()
        {
            var arp =
                new ArpObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Arp,
                        "192.0.2.1",
                        DateTime.UtcNow),
                    new[]
                    {
                        new ArpEntry(
                            10,
                            1,
                            "192.0.2.50",
                            "00:11:22:33:44:55",
                            3,
                            1,
                            ArpTableKind.IpNetToPhysical)
                    });

            var fdb =
                new FdbObservation(
                    new Observation(
                        Guid.NewGuid(),
                        ObservationKind.Fdb,
                        "192.0.2.2",
                        DateTime.UtcNow),
                    new BridgePortMapping[0],
                    new[]
                    {
                        new FdbEntry(
                            "AA:BB:CC:DD:EE:FF",
                            5,
                            3)
                    });

            var result =
                new MacCorrelationResolver()
                    .Correlate(arp, fdb);

            Assert.AreEqual(
                0,
                result.Count);
        }
    }
}
