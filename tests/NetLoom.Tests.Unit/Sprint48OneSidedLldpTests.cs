using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations.Stp;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Diagnostics;
using NetLoom.Topology.Map;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint48OneSidedLldpTests
    {
        private static readonly DateTime Now =
            new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void OneSidedLldpReportsOnlyTheReportingSide()
        {
            var a = Guid.Parse("48484848-0000-0000-0000-000000000001");
            var b = Guid.Parse("48484848-0000-0000-0000-000000000002");

            Assert.AreEqual(
                DiagnosticLldpReporting.OnlySideA,
                ProjectLink(a, b, PhysicalLinkStrength.Observed,
                    "device:" + a.ToString("N") + "|port:1").LldpReporting);
            Assert.AreEqual(
                DiagnosticLldpReporting.OnlySideB,
                ProjectLink(a, b, PhysicalLinkStrength.Observed,
                    "device:" + b.ToString("N") + "|port:1").LldpReporting);
        }

        [TestMethod]
        public void ReciprocalLldpReportsBothSides()
        {
            var a = Guid.Parse("48484848-0000-0000-0000-000000000001");
            var b = Guid.Parse("48484848-0000-0000-0000-000000000002");

            Assert.AreEqual(
                DiagnosticLldpReporting.BothSides,
                ProjectLink(a, b, PhysicalLinkStrength.Confirmed,
                    "device:" + a.ToString("N") + "|port:1",
                    "device:" + b.ToString("N") + "|port:1").LldpReporting);
        }

        [TestMethod]
        public void EvidenceWithoutSideIsNotApplicable()
        {
            Assert.AreEqual(
                DiagnosticLldpReporting.NotApplicable,
                ProjectLink(Guid.NewGuid(), Guid.NewGuid(),
                    PhysicalLinkStrength.Observed, "port:1").LldpReporting);
        }

        private static PhysicalLinkDiagnostic ProjectLink(
            Guid a,
            Guid b,
            PhysicalLinkStrength strength,
            params string[] slots)
        {
            var linkId = Guid.NewGuid();
            var evidence = slots.Select(
                slot => new PhysicalLinkEvidence(
                    linkId,
                    PhysicalLinkEvidenceKind.Lldp,
                    PhysicalLinkEvidenceStrength.Strong,
                    "192.0.2.43",
                    slot,
                    Guid.NewGuid(),
                    Now,
                    "neighbor")).ToArray();
            var readSet = new MaterializedTopologyReadSet(
                new[] { Device(a, "Switch A"), Device(b, "Switch B") },
                new DeviceInterface[0],
                new[] { Link(linkId, a, b, strength) },
                evidence,
                new Location[0],
                new BoundStpObservation[0],
                new InterfaceDegradationState[0]);
            var map = new MaterializedTopologyMapProjector().Project(
                readSet.Devices,
                readSet.Interfaces,
                readSet.PhysicalLinks,
                readSet.PhysicalLinkEvidence,
                readSet.Locations,
                Now);

            return new MaterializedTopologyDiagnosticSnapshotProjector()
                .Project(readSet, map, "cist").Links.Single();
        }

        private static TopologyDevice Device(Guid id, string name)
        {
            return new TopologyDevice(
                id, null, name, DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic, MonitoringCapability.Unknown,
                null, null, null, false, false,
                Now.AddHours(-1), Now, Now, name, null);
        }

        private static PhysicalLink Link(
            Guid id, Guid a, Guid b, PhysicalLinkStrength strength)
        {
            return new PhysicalLink(
                id, a, null, b, null, strength, PhysicalLinkFreshness.Fresh,
                null, 1000000000L, "LLDP", Now.AddHours(-1), Now,
                strength == PhysicalLinkStrength.Confirmed ? Now : (DateTime?)null,
                "sprint48", false, false, null);
        }

        internal static TopologyRefreshSnapshot OneSidedSnapshot(
            string nameA,
            string nameB)
        {
            var now = DateTime.UtcNow;
            var observedUtc = now.AddMinutes(-3);
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var interfaceA = Guid.NewGuid();
            var interfaceB = Guid.NewGuid();
            var linkId = Guid.NewGuid();
            var observationId = Guid.NewGuid();
            var keyA = a.ToString("D");
            var keyB = b.ToString("D");
            const string detail = "lldpRemTable · порт Gi0/24";

            var portA = new InterfaceDiagnostic(
                interfaceA, a, 24, "Gi0/24", null, "up", "up", 1000000000L,
                observedUtc, StpTreePortState.Unknown,
                DiagnosticDegradationStatus.Unknown, null,
                new DiagnosticDegradationReason[0]);
            var portB = new InterfaceDiagnostic(
                interfaceB, b, 1, "Gi0/1", null, "up", "up", 1000000000L,
                observedUtc, StpTreePortState.Unknown,
                DiagnosticDegradationStatus.Unknown, null,
                new DiagnosticDegradationReason[0]);

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    now,
                    new[]
                    {
                        new MapNode(keyA, nameA, null, 100.0, 100.0, null,
                            MapNodeOrigin.Automatic, MapMonitoringCapability.Unknown,
                            MapNodeCategory.Switch, a, "192.0.2.43"),
                        new MapNode(keyB, nameB, null, 420.0, 100.0, null,
                            MapNodeOrigin.Automatic, MapMonitoringCapability.Unknown,
                            MapNodeCategory.Switch, b, "192.0.2.44")
                    },
                    new[]
                    {
                        new MapLink(linkId.ToString("D"), keyA, keyB,
                            "Gi0/24", "Gi0/1", MapConfidence.High, MapFreshness.Fresh,
                            new[]
                            {
                                new MapEvidenceItem(MapEvidenceKind.Lldp,
                                    MapEvidenceStrength.Strong, observationId,
                                    observedUtc, "192.0.2.43", detail)
                            }, linkId)
                    }),
                new TopologyAlertSnapshot(now, "cist", new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(
                    now,
                    new[]
                    {
                        new DeviceDiagnostic(a, nameA, null, null,
                            observedUtc, observedUtc, new[] { portA }, "192.0.2.43"),
                        new DeviceDiagnostic(b, nameB, null, null,
                            observedUtc, observedUtc, new[] { portB }, "192.0.2.44")
                    },
                    new[]
                    {
                        new PhysicalLinkDiagnostic(linkId, a, b, interfaceA, interfaceB,
                            nameA, nameB, "Gi0/24", "Gi0/1", DiagnosticLinkStrength.Observed,
                            MapFreshness.Fresh, "Ethernet", 1000000000L, "LLDP",
                            observedUtc, null, StpTreePortState.Unknown, StpTreePortState.Unknown,
                            new[]
                            {
                                new DiagnosticEvidenceItem(MapEvidenceKind.Lldp,
                                    MapEvidenceStrength.Strong, observationId,
                                    observedUtc, "192.0.2.43", detail,
                                    DiagnosticRawAvailability.Available)
                            }, false, 0, 0, 0L, DiagnosticLldpReporting.OnlySideA)
                    }));
        }
    }

    // Используем существующие STA-хелперы оболочки без копирования.
    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        public void OneSidedLinkShowsEvidenceGapInInspector()
        {
            RunOnSta(
                () =>
                {
                    var snapshot = Sprint48OneSidedLldpTests.OneSidedSnapshot(
                        "Switch A", "Switch B");
                    var window = new MainWindow(
                        new FixedRefreshProvider(snapshot), new EmptyLookupReader());

                    try
                    {
                        window.Show();
                        PumpDispatcher();
                        var showLink = typeof(MainWindow).GetMethod(
                            "ShowLinkDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic);
                        Assert.IsNotNull(showLink);
                        showLink.Invoke(window,
                            new object[] { snapshot.DiagnosticSnapshot.Links.Single() });
                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Format("DiagnosticLldpReportingOneSide", "Switch A"),
                            FindDiagnosticFieldValue(
                                (ItemsControl)window.FindName("DiagnosticFieldsList"),
                                UiText.Get("DiagnosticFieldLldpReporting")));
                        var evidence = (ItemsControl)window.FindName("DiagnosticTertiaryList");
                        Assert.AreEqual(
                            UiText.Format("DiagnosticEvidenceGapOneSidedLldp", "Switch A", "Switch B"),
                            DiagnosticRowString(evidence.Items[0], "Text"));
                        Assert.AreEqual(2, evidence.Items.Count);

                        // При отсутствии предупреждения остаётся «Нет проблем», пояснение и действие скрыты.
                        Assert.AreEqual(
                            UiText.Get("InspectorProblemNone"),
                            ((TextBlock)window.FindName("InspectorProblemText")).Text);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((TextBlock)window.FindName("InspectorProblemExplanationText")).Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((Button)window.FindName("InspectorPrimaryActionButton")).Visibility);
                    }
                    finally
                    {
                        window.Close();
                        PumpDispatcher();
                    }
                });
        }
    }
}
