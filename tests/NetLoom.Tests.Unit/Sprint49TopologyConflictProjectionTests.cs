using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    internal static class Sprint49TopologyConflictFixture
    {
        internal static readonly Guid A = Guid.Parse("49494949-0004-0000-0000-000000000001");
        internal static readonly Guid B = Guid.Parse("49494949-0004-0000-0000-000000000002");
        internal static readonly Guid C = Guid.Parse("49494949-0004-0000-0000-000000000003");
        internal static readonly Guid PortA = Guid.Parse("49494949-0004-0002-0000-000000000001");
        internal static readonly Guid PortB = Guid.Parse("49494949-0004-0002-0000-000000000002");
        internal static readonly Guid PortC = Guid.Parse("49494949-0004-0002-0000-000000000005");
        internal static readonly Guid OtherPortB = Guid.Parse("49494949-0004-0002-0000-000000000007");
        internal static readonly Guid ManualId = Guid.Parse("49494949-0004-0001-0000-000000000001");
        internal static readonly Guid ObservedId = Guid.Parse("49494949-0004-0001-0000-000000000002");

        internal static PhysicalLinkDiagnostic Link(Guid id, Guid deviceB, Guid? portB,
            DiagnosticLinkStrength strength, bool reverse = false, Guid? portA = null)
        {
            var now = DateTime.UtcNow.AddMinutes(-3);
            var nameB = deviceB == B ? "conflict-sw-b" : "conflict-sw-c";
            var portNameB = portB == PortC ? "Gi0/5" : portB == OtherPortB ? "Gi0/7" : "Gi0/2";
            var firstPort = portA ?? PortA;
            return new PhysicalLinkDiagnostic(id, reverse ? deviceB : A, reverse ? A : deviceB,
                reverse ? portB : firstPort, reverse ? firstPort : portB,
                reverse ? nameB : "conflict-sw-a", reverse ? "conflict-sw-a" : nameB,
                reverse ? portNameB : "Gi0/1", reverse ? "Gi0/1" : portNameB,
                strength, MapFreshness.Fresh, "Ethernet", 1000000000L,
                strength == DiagnosticLinkStrength.Manual ? "Manual/User" : "LLDP", now, now,
                StpTreePortState.Unknown, StpTreePortState.Unknown, new DiagnosticEvidenceItem[0],
                false, 0, 0, 0, strength == DiagnosticLinkStrength.Manual
                    ? DiagnosticLldpReporting.NotApplicable : DiagnosticLldpReporting.BothSides);
        }

        internal static NetworkDiagnosticSnapshot Diagnostics(params PhysicalLinkDiagnostic[] links) =>
            new NetworkDiagnosticSnapshot(DateTime.UtcNow, new DeviceDiagnostic[0], links);

        internal static TopologyRefreshSnapshot Snapshot(bool secondConflict = false)
        {
            var ids = new[] { A, B, C };
            var names = new[] { "conflict-sw-a", "conflict-sw-b", "conflict-sw-c" };
            var ports = new[] { PortA, PortB, PortC };
            var portNames = new[] { "Gi0/1", "Gi0/2", "Gi0/5" };
            var now = DateTime.UtcNow;
            var nodes = ids.Select((id, index) => new MapNode(id.ToString("D"), names[index], null,
                100 + index * 260, index == 1 ? 300 : 100, category: MapNodeCategory.Switch, deviceId: id)).ToArray();
            var devices = ids.Select((id, index) => new DeviceDiagnostic(id, names[index], null, null,
                now, now, new[] { new InterfaceDiagnostic(ports[index], id, index + 1, portNames[index],
                    null, "up", "up", 1000000000L, now, StpTreePortState.Unknown,
                    DiagnosticDegradationStatus.Unknown, null, new DiagnosticDegradationReason[0]) })).ToArray();
            var diagnostics = new[]
            {
                Link(ManualId, B, PortB, DiagnosticLinkStrength.Manual),
                Link(ObservedId, C, PortC, DiagnosticLinkStrength.Confirmed),
                Link(Guid.Parse("49494949-0004-0001-0000-000000000003"), B, OtherPortB, DiagnosticLinkStrength.Observed)
            }.Take(secondConflict ? 3 : 2).ToArray();
            var mapLinks = diagnostics.Select(link => new MapLink(link.PhysicalLinkId.ToString("D"),
                link.DeviceAId.ToString("D"), link.DeviceBId.ToString("D"), link.InterfaceAName, link.InterfaceBName,
                MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0], link.PhysicalLinkId));
            return new TopologyRefreshSnapshot(new MapSnapshot(now, nodes, mapLinks),
                new TopologyAlertSnapshot(now, "cist", new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(now, devices, diagnostics));
        }
    }

    [TestClass]
    public sealed class Sprint49TopologyConflictProjectionTests
    {
        [TestMethod]
        [DataRow(DiagnosticLinkStrength.Confirmed, false)]
        [DataRow(DiagnosticLinkStrength.Observed, true)]
        [DataRow(DiagnosticLinkStrength.Inferred, false)]
        public void DifferentDeviceOnSharedPortProducesOneConflict(DiagnosticLinkStrength strength, bool reverse)
        {
            var manual = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ManualId,
                Sprint49TopologyConflictFixture.B, Sprint49TopologyConflictFixture.PortB, DiagnosticLinkStrength.Manual);
            var observed = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ObservedId,
                Sprint49TopologyConflictFixture.C, Sprint49TopologyConflictFixture.PortC, strength, reverse);
            var conflict = TopologyConflictProjection.Build(Sprint49TopologyConflictFixture.Diagnostics(manual, observed), null).Single();
            Assert.AreEqual(manual.PhysicalLinkId, conflict.ManualLinkId);
            Assert.AreEqual(observed.PhysicalLinkId, conflict.ObservedLinkId);
            Assert.AreEqual(Sprint49TopologyConflictFixture.A, conflict.SharedDeviceId);
            Assert.AreEqual(Sprint49TopologyConflictFixture.PortA, conflict.SharedInterfaceId);
            Assert.AreSame(manual, conflict.ManualLink);
            Assert.AreSame(observed, conflict.ObservedLink);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MatchingBothPortsConfirmsManualLinkEvenWithReversedEndpoints(bool reverse)
        {
            var manual = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ManualId,
                Sprint49TopologyConflictFixture.B, Sprint49TopologyConflictFixture.PortB, DiagnosticLinkStrength.Manual);
            var observed = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ObservedId,
                Sprint49TopologyConflictFixture.B, Sprint49TopologyConflictFixture.PortB, DiagnosticLinkStrength.Confirmed, reverse);
            Assert.AreEqual(0, TopologyConflictProjection.Build(Sprint49TopologyConflictFixture.Diagnostics(manual, observed), null).Count);
        }

        [TestMethod]
        public void DifferentRemoteInterfaceOnSameDeviceConflicts()
        {
            var manual = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ManualId,
                Sprint49TopologyConflictFixture.B, Sprint49TopologyConflictFixture.PortB, DiagnosticLinkStrength.Manual);
            var observed = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ObservedId,
                Sprint49TopologyConflictFixture.B, Sprint49TopologyConflictFixture.OtherPortB, DiagnosticLinkStrength.Confirmed);
            Assert.AreEqual(1, TopologyConflictProjection.Build(Sprint49TopologyConflictFixture.Diagnostics(manual, observed), null).Count);
        }

        [TestMethod]
        public void UnspecifiedManualRemotePortComparesOnlyDevice()
        {
            var manual = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ManualId,
                Sprint49TopologyConflictFixture.B, null, DiagnosticLinkStrength.Manual);
            var observed = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ObservedId,
                Sprint49TopologyConflictFixture.B, Sprint49TopologyConflictFixture.OtherPortB, DiagnosticLinkStrength.Confirmed);
            Assert.AreEqual(0, TopologyConflictProjection.Build(Sprint49TopologyConflictFixture.Diagnostics(manual, observed), null).Count);
        }

        [TestMethod]
        public void AcknowledgedPairIsExcludedWithoutHidingOtherConflicts()
        {
            var snapshot = Sprint49TopologyConflictFixture.Snapshot(true);
            var key = new TopologyConflictKey(Sprint49TopologyConflictFixture.ManualId, Sprint49TopologyConflictFixture.ObservedId);
            var conflicts = TopologyConflictProjection.Build(snapshot.DiagnosticSnapshot, new[] { key });
            Assert.AreEqual(1, conflicts.Count);
            Assert.AreNotEqual(key.ObservedLinkId, conflicts.Single().ObservedLinkId);
        }

        [TestMethod]
        public void EmptySharedPortIsIgnoredAndManualPairsDoNotConflict()
        {
            var manual = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ManualId,
                Sprint49TopologyConflictFixture.B, null, DiagnosticLinkStrength.Manual, portA: Guid.Empty);
            var observed = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ObservedId,
                Sprint49TopologyConflictFixture.C, null, DiagnosticLinkStrength.Confirmed, portA: Guid.Empty);
            Assert.AreEqual(0, TopologyConflictProjection.Build(Sprint49TopologyConflictFixture.Diagnostics(manual, observed), null).Count);
            manual = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ManualId,
                Sprint49TopologyConflictFixture.B, Sprint49TopologyConflictFixture.PortB, DiagnosticLinkStrength.Manual);
            observed = Sprint49TopologyConflictFixture.Link(Sprint49TopologyConflictFixture.ObservedId,
                Sprint49TopologyConflictFixture.C, Sprint49TopologyConflictFixture.PortC, DiagnosticLinkStrength.Manual);
            Assert.AreEqual(0, TopologyConflictProjection.Build(Sprint49TopologyConflictFixture.Diagnostics(manual, observed), null).Count);
            Assert.AreEqual(0, TopologyConflictProjection.Build(null, null).Count);
        }

        [TestMethod]
        public void ConflictOrderingAndQualityTargetAreStable()
        {
            var snapshot = Sprint49TopologyConflictFixture.Snapshot(true);
            var diagnostics = snapshot.DiagnosticSnapshot;
            var first = TopologyConflictProjection.Build(diagnostics, null);
            var reversed = TopologyConflictProjection.Build(new NetworkDiagnosticSnapshot(diagnostics.GeneratedUtc,
                diagnostics.Devices.Reverse(), diagnostics.Links.Reverse()), null);
            CollectionAssert.AreEqual(first.Select(conflict => conflict.ObservedLinkId).ToArray(),
                reversed.Select(conflict => conflict.ObservedLinkId).ToArray());
            var report = TopologyQualityProjection.Build(diagnostics, snapshot.MapSnapshot, first);
            var conflictItem = report.Items.Single(item => item.Reasons.Any(reason =>
                reason.Kind == TopologyQualityGapKind.ManualObservedConflict));
            Assert.AreEqual(Sprint49TopologyConflictFixture.ManualId, conflictItem.PhysicalLinkId);
            Assert.AreEqual(2, conflictItem.Reasons.Count(reason => reason.Kind == TopologyQualityGapKind.ManualObservedConflict));
            Assert.AreEqual(2, report.Count);
            var acknowledged = first.Select(conflict => new TopologyConflictKey(conflict.ManualLinkId, conflict.ObservedLinkId)).ToArray();
            Assert.IsFalse(TopologyQualityProjection.Build(diagnostics, snapshot.MapSnapshot,
                TopologyConflictProjection.Build(diagnostics, acknowledged)).Items
                .SelectMany(item => item.Reasons).Any(reason => reason.Kind == TopologyQualityGapKind.ManualObservedConflict));
        }
    }
}
