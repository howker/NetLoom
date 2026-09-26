using System;
using System.Collections;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Protocols.Snmp.Cdp;
using NetLoom.Protocols.Snmp.Lldp;
using NetLoom.Protocols.Snmp.Stp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class ProtocolIdentityBinaryValueTests
    {
        [TestMethod]
        public void LldpMacChassisIdsPreserveDistinctBinaryIdentity()
        {
            var first =
                ParseLocalLldpChassis(
                    new byte[]
                    {
                        0x00, 0x90, 0xE8,
                        0x38, 0xCA, 0x95
                    });

            var second =
                ParseLocalLldpChassis(
                    new byte[]
                    {
                        0x00, 0x90, 0xE8,
                        0x38, 0xCA, 0xDB
                    });

            Assert.AreEqual(
                "00:90:E8:38:CA:95",
                first);

            Assert.AreEqual(
                "00:90:E8:38:CA:DB",
                second);

            Assert.AreNotEqual(
                first,
                second);
        }

        [TestMethod]
        public void LldpRemoteMacChassisIdUsesBerPayload()
        {
            var raw =
                new SnmpObservation(
                    NewObservation(
                        ObservationKind.Lldp),
                    new[]
                    {
                        Display(
                            "1.0.8802.1.1.2.1.4.1.1.4.100.17.1",
                            "4"),
                        Binary(
                            "1.0.8802.1.1.2.1.4.1.1.5.100.17.1",
                            "\u0000??8??",
                            new byte[]
                            {
                                0x00, 0x90, 0xE8,
                                0x38, 0xCA, 0xDB
                            })
                    });

            var parsed =
                new LldpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                1,
                parsed.Neighbors.Count);

            Assert.AreEqual(
                "00:90:E8:38:CA:DB",
                parsed.Neighbors[0].ChassisId);
        }

        [TestMethod]
        public void LldpTextIdentitySupportsLongFormBerLength()
        {
            var payload =
                Enumerable
                    .Repeat(
                        (byte)'A',
                        130)
                    .ToArray();

            var raw =
                new SnmpObservation(
                    NewObservation(
                        ObservationKind.Lldp),
                    new[]
                    {
                        Display(
                            "1.0.8802.1.1.2.1.3.1.0",
                            "7"),
                        Binary(
                            "1.0.8802.1.1.2.1.3.2.0",
                            "wrong-display",
                            payload)
                    });

            var parsed =
                new LldpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                new string(
                    'A',
                    130),
                parsed.LocalSystem.ChassisId);
        }

        [TestMethod]
        public void LldpMacPortIdUsesBerPayload()
        {
            var raw =
                new SnmpObservation(
                    NewObservation(
                        ObservationKind.Lldp),
                    new[]
                    {
                        Display(
                            "1.0.8802.1.1.2.1.3.7.1.2.17",
                            "3"),
                        Binary(
                            "1.0.8802.1.1.2.1.3.7.1.3.17",
                            "??",
                            new byte[]
                            {
                                0x00, 0x90, 0xE8,
                                0x38, 0xCA, 0x95
                            }),
                        Display(
                            "1.0.8802.1.1.2.1.4.1.1.4.100.17.1",
                            "7"),
                        Display(
                            "1.0.8802.1.1.2.1.4.1.1.5.100.17.1",
                            "remote-chassis")
                    });

            var parsed =
                new LldpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                1,
                parsed.Neighbors.Count);

            Assert.IsNotNull(
                parsed.Neighbors[0].LocalPort);

            Assert.AreEqual(
                "00:90:E8:38:CA:95",
                parsed.Neighbors[0]
                    .LocalPort
                    .PortId);
        }

        [TestMethod]
        public void StpBridgeIdentityUsesLosslessCanonicalForm()
        {
            var rootPayload =
                new byte[]
                {
                    0x10, 0x00,
                    0x6C, 0x3B, 0x6B,
                    0xE8, 0xD3, 0xA4
                };

            var designatedBridgePayload =
                new byte[]
                {
                    0x80, 0x00,
                    0x00, 0x90, 0xE8,
                    0x38, 0xCA, 0x95
                };

            var raw =
                new SnmpObservation(
                    NewObservation(
                        ObservationKind.Stp),
                    new[]
                    {
                        Binary(
                            "1.3.6.1.2.1.17.2.5.0",
                            "\u0010\u0000l;k???",
                            rootPayload),
                        Binary(
                            "1.3.6.1.2.1.17.2.15.1.6.5",
                            "\u0010\u0000l;k???",
                            rootPayload),
                        Binary(
                            "1.3.6.1.2.1.17.2.15.1.8.5",
                            "??\u0000??8??",
                            designatedBridgePayload),
                        Binary(
                            "1.3.6.1.2.1.17.2.15.1.9.5",
                            "??",
                            new byte[]
                            {
                                0x80, 0x05
                            })
                    });

            var parsed =
                new StpObservationParser()
                    .Parse(raw);

            Assert.AreEqual(
                "1000.6C3B6BE8D3A4",
                parsed.DesignatedRoot);

            Assert.AreEqual(
                1,
                parsed.Ports.Count);

            Assert.AreEqual(
                "1000.6C3B6BE8D3A4",
                parsed.Ports[0].DesignatedRoot);

            Assert.AreEqual(
                "8000.0090E838CA95",
                parsed.Ports[0].DesignatedBridge);

            Assert.AreEqual(
                "8005",
                parsed.Ports[0].DesignatedPort);
        }

        [TestMethod]
        public void CdpIpAddressesUseBinaryPayloadButTextFieldsStayText()
        {
            var raw =
                new SnmpObservation(
                    NewObservation(
                        ObservationKind.Cdp),
                    new[]
                    {
                        Display(
                            "1.3.6.1.4.1.9.9.23.1.2.1.1.3.7.1",
                            "1"),
                        Binary(
                            "1.3.6.1.4.1.9.9.23.1.2.1.1.4.7.1",
                            "\n0?Q",
                            new byte[]
                            {
                                10, 48, 228, 81
                            }),
                        Display(
                            "1.3.6.1.4.1.9.9.23.1.2.1.1.6.7.1",
                            "edge-switch"),
                        Display(
                            "1.3.6.1.4.1.9.9.23.1.2.1.1.19.7.1",
                            "1"),
                        Binary(
                            "1.3.6.1.4.1.9.9.23.1.2.1.1.20.7.1",
                            "\n0?Q",
                            new byte[]
                            {
                                10, 48, 228, 81
                            })
                    });

            var parsed =
                new CdpObservationParser()
                    .Parse(raw);

            var neighborsProperty =
                parsed
                    .GetType()
                    .GetProperty("Neighbors");

            Assert.IsNotNull(
                neighborsProperty);

            var neighbors =
                ((IEnumerable)neighborsProperty
                    .GetValue(parsed, null))
                    .Cast<object>()
                    .ToArray();

            Assert.AreEqual(
                1,
                neighbors.Length);

            var neighbor =
                neighbors[0];

            Assert.AreEqual(
                "10.48.228.81",
                GetString(
                    neighbor,
                    "Address"));

            Assert.AreEqual(
                "10.48.228.81",
                GetString(
                    neighbor,
                    "PrimaryManagementAddress"));

            Assert.AreEqual(
                "edge-switch",
                GetString(
                    neighbor,
                    "DeviceId"));
        }

        private static string ParseLocalLldpChassis(
            byte[] payload)
        {
            var raw =
                new SnmpObservation(
                    NewObservation(
                        ObservationKind.Lldp),
                    new[]
                    {
                        Display(
                            "1.0.8802.1.1.2.1.3.1.0",
                            "4"),
                        Binary(
                            "1.0.8802.1.1.2.1.3.2.0",
                            "\u0000??8??",
                            payload)
                    });

            return
                new LldpObservationParser()
                    .Parse(raw)
                    .LocalSystem
                    .ChassisId;
        }

        private static Observation NewObservation(
            ObservationKind kind)
        {
            return new Observation(
                Guid.NewGuid(),
                kind,
                "192.0.2.200",
                new DateTime(
                    2026, 9, 26,
                    18, 0, 0,
                    DateTimeKind.Utc));
        }

        private static SnmpVariable Display(
            string oid,
            string value)
        {
            return new SnmpVariable(
                oid,
                2,
                value,
                new byte[0]);
        }

        private static SnmpVariable Binary(
            string oid,
            string displayValue,
            byte[] payload)
        {
            byte[] encoded;
            int payloadOffset;

            if (payload.Length < 128)
            {
                encoded =
                    new byte[payload.Length + 2];

                encoded[0] = 0x04;
                encoded[1] =
                    checked((byte)payload.Length);

                payloadOffset = 2;
            }
            else
            {
                Assert.IsTrue(
                    payload.Length <= 255);

                encoded =
                    new byte[payload.Length + 3];

                encoded[0] = 0x04;
                encoded[1] = 0x81;
                encoded[2] =
                    checked((byte)payload.Length);

                payloadOffset = 3;
            }

            Buffer.BlockCopy(
                payload,
                0,
                encoded,
                payloadOffset,
                payload.Length);

            return new SnmpVariable(
                oid,
                4,
                displayValue,
                encoded);
        }

        private static string GetString(
            object instance,
            string propertyName)
        {
            var property =
                instance
                    .GetType()
                    .GetProperty(propertyName);

            Assert.IsNotNull(
                property,
                "Missing property " +
                propertyName);

            return property
                .GetValue(
                    instance,
                    null) as string;
        }
    }
}
