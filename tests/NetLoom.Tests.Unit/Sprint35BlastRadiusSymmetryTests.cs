using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Safety;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint35BlastRadiusSymmetryTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                16,
                16,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ReverseEndpointConstructionPreservesBlastRadius()
        {
            var a =
                new Guid(
                    "00000000-0000-0000-0000-000000000001");

            var b =
                new Guid(
                    "00000000-0000-0000-0000-000000000002");

            var c =
                new Guid(
                    "00000000-0000-0000-0000-000000000003");

            var abId =
                new Guid(
                    "10000000-0000-0000-0000-000000000001");

            var bcId =
                new Guid(
                    "10000000-0000-0000-0000-000000000002");

            var forward =
                new[]
                {
                    Link(
                        abId,
                        a,
                        b),
                    Link(
                        bcId,
                        b,
                        c)
                };

            var reversed =
                new[]
                {
                    Link(
                        abId,
                        b,
                        a),
                    Link(
                        bcId,
                        c,
                        b)
                };

            var analyzer =
                new PhysicalGraphSafetyAnalyzer();

            var first =
                analyzer
                    .AnalyzePhysicalFailures(
                        forward)
                    .Single(
                        item =>
                            item.PhysicalLinkId ==
                            abId);

            var second =
                analyzer
                    .AnalyzePhysicalFailures(
                        reversed)
                    .Single(
                        item =>
                            item.PhysicalLinkId ==
                            abId);

            Assert.AreEqual(
                first.DeviceAId,
                second.DeviceAId);

            Assert.AreEqual(
                first.DeviceBId,
                second.DeviceBId);

            CollectionAssert.AreEqual(
                first.SideADeviceIds.ToArray(),
                second.SideADeviceIds.ToArray());

            CollectionAssert.AreEqual(
                first.SideBDeviceIds.ToArray(),
                second.SideBDeviceIds.ToArray());

            Assert.AreEqual(
                first.SeparatedDevicePairCount,
                second.SeparatedDevicePairCount);

            Assert.AreEqual(
                2L,
                first.SeparatedDevicePairCount);

            CollectionAssert.AreEqual(
                new[] { 1, 2 },
                new[]
                {
                    first.SideADeviceIds.Count,
                    first.SideBDeviceIds.Count
                }
                .OrderBy(item => item)
                .ToArray());
        }

        private static PhysicalLink Link(
            Guid id,
            Guid deviceAId,
            Guid deviceBId)
        {
            return
                new PhysicalLink(
                    id,
                    deviceAId,
                    null,
                    deviceBId,
                    null,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Fresh,
                    null,
                    null,
                    null,
                    Now,
                    Now,
                    Now,
                    "sprint35-symmetry",
                    false,
                    false,
                    null);
        }
    }
}
