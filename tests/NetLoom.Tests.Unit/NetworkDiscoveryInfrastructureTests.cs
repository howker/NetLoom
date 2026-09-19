using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Protocols.Snmp.Discovery;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class NetworkDiscoveryInfrastructureTests
    {
        [TestMethod]
        public void CidrExpansionReturnsAllIpv4Addresses()
        {
            var addresses =
                Ipv4CidrExpander.Expand(
                    "192.0.2.5/30",
                    16);

            Assert.AreEqual(4, addresses.Count);
            Assert.AreEqual(
                "192.0.2.4",
                addresses[0].ToString());
            Assert.AreEqual(
                "192.0.2.7",
                addresses[3].ToString());
        }

        [TestMethod]
        public void Cidr32ReturnsSingleAddress()
        {
            var addresses =
                Ipv4CidrExpander.Expand(
                    "192.0.2.44/32",
                    1);

            Assert.AreEqual(1, addresses.Count);
            Assert.AreEqual(
                "192.0.2.44",
                addresses[0].ToString());
        }

        [TestMethod]
        public void LargeCidrIsRejectedBeforeExpansion()
        {
            try
            {
                Ipv4CidrExpander.Expand(
                    "10.0.0.0/8",
                    65536);

                Assert.Fail(
                    "Oversized CIDR must be rejected.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        [TestMethod]
        public void TcpProbeFindsListeningLocalPort()
        {
            var listener = new TcpListener(
                IPAddress.Loopback,
                0);

            listener.Start();

            try
            {
                var endpoint =
                    (IPEndPoint)listener.LocalEndpoint;

                var probe =
                    new SystemNetworkDiscoveryProbe();

                var ports = probe.FindOpenTcpPorts(
                    IPAddress.Loopback,
                    new[] { endpoint.Port },
                    1000,
                    CancellationToken.None);

                Assert.AreEqual(1, ports.Count);
                Assert.AreEqual(
                    endpoint.Port,
                    ports.Single());
            }
            finally
            {
                listener.Stop();
            }
        }

        [TestMethod]
        public void TcpProbeHonorsCancellationBeforeConnect()
        {
            var probe =
                new SystemNetworkDiscoveryProbe();

            using (var cancellation =
                new CancellationTokenSource())
            {
                cancellation.Cancel();

                try
                {
                    probe.FindOpenTcpPorts(
                        IPAddress.Loopback,
                        new[] { 443 },
                        1000,
                        cancellation.Token);

                    Assert.Fail(
                        "Cancelled TCP probe must not start a connection attempt.");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [TestMethod]
        public void IcmpProbeCanReachLoopback()
        {
            var probe =
                new SystemNetworkDiscoveryProbe();

            Assert.IsTrue(
                probe.IsIcmpReachable(
                    IPAddress.Loopback,
                    1000,
                    CancellationToken.None));
        }
    }
}
