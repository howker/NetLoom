using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.MapInteraction;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46ShellFoundationTests
    {
        private static TopologyRefreshSnapshot SinglePointsSnapshot()
        {
            var chain = FailureChainSnapshot();
            var source = Sprint49NeighborhoodFixture.Snapshot();
            var ids = Sprint49NeighborhoodFixture.Devices;
            var nodes = chain.MapSnapshot.Nodes.Concat(source.MapSnapshot.Nodes.Skip(4).Take(3)).ToArray();
            var devices = chain.DiagnosticSnapshot.Devices
                .Concat(source.DiagnosticSnapshot.Devices.Skip(4).Take(3)).ToArray();
            var mapLinks = chain.MapSnapshot.Links.Concat(source.MapSnapshot.Links.Skip(4).Take(2)).ToList();
            var diagnosticLinks = chain.DiagnosticSnapshot.Links
                .Concat(source.DiagnosticSnapshot.Links.Skip(4).Take(2)).ToList();
            var now = DateTime.UtcNow;
            foreach (var pair in new[] { new[] { 0, 4 }, new[] { 0, 6 }, new[] { 4, 6 } })
            {
                var id = Guid.NewGuid();
                mapLinks.Add(new MapLink(id.ToString("D"), ids[pair[0]].ToString("D"),
                    ids[pair[1]].ToString("D"), "Gi0/1", "Gi0/2", MapConfidence.High,
                    MapFreshness.Fresh, new MapEvidenceItem[0], id));
                diagnosticLinks.Add(new PhysicalLinkDiagnostic(id, ids[pair[0]], ids[pair[1]],
                    null, null, nodes.Single(node => node.DeviceId == ids[pair[0]]).Label,
                    nodes.Single(node => node.DeviceId == ids[pair[1]]).Label,
                    "Gi0/1", "Gi0/2", DiagnosticLinkStrength.Confirmed, MapFreshness.Fresh,
                    null, null, "STP", now, now,
                    pair[0] == 4 ? StpTreePortState.Blocking : StpTreePortState.Forwarding,
                    StpTreePortState.Forwarding, new DiagnosticEvidenceItem[0],
                    false, 0, 0, 0));
            }
            return new TopologyRefreshSnapshot(
                new MapSnapshot(now, nodes, mapLinks, chain.MapSnapshot.Locations),
                chain.AlertSnapshot, new NetworkDiagnosticSnapshot(now, devices, diagnosticLinks));
        }

        [TestMethod]
        public void SinglePointsModeHighlightsChainAndRestoresPreviousView()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var snapshot = SinglePointsSnapshot();
                var window = new MainWindow(new FixedRefreshProvider(snapshot),
                    new Sprint49PollingPointLookupReader(Sprint49NeighborhoodFixture.PollingMac, ids[0]))
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[6]) != null &&
                        window.PollingPoint?.Status == EnginePollingPointStatus.Determined);
                    SelectSinglePointsMode(window);
                    Assert.AreEqual("SinglePointsOfFailure", FailureField(window, "_operationalFocusMode").ToString());
                    Assert.AreEqual(UiText.Get("MapOperationalFocusSinglePointsShow"),
                        ((Button)window.FindName("MapOperationalFocusButton")).Content);
                    var focused = (HashSet<Guid>)FailureField(window, "_operationalFocusDeviceIds");
                    Assert.IsTrue(focused.Contains(ids[1]) && focused.Contains(ids[2]));
                    Assert.IsFalse(focused.Contains(ids[4]) || focused.Contains(ids[5]));
                    typeof(MainWindow).GetField("_hoveredPhysicalLinkId", FailureFlags).SetValue(window, null);
                    typeof(MainWindow).GetMethod("ReapplyOperationalFocusPresentation", FailureFlags).Invoke(window, null);
                    Assert.IsTrue(DeviceBorder(window, ids[1]).Opacity > 0.5);
                    Assert.IsTrue(DeviceBorder(window, ids[5]).Opacity < 0.2);
                    var line = ((Canvas)window.FindName("MapCanvas")).Children.OfType<Line>()
                        .Single(item => Equals(item.Tag, snapshot.DiagnosticSnapshot.Links[1].PhysicalLinkId));
                    Assert.IsTrue(line.Opacity > 0.5);
                    var ringLine = ((Canvas)window.FindName("MapCanvas")).Children.OfType<Line>()
                        .Single(item => Equals(item.Tag, snapshot.DiagnosticSnapshot.Links[3].PhysicalLinkId));
                    Assert.IsTrue(ringLine.Opacity < 0.2);
                    Assert.IsTrue(((Border)window.FindName("MapSinglePointsNotice")).IsVisible);
                    var summary = ((TextBlock)window.FindName("MapSinglePointsSummaryText")).Text;
                    StringAssert.Contains(summary, UiText.FormatCount("DiagnosticDeviceCount", 2));
                    SelectDevice(window, ids[1]);
                    Assert.AreEqual(UiText.Format("SpofDependents",
                        UiText.FormatCount("DiagnosticDeviceCount", 2)), FailureRows(window)[0]);
                    SelectDevice(window, ids[5]);
                    Assert.IsFalse(FailureRows(window).Any(row => row.StartsWith("Единая точка отказа:",
                        StringComparison.Ordinal)));
                    SelectDevice(window, ids[1]);
                    Click((Button)window.FindName("InspectorFailurePredictionShowButton"));
                    Assert.AreEqual("FailurePrediction", FailureField(window, "_operationalFocusMode").ToString());
                    Assert.IsTrue(window.HandleMapNavigationKey(Key.Escape, ModifierKeys.None, false, window));
                    Assert.AreEqual("SinglePointsOfFailure", FailureField(window, "_operationalFocusMode").ToString());
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void SinglePointsWithoutPollingPointShowStructuralReason()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(SinglePointsSnapshot()),
                    new EmptyLookupReader())
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[1]) != null && window.PollingPoint != null);
                    SelectSinglePointsMode(window);
                    SelectDevice(window, ids[1]);
                    StringAssert.Contains(FailureRows(window)[0], UiText.Get("ImpactNoDirection")
                        .Split(':')[0]);
                    StringAssert.Contains(((TextBlock)window.FindName("MapSinglePointsSummaryText")).Text,
                        UiText.Get("MapOperationalFocusSinglePoints"));
                    Assert.IsTrue(((HashSet<Guid>)FailureField(window, "_operationalFocusDeviceIds"))
                        .Contains(ids[1]));
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void SinglePointsListShowsTargetsAndShowSelectsOne()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(SinglePointsSnapshot()),
                    new Sprint49PollingPointLookupReader(Sprint49NeighborhoodFixture.PollingMac, ids[0]))
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[6]) != null &&
                        window.PollingPoint?.Status == EnginePollingPointStatus.Determined);
                    SelectSinglePointsMode(window);
                    var toggle = (System.Windows.Controls.Primitives.ToggleButton)
                        window.FindName("MapSinglePointsToggle");
                    toggle.IsChecked = true;
                    window.UpdateLayout();
                    Assert.IsTrue(((Border)window.FindName("MapSinglePointsDetails")).IsVisible);
                    var items = (ItemsControl)window.FindName("MapSinglePointsItems");
                    Assert.IsTrue(items.Items.Count > 0);
                    var button = TopologyQualityVisualButtons(items).First();
                    var target = button.Tag;
                    Click(button);
                    Assert.AreEqual("FailurePrediction", FailureField(window, "_operationalFocusMode").ToString());
                    Assert.IsTrue(Equals(target.GetType().GetProperty("DeviceId").GetValue(target),
                        FailureField(window, "_selectedDeviceId")) ||
                        Equals(target.GetType().GetProperty("LinkId").GetValue(target),
                            FailureField(window, "_selectedPhysicalLinkId")));
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        private static void SelectSinglePointsMode(MainWindow window)
        {
            var show = (Button)window.FindName("MapOperationalFocusButton");
            Click(show);
            var item = show.ContextMenu.Items.OfType<MenuItem>().Single(menu =>
                Equals(menu.Header, UiText.Get("MapOperationalFocusSinglePoints")));
            show.ContextMenu.IsOpen = false;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            PumpDispatcher();
        }
    }
}
