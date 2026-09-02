using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class DiscoveryAddressResolverTests
    {
        [TestMethod]
        public void TargetsAreExpandedDeduplicatedAndExcluded()
        {
            var resolver =
                new DiscoveryAddressResolver(
                    new FakeHostnameResolver());

            var addresses = resolver.Resolve(
                new[]
                {
                    new DiscoveryTarget(
                        AccessTargetKind.Cidr,
                        "192.0.2.0/30"),

                    new DiscoveryTarget(
                        AccessTargetKind.IpAddress,
                        "192.0.2.2"),

                    new DiscoveryTarget(
                        AccessTargetKind.Hostname,
                        "switch.example")
                },
                new[]
                {
                    new DiscoveryTarget(
                        AccessTargetKind.IpAddress,
                        "192.0.2.1"),

                    new DiscoveryTarget(
                        AccessTargetKind.Hostname,
                        "excluded.example")
                },
                256);

            var values = addresses
                .Select(address => address.ToString())
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "192.0.2.0",
                    "192.0.2.2",
                    "192.0.2.3"
                },
                values);
        }

        [TestMethod]
        public void Ipv6TargetIsRejected()
        {
            var resolver =
                new DiscoveryAddressResolver(
                    new FakeHostnameResolver());

            try
            {
                resolver.Resolve(
                    new[]
                    {
                        new DiscoveryTarget(
                            AccessTargetKind.IpAddress,
                            "::1")
                    },
                    new DiscoveryTarget[0],
                    256);

                Assert.Fail(
                    "IPv6 target must be rejected.");
            }
            catch (System.FormatException)
            {
            }
        }

        private sealed class FakeHostnameResolver
            : IHostnameResolver
        {
            public IReadOnlyList<IPAddress> Resolve(
                string hostname)
            {
                if (hostname == "switch.example")
                {
                    return new[]
                    {
                        IPAddress.Parse("192.0.2.2"),
                        IPAddress.Parse("192.0.2.10"),
                        IPAddress.Parse("192.0.2.2")
                    };
                }

                if (hostname == "excluded.example")
                {
                    return new[]
                    {
                        IPAddress.Parse("192.0.2.10")
                    };
                }

                return new IPAddress[0];
            }
        }
    }
}
