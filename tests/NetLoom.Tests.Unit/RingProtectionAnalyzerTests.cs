using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Rings;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Rings;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class RingProtectionAnalyzerTests
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
        public void SimpleRingWithExactlyOneBlockingLinkIsProtected()
        {
            var topology =
                Triangle();

            var result =
                Analyze(
                    topology,
                    topology.Links[2].Id,
                    null,
                    null);

            Assert.AreEqual(
                RingProtectionStatus.Protected,
                result.Status);

            Assert.IsTrue(
                result.IsComplete);

            CollectionAssert.AreEqual(
                new[]
                {
                    topology.Links[2].Id
                },
                result.BlockingPhysicalLinkIds
                    .ToArray());

            Assert.AreEqual(
                2,
                result.ForwardingPhysicalLinkIds.Count);
        }

        [TestMethod]
        public void AllForwardingSimpleRingIsUnprotected()
        {
            var topology =
                Triangle();

            var result =
                Analyze(
                    topology,
                    null,
                    null,
                    null);

            Assert.AreEqual(
                RingProtectionStatus.Unprotected,
                result.Status);

            Assert.IsTrue(
                result.IsComplete);

            CollectionAssert.AreEqual(
                topology.Links
                    .Select(link => link.Id)
                    .OrderBy(id => id)
                    .ToArray(),
                result.ForwardingPhysicalLinkIds
                    .ToArray());
        }

        [TestMethod]
        public void DisabledLinkIsDegradedNotProtected()
        {
            var topology =
                Triangle();

            var result =
                Analyze(
                    topology,
                    null,
                    topology.Links[1].Id,
                    null);

            Assert.AreEqual(
                RingProtectionStatus.Degraded,
                result.Status);

            CollectionAssert.AreEqual(
                new[]
                {
                    topology.Links[1].Id
                },
                result.DisabledPhysicalLinkIds
                    .ToArray());
        }

        [TestMethod]
        public void TwoBlockingLinksAreDegraded()
        {
            var topology =
                Triangle();

            var snapshots =
                new[]
                {
                    Snapshot(
                        topology.A,
                        topology.A1,
                        StpTreePortState.Blocking,
                        topology.A2,
                        StpTreePortState.Forwarding,
                        "cist"),
                    Snapshot(
                        topology.B,
                        topology.B1,
                        StpTreePortState.Blocking,
                        topology.B2,
                        StpTreePortState.Blocking,
                        "cist"),
                    Snapshot(
                        topology.C,
                        topology.C1,
                        StpTreePortState.Blocking,
                        topology.C2,
                        StpTreePortState.Forwarding,
                        "cist")
                };

            var result =
                new RingProtectionAnalyzer()
                    .Analyze(
                        topology.Region,
                        topology.Links,
                        snapshots,
                        "cist");

            Assert.AreEqual(
                RingProtectionStatus.Degraded,
                result.Status);

            Assert.AreEqual(
                2,
                result.BlockingPhysicalLinkIds.Count);
        }

        [TestMethod]
        public void MissingSnapshotMakesRingUnresolved()
        {
            var topology =
                Triangle();

            var snapshots =
                Snapshots(
                    topology,
                    topology.Links[2].Id,
                    null,
                    null,
                    "cist")
                    .Where(
                        snapshot =>
                            snapshot.DeviceId !=
                            topology.C)
                    .ToArray();

            var result =
                new RingProtectionAnalyzer()
                    .Analyze(
                        topology.Region,
                        topology.Links,
                        snapshots,
                        "cist");

            Assert.AreEqual(
                RingProtectionStatus.Unresolved,
                result.Status);

            Assert.IsFalse(
                result.IsComplete);

            Assert.IsTrue(
                result.UnresolvedPhysicalLinkIds.Count > 0);
        }

        [TestMethod]
        public void DuplicateDeviceSnapshotMakesRingUnresolved()
        {
            var topology =
                Triangle();

            var snapshots =
                Snapshots(
                    topology,
                    topology.Links[2].Id,
                    null,
                    null,
                    "cist")
                    .ToList();

            snapshots.Add(
                Snapshot(
                    topology.A,
                    topology.A1,
                    StpTreePortState.Forwarding,
                    topology.A2,
                    StpTreePortState.Blocking,
                    "cist"));

            var result =
                new RingProtectionAnalyzer()
                    .Analyze(
                        topology.Region,
                        topology.Links,
                        snapshots,
                        "cist");

            Assert.AreEqual(
                RingProtectionStatus.Unresolved,
                result.Status);
        }

        [TestMethod]
        public void TransitionalAndUnknownStatesRemainUnresolved()
        {
            foreach (var state in new[]
            {
                StpTreePortState.Unknown,
                StpTreePortState.Listening,
                StpTreePortState.Learning,
                StpTreePortState.Broken
            })
            {
                var topology =
                    Triangle();

                var snapshots =
                    Snapshots(
                        topology,
                        topology.Links[2].Id,
                        null,
                        null,
                        "cist")
                        .ToArray();

                snapshots[0] =
                    Snapshot(
                        topology.A,
                        topology.A1,
                        state,
                        topology.A2,
                        StpTreePortState.Blocking,
                        "cist");

                var result =
                    new RingProtectionAnalyzer()
                        .Analyze(
                            topology.Region,
                            topology.Links,
                            snapshots,
                            "cist");

                Assert.AreEqual(
                    RingProtectionStatus.Unresolved,
                    result.Status,
                    state.ToString());
            }
        }

        [TestMethod]
        public void WrongInstanceDoesNotReuseCistSnapshots()
        {
            var topology =
                Triangle();

            var snapshots =
                Snapshots(
                    topology,
                    topology.Links[2].Id,
                    null,
                    null,
                    "cist");

            var result =
                new RingProtectionAnalyzer()
                    .Analyze(
                        topology.Region,
                        topology.Links,
                        snapshots,
                        "mst-1");

            Assert.AreEqual(
                RingProtectionStatus.Unresolved,
                result.Status);

            Assert.AreEqual(
                3,
                result.UnresolvedPhysicalLinkIds.Count);
        }

        [TestMethod]
        public void ManualUnmanagedMemberWithoutInterfaceIdsIsUnresolved()
        {
            var topology =
                Triangle();

            var manual =
                Link(
                    topology.Links[2].Id,
                    topology.C,
                    null,
                    topology.A,
                    null,
                    PhysicalLinkStrength.Manual,
                    PhysicalLinkFreshness.Fresh);

            var links =
                new[]
                {
                    topology.Links[0],
                    topology.Links[1],
                    manual
                };

            var result =
                new RingProtectionAnalyzer()
                    .Analyze(
                        topology.Region,
                        links,
                        Snapshots(
                            topology,
                            topology.Links[2].Id,
                            null,
                            null,
                            "cist"),
                        "cist");

            Assert.AreEqual(
                RingProtectionStatus.Unresolved,
                result.Status);

            CollectionAssert.Contains(
                result.UnresolvedPhysicalLinkIds
                    .ToArray(),
                manual.Id);
        }

        [TestMethod]
        public void StaleMemberCanStillBeProtectedWhenStpCoverageIsComplete()
        {
            var topology =
                Triangle();

            var stale =
                Link(
                    topology.Links[0].Id,
                    topology.A,
                    topology.A1,
                    topology.B,
                    topology.B1,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Stale);

            var links =
                new[]
                {
                    stale,
                    topology.Links[1],
                    topology.Links[2]
                };

            var result =
                new RingProtectionAnalyzer()
                    .Analyze(
                        topology.Region,
                        links,
                        Snapshots(
                            topology,
                            topology.Links[2].Id,
                            null,
                            null,
                            "cist"),
                        "cist");

            Assert.AreEqual(
                RingProtectionStatus.Protected,
                result.Status);
        }

        [TestMethod]
        public void NonSimpleRegionsAreNotApplicable()
        {
            var topology =
                Triangle();

            foreach (var kind in new[]
            {
                PhysicalRedundancyRegionKind.ParallelLinks,
                PhysicalRedundancyRegionKind.Composite
            })
            {
                var region =
                    new PhysicalRedundancyRegion(
                        "region-" + kind,
                        kind,
                        topology.Region.DeviceIds,
                        topology.Region.PhysicalLinkIds);

                var result =
                    new RingProtectionAnalyzer()
                        .Analyze(
                            region,
                            topology.Links,
                            new StpTreeSnapshot[0],
                            "cist");

                Assert.AreEqual(
                    RingProtectionStatus.NotApplicable,
                    result.Status,
                    kind.ToString());

                Assert.IsTrue(
                    result.IsComplete);
            }
        }

        [TestMethod]
        public void MissingRegionLinkMakesSimpleRingUnresolved()
        {
            var topology =
                Triangle();

            var result =
                new RingProtectionAnalyzer()
                    .Analyze(
                        topology.Region,
                        topology.Links.Take(2),
                        Snapshots(
                            topology,
                            topology.Links[2].Id,
                            null,
                            null,
                            "cist"),
                        "cist");

            Assert.AreEqual(
                RingProtectionStatus.Unresolved,
                result.Status);

            CollectionAssert.Contains(
                result.UnresolvedPhysicalLinkIds
                    .ToArray(),
                topology.Links[2].Id);
        }

        [TestMethod]
        public void ResultIsDeterministicForReversedInputs()
        {
            var topology =
                Triangle();

            var snapshots =
                Snapshots(
                    topology,
                    topology.Links[2].Id,
                    null,
                    null,
                    "cist");

            var analyzer =
                new RingProtectionAnalyzer();

            var first =
                analyzer.Analyze(
                    topology.Region,
                    topology.Links,
                    snapshots,
                    "cist");

            var second =
                analyzer.Analyze(
                    topology.Region,
                    topology.Links.Reverse(),
                    snapshots.Reverse(),
                    "cist");

            Assert.AreEqual(
                first.Status,
                second.Status);

            CollectionAssert.AreEqual(
                first.ForwardingPhysicalLinkIds
                    .ToArray(),
                second.ForwardingPhysicalLinkIds
                    .ToArray());

            CollectionAssert.AreEqual(
                first.BlockingPhysicalLinkIds
                    .ToArray(),
                second.BlockingPhysicalLinkIds
                    .ToArray());

            CollectionAssert.AreEqual(
                first.DisabledPhysicalLinkIds
                    .ToArray(),
                second.DisabledPhysicalLinkIds
                    .ToArray());

            CollectionAssert.AreEqual(
                first.UnresolvedPhysicalLinkIds
                    .ToArray(),
                second.UnresolvedPhysicalLinkIds
                    .ToArray());
        }

        private static RingProtectionAnalysis Analyze(
            TriangleTopology topology,
            Guid? blockingLinkId,
            Guid? disabledLinkId,
            Guid? unresolvedLinkId)
        {
            return new RingProtectionAnalyzer()
                .Analyze(
                    topology.Region,
                    topology.Links,
                    Snapshots(
                        topology,
                        blockingLinkId,
                        disabledLinkId,
                        unresolvedLinkId,
                        "cist"),
                    "cist");
        }

        private static StpTreeSnapshot[] Snapshots(
            TriangleTopology topology,
            Guid? blockingLinkId,
            Guid? disabledLinkId,
            Guid? unresolvedLinkId,
            string instanceId)
        {
            return new[]
            {
                Snapshot(
                    topology.A,
                    topology.A1,
                    StateFor(
                        topology.Links[0],
                        blockingLinkId,
                        disabledLinkId,
                        unresolvedLinkId),
                    topology.A2,
                    StateFor(
                        topology.Links[2],
                        blockingLinkId,
                        disabledLinkId,
                        unresolvedLinkId),
                    instanceId),
                Snapshot(
                    topology.B,
                    topology.B1,
                    StateFor(
                        topology.Links[0],
                        blockingLinkId,
                        disabledLinkId,
                        unresolvedLinkId),
                    topology.B2,
                    StateFor(
                        topology.Links[1],
                        blockingLinkId,
                        disabledLinkId,
                        unresolvedLinkId),
                    instanceId),
                Snapshot(
                    topology.C,
                    topology.C1,
                    StateFor(
                        topology.Links[1],
                        blockingLinkId,
                        disabledLinkId,
                        unresolvedLinkId),
                    topology.C2,
                    StateFor(
                        topology.Links[2],
                        blockingLinkId,
                        disabledLinkId,
                        unresolvedLinkId),
                    instanceId)
            };
        }

        private static StpTreePortState StateFor(
            PhysicalLink link,
            Guid? blockingLinkId,
            Guid? disabledLinkId,
            Guid? unresolvedLinkId)
        {
            if (unresolvedLinkId.HasValue &&
                link.Id ==
                    unresolvedLinkId.Value)
            {
                return
                    StpTreePortState.Listening;
            }

            if (disabledLinkId.HasValue &&
                link.Id ==
                    disabledLinkId.Value)
            {
                return
                    StpTreePortState.Disabled;
            }

            if (blockingLinkId.HasValue &&
                link.Id ==
                    blockingLinkId.Value)
            {
                return
                    StpTreePortState.Blocking;
            }

            return
                StpTreePortState.Forwarding;
        }

        private static StpTreeSnapshot Snapshot(
            Guid deviceId,
            Guid interface1,
            StpTreePortState state1,
            Guid interface2,
            StpTreePortState state2,
            string instanceId)
        {
            return new StpTreeSnapshot(
                deviceId,
                Guid.NewGuid(),
                Now,
                instanceId,
                "8000.001122334455",
                0,
                null,
                null,
                null,
                new[]
                {
                    Port(
                        1,
                        interface1,
                        state1),
                    Port(
                        2,
                        interface2,
                        state2)
                });
        }

        private static StpTreePort Port(
            int bridgePortIndex,
            Guid interfaceId,
            StpTreePortState state)
        {
            return new StpTreePort(
                bridgePortIndex,
                100 + bridgePortIndex,
                interfaceId,
                "port-" + bridgePortIndex,
                state,
                false,
                10);
        }

        private static TriangleTopology Triangle()
        {
            var topology =
                new TriangleTopology
                {
                    A = Guid.NewGuid(),
                    B = Guid.NewGuid(),
                    C = Guid.NewGuid(),
                    A1 = Guid.NewGuid(),
                    A2 = Guid.NewGuid(),
                    B1 = Guid.NewGuid(),
                    B2 = Guid.NewGuid(),
                    C1 = Guid.NewGuid(),
                    C2 = Guid.NewGuid()
                };

            topology.Links =
                new[]
                {
                    Link(
                        Guid.NewGuid(),
                        topology.A,
                        topology.A1,
                        topology.B,
                        topology.B1),
                    Link(
                        Guid.NewGuid(),
                        topology.B,
                        topology.B2,
                        topology.C,
                        topology.C1),
                    Link(
                        Guid.NewGuid(),
                        topology.C,
                        topology.C2,
                        topology.A,
                        topology.A2)
                };

            topology.Region =
                new PhysicalRedundancyRegion(
                    "region-triangle",
                    PhysicalRedundancyRegionKind.SimpleRing,
                    new[]
                    {
                        topology.A,
                        topology.B,
                        topology.C
                    },
                    topology.Links
                        .Select(link => link.Id));

            return topology;
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
                PhysicalLinkFreshness.Fresh)
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
                "ring-protection-test",
                Now,
                Now,
                Now,
                "sprint29",
                false,
                false,
                null);
        }

        private sealed class TriangleTopology
        {
            public Guid A { get; set; }

            public Guid B { get; set; }

            public Guid C { get; set; }

            public Guid A1 { get; set; }

            public Guid A2 { get; set; }

            public Guid B1 { get; set; }

            public Guid B2 { get; set; }

            public Guid C1 { get; set; }

            public Guid C2 { get; set; }

            public PhysicalLink[] Links { get; set; }

            public PhysicalRedundancyRegion Region { get; set; }
        }
    }
}
