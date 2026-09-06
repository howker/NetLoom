using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Fdb;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Simulator.Replay;

namespace NetLoom.Tests.Snapshots
{
    [TestClass]
    public sealed class RawSnmpSnapshotReplayTests
    {
        [TestMethod]
        public void LldpFixtureUsesProductionParser()
        {
            var result =
                Replay("lldp-basic.json");

            var parsed =
                result.ParsedObservation
                as LldpObservation;

            Assert.IsNotNull(parsed);
            Assert.AreEqual(1, parsed.Neighbors.Count);

            var neighbor =
                parsed.Neighbors[0];

            Assert.AreEqual(
                17,
                neighbor.LocalPortNumber);

            Assert.AreEqual(
                "edge-switch",
                neighbor.SystemName);

            Assert.IsNotNull(
                neighbor.LocalPort);

            Assert.AreEqual(
                "Gi1/0/17",
                neighbor.LocalPort.PortId);
        }

        [TestMethod]
        public void CdpFixtureUsesProductionParser()
        {
            var result =
                Replay("cdp-basic.json");

            var parsed =
                result.ParsedObservation
                as CdpObservation;

            Assert.IsNotNull(parsed);
            Assert.AreEqual(1, parsed.Neighbors.Count);
            Assert.AreEqual(
                17,
                parsed.Neighbors[0].CacheIfIndex);
            Assert.AreEqual(
                "edge-switch",
                parsed.Neighbors[0].DeviceId);
            Assert.AreEqual(
                "GigabitEthernet0/1",
                parsed.Neighbors[0].DevicePort);
        }

        [TestMethod]
        public void FdbFixturePreservesBridgePortMapping()
        {
            var result =
                Replay("fdb-basic.json");

            var parsed =
                result.ParsedObservation
                as FdbObservation;

            Assert.IsNotNull(parsed);
            Assert.AreEqual(
                1,
                parsed.BridgePortMappings.Count);
            Assert.AreEqual(
                5,
                parsed.BridgePortMappings[0]
                    .BridgePortIndex);
            Assert.AreEqual(
                101,
                parsed.BridgePortMappings[0]
                    .IfIndex);
            Assert.AreEqual(1, parsed.Entries.Count);
            Assert.AreEqual(
                "00:11:22:33:44:55",
                parsed.Entries[0].MacAddress);
            Assert.AreEqual(
                5,
                parsed.Entries[0]
                    .BridgePortIndex);
        }

        [TestMethod]
        public void ArpFixtureReplaysEncodedBerValue()
        {
            var result =
                Replay("arp-basic.json");

            var parsed =
                result.ParsedObservation
                as ArpObservation;

            Assert.IsNotNull(parsed);
            Assert.AreEqual(1, parsed.Entries.Count);
            Assert.AreEqual(
                101,
                parsed.Entries[0].IfIndex);
            Assert.AreEqual(
                "192.0.2.50",
                parsed.Entries[0].IpAddress);
            Assert.AreEqual(
                "00:11:22:33:44:55",
                parsed.Entries[0]
                    .PhysicalAddress);
        }

        [TestMethod]
        public void StpFixtureUsesProductionParserAndBridgeMapping()
        {
            var result =
                Replay("stp-basic.json");

            var parsed =
                result.ParsedObservation
                as StpObservation;

            Assert.IsNotNull(parsed);
            Assert.AreEqual("cist", parsed.InstanceId);
            Assert.AreEqual(5, parsed.RootPortBridgePortIndex);
            Assert.AreEqual(101, parsed.RootPortIfIndex);
            Assert.AreEqual(1, parsed.Ports.Count);

            var port =
                parsed.Ports[0];

            Assert.AreEqual(5, port.BridgePortIndex);
            Assert.AreEqual(101, port.IfIndex);
        }
        [TestMethod]
        public void CodecRoundTripPreservesEncodedValue()
        {
            var codec =
                new RawSnmpSnapshotCodec();

            var original =
                codec.Load(
                    Fixture("arp-basic.json"));

            var temp =
                Path.Combine(
                    Path.GetTempPath(),
                    Guid.NewGuid().ToString("N") +
                    ".json");

            try
            {
                codec.Save(
                    temp,
                    original);

                var replayed =
                    codec.BuildObservation(
                        codec.Load(temp));

                CollectionAssert.AreEqual(
                    new byte[]
                    {
                        0x04,
                        0x06,
                        0x00,
                        0x11,
                        0x22,
                        0x33,
                        0x44,
                        0x55
                    },
                    replayed.Variables[0]
                        .GetEncodedValue());
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        private static SnmpSnapshotReplayResult Replay(
            string fileName)
        {
            var codec =
                new RawSnmpSnapshotCodec();

            return
                new SnmpSnapshotReplayer(
                    codec)
                    .Replay(
                        codec.Load(
                            Fixture(fileName)));
        }

        private static string Fixture(
            string fileName)
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Fixtures",
                fileName);
        }
    }
}
