using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    internal static class Sprint48DeviceConfirmationFixture
    {
        internal static TopologyRefreshSnapshot Snapshot(bool onlyUnconfirmed = false, string description = "MOXA EDS-518A")
        {
            var now = DateTime.UtcNow;
            var observedUtc = now.AddMinutes(-3);
            var ids = new[]
            {
                Guid.Parse("48484848-0005-0000-0000-000000000001"),
                Guid.Parse("48484848-0005-0000-0000-000000000002"),
                Guid.Parse("48484848-0005-0000-0000-000000000003"),
                Guid.Parse("48484848-0005-0000-0000-000000000007")
            };
            var names = new[] { "kb-sw-01", "kb-sw-02", "kb-sw-03", "kb-sw-07" };
            var addresses = new[] { "10.48.228.51", "10.48.228.52", "10.48.228.53", "10.48.228.57" };
            var nodes = new List<MapNode>();
            var devices = new List<DeviceDiagnostic>();
            var links = new List<MapLink>();
            var diagnostics = new List<PhysicalLinkDiagnostic>();
            var ports = ids.Select(id => new[]
            {
                new InterfaceDiagnostic(Guid.NewGuid(), id, 1, "Gi0/1", null,
                    "up", "up", 1000000000L, observedUtc, StpTreePortState.Unknown,
                    DiagnosticDegradationStatus.Unknown, null, new DiagnosticDegradationReason[0]),
                new InterfaceDiagnostic(Guid.NewGuid(), id, 24, "Gi0/24", null,
                    "up", "up", 1000000000L, observedUtc, StpTreePortState.Unknown,
                    DiagnosticDegradationStatus.Unknown, null, new DiagnosticDegradationReason[0])
            }).ToArray();

            for (var i = onlyUnconfirmed ? 3 : 0; i < ids.Length; i++)
            {
                nodes.Add(new MapNode(ids[i].ToString("D"), names[i], description,
                    100.0 + (i % 2) * 320.0, 100.0 + (i / 2) * 180.0, null,
                    MapNodeOrigin.Automatic, MapMonitoringCapability.Unknown,
                    MapNodeCategory.Switch, ids[i], addresses[i], isUnconfirmed: i == 3));
                devices.Add(new DeviceDiagnostic(ids[i], names[i], description, null,
                    observedUtc, observedUtc, ports[i], addresses[i]));
            }

            if (!onlyUnconfirmed)
            {
                for (var i = 0; i < 2; i++)
                {
                    var linkId = Guid.NewGuid();
                    var observationId = Guid.NewGuid();
                    const string detail = "lldpRemTable · порт Gi0/24";
                    links.Add(new MapLink(linkId.ToString("D"), ids[i].ToString("D"),
                        ids[i + 1].ToString("D"), "Gi0/24", "Gi0/1",
                        MapConfidence.High, MapFreshness.Fresh,
                        new[] { new MapEvidenceItem(MapEvidenceKind.Lldp, MapEvidenceStrength.Strong,
                            observationId, observedUtc, addresses[i], detail) }, linkId));
                    diagnostics.Add(new PhysicalLinkDiagnostic(linkId, ids[i], ids[i + 1],
                        ports[i][1].InterfaceId, ports[i + 1][0].InterfaceId,
                        names[i], names[i + 1], "Gi0/24", "Gi0/1", DiagnosticLinkStrength.Confirmed,
                        MapFreshness.Fresh, "Ethernet", 1000000000L, "LLDP",
                        observedUtc, observedUtc, StpTreePortState.Unknown, StpTreePortState.Unknown,
                        new[] { new DiagnosticEvidenceItem(MapEvidenceKind.Lldp, MapEvidenceStrength.Strong,
                            observationId, observedUtc, addresses[i], detail, DiagnosticRawAvailability.Available) },
                        false, 0, 0, 0L, DiagnosticLldpReporting.BothSides));
                }
            }

            return new TopologyRefreshSnapshot(
                new MapSnapshot(now, nodes, links),
                new TopologyAlertSnapshot(now, "cist", new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(now, devices, diagnostics));
        }
    }

    // Используем существующие STA-хелперы оболочки.
    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        [DataRow(null)]
        [DataRow("MOXA EDS-518A")]
        public void UnconfirmedDeviceShowsMarkOnMapEquipmentAndInspector(string description)
        {
            RunOnSta(() =>
            {
                var snapshot = Sprint48DeviceConfirmationFixture.Snapshot(true, description);
                var id = snapshot.MapSnapshot.Nodes.Single().DeviceId.Value;
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader());
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, id) != null);
                    var card = DeviceBorder(window, id);
                    var mark = ConfirmationTextBlocks(card)
                        .Single(item => item.Text == UiText.Get("DeviceUnconfirmedMark"));
                    Assert.AreEqual(Visibility.Visible, mark.Visibility);
                    Assert.AreEqual(UiText.Get("DeviceUnconfirmedHint"), mark.ToolTip);
                    window.UpdateLayout();
                    var bounds = mark.TransformToAncestor(card).TransformBounds(new Rect(mark.RenderSize));
                    Assert.IsTrue(bounds.Top >= 0 && bounds.Bottom <= card.ActualHeight,
                        "The confirmation line must fit inside the node card.");

                    SelectDevice(window, id);
                    PumpDispatcher();
                    var fields = (ItemsControl)window.FindName("DiagnosticFieldsList");
                    Assert.AreEqual(UiText.Get("DeviceUnconfirmedHint"),
                        FindDiagnosticFieldValue(fields, UiText.Get("DiagnosticFieldConfirmation")));
                    Assert.AreEqual(UiText.Get("DiagnosticFieldConfirmation"),
                        DiagnosticRowString(fields.Items[0], "Label"));

                    Click((Button)window.FindName("ShellEquipmentButton"));
                    var rows = (ItemsControl)window.FindName("EquipmentList");
                    Assert.AreEqual(1, rows.Items.Count);
                    // Отметка — строкой под именем: столбец описания на узкой ширине скрыт.
                    Assert.AreEqual(UiText.Get("DeviceUnconfirmedMark"),
                        DiagnosticRowString(rows.Items[0], "UnconfirmedText"));
                    Assert.AreEqual(description ?? UiText.Get("DiagnosticValueAbsent"),
                        DiagnosticRowString(rows.Items[0], "Description"));
                    window.UpdateLayout();
                    Assert.IsTrue(ConfirmationTextBlocks(rows)
                        .Any(text => text.Name == "EquipmentUnconfirmedText" &&
                            text.IsVisible &&
                            text.Text == UiText.Get("DeviceUnconfirmedMark")));
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            });
        }

        private static IEnumerable<TextBlock> ConfirmationTextBlocks(DependencyObject root)
        {
            if (root is TextBlock text)
            {
                yield return text;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                foreach (var item in ConfirmationTextBlocks(VisualTreeHelper.GetChild(root, i)))
                {
                    yield return item;
                }
            }
        }
    }

    // Повторно используем фейки обнаружения и мониторинга из существующих сценариев.
    public sealed partial class Sprint42WpfDiscoveryPanelTests
    {
        [TestMethod]
        [DataRow(DiscoveryControlState.Completed, true, 2)]
        [DataRow(DiscoveryControlState.Stopped, true, 2)]
        [DataRow(DiscoveryControlState.Completed, false, 1)]
        [DataRow(DiscoveryControlState.Faulted, true, 1)]
        public void DiscoveryRunThatAddsDevicesRestartsRunningMonitoringSet(
            DiscoveryControlState finishState, bool addsDevice, int expectedStarts)
        {
            RunOnSta(() =>
            {
                // В приложении события приходят через диспетчер; тест вызывает их напрямую, поэтому ставит его контекст.
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext());
                var full = Sprint48DeviceConfirmationFixture.Snapshot();
                var initial = new TopologyRefreshSnapshot(
                    new MapSnapshot(full.MapSnapshot.GeneratedUtc,
                        full.MapSnapshot.Nodes.Take(3).ToArray(), full.MapSnapshot.Links),
                    full.AlertSnapshot,
                    new NetworkDiagnosticSnapshot(full.DiagnosticSnapshot.GeneratedUtc,
                        full.DiagnosticSnapshot.Devices.Take(3).ToArray(), full.DiagnosticSnapshot.Links));
                var provider = new MutableRefreshProvider(initial);
                var discovery = new RecordingDiscoveryControl();
                var monitoring = new Sprint43WpfMultiTargetMonitoringTests.RecordingMultiTargetMonitoringControl();
                var profile = new AccessProfile(Guid.NewGuid(), "Площадка А", true, SnmpVersion.V2C, null);
                var materializer = new RecordingCandidateMaterializer(candidate => provider.SetSnapshot(full));
                var window = new MainWindow(provider, new EmptyLookupReader(), monitoring,
                    discovery, new[] { profile }, materializer);
                try
                {
                    window.Show();
                    WaitForCondition(() => ((ItemsControl)window.FindName("EquipmentList")).Items.Count == 3);
                    ((ComboBox)window.FindName("DiscoveryProfileComboBox")).SelectedIndex = 0;
                    Click((Button)window.FindName("MonitoringStartButton"));
                    WaitForCondition(() => monitoring.StartSetCalls.Count == 1);
                    Assert.AreEqual(3, monitoring.StartSetCalls[0].Count);

                    StartDiscoveryRunThroughTheWindow(window, "10.48.228.1", "10.48.228.254");
                    if (addsDevice)
                    {
                        discovery.EmitCandidate(new DiscoveryCandidateSnapshot(
                            IPAddress.Parse("10.48.228.57"), profile.Id, true, true,
                            new[] { 22 }, "kb-sw-07", "MOXA EDS-518A", null, null, 8));
                    }

                    // Завершаем сразу после кандидата, пока чтение карты ещё может выполняться.
                    discovery.PublishState(finishState, 254, 254, addsDevice ? 1 : 0);
                    if (expectedStarts == 2)
                    {
                        WaitForCondition(() => monitoring.StartSetCalls.Count == 2);
                        Assert.AreEqual(1, monitoring.StopCallCount);
                        Assert.AreEqual(4, monitoring.StartSetCalls[1].Count);
                        Assert.IsTrue(monitoring.StartSetCalls[1].Any(
                            target => target.TargetAddress.Equals(IPAddress.Parse("10.48.228.57"))));
                        discovery.PublishState(finishState, 254, 254, 1);
                        PumpDispatcher();
                        Assert.AreEqual(2, monitoring.StartSetCalls.Count);
                        Assert.AreEqual(1, monitoring.StopCallCount);
                    }
                    else
                    {
                        PumpDispatcher();
                        Assert.AreEqual(0, monitoring.StopCallCount);
                        Assert.AreEqual(1, monitoring.StartSetCalls.Count);
                    }
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
