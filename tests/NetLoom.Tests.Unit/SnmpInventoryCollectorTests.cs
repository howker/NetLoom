using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Inventory;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Protocols.Snmp.Inventory;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class SnmpInventoryCollectorTests
    {
        [TestMethod]
        public void CollectBuildsSystemAndInterfaceInventory()
        {
            var transport = new FakeTransport();

            var collector =
                new SnmpInventoryCollector(transport);

            var snapshot = collector.Collect(
                new InventoryCollectionRequest(
                    IPAddress.Parse("192.0.2.10"),
                    161,
                    SnmpVersion.V2C,
                    new SnmpCommunityCredentials(
                        Encoding.ASCII.GetBytes("public")),
                    1000,
                    1,
                    20));

            Assert.AreEqual(
                "switch-core-01",
                snapshot.SysName);

            Assert.AreEqual(
                "1.3.6.1.4.1.9999.1",
                snapshot.SysObjectId);

            Assert.AreEqual(
                IPAddress.Parse("192.0.2.10"),
                snapshot.ManagementAddress);

            Assert.AreEqual(
                2,
                snapshot.Interfaces.Count);

            Assert.AreEqual(
                1,
                snapshot.Interfaces[0].IfIndex);

            Assert.AreEqual(
                "GigabitEthernet1/0/1",
                snapshot.Interfaces[0].Name);

            Assert.AreEqual(
                "uplink",
                snapshot.Interfaces[0].Alias);

            Assert.AreEqual(
                1000L,
                snapshot.Interfaces[0].HighSpeedMbps);

            Assert.AreEqual(
                2,
                snapshot.Interfaces[1].IfIndex);

            Assert.AreEqual(
                "GigabitEthernet1/0/2",
                snapshot.Interfaces[1].Name);
        }

        [TestMethod]
        public void CollectWorksWhenIfXTableIsUnavailable()
        {
            var collector =
                new SnmpInventoryCollector(
                    new WithoutIfXTableTransport());

            var snapshot = collector.Collect(
                new InventoryCollectionRequest(
                    IPAddress.Parse("192.0.2.20"),
                    161,
                    SnmpVersion.V2C,
                    new SnmpCommunityCredentials(
                        Encoding.ASCII.GetBytes("public")),
                    1000,
                    0,
                    20));

            Assert.AreEqual(1, snapshot.Interfaces.Count);
            Assert.AreEqual(7, snapshot.Interfaces[0].IfIndex);
            Assert.AreEqual(
                "Ethernet port 7",
                snapshot.Interfaces[0].Description);

            Assert.IsNull(snapshot.Interfaces[0].Name);
            Assert.IsNull(snapshot.Interfaces[0].Alias);
            Assert.IsNull(snapshot.Interfaces[0].HighSpeedMbps);
        }

        private sealed class WithoutIfXTableTransport : ISnmpTransport
        {
            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new[]
                {
                    Variable(
                        "1.3.6.1.2.1.1.1.0",
                        "Legacy switch"),
                    Variable(
                        "1.3.6.1.2.1.1.2.0",
                        "1.3.6.1.4.1.9999.2"),
                    Variable(
                        "1.3.6.1.2.1.1.3.0",
                        "100"),
                    Variable(
                        "1.3.6.1.2.1.1.4.0",
                        ""),
                    Variable(
                        "1.3.6.1.2.1.1.5.0",
                        "legacy-switch"),
                    Variable(
                        "1.3.6.1.2.1.1.6.0",
                        "")
                };
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                if (request.RootOid.StartsWith(
                    "1.3.6.1.2.1.31.",
                    System.StringComparison.Ordinal))
                {
                    throw new SnmpTransportException(
                        SnmpTransportFailure.Protocol,
                        "ifXTable is unavailable.",
                        null);
                }

                if (request.RootOid ==
                    "1.3.6.1.2.1.2.2.1.1")
                {
                    return new[]
                    {
                        Variable(
                            request.RootOid + ".7",
                            "7")
                    };
                }

                if (request.RootOid ==
                    "1.3.6.1.2.1.2.2.1.2")
                {
                    return new[]
                    {
                        Variable(
                            request.RootOid + ".7",
                            "Ethernet port 7")
                    };
                }

                return new SnmpVariable[0];
            }

            private static SnmpVariable Variable(
                string oid,
                string value)
            {
                return new SnmpVariable(
                    oid,
                    0,
                    value,
                    new byte[0]);
            }
        }
        private sealed class FakeTransport : ISnmpTransport
        {
            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new[]
                {
                    Variable(
                        "1.3.6.1.2.1.1.1.0",
                        "Test Ethernet Switch"),
                    Variable(
                        "1.3.6.1.2.1.1.2.0",
                        "1.3.6.1.4.1.9999.1"),
                    Variable(
                        "1.3.6.1.2.1.1.3.0",
                        "123456"),
                    Variable(
                        "1.3.6.1.2.1.1.4.0",
                        "noc@example.invalid"),
                    Variable(
                        "1.3.6.1.2.1.1.5.0",
                        "switch-core-01"),
                    Variable(
                        "1.3.6.1.2.1.1.6.0",
                        "Server room")
                };
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                switch (request.RootOid)
                {
                    case "1.3.6.1.2.1.2.2.1.1":
                        return Pair(
                            request.RootOid,
                            "1",
                            "2");

                    case "1.3.6.1.2.1.2.2.1.2":
                        return Pair(
                            request.RootOid,
                            "Gigabit port 1",
                            "Gigabit port 2");

                    case "1.3.6.1.2.1.2.2.1.3":
                        return Pair(
                            request.RootOid,
                            "6",
                            "6");

                    case "1.3.6.1.2.1.2.2.1.6":
                        return Pair(
                            request.RootOid,
                            "00:11:22:33:44:01",
                            "00:11:22:33:44:02");

                    case "1.3.6.1.2.1.2.2.1.7":
                        return Pair(
                            request.RootOid,
                            "1",
                            "1");

                    case "1.3.6.1.2.1.2.2.1.8":
                        return Pair(
                            request.RootOid,
                            "1",
                            "2");

                    case "1.3.6.1.2.1.31.1.1.1.1":
                        return Pair(
                            request.RootOid,
                            "GigabitEthernet1/0/1",
                            "GigabitEthernet1/0/2");

                    case "1.3.6.1.2.1.31.1.1.1.15":
                        return Pair(
                            request.RootOid,
                            "1000",
                            "1000");

                    case "1.3.6.1.2.1.31.1.1.1.18":
                        return Pair(
                            request.RootOid,
                            "uplink",
                            "office");

                    default:
                        return new SnmpVariable[0];
                }
            }

            private static IReadOnlyList<SnmpVariable> Pair(
                string root,
                string first,
                string second)
            {
                return new[]
                {
                    Variable(root + ".1", first),
                    Variable(root + ".2", second)
                };
            }

            private static SnmpVariable Variable(
                string oid,
                string value)
            {
                return new SnmpVariable(
                    oid,
                    0,
                    value,
                    new byte[0]);
            }
        }
    }
}
