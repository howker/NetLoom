using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
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
        private static readonly Guid OtherPortA = Guid.Parse("49494949-0003-0002-0000-000000000004");

        internal static TopologyRefreshSnapshot Snapshot(
            DiagnosticLinkStrength strength = DiagnosticLinkStrength.Observed,
            DiagnosticLldpReporting reporting = DiagnosticLldpReporting.OnlySideA,
            bool synthetic = true, bool manualDevice = false, bool secondLink = false,
            DiagnosticLinkStrength secondStrength = DiagnosticLinkStrength.Confirmed,
            bool secondSyntheticPort = false)
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
                    DiagnosticDegradationStatus.Unknown, null, new DiagnosticDegradationReason[0]) }
                    .Concat(index == 0 && secondSyntheticPort
                        ? new[] { new InterfaceDiagnostic(OtherPortA, id, null, "Gi0/4",
                            null, "up", "up", 1000000000L, Now, StpTreePortState.Unknown,
                            DiagnosticDegradationStatus.Unknown, null, new DiagnosticDegradationReason[0]) }
                        : new InterfaceDiagnostic[0]))).ToArray();
            var diagnostics = new[]
            {
                Link(LinkAB, DeviceB, PortB, names[1], "Gi0/2", strength, reporting),
                Link(LinkAC, DeviceC, PortC, names[2], "Gi0/3", secondStrength,
                    DiagnosticLldpReporting.BothSides, secondSyntheticPort ? OtherPortA : PortA,
                    secondSyntheticPort ? "Gi0/4" : "Gi0/1")
            }.Take(secondLink ? 2 : 1).ToArray();
            var links = diagnostics.Select(link => new MapLink(link.PhysicalLinkId.ToString("D"),
                DeviceA.ToString("D"), link.DeviceBId.ToString("D"), link.InterfaceAName, link.InterfaceBName,
                MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0], link.PhysicalLinkId));
            return new TopologyRefreshSnapshot(new MapSnapshot(Now, nodes, links),
                new TopologyAlertSnapshot(Now, "cist", new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(Now, devices, diagnostics));
        }

        private static PhysicalLinkDiagnostic Link(Guid linkId, Guid deviceB, Guid portB,
            string nameB, string portNameB, DiagnosticLinkStrength strength, DiagnosticLldpReporting reporting,
            Guid? portA = null, string portNameA = "Gi0/1")
        {
            return new PhysicalLinkDiagnostic(linkId, DeviceA, deviceB, portA ?? PortA, portB,
                "quality-sw-a", nameB, portNameA, portNameB, strength, MapFreshness.Fresh,
                "Ethernet", 1000000000L, null, Now, null, StpTreePortState.Unknown,
                StpTreePortState.Unknown, new DiagnosticEvidenceItem[0], false, 0, 0, 0, reporting);
        }
    }

    [TestClass]
    public sealed class Sprint49TopologyQualityProjectionTests
    {
        [TestMethod]
        [DataRow(DiagnosticLinkStrength.Observed, TopologyQualityGapKind.ObservedLink, "MapQualityObservedReason")]
        [DataRow(DiagnosticLinkStrength.Inferred, TopologyQualityGapKind.InferredLink, "MapQualityInferredReason")]
        public void UnconfirmedStrengthProducesLinkReason(DiagnosticLinkStrength strength,
            TopologyQualityGapKind kind, string resourceKey)
        {
            var snapshot = Sprint49TopologyQualityFixture.Snapshot(strength,
                DiagnosticLldpReporting.BothSides, synthetic: false);
            var report = Build(snapshot);
            Assert.AreEqual(1, report.Count);
            var item = report.Items.Single();
            Assert.AreEqual(Sprint49TopologyQualityFixture.LinkAB, item.PhysicalLinkId);
            Assert.AreEqual("quality-sw-a ↔ quality-sw-b", item.Subject);
            Assert.AreEqual(kind, item.Reasons.Single().Kind);
            Assert.AreEqual(UiText.Get(resourceKey), item.Reasons.Single().Text);
        }

        [TestMethod]
        public void ObservedAndOneSidedLldpAreTwoReasonsForOneLink()
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(synthetic: false));
            Assert.AreEqual(1, report.Count);
            var item = report.Items.Single();
            Assert.AreEqual(Sprint49TopologyQualityFixture.LinkAB, item.PhysicalLinkId);
            CollectionAssert.AreEqual(new[] { TopologyQualityGapKind.ObservedLink, TopologyQualityGapKind.OneSidedLldp },
                item.Reasons.Select(reason => reason.Kind).ToArray());
            Assert.AreEqual(UiText.Get("MapQualityObservedReason"), item.Reasons[0].Text);
            Assert.AreEqual(UiText.Format("MapQualityLldpReporter", "quality-sw-a"), item.Reasons[1].Text);
        }

        [TestMethod]
        [DataRow(DiagnosticLldpReporting.OnlySideA, "quality-sw-a")]
        [DataRow(DiagnosticLldpReporting.OnlySideB, "quality-sw-b")]
        public void OneSidedLldpNamesReportingDevice(DiagnosticLldpReporting reporting, string reporter)
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, reporting, synthetic: false));
            Assert.AreEqual(1, report.Count);
            var reason = report.Items.Single().Reasons.Single();
            Assert.AreEqual(TopologyQualityGapKind.OneSidedLldp, reason.Kind);
            Assert.AreEqual(UiText.Format("MapQualityLldpReporter", reporter), reason.Text);
        }

        [TestMethod]
        public void SyntheticEndpointSelectsDeviceAndNamesPort()
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, DiagnosticLldpReporting.BothSides));
            Assert.AreEqual(1, report.Count);
            var item = report.Items.Single();
            Assert.AreEqual(Sprint49TopologyQualityFixture.DeviceA, item.DeviceId);
            Assert.IsNull(item.PhysicalLinkId);
            Assert.AreEqual("quality-sw-a", item.Subject);
            Assert.AreEqual(TopologyQualityGapKind.SyntheticInterface, item.Reasons.Single().Kind);
            Assert.AreEqual(UiText.Format("MapQualitySyntheticPort", "Gi0/1"), item.Reasons.Single().Text);
        }

        [TestMethod]
        public void TwoSyntheticPortsAreTwoReasonsForOneDevice()
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(
                DiagnosticLinkStrength.Confirmed, DiagnosticLldpReporting.BothSides,
                secondLink: true, secondSyntheticPort: true));
            Assert.AreEqual(1, report.Count);
            var item = report.Items.Single();
            Assert.AreEqual(Sprint49TopologyQualityFixture.DeviceA, item.DeviceId);
            Assert.AreEqual(2, item.Reasons.Count);
            Assert.IsTrue(item.Reasons.All(reason => reason.Kind == TopologyQualityGapKind.SyntheticInterface));
            CollectionAssert.AreEquivalent(new[] { UiText.Format("MapQualitySyntheticPort", "Gi0/1"),
                UiText.Format("MapQualitySyntheticPort", "Gi0/4") }, item.Reasons.Select(reason => reason.Text).ToArray());
        }

        [TestMethod]
        [DataRow(DiagnosticLinkStrength.Confirmed)]
        [DataRow(DiagnosticLinkStrength.Manual)]
        public void ConfirmedAndManualStrengthsAreNotGaps(DiagnosticLinkStrength strength)
        {
            var report = Build(Sprint49TopologyQualityFixture.Snapshot(strength,
                DiagnosticLldpReporting.BothSides, synthetic: false));
            Assert.AreEqual(0, report.Count);
            Assert.AreEqual(0, report.Items.Count);
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
            Assert.AreEqual(TopologyQualityGapKind.SyntheticInterface, report.Items.Single().Reasons.Single().Kind);
        }

        [TestMethod]
        public void IndependentObjectsAreCountedAndSortedByReasonCountThenSubject()
        {
            var snapshot = Sprint49TopologyQualityFixture.Snapshot(secondLink: true,
                secondStrength: DiagnosticLinkStrength.Observed);
            var reversed = new NetworkDiagnosticSnapshot(snapshot.DiagnosticSnapshot.GeneratedUtc,
                snapshot.DiagnosticSnapshot.Devices, snapshot.DiagnosticSnapshot.Links.Reverse());
            var report = TopologyQualityProjection.Build(reversed, snapshot.MapSnapshot);
            Assert.AreEqual(3, report.Count);
            Assert.AreEqual(Sprint49TopologyQualityFixture.LinkAB, report.Items[0].PhysicalLinkId);
            CollectionAssert.AreEqual(new[] { 2, 1, 1 }, report.Items.Select(item => item.Reasons.Count).ToArray());
            CollectionAssert.AreEqual(new[] { "quality-sw-a", "quality-sw-a ↔ quality-sw-c" },
                report.Items.Skip(1).Select(item => item.Subject).ToArray());
            CollectionAssert.AreEqual(Build(snapshot).Items.Select(item => item.Subject).ToArray(),
                report.Items.Select(item => item.Subject).ToArray());
        }

        [TestMethod]
        [DataRow("en-US", "Alpha", "Ångström", "Zulu")]
        [DataRow("sv-SE", "Alpha", "Zulu", "Ångström")]
        public void EqualReasonCountsUseCurrentCultureForSubjects(string cultureName,
            string first, string second, string third)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var subjects = new[] { "Zulu", "Ångström", "Alpha" };
                var report = new TopologyQualityReport(subjects.Select(subject =>
                    new TopologyQualityItem(subject, Guid.NewGuid(), null,
                        new[] { new TopologyQualityReason(TopologyQualityGapKind.ObservedLink, "LLDP") })));
                CollectionAssert.AreEqual(new[] { first, second, third }, report.Items.Select(item => item.Subject).ToArray());
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void LocationPairNamesOtherFrameAndPreservesNavigationTargets(bool childOutsideParent)
        {
            var first = Guid.Parse("49494949-0003-0003-0000-000000000001");
            var second = Guid.Parse("49494949-0003-0003-0000-000000000002");
            var overlaps = LocationOverlapProjection.Build(new[]
            {
                new LocationOverlapFrame(first, childOutsideParent ? (Guid?)second : null,
                    "А", new Rect(0, 0, 100, 100)),
                new LocationOverlapFrame(second, null, "Б", new Rect(20, 20, 100, 100))
            });
            var report = TopologyQualityProjection.Build(null, null, overlaps: overlaps);
            Assert.AreEqual(1, report.Count);
            var item = report.Items.Single();
            Assert.AreEqual("А", item.Subject);
            Assert.AreEqual(first, item.LocationId);
            Assert.AreEqual(second, item.OtherLocationId);
            Assert.IsNull(item.PhysicalLinkId);
            Assert.IsNull(item.DeviceId);
            Assert.AreEqual(TopologyQualityGapKind.LocationOverlap, item.Reasons.Single().Kind);
            Assert.AreEqual(UiText.Format(childOutsideParent ? "MapQualityLocationOutsideParentReason" :
                "MapQualityLocationOverlapReason", "Б"), item.Reasons.Single().Text);
        }

        [TestMethod]
        public void MultipleConflictsUseOneManualLinkWithBothEndpointReasons()
        {
            var snapshot = Sprint49TopologyConflictFixture.Snapshot(true);
            var report = Build(snapshot);
            Assert.AreEqual(2, report.Count);
            var item = report.Items.Single(candidate =>
                candidate.PhysicalLinkId == Sprint49TopologyConflictFixture.ManualId);
            Assert.AreEqual(item, report.Items[0]);
            Assert.AreEqual("conflict-sw-a ↔ conflict-sw-b", item.Subject);
            Assert.AreEqual(2, item.Reasons.Count);
            Assert.IsTrue(item.Reasons.All(reason => reason.Kind == TopologyQualityGapKind.ManualObservedConflict));
            var manual = UiText.Format("TopologyConflictEndpoints", "conflict-sw-a", "Gi0/1", "conflict-sw-b", "Gi0/2");
            CollectionAssert.AreEquivalent(new[]
            {
                UiText.Format("TopologyConflictQualityReason", manual,
                    UiText.Format("TopologyConflictEndpoints", "conflict-sw-a", "Gi0/1", "conflict-sw-b", "Gi0/7")),
                UiText.Format("TopologyConflictQualityReason", manual,
                    UiText.Format("TopologyConflictEndpoints", "conflict-sw-a", "Gi0/1", "conflict-sw-c", "Gi0/5"))
            }, item.Reasons.Select(reason => reason.Text).ToArray());
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
