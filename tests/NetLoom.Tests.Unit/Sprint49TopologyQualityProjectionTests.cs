using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    internal static class Sprint49TopologyQualityFixture
    {
        internal static readonly DateTime Now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        internal static readonly Guid DeviceA = Guid.Parse("49494949-0003-0000-0000-000000000001");
        internal static readonly Guid DeviceB = Guid.Parse("49494949-0003-0000-0000-000000000002");
        internal static readonly Guid DeviceC = Guid.Parse("49494949-0003-0000-0000-000000000003");
        internal static readonly Guid LinkAB = Guid.Parse("49494949-0003-0001-0000-000000000001");
        internal static readonly Guid LinkAC = Guid.Parse("49494949-0003-0001-0000-000000000002");
        private static readonly Guid PortA = Guid.Parse("49494949-0003-0002-0000-000000000001");
        private static readonly Guid PortB = Guid.Parse("49494949-0003-0002-0000-000000000002");
        private static readonly Guid PortC = Guid.Parse("49494949-0003-0002-0000-000000000003");

        internal static TopologyRefreshSnapshot Snapshot(
            DiagnosticLinkStrength strength = DiagnosticLinkStrength.Observed,
            DiagnosticLldpReporting reporting = DiagnosticLldpReporting.OnlySideA,
            bool synthetic = true, bool manualDevice = false, bool secondLink = false,
            DiagnosticLinkStrength secondStrength = DiagnosticLinkStrength.Confirmed)
        {
            var ids = new[] { DeviceA, DeviceB, DeviceC };
            var ports = new[] { PortA, PortB, PortC };
            var names = new[] { "quality-sw-a", "quality-sw-b", "quality-sw-c" };
            var nodes = ids.Select((id, index) => new MapNode(id.ToString("D"), names[index], null,
                100 + index * 300, 100, origin: index == 0 && manualDevice
                    ? MapNodeOrigin.Manual : MapNodeOrigin.Automatic,
                category: MapNodeCategory.Switch, deviceId: id)).ToArray();
            var devices = ids.Select((id, index) => new DeviceDiagnostic(id, names[index], null, null,
                Now, Now, new[] { new InterfaceDiagnostic(ports[index], id,
                    index == 0 && synthetic ? (int?)null : index + 1, "Gi0/" + (index + 1),
                    null, "up", "up", 1000000000L, Now, StpTreePortState.Unknown,
                    DiagnosticDegradationStatus.Unknown, null, new DiagnosticDegradationReason[0]) })).ToArray();
            var diagnostics = new[]
            {
                Link(LinkAB, DeviceB, PortB, names[1], "Gi0/2", strength, reporting),
                Link(LinkAC, DeviceC, PortC, names[2], "Gi0/3", secondStrength,
                    DiagnosticLldpReporting.BothSides)
            }.Take(secondLink ? 2 : 1).ToArray();
            var links = diagnostics.Select(link => new MapLink(link.PhysicalLinkId.ToString("D"),
                DeviceA.ToString("D"), link.DeviceBId.ToString("D"), "Gi0/1", link.InterfaceBName,
                MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0], link.PhysicalLinkId));
            return new TopologyRefreshSnapshot(new MapSnapshot(Now, nodes, links),
                new TopologyAlertSnapshot(Now, "cist", new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(Now, devices, diagnostics));
        }

        private static PhysicalLinkDiagnostic Link(Guid linkId, Guid deviceB, Guid portB,
            string nameB, string portNameB, DiagnosticLinkStrength strength, DiagnosticLldpReporting reporting)
        {
            return new PhysicalLinkDiagnostic(linkId, DeviceA, deviceB, PortA, portB,
                "quality-sw-a", nameB, "Gi0/1", portNameB, strength, MapFreshness.Fresh,
                "Ethernet", 1000000000L, null, Now, null, StpTreePortState.Unknown,
                StpTreePortState.Unknown, new DiagnosticEvidenceItem[0], false, 0, 0, 0, reporting);
        }
    }

    [TestClass]
    public sealed class Sprint49TopologyQualityProjectionTests
    {
        [TestMethod]
        [DataRow(DiagnosticLinkStrength.Observed, TopologyQualityGapKind.ObservedLink)]
        [DataRow(DiagnosticLinkStrength.Inferred, TopologyQualityGapKind.InferredLink)]
        public void UnconfirmedStrengthProducesLinkGap(DiagnosticLinkStrength strength, TopologyQualityGapKind kind)
        {
            var snapshot = Sprint49TopologyQualityFixture.Snapshot(strength,
                DiagnosticLldpReporting.BothSides, synthetic: false);
            var report = Build(snapshot);
            Assert.AreEqual(1, report.Count);
            Assert.AreEqual(1, report.Counts[kind]);
            var gap = report.Gaps.Single();
            Assert.AreEqual(kind, gap.Kind);
            Assert.AreEqual(Sprint49TopologyQualityFixture.LinkAB, gap.PhysicalLinkId);
            Assert.AreEqual("quality-sw-a ↔ quality-sw-b", gap.Subject);
            StringAssert.Contains(gap.Detail, "Gi0/1");
            StringAssert.Contains(gap.Detail, "Gi0/2");
        }

        [TestMethod]
        [DataRow(DiagnosticLldpReporting.OnlySideA, "quality-sw-a")]
        [DataRow(DiagnosticLldpReporting.OnlySideB, "quality-sw-b")]
        public void OneSidedLldpNamesReportingDevice(DiagnosticLldpReporting reporting, string reporter)
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, reporting, synthetic: false));
            Assert.AreEqual(1, report.Count);
            Assert.AreEqual(TopologyQualityGapKind.OneSidedLldp, report.Gaps.Single().Kind);
            StringAssert.Contains(report.Gaps.Single().Detail, reporter);
        }

        [TestMethod]
        public void SyntheticEndpointSelectsDeviceAndNamesPort()
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, DiagnosticLldpReporting.BothSides));
            Assert.AreEqual(1, report.Count);
            var gap = report.Gaps.Single();
            Assert.AreEqual(TopologyQualityGapKind.SyntheticInterface, gap.Kind);
            Assert.AreEqual(Sprint49TopologyQualityFixture.DeviceA, gap.DeviceId);
            Assert.IsNull(gap.PhysicalLinkId);
            StringAssert.Contains(gap.Detail, "Gi0/1");
            StringAssert.Contains(gap.Detail, "ifIndex");
        }

        [TestMethod]
        [DataRow(DiagnosticLinkStrength.Confirmed)]
        [DataRow(DiagnosticLinkStrength.Manual)]
        public void ConfirmedAndManualStrengthsAreNotGaps(DiagnosticLinkStrength strength)
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(strength,
                DiagnosticLldpReporting.BothSides, synthetic: false));
            Assert.AreEqual(0, report.Count);
            Assert.IsTrue(report.Counts.Values.All(count => count == 0));
        }

        [TestMethod]
        public void ManualDevicePortWithoutIfIndexIsNotGap()
        {
            Assert.AreEqual(0, Build(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Manual, DiagnosticLldpReporting.NotApplicable,
                manualDevice: true)).Count);
        }

        [TestMethod]
        public void SharedSyntheticPortIsCountedOnceAcrossLinks()
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, DiagnosticLldpReporting.BothSides, secondLink: true));
            Assert.AreEqual(1, report.Count);
            Assert.AreEqual(1, report.Counts[TopologyQualityGapKind.SyntheticInterface]);
        }

        [TestMethod]
        public void IndependentReasonsAreCountedAndSortedByKindThenSubject()
        {
            var snapshot = Sprint49TopologyQualityFixture.Snapshot(secondLink: true,
                secondStrength: DiagnosticLinkStrength.Observed);
            var reversed = new NetworkDiagnosticSnapshot(snapshot.DiagnosticSnapshot.GeneratedUtc,
                snapshot.DiagnosticSnapshot.Devices, snapshot.DiagnosticSnapshot.Links.Reverse());
            var report = TopologyQualityProjection.Build(reversed, snapshot.MapSnapshot);
            Assert.AreEqual(4, report.Count);
            Assert.AreEqual(2, report.Counts[TopologyQualityGapKind.ObservedLink]);
            Assert.AreEqual(0, report.Counts[TopologyQualityGapKind.InferredLink]);
            Assert.AreEqual(1, report.Counts[TopologyQualityGapKind.OneSidedLldp]);
            Assert.AreEqual(1, report.Counts[TopologyQualityGapKind.SyntheticInterface]);
            CollectionAssert.AreEqual(new[] { Sprint49TopologyQualityFixture.LinkAB, Sprint49TopologyQualityFixture.LinkAC },
                report.Gaps.Take(2).Select(gap => gap.PhysicalLinkId.Value).ToArray());
            CollectionAssert.AreEqual(new[] { TopologyQualityGapKind.ObservedLink, TopologyQualityGapKind.ObservedLink,
                TopologyQualityGapKind.OneSidedLldp, TopologyQualityGapKind.SyntheticInterface },
                report.Gaps.Select(gap => gap.Kind).ToArray());
        }

        [TestMethod]
        public void EmptyAndNullSnapshotsProduceEmptyReports()
        {
            var snapshot = Sprint49TopologyQualityFixture.Snapshot();
            var emptyMap = new MapSnapshot(Sprint49TopologyQualityFixture.Now, new MapNode[0], new MapLink[0]);
            var emptyDiagnostics = new NetworkDiagnosticSnapshot(Sprint49TopologyQualityFixture.Now,
                new DeviceDiagnostic[0], new PhysicalLinkDiagnostic[0]);
            Assert.AreEqual(0, TopologyQualityProjection.Build(null, null).Count);
            Assert.AreEqual(0, TopologyQualityProjection.Build(null, snapshot.MapSnapshot).Count);
            Assert.AreEqual(0, TopologyQualityProjection.Build(snapshot.DiagnosticSnapshot, null).Count);
            Assert.AreEqual(0, TopologyQualityProjection.Build(snapshot.DiagnosticSnapshot, emptyMap).Count);
            Assert.AreEqual(0, TopologyQualityProjection.Build(emptyDiagnostics, snapshot.MapSnapshot).Count);
        }

        private static TopologyQualityReport Build(TopologyRefreshSnapshot snapshot)
        {
            return TopologyQualityProjection.Build(snapshot.DiagnosticSnapshot, snapshot.MapSnapshot);
        }
    }
}
