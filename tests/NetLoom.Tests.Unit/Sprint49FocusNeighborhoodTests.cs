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
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    internal static class Sprint49NeighborhoodFixture
    {
        internal static readonly Guid[] Devices = Enumerable.Range(1, 8)
            .Select(i => Guid.Parse("49494949-0008-0000-0000-" + i.ToString("D12"))).ToArray();
        internal static readonly Guid Root = Guid.Parse("49494949-0008-0001-0000-000000000001");
        internal static readonly Guid Child = Guid.Parse("49494949-0008-0001-0000-000000000002");
        internal static readonly Guid Empty = Guid.Parse("49494949-0008-0001-0000-000000000003");

        internal static TopologyRefreshSnapshot Snapshot(bool reverse = false, bool withAlert = false)
        {
            var now = DateTime.UtcNow;
            var nodes = Devices.Select((id, i) => new MapNode(id.ToString("D"), "neighbor-sw-" + i,
                null, 100 + i * 450, 100, i < 6 ? Child : Empty,
                MapNodeOrigin.Automatic, MapMonitoringCapability.Unknown, MapNodeCategory.Switch, id)).ToArray();
            var links = new List<MapLink>();
            var diagnostics = new List<PhysicalLinkDiagnostic>();
            for (var i = 0; i < Devices.Length - 1; i++)
            {
                var id = Guid.NewGuid();
                var direction = i < 4 ? DiagnosticStpUplink.SideAIsUpstream : DiagnosticStpUplink.Unknown;
                links.Add(new MapLink(id.ToString("D"), Devices[reverse ? i + 1 : i].ToString("D"),
                    Devices[reverse ? i : i + 1].ToString("D"), "Gi0/1", "Gi0/2",
                    MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0], id));
                diagnostics.Add(new PhysicalLinkDiagnostic(id, Devices[i], Devices[i + 1], null, null,
                    nodes[i].Label, nodes[i + 1].Label, "Gi0/1", "Gi0/2", DiagnosticLinkStrength.Confirmed,
                    MapFreshness.Fresh, null, null, "STP", now, now, StpTreePortState.Forwarding,
                    StpTreePortState.Forwarding, new DiagnosticEvidenceItem[0], false, 0, 0, 0,
                    stpUplink: direction));
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
                new NetworkDiagnosticSnapshot(now, new DeviceDiagnostic[0], diagnostics));
        }
    }

    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NeighborhoodMenuHidesOtherDevicesAndExpandsStpAndUnknownSeparately(bool reverse)
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = Sprint49NeighborhoodFixture.Snapshot(reverse);
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader());
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null);
                    SelectDevice(window, ids[2]);
                    NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 1, 2, 3);
                    Assert.AreEqual(UiText.Format("MapNeighborhoodSummary", "neighbor-sw-2", 3, 8),
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

                    Click((Button)window.FindName("MapNeighborhoodUpButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3);
                    Assert.IsFalse(((Button)window.FindName("MapNeighborhoodUpButton")).IsEnabled);
                    Assert.IsTrue(((TextBlock)window.FindName("MapNeighborhoodUpReason")).IsVisible);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodDownButton")).IsKeyboardFocused);
                    Click((Button)window.FindName("MapNeighborhoodDownButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3, 4);
                    Assert.IsFalse(((Button)window.FindName("MapNeighborhoodDownButton")).IsEnabled);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodOtherButton")).IsKeyboardFocused);
                    Click((Button)window.FindName("MapNeighborhoodOtherButton"));
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3, 4, 5);
                    Assert.IsTrue(((Button)window.FindName("MapNeighborhoodOtherButton")).IsKeyboardFocused);

                    window.ShowMap(snapshot.MapSnapshot);
                    PumpDispatcher();
                    AssertNeighborhoodDevices(window, 0, 1, 2, 3, 4, 5);
                    var zoom = NeighborhoodZoom(window);
                    Assert.IsTrue(zoom >= (double)window.FindResource("NetLoom.Map.ReadableZoomMin") && zoom <= 1.0);

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
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null &&
                        NeighborhoodZoom(window) < (double)window.FindResource("NetLoom.Map.ReadableZoomMin"));
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
