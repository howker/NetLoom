using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Ipv4RangeExpanderTests
    {
        [TestMethod]
        public void ExpandsInclusiveRangeInsideSpecifiedSubnet()
        {
            var addresses =
                Ipv4RangeExpander.Expand(
                    "192.0.2.10",
                    "192.0.2.12",
                    "255.255.255.0",
                    16);

            CollectionAssert.AreEqual(
                new[]
                {
                    IPAddress.Parse("192.0.2.10"),
                    IPAddress.Parse("192.0.2.11"),
                    IPAddress.Parse("192.0.2.12")
                },
                new List<IPAddress>(addresses));
        }

        [TestMethod]
        public void RejectsRangeThatCrossesMaskBoundary()
        {
            try
            {
                Ipv4RangeExpander.Expand(
                    "192.0.2.250",
                    "192.0.3.5",
                    "255.255.255.0",
                    32);

                Assert.Fail(
                    "A discovery range must remain inside the subnet selected by the operator mask.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "DISCOVERY_RANGE_CROSSES_SUBNET",
                    exception.Message);
            }
        }

        [TestMethod]
        public void RejectsNonContiguousMaskAndOversizedRange()
        {
            try
            {
                Ipv4RangeExpander.Expand(
                    "192.0.2.1",
                    "192.0.2.10",
                    "255.0.255.0",
                    32);

                Assert.Fail(
                    "A non-contiguous IPv4 subnet mask must be rejected.");
            }
            catch (FormatException exception)
            {
                Assert.AreEqual(
                    "DISCOVERY_SUBNET_MASK_INVALID",
                    exception.Message);
            }

            try
            {
                Ipv4RangeExpander.Expand(
                    "10.0.0.1",
                    "10.0.31.255",
                    "255.255.0.0",
                    4096);

                Assert.Fail(
                    "The discovery safety limit must be enforced before Engine hosting.");
            }
            catch (InvalidOperationException exception)
            {
                Assert.AreEqual(
                    "DISCOVERY_RANGE_TOO_LARGE",
                    exception.Message);
            }
        }
    }
}
