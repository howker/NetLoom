using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.GraphSafety;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Safety;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class PhysicalGraphSafetyAnalyzerTests
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
        public void PhysicalTreeReportsBridgeBlastRadiusForEveryEdge()
        {
            var a = G("00000000-0000-0000-0000-000000000001");
            var b = G("00000000-0000-0000-0000-000000000002");
            var c = G("00000000-0000-0000-0000-000000000003");
            var d = G("00000000-0000-0000-0000-000000000004");

            var ab = Link(Guid.NewGuid(), a, null, b, null);
            var bc = Link(Guid.NewGuid(), b, null, c, null);
            var cd = Link(Guid.NewGuid(), c, null, d, null);

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzePhysicalFailures(
                        new[]
                        {
                            cd,
                            ab,
                            bc
                        });

            Assert.AreEqual(
                3,
                result.Count);

            Assert.IsTrue(
                result.All(item => item.IsBridge));

            var abImpact =
                result.Single(
                    item =>
                        item.PhysicalLinkId ==
                        ab.Id);

            var bcImpact =
                result.Single(
                    item =>
                        item.PhysicalLinkId ==
                        bc.Id);

            Assert.AreEqual(
                3L,
                abImpact.SeparatedDevicePairCount);

            Assert.AreEqual(
                4L,
                bcImpact.SeparatedDevicePairCount);

            Assert.IsTrue(
                abImpact.SideADeviceIds.Contains(
                    abImpact.DeviceAId));

            Assert.IsTrue(
                abImpact.SideBDeviceIds.Contains(
                    abImpact.DeviceBId));

            Assert.AreEqual(
                4,
                abImpact.SideADeviceIds
                    .Concat(
                        abImpact.SideBDeviceIds)
                    .Distinct()
                    .Count());
        }

        [TestMethod]
        public void TriangleHasZeroFailureImpactForEveryLink()
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
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzePhysicalFailures(
                        links);

            Assert.AreEqual(
                3,
                result.Count);

            Assert.IsTrue(
                result.All(
                    item =>
                        !item.IsBridge &&
                        item.SeparatedDevicePairCount == 0 &&
                        item.SideADeviceIds.Count == 0 &&
                        item.SideBDeviceIds.Count == 0));
        }

        [TestMethod]
        public void ParallelLinksAreNotBridgesAndReverseDuplicateIsDeduplicated()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var a1 = Guid.NewGuid();
            var b1 = Guid.NewGuid();
            var a2 = Guid.NewGuid();
            var b2 = Guid.NewGuid();

            var first =
                Link(
                    G("10000000-0000-0000-0000-000000000001"),
                    a,
                    a1,
                    b,
                    b1);

            var reverseDuplicate =
                Link(
                    G("f0000000-0000-0000-0000-000000000001"),
                    b,
                    b1,
                    a,
                    a1);

            var second =
                Link(
                    G("20000000-0000-0000-0000-000000000001"),
                    a,
                    a2,
                    b,
                    b2);

            Assert.AreEqual(
                first.LinkKey,
                reverseDuplicate.LinkKey);

            Assert.AreNotEqual(
                first.LinkKey,
                second.LinkKey);

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzePhysicalFailures(
                        new[]
                        {
                            reverseDuplicate,
                            second,
                            first
                        });

            Assert.AreEqual(
                2,
                result.Count);

            Assert.IsTrue(
                result.All(item => !item.IsBridge));

            CollectionAssert.AreEqual(
                new[]
                {
                    first.Id,
                    second.Id
                }.OrderBy(id => id).ToArray(),
                result
                    .Select(item => item.PhysicalLinkId)
                    .ToArray());
        }

        [TestMethod]
        public void ManualHiddenAndStaleRemainPhysicalWhileArchivedIsIgnored()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();

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
                    d,
                    null,
                    PhysicalLinkStrength.Manual);

            var archived =
                Link(
                    Guid.NewGuid(),
                    d,
                    null,
                    a,
                    null,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Fresh,
                    false,
                    true);

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzePhysicalFailures(
                        new[]
                        {
                            hidden,
                            stale,
                            manual,
                            archived
                        });

            Assert.AreEqual(
                3,
                result.Count);

            CollectionAssert.Contains(
                result
                    .Select(item => item.PhysicalLinkId)
                    .ToArray(),
                manual.Id);

            Assert.IsTrue(
                result.All(item => item.IsBridge));
        }

        [TestMethod]
        public void AllForwardingTriangleReportsEveryCycleEdge()
        {
            var topology =
                Triangle();

            var snapshots =
                new[]
                {
                    Snapshot(
                        topology.A,
                        Port(1, topology.A1, StpTreePortState.Forwarding),
                        Port(2, topology.A2, StpTreePortState.Forwarding)),
                    Snapshot(
                        topology.B,
                        Port(1, topology.B1, StpTreePortState.Forwarding),
                        Port(2, topology.B2, StpTreePortState.Forwarding)),
                    Snapshot(
                        topology.C,
                        Port(1, topology.C1, StpTreePortState.Forwarding),
                        Port(2, topology.C2, StpTreePortState.Forwarding))
                };

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzeForwardingCycles(
                        topology.Links,
                        snapshots,
                        "cist");

            Assert.IsTrue(
                result.HasConfirmedForwardingCycle);

            Assert.IsTrue(
                result.IsComplete);

            CollectionAssert.AreEqual(
                topology.Links
                    .Select(link => link.Id)
                    .OrderBy(id => id)
                    .ToArray(),
                result.ConfirmedCyclePhysicalLinkIds
                    .ToArray());

            Assert.AreEqual(
                0,
                result.UnresolvedPhysicalLinkIds.Count);
        }

        [TestMethod]
        public void BlockingEndpointBreaksForwardingCycleWithoutMakingCoverageIncomplete()
        {
            var topology =
                Triangle();

            var snapshots =
                new[]
                {
                    Snapshot(
                        topology.A,
                        Port(1, topology.A1, StpTreePortState.Forwarding),
                        Port(2, topology.A2, StpTreePortState.Blocking)),
                    Snapshot(
                        topology.B,
                        Port(1, topology.B1, StpTreePortState.Forwarding),
                        Port(2, topology.B2, StpTreePortState.Forwarding)),
                    Snapshot(
                        topology.C,
                        Port(1, topology.C1, StpTreePortState.Forwarding),
                        Port(2, topology.C2, StpTreePortState.Forwarding))
                };

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzeForwardingCycles(
                        topology.Links,
                        snapshots,
                        "cist");

            Assert.IsFalse(
                result.HasConfirmedForwardingCycle);

            Assert.IsTrue(
                result.IsComplete);

            Assert.AreEqual(
                0,
                result.UnresolvedPhysicalLinkIds.Count);
        }

        [TestMethod]
        public void MissingSnapshotMakesAffectedLinksUnresolvedWithoutFalseCycle()
        {
            var topology =
                Triangle();

            var snapshots =
                new[]
                {
                    Snapshot(
                        topology.A,
                        Port(1, topology.A1, StpTreePortState.Forwarding),
                        Port(2, topology.A2, StpTreePortState.Forwarding)),
                    Snapshot(
                        topology.B,
                        Port(1, topology.B1, StpTreePortState.Forwarding),
                        Port(2, topology.B2, StpTreePortState.Forwarding))
                };

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzeForwardingCycles(
                        topology.Links,
                        snapshots,
                        "cist");

            Assert.IsFalse(
                result.HasConfirmedForwardingCycle);

            Assert.IsFalse(
                result.IsComplete);

            CollectionAssert.AreEqual(
                topology.Links
                    .Where(
                        link =>
                            link.DeviceAId == topology.C ||
                            link.DeviceBId == topology.C)
                    .Select(link => link.Id)
                    .OrderBy(id => id)
                    .ToArray(),
                result.UnresolvedPhysicalLinkIds
                    .ToArray());
        }

        [TestMethod]
        public void TransitionalAndUnknownStpStatesRemainUnresolved()
        {
            foreach (var state in new[]
            {
                StpTreePortState.Unknown,
                StpTreePortState.Listening,
                StpTreePortState.Learning,
                StpTreePortState.Broken
            })
            {
                var a = Guid.NewGuid();
                var b = Guid.NewGuid();
                var a1 = Guid.NewGuid();
                var b1 = Guid.NewGuid();

                var link =
                    Link(
                        Guid.NewGuid(),
                        a,
                        a1,
                        b,
                        b1);

                var result =
                    new PhysicalGraphSafetyAnalyzer()
                        .AnalyzeForwardingCycles(
                            new[]
                            {
                                link
                            },
                            new[]
                            {
                                Snapshot(
                                    a,
                                    Port(1, a1, state)),
                                Snapshot(
                                    b,
                                    Port(
                                        1,
                                        b1,
                                        StpTreePortState.Forwarding))
                            },
                            "cist");

                Assert.IsFalse(
                    result.IsComplete,
                    state.ToString());

                CollectionAssert.AreEqual(
                    new[]
                    {
                        link.Id
                    },
                    result.UnresolvedPhysicalLinkIds
                        .ToArray());
            }
        }

        [TestMethod]
        public void DuplicateDeviceSnapshotIsUnresolvedInsteadOfChoosingByTime()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var a1 = Guid.NewGuid();
            var b1 = Guid.NewGuid();

            var link =
                Link(
                    Guid.NewGuid(),
                    a,
                    a1,
                    b,
                    b1);

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzeForwardingCycles(
                        new[]
                        {
                            link
                        },
                        new[]
                        {
                            Snapshot(
                                a,
                                Port(
                                    1,
                                    a1,
                                    StpTreePortState.Forwarding)),
                            Snapshot(
                                a,
                                Port(
                                    1,
                                    a1,
                                    StpTreePortState.Blocking)),
                            Snapshot(
                                b,
                                Port(
                                    1,
                                    b1,
                                    StpTreePortState.Forwarding))
                        },
                        "cist");

            Assert.IsFalse(
                result.IsComplete);

            CollectionAssert.AreEqual(
                new[]
                {
                    link.Id
                },
                result.UnresolvedPhysicalLinkIds
                    .ToArray());
        }

        [TestMethod]
        public void TwoParallelForwardingLinksAreBothBasisIndependentCycleEdges()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var a1 = Guid.NewGuid();
            var b1 = Guid.NewGuid();
            var a2 = Guid.NewGuid();
            var b2 = Guid.NewGuid();

            var first =
                Link(
                    Guid.NewGuid(),
                    a,
                    a1,
                    b,
                    b1);

            var second =
                Link(
                    Guid.NewGuid(),
                    a,
                    a2,
                    b,
                    b2);

            var result =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzeForwardingCycles(
                        new[]
                        {
                            second,
                            first
                        },
                        new[]
                        {
                            Snapshot(
                                b,
                                Port(
                                    2,
                                    b2,
                                    StpTreePortState.Forwarding),
                                Port(
                                    1,
                                    b1,
                                    StpTreePortState.Forwarding)),
                            Snapshot(
                                a,
                                Port(
                                    2,
                                    a2,
                                    StpTreePortState.Forwarding),
                                Port(
                                    1,
                                    a1,
                                    StpTreePortState.Forwarding))
                        },
                        "cist");

            Assert.IsTrue(
                result.HasConfirmedForwardingCycle);

            CollectionAssert.AreEqual(
                new[]
                {
                    first.Id,
                    second.Id
                }.OrderBy(id => id).ToArray(),
                result.ConfirmedCyclePhysicalLinkIds
                    .ToArray());
        }

        [TestMethod]
        public void ResultIsDeterministicForReversedInputOrder()
        {
            var topology =
                Triangle();

            var snapshots =
                new[]
                {
                    Snapshot(
                        topology.A,
                        Port(1, topology.A1, StpTreePortState.Forwarding),
                        Port(2, topology.A2, StpTreePortState.Forwarding)),
                    Snapshot(
                        topology.B,
                        Port(1, topology.B1, StpTreePortState.Forwarding),
                        Port(2, topology.B2, StpTreePortState.Forwarding)),
                    Snapshot(
                        topology.C,
                        Port(1, topology.C1, StpTreePortState.Forwarding),
                        Port(2, topology.C2, StpTreePortState.Forwarding))
                };

            var analyzer =
                new PhysicalGraphSafetyAnalyzer();

            var firstFailures =
                analyzer.AnalyzePhysicalFailures(
                    topology.Links);

            var secondFailures =
                analyzer.AnalyzePhysicalFailures(
                    topology.Links.Reverse());

            CollectionAssert.AreEqual(
                firstFailures
                    .Select(item => item.PhysicalLinkId)
                    .ToArray(),
                secondFailures
                    .Select(item => item.PhysicalLinkId)
                    .ToArray());

            var firstForwarding =
                analyzer.AnalyzeForwardingCycles(
                    topology.Links,
                    snapshots,
                    "cist");

            var secondForwarding =
                analyzer.AnalyzeForwardingCycles(
                    topology.Links.Reverse(),
                    snapshots.Reverse(),
                    "cist");

            CollectionAssert.AreEqual(
                firstForwarding
                    .ConfirmedCyclePhysicalLinkIds
                    .ToArray(),
                secondForwarding
                    .ConfirmedCyclePhysicalLinkIds
                    .ToArray());

            CollectionAssert.AreEqual(
                firstForwarding
                    .UnresolvedPhysicalLinkIds
                    .ToArray(),
                secondForwarding
                    .UnresolvedPhysicalLinkIds
                    .ToArray());
        }

        private static TriangleTopology Triangle()
        {
            var result =
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

            result.Links =
                new[]
                {
                    Link(
                        Guid.NewGuid(),
                        result.A,
                        result.A1,
                        result.B,
                        result.B1),
                    Link(
                        Guid.NewGuid(),
                        result.B,
                        result.B2,
                        result.C,
                        result.C1),
                    Link(
                        Guid.NewGuid(),
                        result.C,
                        result.C2,
                        result.A,
                        result.A2)
                };

            return result;
        }

        private static StpTreeSnapshot Snapshot(
            Guid deviceId,
            params StpTreePort[] ports)
        {
            return new StpTreeSnapshot(
                deviceId,
                Guid.NewGuid(),
                Now,
                "cist",
                "8000.001122334455",
                0,
                null,
                null,
                null,
                ports);
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
                "graph-safety-test",
                Now,
                Now,
                Now,
                "sprint27",
                isHidden,
                isArchived,
                null);
        }

        private static Guid G(
            string value)
        {
            return Guid.Parse(
                value);
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
        }
    }
}
