using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Rings;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Rings;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class PhysicalRedundancyRegionDetectorTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                7,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void TriangleIsOneNamedSimpleRing()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var links =
                new[]
                {
                    Link(Guid.NewGuid(), a, null, b, null),
                    Link(Guid.NewGuid(), b, null, c, null),
                    Link(Guid.NewGuid(), c, null, a, null)
                };

            var result =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        links);

            Assert.AreEqual(
                1,
                result.Count);

            Assert.AreEqual(
                PhysicalRedundancyRegionKind.SimpleRing,
                result[0].Kind);

            Assert.IsTrue(
                result[0].IsNamedRingCandidate);

            CollectionAssert.AreEqual(
                links
                    .Select(link => link.Id)
                    .OrderBy(id => id)
                    .ToArray(),
                result[0]
                    .PhysicalLinkIds
                    .ToArray());
        }

        [TestMethod]
        public void SquareWithDiagonalIsOneCompositeRegionNotArbitraryNamedRings()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();

            var links =
                new[]
                {
                    Link(Guid.NewGuid(), a, null, b, null),
                    Link(Guid.NewGuid(), b, null, c, null),
                    Link(Guid.NewGuid(), c, null, d, null),
                    Link(Guid.NewGuid(), d, null, a, null),
                    Link(Guid.NewGuid(), a, null, c, null)
                };

            var result =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        links);

            Assert.AreEqual(
                1,
                result.Count);

            Assert.AreEqual(
                PhysicalRedundancyRegionKind.Composite,
                result[0].Kind);

            Assert.IsFalse(
                result[0].IsNamedRingCandidate);

            Assert.AreEqual(
                5,
                result[0].PhysicalLinkIds.Count);
        }

        [TestMethod]
        public void FigureEightSharingOneDeviceProducesTwoSimpleRingRegions()
        {
            var center = Guid.NewGuid();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();

            var first =
                new[]
                {
                    Link(Guid.NewGuid(), center, null, a, null),
                    Link(Guid.NewGuid(), a, null, b, null),
                    Link(Guid.NewGuid(), b, null, center, null)
                };

            var second =
                new[]
                {
                    Link(Guid.NewGuid(), center, null, c, null),
                    Link(Guid.NewGuid(), c, null, d, null),
                    Link(Guid.NewGuid(), d, null, center, null)
                };

            var result =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        first.Concat(second));

            Assert.AreEqual(
                2,
                result.Count);

            Assert.IsTrue(
                result.All(
                    item =>
                        item.Kind ==
                            PhysicalRedundancyRegionKind.SimpleRing &&
                        item.IsNamedRingCandidate));

            Assert.AreEqual(
                2,
                result
                    .Select(item => item.RegionKey)
                    .Distinct(
                        StringComparer.Ordinal)
                    .Count());
        }

        [TestMethod]
        public void ParallelLinksAreRedundancyButNotNamedRing()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var first =
                Link(
                    Guid.NewGuid(),
                    a,
                    Guid.NewGuid(),
                    b,
                    Guid.NewGuid());

            var second =
                Link(
                    Guid.NewGuid(),
                    a,
                    Guid.NewGuid(),
                    b,
                    Guid.NewGuid());

            var result =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        new[]
                        {
                            second,
                            first
                        });

            Assert.AreEqual(
                1,
                result.Count);

            Assert.AreEqual(
                PhysicalRedundancyRegionKind.ParallelLinks,
                result[0].Kind);

            Assert.IsFalse(
                result[0].IsNamedRingCandidate);

            Assert.AreEqual(
                2,
                result[0].PhysicalLinkIds.Count);
        }

        [TestMethod]
        public void ReverseDuplicateDoesNotCreateFalseRedundancyRegion()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var ia = Guid.NewGuid();
            var ib = Guid.NewGuid();

            var forward =
                Link(
                    Guid.NewGuid(),
                    a,
                    ia,
                    b,
                    ib);

            var reverse =
                Link(
                    Guid.NewGuid(),
                    b,
                    ib,
                    a,
                    ia);

            Assert.AreEqual(
                forward.LinkKey,
                reverse.LinkKey);

            var result =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        new[]
                        {
                            reverse,
                            forward
                        });

            Assert.AreEqual(
                0,
                result.Count);
        }

        [TestMethod]
        public void AcyclicDisconnectedGraphProducesNoRegion()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();

            var result =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        new[]
                        {
                            Link(Guid.NewGuid(), a, null, b, null),
                            Link(Guid.NewGuid(), b, null, c, null),
                            Link(Guid.NewGuid(), d, null, Guid.NewGuid(), null)
                        });

            Assert.AreEqual(
                0,
                result.Count);
        }

        [TestMethod]
        public void ManualHiddenAndStaleRemainMembersWhileArchivedIsExcluded()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var hidden =
                Link(
                    Guid.NewGuid(),
                    a,
                    null,
                    b,
                    null,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Fresh,
                    true,
                    false);

            var stale =
                Link(
                    Guid.NewGuid(),
                    b,
                    null,
                    c,
                    null,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Stale,
                    false,
                    false);

            var manual =
                Link(
                    Guid.NewGuid(),
                    c,
                    null,
                    a,
                    null,
                    PhysicalLinkStrength.Manual);

            var current =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        new[]
                        {
                            hidden,
                            stale,
                            manual
                        });

            Assert.AreEqual(
                1,
                current.Count);

            Assert.AreEqual(
                PhysicalRedundancyRegionKind.SimpleRing,
                current[0].Kind);

            var archived =
                Link(
                    manual.Id,
                    manual.DeviceAId,
                    manual.InterfaceAId,
                    manual.DeviceBId,
                    manual.InterfaceBId,
                    manual.Strength,
                    manual.Freshness,
                    manual.IsHidden,
                    true);

            var withoutArchived =
                new PhysicalRedundancyRegionDetector()
                    .Detect(
                        new[]
                        {
                            hidden,
                            stale,
                            archived
                        });

            Assert.AreEqual(
                0,
                withoutArchived.Count);
        }

        [TestMethod]
        public void RegionIdentityIsStableAcrossEndpointRefinementWhenLinkIdsStayStable()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var abId = Guid.NewGuid();
            var bcId = Guid.NewGuid();
            var caId = Guid.NewGuid();

            var provisional =
                new[]
                {
                    Link(abId, a, null, b, null),
                    Link(bcId, b, null, c, null),
                    Link(caId, c, null, a, null)
                };

            var refined =
                new[]
                {
                    Link(abId, a, Guid.NewGuid(), b, Guid.NewGuid()),
                    Link(bcId, b, Guid.NewGuid(), c, Guid.NewGuid()),
                    Link(caId, c, Guid.NewGuid(), a, Guid.NewGuid())
                };

            var detector =
                new PhysicalRedundancyRegionDetector();

            var first =
                detector.Detect(
                    provisional);

            var second =
                detector.Detect(
                    refined);

            Assert.AreEqual(
                1,
                first.Count);

            Assert.AreEqual(
                1,
                second.Count);

            Assert.AreEqual(
                first[0].RegionKey,
                second[0].RegionKey);
        }

        [TestMethod]
        public void ResultsAreDeterministicForReversedInputOrder()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();

            var links =
                new[]
                {
                    Link(Guid.NewGuid(), a, null, b, null),
                    Link(Guid.NewGuid(), b, null, c, null),
                    Link(Guid.NewGuid(), c, null, a, null),
                    Link(Guid.NewGuid(), c, null, d, null),
                    Link(Guid.NewGuid(), d, null, b, null)
                };

            var detector =
                new PhysicalRedundancyRegionDetector();

            var first =
                detector.Detect(
                    links);

            var second =
                detector.Detect(
                    links.Reverse());

            CollectionAssert.AreEqual(
                first
                    .Select(item => item.RegionKey)
                    .ToArray(),
                second
                    .Select(item => item.RegionKey)
                    .ToArray());

            Assert.AreEqual(
                first.Count,
                second.Count);

            for (var index = 0;
                 index < first.Count;
                 index++)
            {
                Assert.AreEqual(
                    first[index].Kind,
                    second[index].Kind);

                CollectionAssert.AreEqual(
                    first[index]
                        .PhysicalLinkIds
                        .ToArray(),
                    second[index]
                        .PhysicalLinkIds
                        .ToArray());
            }
        }

        private static PhysicalLink Link(
            Guid id,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            PhysicalLinkStrength strength =
                PhysicalLinkStrength.Confirmed,
            PhysicalLinkFreshness freshness =
                PhysicalLinkFreshness.Fresh,
            bool isHidden = false,
            bool isArchived = false)
        {
            return new PhysicalLink(
                id,
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId,
                strength,
                freshness,
                null,
                null,
                "ring-region-test",
                Now,
                Now,
                Now,
                "sprint28",
                isHidden,
                isArchived,
                null);
        }
    }
}
