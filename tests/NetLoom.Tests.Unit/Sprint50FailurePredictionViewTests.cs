using System;
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
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46ShellFoundationTests
    {
        private const BindingFlags FailureFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        // Цепочка P–A–B–C и отдельное устройство D проверяют направление и приглушение.
        private static TopologyRefreshSnapshot FailureChainSnapshot()
        {
            var source = Sprint49NeighborhoodFixture.Snapshot();
            var ids = Sprint49NeighborhoodFixture.Devices;
            var included = new[] { ids[0], ids[1], ids[2], ids[3], ids[7] };
            var nodes = source.MapSnapshot.Nodes.Where(node => included.Contains(node.DeviceId.Value)).ToArray();
            var links = source.MapSnapshot.Links.Take(3).ToArray();
            var diagnosticLinks = source.DiagnosticSnapshot.Links.Take(3).ToArray();
            var bridge = diagnosticLinks[1];
            diagnosticLinks[1] = new PhysicalLinkDiagnostic(bridge.PhysicalLinkId,
                bridge.DeviceAId, bridge.DeviceBId, null, null, bridge.DeviceAName,
                bridge.DeviceBName, "Gi0/1", "Gi0/2", DiagnosticLinkStrength.Confirmed,
                MapFreshness.Fresh, null, null, "STP", DateTime.UtcNow, DateTime.UtcNow,
                StpTreePortState.Forwarding, StpTreePortState.Forwarding,
                new DiagnosticEvidenceItem[0], true, 2, 2, 4);
            var devices = source.DiagnosticSnapshot.Devices.Select(device =>
                device.DeviceId == ids[1]
                    ? new DeviceDiagnostic(device.DeviceId, device.DisplayName, null, null,
                        DateTime.UtcNow, DateTime.UtcNow, new InterfaceDiagnostic[0],
                        isArticulationPoint: true, failurePartDeviceCounts: new[] { 2, 1 })
                    : device).Where(device => included.Contains(device.DeviceId)).ToArray();
            var now = DateTime.UtcNow;
            return new TopologyRefreshSnapshot(
                new MapSnapshot(now, nodes, links, source.MapSnapshot.Locations),
                source.AlertSnapshot,
                new NetworkDiagnosticSnapshot(now, devices, diagnosticLinks));
        }

        [TestMethod]
        public void FailurePredictionChainShowsInspectorFocusAndRestoresView()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(FailureChainSnapshot()),
                    new Sprint49PollingPointLookupReader(Sprint49NeighborhoodFixture.PollingMac, ids[0]))
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null &&
                        window.PollingPoint?.Status == EnginePollingPointStatus.Determined);
                    SelectDevice(window, ids[1]);
                    Assert.AreEqual(UiText.Get("DiagnosticDeviceImpactTitle").ToUpper(CultureInfo.CurrentCulture),
                        ((TextBlock)window.FindName("DiagnosticSecondaryTitleText")).Text.ToUpper(CultureInfo.CurrentCulture));
                    var rows = FailureRows(window);
                    CollectionAssert.Contains(rows, UiText.Format("ImpactCutOff",
                        UiText.FormatCount("DiagnosticDeviceCount", 2)));
                    CollectionAssert.Contains(rows, UiText.Get("ImpactPredictionNote"));
                    var show = (Button)window.FindName("InspectorFailurePredictionShowButton");
                    Assert.AreEqual(UiText.Get("ImpactShowOnMap"), AutomationProperties.GetName(show));
                    Click(show);
                    Assert.AreEqual("FailurePrediction", FailureField(window, "_operationalFocusMode").ToString());
                    Assert.AreEqual(UiText.Get("MapOperationalFocusFailurePredictionShow"),
                        ((Button)window.FindName("MapOperationalFocusButton")).Content);
                    typeof(MainWindow).GetField("_hoveredPhysicalLinkId", FailureFlags).SetValue(window, null);
                    typeof(MainWindow).GetMethod("ReapplyOperationalFocusPresentation", FailureFlags).Invoke(window, null);
                    Assert.IsTrue(DeviceBorder(window, ids[2]).Opacity > 0.5);
                    Assert.IsTrue(DeviceBorder(window, ids[3]).Opacity > 0.5);
                    Assert.IsTrue(DeviceBorder(window, ids[7]).Opacity < 0.2);
                    var impacted = (System.Collections.Generic.HashSet<Guid>)FailureField(window, "_operationalFocusDeviceIds");
                    Assert.IsTrue(impacted.Contains(ids[2]) && impacted.Contains(ids[3]));
                    Assert.IsTrue(((Border)window.FindName("MapFailurePredictionNotice")).IsVisible);
                    Assert.IsTrue(window.HandleMapNavigationKey(Key.Escape, ModifierKeys.None, false, window));
                    Assert.AreEqual("None", FailureField(window, "_operationalFocusMode").ToString());

                    Click(show);
                    Click((Button)window.FindName("MapFailurePredictionResetButton"));
                    Assert.AreEqual("None", FailureField(window, "_operationalFocusMode").ToString());
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void FailurePredictionWithoutPollingPointShowsPartsAndHidesMapAction()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(FailureChainSnapshot()), new EmptyLookupReader())
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[1]) != null && window.PollingPoint != null);
                    SelectDevice(window, ids[1]);
                    var rows = FailureRows(window);
                    // Устройство — точка сочленения: строка единой точки отказа сама называет причину и не повторяет её.
                    Assert.AreEqual(UiText.Format("SpofStructural",
                        UiText.FormatCount("DiagnosticDeviceCount", 2) + ", " + UiText.FormatCount("DiagnosticDeviceCount", 1),
                        UiText.Get("MapNeighborhoodPollingNotFound")), rows[0]);
                    Assert.AreEqual(1, rows.Count(row => row.Contains(UiText.Get("MapNeighborhoodPollingNotFound"))));
                    CollectionAssert.Contains(rows, UiText.Format("ImpactDeviceParts", 2,
                        UiText.FormatCount("DiagnosticDeviceCount", 2) + ", " +
                        UiText.FormatCount("DiagnosticDeviceCount", 1)));
                    Assert.AreEqual(Visibility.Collapsed,
                        ((Button)window.FindName("InspectorFailurePredictionShowButton")).Visibility);
                    var current = (NetworkDiagnosticSnapshot)typeof(MainWindow)
                        .GetField("_lastDiagnosticSnapshot", FailureFlags).GetValue(window);
                    FailureSelectLink(window, current.Links[1].PhysicalLinkId);
                    rows = FailureRows(window);
                    CollectionAssert.Contains(rows, UiText.Get("DiagnosticImpactSinglePath"));
                    Assert.AreEqual(Visibility.Collapsed,
                        ((Button)window.FindName("InspectorFailurePredictionShowButton")).Visibility);
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void DisconnectedDeviceShowsUnknownDirectionInsteadOfBypass()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var source = FailureChainSnapshot();
                var ids = Sprint49NeighborhoodFixture.Devices;
                var mapLinks = new[] { source.MapSnapshot.Links[0], source.MapSnapshot.Links[2] };
                var diagnosticLinks = new[] { source.DiagnosticSnapshot.Links[0], source.DiagnosticSnapshot.Links[2] };
                var snapshot = new TopologyRefreshSnapshot(
                    new MapSnapshot(DateTime.UtcNow, source.MapSnapshot.Nodes, mapLinks,
                        source.MapSnapshot.Locations), source.AlertSnapshot,
                    new NetworkDiagnosticSnapshot(DateTime.UtcNow, source.DiagnosticSnapshot.Devices,
                        diagnosticLinks));
                var window = new MainWindow(new FixedRefreshProvider(snapshot),
                    new Sprint49PollingPointLookupReader(Sprint49NeighborhoodFixture.PollingMac, ids[0]))
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[2]) != null &&
                        window.PollingPoint?.Status == EnginePollingPointStatus.Determined);
                    SelectDevice(window, ids[2]);
                    var rows = FailureRows(window);
                    Assert.AreEqual(UiText.Get("ImpactNotConnected"), rows[0]);
                    Assert.IsFalse(rows.Contains(UiText.Get("ImpactNoneAffected")));
                    CollectionAssert.Contains(rows, UiText.Get("ImpactPredictionNote"));
                    Assert.AreEqual(Visibility.Collapsed,
                        ((Button)window.FindName("InspectorFailurePredictionShowButton")).Visibility);
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void FailurePredictionLinkUsesDirectionalCountAndStructuralFallback()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = FailureChainSnapshot();
                var ids = Sprint49NeighborhoodFixture.Devices;
                var linkId = snapshot.DiagnosticSnapshot.Links[1].PhysicalLinkId;
                var window = new MainWindow(new FixedRefreshProvider(snapshot),
                    new Sprint49PollingPointLookupReader(Sprint49NeighborhoodFixture.PollingMac, ids[0]))
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[3]) != null &&
                        window.PollingPoint?.Status == EnginePollingPointStatus.Determined);
                    FailureSelectLink(window, linkId);
                    Assert.AreEqual(UiText.Get("DiagnosticImpactTitle").ToUpper(CultureInfo.CurrentCulture),
                        ((TextBlock)window.FindName("DiagnosticSecondaryTitleText")).Text.ToUpper(CultureInfo.CurrentCulture));
                    CollectionAssert.Contains(FailureRows(window), UiText.Format("ImpactCutOff",
                        UiText.FormatCount("DiagnosticDeviceCount", 2)));
                    Click((Button)window.FindName("InspectorFailurePredictionShowButton"));
                    Assert.IsTrue(((Border)window.FindName("MapFailurePredictionNotice")).IsVisible);
                    Assert.IsTrue(((TextBlock)window.FindName("MapFailurePredictionSummaryText")).Text
                        .StartsWith(UiText.Get("ImpactStripForLink").Split('{')[0], StringComparison.Ordinal));
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        [TestMethod]
        public void ProtectedRingFailureNamesStpStandbyInsteadOfUnqualifiedBypass()
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var source = Sprint49NeighborhoodFixture.Snapshot();
                var links = source.DiagnosticSnapshot.Links.ToArray();
                var standby = links[links.Length - 1];
                links[links.Length - 1] = new PhysicalLinkDiagnostic(standby.PhysicalLinkId,
                    standby.DeviceAId, standby.DeviceBId, null, null, standby.DeviceAName,
                    standby.DeviceBName, "Gi0/1", "Gi0/2", DiagnosticLinkStrength.Confirmed,
                    MapFreshness.Fresh, null, null, "STP", DateTime.UtcNow, DateTime.UtcNow,
                    StpTreePortState.Blocking, StpTreePortState.Forwarding,
                    new DiagnosticEvidenceItem[0], false, 0, 0, 0);
                var snapshot = new TopologyRefreshSnapshot(source.MapSnapshot, source.AlertSnapshot,
                    new NetworkDiagnosticSnapshot(DateTime.UtcNow, source.DiagnosticSnapshot.Devices, links));
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(snapshot),
                    new Sprint49PollingPointLookupReader(Sprint49NeighborhoodFixture.PollingMac, ids[0]))
                {
                    EngineHostAddresses = new Sprint49FixedHostAddresses(Sprint49NeighborhoodFixture.PollingMac)
                };
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null &&
                        window.PollingPoint?.Status == EnginePollingPointStatus.Determined);
                    SelectDevice(window, ids[2]);
                    var rows = FailureRows(window);
                    Assert.IsTrue(rows.Any(row => row.StartsWith("Только через резерв STP:", StringComparison.Ordinal)));
                    Assert.IsFalse(rows.Contains(UiText.Get("ImpactNoneAffected")));
                }
                finally { window.Close(); PumpDispatcher(); }
            });
        }

        private static void FailureSelectLink(MainWindow window, Guid id)
        {
            var line = ((Canvas)window.FindName("MapCanvas")).Children.OfType<Line>()
                .Single(item => Equals(item.Tag, id));
            line.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = line });
            PumpDispatcher();
        }

        private static string[] FailureRows(MainWindow window) =>
            ((ItemsControl)window.FindName("DiagnosticSecondaryList")).Items.Cast<object>()
                .Select(item => (string)item.GetType().GetProperty("Text").GetValue(item)).ToArray();

        private static object FailureField(MainWindow window, string name) =>
            typeof(MainWindow).GetField(name, FailureFlags).GetValue(window);
    }
}
