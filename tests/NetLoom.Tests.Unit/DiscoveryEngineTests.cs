using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Inventory;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class DiscoveryEngineTests
    {
        [TestMethod]
        public void FailedIcmpDoesNotBlockSnmpDiscovery()
        {
            var profileId = Guid.NewGuid();

            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new int[0]),
                new SuccessfulInventoryCollector());

            var results = engine.Discover(
                CreateRequest(
                    profileId,
                    IPAddress.Parse("192.0.2.10")));

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IcmpReachable);
            Assert.IsTrue(results[0].SnmpResponded);
            Assert.AreEqual(
                profileId,
                results[0].AccessProfileId);
        }

        [TestMethod]
        public void OpenTcpPortCreatesCandidateWithoutSnmp()
        {
            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new[] { 443 }),
                new FailingInventoryCollector());

            var results = engine.Discover(
                CreateRequest(
                    Guid.NewGuid(),
                    IPAddress.Parse("192.0.2.20")));

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].SnmpResponded);
            Assert.AreEqual(
                443,
                results[0].OpenTcpPorts[0]);
        }

        [TestMethod]
        public void ExcludedAddressIsNotProbed()
        {
            var address =
                IPAddress.Parse("192.0.2.30");

            var network =
                new CountingNetworkProbe();

            var inventory =
                new CountingInventoryCollector();

            var request = new DiscoveryRequest(
                new[] { address },
                new[] { address },
                new[] { 22, 443 },
                new[]
                {
                    CreateProfile(Guid.NewGuid())
                },
                100,
                100);

            var engine =
                new DiscoveryEngine(
                    network,
                    inventory);

            var results =
                engine.Discover(request);

            Assert.AreEqual(0, results.Count);
            Assert.AreEqual(0, network.ProbeCount);
            Assert.AreEqual(0, inventory.CollectionCount);
        }

        private static DiscoveryRequest CreateRequest(
            Guid profileId,
            IPAddress address)
        {
            return new DiscoveryRequest(
                new[] { address },
                new IPAddress[0],
                new[] { 22, 443 },
                new[]
                {
                    CreateProfile(profileId)
                },
                100,
                100);
        }

        private static DiscoverySnmpProfile CreateProfile(
            Guid profileId)
        {
            return new DiscoverySnmpProfile(
                profileId,
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    Encoding.ASCII.GetBytes("public")),
                100,
                0,
                10);
        }

        private sealed class FakeNetworkProbe
            : INetworkDiscoveryProbe
        {
            private readonly bool _icmp;
            private readonly IReadOnlyList<int> _ports;

            public FakeNetworkProbe(
                bool icmp,
                IReadOnlyList<int> ports)
            {
                _icmp = icmp;
                _ports = ports;
            }

            public bool IsIcmpReachable(
                IPAddress address,
                int timeoutMilliseconds)
            {
                return _icmp;
            }

            public IReadOnlyList<int> FindOpenTcpPorts(
                IPAddress address,
                IReadOnlyList<int> ports,
                int timeoutMilliseconds)
            {
                return _ports;
            }
        }

        private sealed class CountingNetworkProbe
            : INetworkDiscoveryProbe
        {
            public int ProbeCount { get; private set; }

            public bool IsIcmpReachable(
                IPAddress address,
                int timeoutMilliseconds)
            {
                ProbeCount++;
                return false;
            }

            public IReadOnlyList<int> FindOpenTcpPorts(
                IPAddress address,
                IReadOnlyList<int> ports,
                int timeoutMilliseconds)
            {
                ProbeCount++;
                return new int[0];
            }
        }

        private sealed class SuccessfulInventoryCollector
            : IInventoryCollector
        {
            public InventorySnapshot Collect(
                InventoryCollectionRequest request)
            {
                return new InventorySnapshot(
                    request.Address,
                    "switch-01",
                    "Test switch",
                    "1.3.6.1.4.1.9999",
                    null,
                    null,
                    "100",
                    new InventoryInterface[0]);
            }
        }

        private sealed class FailingInventoryCollector
            : IInventoryCollector
        {
            public InventorySnapshot Collect(
                InventoryCollectionRequest request)
            {
                throw new SnmpTransportException(
                    SnmpTransportFailure.Timeout,
                    "Timeout",
                    null);
            }
        }

        private sealed class CountingInventoryCollector
            : IInventoryCollector
        {
            public int CollectionCount { get; private set; }

            public InventorySnapshot Collect(
                InventoryCollectionRequest request)
            {
                CollectionCount++;

                throw new SnmpTransportException(
                    SnmpTransportFailure.Timeout,
                    "Timeout",
                    null);
            }
        }
    }
}
