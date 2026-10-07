using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryInbox;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint48DiscoveryExclusionRuleTests
    {
        [TestMethod]
        public void AddressRuleMatchesOnlySameIpv4Address()
        {
            var rule = new DiscoveryExclusionRule(
                "IpAddress",
                "10.0.0.5");

            Assert.IsTrue(rule.Matches(IPAddress.Parse("10.0.0.5")));
            Assert.IsFalse(rule.Matches(IPAddress.Parse("10.0.0.6")));
            Assert.IsFalse(rule.Matches(IPAddress.IPv6Loopback));
            Assert.IsFalse(rule.Matches(null));
        }

        [TestMethod]
        public void CidrRuleIncludesAllFourAddressesOfSlash30()
        {
            var rule = new DiscoveryExclusionRule(
                "Cidr",
                "10.0.0.5/30");

            foreach (var address in new[]
            {
                "10.0.0.4",
                "10.0.0.5",
                "10.0.0.6",
                "10.0.0.7"
            })
            {
                Assert.IsTrue(rule.Matches(IPAddress.Parse(address)));
            }

            Assert.IsFalse(rule.Matches(IPAddress.Parse("10.0.0.3")));
            Assert.IsFalse(rule.Matches(IPAddress.Parse("10.0.0.8")));
        }

        [TestMethod]
        public void MalformedAndUnsupportedRulesDoNotMatchOrThrow()
        {
            var address = IPAddress.Parse("10.0.0.5");

            foreach (var value in new[]
            {
                "",
                "garbage",
                "10.0.0.0",
                "10.0.0.0/garbage",
                "10.0.0.0/-1",
                "10.0.0.0/33",
                "10.0.0.0/30/1",
                "999.0.0.0/30",
                "::1/30"
            })
            {
                Assert.IsFalse(
                    new DiscoveryExclusionRule("Cidr", value).Matches(address));
            }

            Assert.IsFalse(new DiscoveryExclusionRule("IpAddress", "garbage").Matches(address));
            Assert.IsFalse(new DiscoveryExclusionRule("Hostname", "10.0.0.5").Matches(address));
            Assert.IsFalse(new DiscoveryExclusionRule("Unknown", "10.0.0.0/30").Matches(address));
        }

        [TestMethod]
        public void CidrBoundaryPrefixesDoNotExpandLargeAddressLists()
        {
            Assert.IsTrue(
                new DiscoveryExclusionRule("Cidr", "0.0.0.0/0")
                    .Matches(IPAddress.Parse("203.0.113.7")));
            var exact = new DiscoveryExclusionRule("Cidr", "10.0.0.5/32");
            Assert.IsTrue(exact.Matches(IPAddress.Parse("10.0.0.5")));
            Assert.IsFalse(exact.Matches(IPAddress.Parse("10.0.0.6")));
        }
    }
}
