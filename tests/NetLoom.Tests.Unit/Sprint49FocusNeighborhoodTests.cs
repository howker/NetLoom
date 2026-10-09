using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    internal static class Sprint49NeighborhoodFixture
    {
        internal static readonly Guid[] Devices = Enumerable.Range(1, 8)
            .Select(i => Guid.Parse("49494949-0008-0000-0000-" + i.ToString("D12"))).ToArray();
        internal static readonly Guid Root = Guid.Parse("49494949-0008-0001-0000-000000000001");
        internal static readonly Guid Child = Guid.Parse("49494949-0008-0001-0000-000000000002");
        internal static readonly Guid Empty = Guid.Parse("49494949-0008-0001-0000-000000000003");

        // Локально администрируемый MAC машины Engine (второй бит первого октета установлен).
        internal const string PollingMac = "02:00:5E:10:49:01";

        internal static TopologyRefreshSnapshot Snapshot(bool reverse = false, bool withAlert = false)
        {
            var now = DateTime.UtcNow;
            var nodes = Devices.Select((id, i) => new MapNode(id.ToString("D"), "neighbor-sw-" + i,
                null, 100 + i * 450, 100, i < 6 ? Child : Empty,
                MapNodeOrigin.Automatic, MapMonitoringCapability.Unknown, MapNodeCategory.Switch, id)).ToArray();
            var links = new List<MapLink>();
            var diagnostics = new List<PhysicalLinkDiagnostic>();
            // Цепочка 0–1–…–7 плюс связь 1–3 (индекс 7): устройства 2 и 3 равноудалены от точки опроса
            // (устройство 0), поэтому связь между ними не имеет направления.
            var pairs = Enumerable.Range(0, Devices.Length - 1).Select(i => new[] { i, i + 1 })
                .Concat(new[] { new[] { 1, 3 } }).ToArray();
            foreach (var pair in pairs)
            {
                var id = Guid.NewGuid();
                var a = pair[0];
                var b = pair[1];
                links.Add(new MapLink(id.ToString("D"), Devices[reverse ? b : a].ToString("D"),
                    Devices[reverse ? a : b].ToString("D"), "Gi0/1", "Gi0/2",
                    MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0], id));
                diagnostics.Add(new PhysicalLinkDiagnostic(id, Devices[a], Devices[b], null, null,
                    nodes[a].Label, nodes[b].Label, "Gi0/1", "Gi0/2", DiagnosticLinkStrength.Confirmed,
                    MapFreshness.Fresh, null, null, "STP", now, now, StpTreePortState.Forwarding,
                    StpTreePortState.Forwarding, new DiagnosticEvidenceItem[0], false, 0, 0, 0));
            }
            var alerts = withAlert ? new[]
            {
                new TopologyAlert("sprint49-wide-participants", TopologyAlertKind.RingProtectionDegraded,
                    TopologyAlertSeverity.Warning, "cist", new[] { "sprint49-region" },
                    new[] { links[0].PhysicalLinkId.Value, links[6].PhysicalLinkId.Value },
                    new[] { TopologyAlertReason.DisabledRingLink })
            } : new TopologyAlert[0];
            return new TopologyRefreshSnapshot(new MapSnapshot(now, nodes, links, new[]
            {
                new MapLocation(Root, null, "Site", null),
                new MapLocation(Child, Root, "Distribution", null),
                new MapLocation(Empty, null, "Other building", null)
            }), new TopologyAlertSnapshot(now, "cist", alerts),
                // Карта и диагностика — из одного набора: у каждого узла карты есть диагностика устройства,
                // Иначе выбор устройства снимается как «выбранный объект пропал».
                new NetworkDiagnosticSnapshot(now, nodes.Select(node => new DeviceDiagnostic(node.DeviceId.Value,
                    node.Label, null, null, now, now, new InterfaceDiagnostic[0],
                    "192.0.2." + (10 + Array.IndexOf(Devices, node.DeviceId.Value)))).ToArray(), diagnostics));
        }
    }

    // Адреса машины Engine для тестов и галереи (ADR-085).
    internal sealed class Sprint49FixedHostAddresses : IEngineHostAddresses
    {
        private readonly string[] _macs;

        internal Sprint49FixedHostAddresses(params string[] macs)
        {
            _macs = macs;
        }

        public IReadOnlyList<string> GetMacAddresses() => _macs;
    }

    // Поиск по MAC, который видит заданный MAC на порту доступа одного устройства (порт не входит в связи снимка).
    internal sealed class Sprint49PollingPointLookupReader : IMacIpLookupReader
    {
        private readonly string _mac;
        private readonly Guid _deviceId;

        internal Sprint49PollingPointLookupReader(string mac, Guid deviceId)
        {
            _mac = mac;
            _deviceId = deviceId;
        }

        public MacIpLookupResult FindByMac(string macAddress, int maxCandidates)
        {
            var found = string.Equals(macAddress, _mac, StringComparison.OrdinalIgnoreCase);
            var candidates = found
                ? new[]
                {
                    new MacIpLookupCandidate("192.0.2.49", _mac, _deviceId, Guid.NewGuid(), 1, 1,
                        Guid.NewGuid(), DateTime.UtcNow, "192.0.2.1", null, null, null,
                        MacIpLookupCandidateStatus.ResolvedInterface)
                }
                : new MacIpLookupCandidate[0];
            return new MacIpLookupResult(MacIpLookupKind.Mac, macAddress, candidates);
        }

        public MacIpLookupResult FindByIp(string ipAddress, int maxCandidates) =>
            new MacIpLookupResult(MacIpLookupKind.Ip, ipAddress, new MacIpLookupCandidate[0]);
    }

    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        public void NeighborhoodWithUndeterminedPollingPointDisablesDirectionsWithReasonAndExpandsOtherLinks()
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = Sprint49NeighborhoodFixture.Snapshot();
                var ids = Sprint49NeighborhoodFixture.Devices;
                // MAC машины не виден ни на одном порту: точка опроса не найдена.
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader())
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null && window.PollingPoint != null);
                    Assert.AreEqual(EnginePollingPointStatus.NotFound, window.PollingPoint.Status);
                    SelectDevice(window, ids[2]);
                    NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 1, 2, 3);
                    var up = (Button)window.FindName("MapNeighborhoodUpButton");
                    var down = (Button)window.FindName("MapNeighborhoodDownButton");
                    Assert.IsFalse(up.IsEnabled);
                    Assert.IsFalse(down.IsEnabled);
                    var upReason = (TextBlock)window.FindName("MapNeighborhoodUpReason");
                    var downReason = (TextBlock)window.FindName("MapNeighborhoodDownReason");
                    Assert.IsTrue(upReason.IsVisible && downReason.IsVisible);
                    Assert.AreEqual(UiText.Get("MapNeighborhoodPollingNotFound"), upReason.Text);
                    Assert.AreEqual(UiText.Get("MapNeighborhoodPollingNotFound"), downReason.Text);
                    Click((Button)window.FindName("MapNeighborhoodOtherButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3, 4);
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NeighborhoodMenuHidesOtherDevicesAndExpandsByDistanceToPollingPoint(bool reverse)
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = Sprint49NeighborhoodFixture.Snapshot(reverse);
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(snapshot),
                    new Sprint49PollingPointLookupReader(Sprint49NeighborhoodFixture.PollingMac, ids[0]))
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null && window.PollingPoint != null);
                    Assert.AreEqual(EnginePollingPointStatus.Determined, window.PollingPoint.Status);
                    Assert.AreEqual(ids[0], window.PollingPoint.DeviceId);
                    SelectDevice(window, ids[4]);
                    NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 3, 4, 5);
                    Assert.AreEqual(UiText.Format("MapNeighborhoodSummary", "neighbor-sw-4", 3, 8),
                        ((TextBlock)window.FindName("MapNeighborhoodSummaryText")).Text);
                    Assert.AreEqual(UiText.Get("MapNeighborhoodShow"),
                        ((Button)window.FindName("MapOperationalFocusButton")).Content);
                    Assert.IsTrue(((Border)window.FindName("MapNeighborhoodNotice")).IsVisible);
                    var canvas = (Canvas)window.FindName("MapCanvas");
                    Assert.AreEqual(2, canvas.Children.OfType<Line>().Count(line => line.Visibility == Visibility.Visible));
                    Assert.AreEqual(Visibility.Collapsed, canvas.Children.OfType<Border>()
                        .Single(border => Equals(border.Tag, Sprint49NeighborhoodFixture.Empty)).Visibility);
                    Assert.AreEqual(Visibility.Visible, canvas.Children.OfType<Border>()
                        .Single(border => Equals(border.Tag, Sprint49NeighborhoodFixture.Root)).Visibility);

                    // Вверх — на одну связь ближе к точке опроса (устройство 0); равноудалённые 2 и 3 — без направления.
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodUpButton")).IsEnabled);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodDownButton")).IsEnabled);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodOtherButton")).IsVisible);
                    Click((Button)window.FindName("MapNeighborhoodUpButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 1, 3, 4, 5);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodUpButton")).IsEnabled);
                    Click((Button)window.FindName("MapNeighborhoodUpButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 3, 4, 5);
                    Assert.IsFalse(((Button)window.FindName("MapNeighborhoodUpButton")).IsEnabled);
                    Assert.IsTrue(((TextBlock)window.FindName("MapNeighborhoodUpReason")).IsVisible);
                    Assert.AreEqual(UiText.Get("MapNeighborhoodNoMoreUp"),
                        ((TextBlock)window.FindName("MapNeighborhoodUpReason")).Text);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodDownButton")).IsKeyboardFocused);
                    Click((Button)window.FindName("MapNeighborhoodOtherButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3, 4, 5);
                    Assert.IsFalse(((Button)window.FindName("MapNeighborhoodOtherButton")).IsVisible);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodWholeSiteButton")).IsKeyboardFocused);

                    window.ShowMap(snapshot.MapSnapshot);
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3, 4, 5);
                    var zoom = NeighborhoodZoom(window);
                    Assert.IsTrue(zoom >= (double)window.FindResource("NetLoom.Map.ZoomMin") - 0.0001 && zoom <= 1.0);
                    // Окрестность видна целиком: каждое её устройство в видимой области карты.
                    var mapViewer = (ScrollViewer)window.FindName("MapScrollViewer");
                    var mapViewport = new Rect(0, 0, mapViewer.ViewportWidth, mapViewer.ViewportHeight);
                    foreach (var index in new[] { 0, 1, 2, 3, 4, 5 })
                    {
                        var border = DeviceBorder(window, ids[index]);
                        Assert.IsTrue(mapViewport.Contains(border.TransformToAncestor(mapViewer)
                            .TransformBounds(new Rect(border.RenderSize))), "Neighborhood device outside the viewport.");
                    }

                    Click((Button)window.FindName("MapNeighborhoodDownButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3, 4, 5, 6);
                    Click((Button)window.FindName("MapNeighborhoodDownButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, Enumerable.Range(0, 8).ToArray());
                    Assert.IsFalse(((Button)window.FindName("MapNeighborhoodDownButton")).IsEnabled);
                    Assert.AreEqual(UiText.Get("MapNeighborhoodNoMoreDown"),
                        ((TextBlock)window.FindName("MapNeighborhoodDownReason")).Text);

                    Click((Button)window.FindName("MapNeighborhoodWholeSiteButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, Enumerable.Range(0, 8).ToArray());
                    Assert.IsFalse(((Border)window.FindName("MapNeighborhoodNotice")).IsVisible);
                    Assert.IsTrue(NeighborhoodZoom(window) < (double)window.FindResource("NetLoom.Map.ReadableZoomMin"));
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void NeighborhoodWithoutSelectionIsDisabledWithVisibleReasonAndModesAreExclusive()
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(Sprint49NeighborhoodFixture.Snapshot()),
                    new EmptyLookupReader());
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[2]) != null);
                    var item = NeighborhoodMenu(window);
                    Assert.IsFalse(item.IsEnabled);
                    var reason = ((StackPanel)item.Header).Children.OfType<TextBlock>().Last();
                    Assert.AreEqual(UiText.Get("MapNeighborhoodSelectDevice"), reason.Text);
                    Assert.AreEqual(Visibility.Visible, reason.Visibility);
                    ((Button)window.FindName("MapOperationalFocusButton")).ContextMenu.IsOpen = false;
                    SelectDevice(window, ids[2]);
                    var problems = NeighborhoodFocusMenu(window).Items.OfType<MenuItem>()
                        .Single(menu => Equals(menu.Header, UiText.Get("MapOperationalFocusAllProblems")));
                    problems.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    problems = NeighborhoodFocusMenu(window).Items.OfType<MenuItem>()
                        .Single(menu => Equals(menu.Header, UiText.Get("MapOperationalFocusAllProblems")));
                    Assert.IsFalse(problems.IsChecked);
                    AssertNeighborhoodDevices(window, 1, 2, 3);
                    problems = NeighborhoodFocusMenu(window).Items.OfType<MenuItem>()
                        .Single(menu => Equals(menu.Header, UiText.Get("MapOperationalFocusAllProblems")));
                    problems.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.IsTrue(problems.IsChecked);
                    Assert.IsFalse(((Border)window.FindName("MapNeighborhoodNotice")).IsVisible);
                    AssertNeighborhoodDevices(window, Enumerable.Range(0, 8).ToArray());
                    NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Click((Button)window.FindName("MapFitAllButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, Enumerable.Range(0, 8).ToArray());
                    Assert.IsFalse(((Border)window.FindName("MapNeighborhoodNotice")).IsVisible);
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void NeighborhoodDisappearingAnchorDisablesModeAndStartupFitsTheWholeSite()
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = Sprint49NeighborhoodFixture.Snapshot();
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader());
                try
                {
                    window.Show();
                    // Стартовый вид — вся площадка: ждём, пока последний узел окажется в видимой области.
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null &&
                        new Rect(0, 0, ((ScrollViewer)window.FindName("MapScrollViewer")).ViewportWidth,
                            ((ScrollViewer)window.FindName("MapScrollViewer")).ViewportHeight).Contains(
                            DeviceBorder(window, ids[7]).TransformToAncestor((ScrollViewer)window.FindName("MapScrollViewer"))
                                .TransformBounds(new Rect(DeviceBorder(window, ids[7]).RenderSize))));
                    PumpDispatcher();
                    var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                    var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                    foreach (var id in ids)
                    {
                        var border = DeviceBorder(window, id);
                        var bounds = border.TransformToAncestor(viewer).TransformBounds(new Rect(border.RenderSize));
                        Assert.IsTrue(viewport.Contains(bounds), "Every device must fit in the initial viewport.");
                    }
                    SelectDevice(window, ids[2]);
                    NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    window.ShowMap(new MapSnapshot(DateTime.UtcNow,
                        snapshot.MapSnapshot.Nodes.Where(node => node.DeviceId != ids[2]),
                        snapshot.MapSnapshot.Links.Where(link => link.SourceNodeKey != ids[2].ToString("D") &&
                            link.TargetNodeKey != ids[2].ToString("D")), snapshot.MapSnapshot.Locations));
                    Assert.IsFalse(((Border)window.FindName("MapNeighborhoodNotice")).IsVisible);
                    // Удалённая карточка исчезает с анимацией — дождаться её снятия с холста.
                    WaitForCondition(() => DeviceBorder(window, ids[2]) == null);
                    AssertNeighborhoodDevices(window, 0, 1, 3, 4, 5, 6, 7);
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void AlertParticipantsFitBelowReadableZoomAndLeavingAlertsRestoresWorkingView(bool neighborhood)
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = Sprint49NeighborhoodFixture.Snapshot(withAlert: true);
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader());
                try
                {
                    window.Show();
                    WaitForCondition(() => ((ItemsControl)window.FindName("AlertList")).Items.Count == 1);
                    PumpDispatcher();
                    if (neighborhood)
                    {
                        SelectDevice(window, ids[2]);
                        NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        PumpDispatcher();
                    }
                    for (var i = 0; i < 8; i++) Click((Button)window.FindName("MapZoomInButton"));
                    PumpDispatcher();
                    var previousZoom = NeighborhoodZoom(window);
                    var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                    var previousX = viewer.HorizontalOffset;
                    var previousY = viewer.VerticalOffset;
                    Click((Button)window.FindName("ShellAlertsButton"));
                    WaitForCondition(() => NeighborhoodZoom(window) <
                        (double)window.FindResource("NetLoom.Map.ReadableZoomMin"));
                    PumpDispatcher();
                    var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                    foreach (var index in new[] { 0, 1, 6, 7 })
                    {
                        var border = DeviceBorder(window, ids[index]);
                        Assert.AreEqual(Visibility.Visible, border.Visibility);
                        var bounds = border.TransformToAncestor(viewer).TransformBounds(new Rect(border.RenderSize));
                        Assert.IsTrue(viewport.Contains(bounds), "All alert participants must fit in the viewport.");
                    }
                    Click((Button)window.FindName("ShellMapButton"));
                    PumpDispatcher();
                    Assert.AreEqual(previousZoom, NeighborhoodZoom(window), 0.001);
                    Assert.AreEqual(previousX, viewer.HorizontalOffset, 0.5);
                    Assert.AreEqual(previousY, viewer.VerticalOffset, 0.5);
                    if (neighborhood) AssertNeighborhoodDevices(window, 1, 2, 3);
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        private static ContextMenu NeighborhoodFocusMenu(MainWindow window)
        {
            var button = (Button)window.FindName("MapOperationalFocusButton");
            Click(button);
            PumpDispatcher();
            var menu = button.ContextMenu;
            menu.IsOpen = false;
            return menu;
        }

        private static MenuItem NeighborhoodMenu(MainWindow window)
        {
            return NeighborhoodFocusMenu(window).Items.OfType<MenuItem>()
                .Single(item => item.Header is StackPanel);
        }

        private static void AssertNeighborhoodDevices(MainWindow window, params int[] indices)
        {
            var visible = Sprint49NeighborhoodFixture.Devices.Where(id =>
                DeviceBorder(window, id)?.Visibility == Visibility.Visible).ToArray();
            CollectionAssert.AreEquivalent(indices.Select(i => Sprint49NeighborhoodFixture.Devices[i]).ToArray(), visible);
        }

        private static double NeighborhoodZoom(MainWindow window) =>
            (double)typeof(MainWindow).GetField("_zoom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
    }
}
