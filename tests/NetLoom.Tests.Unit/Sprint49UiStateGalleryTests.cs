using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Locations;
using NetLoom.Application.MapLayout;
using NetLoom.Application.Topology;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Lookup;
using NetLoom.Persistence.Sqlite.MapLayout;
using NetLoom.Persistence.Sqlite.Repositories;
using NetLoom.Persistence.Sqlite.Stp;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Alerts;
using NetLoom.Topology.Map;
using NetLoom.Topology.Refresh;
using NetLoom.Wpf;
using NetLoom.Wpf.MapInteraction;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46UiStateGalleryTests
    {
        // Четыре состояния, две темы; каждая PNG объединяет ширины 1100 и 1440.
        [TestMethod]
        public void FocusNeighborhoodGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ResolveOutputDirectory()),
                    "sprint49-neighborhood");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png")) File.Delete(file);
                var findings = new List<string>();
                var information = new List<string>();
                var scenarios = new[] { "61-whole-site", "62-neighborhood", "63-neighborhood-expanded-up",
                    "64-alert-participants" };
                try
                {
                    foreach (var scenario in scenarios)
                    foreach (var dark in new[] { false, true })
                    {
                        var theme = dark ? "dark" : "light";
                        var bitmaps = new List<BitmapSource>();
                        foreach (var width in new[] { 1100, 1440 })
                        {
                            var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                "netloom-s49-neighborhood-" + Guid.NewGuid().ToString("N") + ".db");
                            MainWindow window = null;
                            File.Copy(source, database);
                            try
                            {
                                // Копия стенда открывается без рабочего вида; оригинальная база не меняется.
                                window = CreateParallelLinksFieldWindow(database, dark, withoutSavedView: true);
                                PrepareWindow(window, width, GalleryHeight);
                                var current = window;
                                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                                WaitForCondition(() => ((MapSnapshot)typeof(MainWindow)
                                    .GetField("_lastMapSnapshot", flags).GetValue(current)).Nodes.Count > 0);
                                PumpDispatcher();
                                var map = (MapSnapshot)typeof(MainWindow).GetField("_lastMapSnapshot", flags).GetValue(window);
                                if (scenario == "62-neighborhood" || scenario == "63-neighborhood-expanded-up")
                                {
                                    var selected = map.Nodes.Where(node => node.DeviceId.HasValue)
                                        .Where(node => map.Links.Where(link => link.SourceNodeKey == node.Key ||
                                            link.TargetNodeKey == node.Key)
                                            .Select(link => link.SourceNodeKey == node.Key ? link.TargetNodeKey : link.SourceNodeKey)
                                            .Distinct().Count() >= 3)
                                        .OrderByDescending(node => node.Label == "ps1-sw-01").FirstOrDefault();
                                    Assert.IsNotNull(selected, "Field stand needs a switch with at least three neighbors.");
                                    var border = ((Canvas)window.FindName("MapCanvas")).Children.OfType<Border>()
                                        .Single(item => Equals(item.Tag, selected.DeviceId.Value));
                                    border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,
                                        Environment.TickCount, MouseButton.Left)
                                    { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = border });
                                    PumpDispatcher();
                                    var show = (Button)window.FindName("MapOperationalFocusButton");
                                    show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                    var neighborhood = show.ContextMenu.Items.OfType<MenuItem>()
                                        .Single(item => System.Windows.Automation.AutomationProperties.GetName(item) ==
                                            UiText.Get("MapNeighborhoodMenu"));
                                    show.ContextMenu.IsOpen = false;
                                    neighborhood.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                                    PumpDispatcher();
                                    if (scenario == "63-neighborhood-expanded-up")
                                    {
                                        var up = (Button)window.FindName("MapNeighborhoodUpButton");
                                        if (up.IsEnabled) up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                        else
                                        {
                                            var reason = (TextBlock)window.FindName("MapNeighborhoodUpReason");
                                            Assert.IsTrue(reason.IsVisible);
                                            information.Add("ИНФО " + scenario + "/" + theme + "/" + width +
                                                " — На границе окрестности нет направления STP вверх: " + reason.Text);
                                        }
                                    }
                                }
                                else if (scenario == "64-alert-participants")
                                {
                                    ((Button)window.FindName("ShellAlertsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                    PumpDispatcher();
                                    var alerts = (ItemsControl)window.FindName("AlertList");
                                    Assert.IsTrue(alerts.Items.Count > 0, "Field stand needs an active alert.");
                                    WaitForCondition(() => typeof(MainWindow).GetField("_selectedAlertKey", flags)
                                        .GetValue(current) != null);
                                }
                                PumpDispatcher();
                                window.UpdateLayout();
                                CollectTextClipping(window.Content as DependencyObject,
                                    scenario + "/" + theme + "/" + width, findings);
                                bitmaps.Add(Capture(window.Content as FrameworkElement));
                            }
                            finally
                            {
                                window?.Close();
                                PumpDispatcher();
                                DeleteParallelLinksFieldCopy(database);
                            }
                        }
                        SaveSideBySide(bitmaps[0], bitmaps[1],
                            System.IO.Path.Combine(output, scenario + "-" + theme + ".png"));
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 && information.Count == 0 ? new[] { "Находок нет." }
                            : findings.Concat(information).ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(8, Directory.GetFiles(output, "*.png").Length);
            });
        }

        private sealed class NeighborhoodStartupLayoutStore : IMapLayoutStore
        {
            private readonly IMapLayoutStore _inner;
            internal NeighborhoodStartupLayoutStore(IMapLayoutStore inner) { _inner = inner; }
            public MapLayoutSnapshot Load(Guid mapId) => null;
            public void SaveViewport(Guid mapId, MapViewportLayout viewport) => _inner.SaveViewport(mapId, viewport);
            public void SaveDevice(Guid mapId, MapDeviceLayout deviceLayout) => _inner.SaveDevice(mapId, deviceLayout);
        }

        // Оси: четыре уровня детализации, две темы, ширины 1100/1440; данные полевого стенда.
        // Наведение и редакторы не входят: здесь проверяется только представление масштаба.
        [TestMethod]
        public void SemanticZoomGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint49-semantic-zoom");
                Directory.CreateDirectory(output);
                var findings = new List<string>();
                var scenarios = new[] { "57-zoom-far", "58-zoom-medium", "59-zoom-close", "60-zoom-detailed" };
                try
                {
                    for (var level = 0; level < scenarios.Length; level++)
                    foreach (var dark in new[] { false, true })
                    {
                        var bitmaps = new List<BitmapSource>();
                        foreach (var width in new[] { 1100, 1440 })
                        {
                            var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                "netloom-s49-semantic-" + Guid.NewGuid().ToString("N") + ".db");
                            MainWindow window = null;
                            File.Copy(source, database);
                            try
                            {
                                window = CreateParallelLinksFieldWindow(database, dark);
                                PrepareWindow(window, width, GalleryHeight);
                                var currentWindow = window;
                                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                                WaitForCondition(() => ((MapSnapshot)typeof(MainWindow)
                                    .GetField("_lastMapSnapshot", flags).GetValue(currentWindow))
                                    .Nodes.Any(node => node.Label == "core-sw-01"));
                                FocusSemanticFieldContext(window, level);
                                PumpDispatcher();
                                window.UpdateLayout();
                                CollectTextClipping(window.Content as DependencyObject,
                                    scenarios[level] + "/" + (dark ? "dark" : "light") + "/" + width, findings);
                                bitmaps.Add(Capture(window.Content as FrameworkElement));
                            }
                            finally
                            {
                                if (window != null) window.Close();
                                PumpDispatcher();
                                DeleteParallelLinksFieldCopy(database);
                            }
                        }
                        SaveSideBySide(bitmaps[0], bitmaps[1], System.IO.Path.Combine(output,
                            scenarios[level] + "-" + (dark ? "dark" : "light") + ".png"));
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(8, Directory.GetFiles(output, "*.png").Length);
            });
        }

        private static void FocusSemanticFieldContext(MainWindow window, int level)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var map = (MapSnapshot)typeof(MainWindow).GetField("_lastMapSnapshot", flags).GetValue(window);
            var canvas = (Canvas)window.FindName("MapCanvas");
            typeof(MainWindow).GetField("_hoveredPhysicalLinkId", flags).SetValue(window, null);
            if (level == 0)
            {
                var site = map.Locations.Single(location => location.Name == "Площадка А");
                var ids = new HashSet<Guid> { site.Id };
                while (true)
                {
                    var count = ids.Count;
                    foreach (var location in map.Locations.Where(location =>
                        location.ParentLocationId.HasValue && ids.Contains(location.ParentLocationId.Value)))
                        ids.Add(location.Id);
                    if (ids.Count == count) break;
                }
                var devices = map.Nodes.Where(node => node.LocationId.HasValue && ids.Contains(node.LocationId.Value))
                    .Select(node => node.DeviceId).ToArray();
                var bounds = canvas.Children.OfType<Border>().Where(border =>
                        border.Tag is Guid && (ids.Contains((Guid)border.Tag) || devices.Contains((Guid)border.Tag)))
                    .Select(border => new Rect(Canvas.GetLeft(border), Canvas.GetTop(border),
                        border.ActualWidth, border.ActualHeight)).ToList();
                Assert.IsTrue(bounds.Count > 0);
                var maximum = (double)window.FindResource("NetLoom.Map.ReadableZoomMin") -
                    (double)window.FindResource("NetLoom.Map.ZoomStep");
                var fit = typeof(MainWindow).GetMethod("TryFitMapBoundsToViewport", flags, null,
                    new[] { typeof(IReadOnlyList<Rect>), typeof(double?), typeof(double?) }, null);
                Assert.IsTrue((bool)fit.Invoke(window, new object[] { bounds, null, maximum }));
                PumpDispatcher();
                window.UpdateLayout();
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var siteBorder = canvas.Children.OfType<Border>().Single(border => Equals(border.Tag, site.Id));
                var visible = siteBorder.TransformToAncestor(viewer).TransformBounds(new Rect(siteBorder.RenderSize));
                Assert.IsTrue(visible.Left >= -1 && visible.Top >= -1 &&
                    visible.Right <= viewer.ActualWidth + 1 && visible.Bottom <= viewer.ActualHeight + 1);
            }
            else
            {
                var core = map.Nodes.Single(node => node.Label == "core-sw-01");
                var border = canvas.Children.OfType<Border>().Single(item => Equals(item.Tag, core.DeviceId));
                border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = border });
                PumpDispatcher();
                var key = level == 1 ? "NetLoom.Map.ReadableZoomMin" :
                    level == 2 ? "NetLoom.Map.LinkLabelMinZoom" : "NetLoom.Map.SemanticDetailMinZoom";
                var zoom = (double)window.FindResource(key);
                // Тот же путь, что у кнопок; явно центрируем выбор даже при неизменном масштабе.
                typeof(MainWindow).GetMethod("ApplyZoomCenteredOnSelection", flags)
                    .Invoke(window, new object[] { zoom, null });
            }
        }

        // Ручной кабель противоречит настоящей LLDP-связи на копии полевого стенда.
        [TestMethod]
        public void TopologyConflictGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");

            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint49-conflict");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png")) File.Delete(file);
                var findings = new List<string>();
                const string scenario = "54-manual-observed-conflict";
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

                try
                {
                    foreach (var dark in new[] { false, true })
                    {
                        var theme = dark ? "dark" : "light";
                        var bitmaps = new List<BitmapSource>();
                        foreach (var width in new[] { 1100, 1440 })
                        {
                            var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                "netloom-s49-conflict-gallery-" + Guid.NewGuid().ToString("N") + ".db");
                            MainWindow window = null;
                            File.Copy(source, database);
                            try
                            {
                                var pair = AddTopologyConflictToFieldCopy(database);
                                window = CreateParallelLinksFieldWindow(database, dark);
                                window.TopologyConflictAcknowledgements = new SqliteTopologyConflictAcknowledgementStore(
                                    new SqliteConnectionFactory(database));
                                PrepareWindow(window, width, GalleryHeight);
                                var currentWindow = window;
                                WaitForCondition(() =>
                                {
                                    var conflicts = (IReadOnlyList<TopologyConflict>)typeof(MainWindow)
                                        .GetField("_topologyConflicts", flags).GetValue(currentWindow);
                                    return conflicts.Any(conflict => conflict.ManualLinkId == pair.ManualLinkId &&
                                        conflict.ObservedLinkId == pair.ObservedLinkId);
                                });
                                var conflict = ((IReadOnlyList<TopologyConflict>)typeof(MainWindow)
                                    .GetField("_topologyConflicts", flags).GetValue(window))
                                    .Single(item => item.ManualLinkId == pair.ManualLinkId && item.ObservedLinkId == pair.ObservedLinkId);
                                FocusTopologyConflictFieldContext(window, conflict);
                                var blocks = (ItemsControl)window.FindName("InspectorTopologyConflicts");
                                Assert.IsTrue(blocks.IsVisible);
                                Assert.AreEqual(1, blocks.Items.Count);
                                var labels = ((Canvas)window.FindName("MapCanvas")).Children.OfType<TextBlock>()
                                    .Where(label => Equals(label.Tag, pair.ManualLinkId) || Equals(label.Tag, pair.ObservedLinkId)).ToArray();
                                Assert.AreEqual(2, labels.Length);
                                Assert.IsTrue(labels.All(label => label.IsVisible &&
                                    label.Text.StartsWith("⚠ ", StringComparison.Ordinal)));
                                PumpDispatcher();
                                window.UpdateLayout();
                                CollectTextClipping(window.Content as DependencyObject,
                                    scenario + "/" + theme + "/" + width, findings);
                                bitmaps.Add(Capture(window.Content as FrameworkElement));
                            }
                            finally
                            {
                                if (window != null) window.Close();
                                PumpDispatcher();
                                DeleteParallelLinksFieldCopy(database);
                            }
                        }
                        SaveSideBySide(bitmaps[0], bitmaps[1],
                            System.IO.Path.Combine(output, scenario + "-" + theme + ".png"));
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(2, Directory.GetFiles(output, "*.png").Length);
            });
        }

        private static TopologyConflictKey AddTopologyConflictToFieldCopy(string database)
        {
            var factory = new SqliteConnectionFactory(database);
            new DatabaseInitializer(factory).Initialize();
            var topology = new SqliteMaterializedTopologyRepository(factory);
            var devices = topology.GetDevices();
            var core1 = devices.Single(device => (device.CustomName ?? device.DiscoveredName) == "core-sw-01");
            var core2 = devices.Single(device => (device.CustomName ?? device.DiscoveredName) == "core-sw-02");
            var gateway = devices.Single(device => (device.CustomName ?? device.DiscoveredName) == "gw-01");
            var evidence = topology.GetPhysicalLinkEvidence();
            var observed = topology.GetPhysicalLinks().Where(link => !link.IsHidden && !link.IsArchived &&
                    link.Strength != NetLoom.Domain.Topology.PhysicalLinkStrength.Manual &&
                    ((link.DeviceAId == core1.Id && link.DeviceBId == core2.Id) ||
                     (link.DeviceBId == core1.Id && link.DeviceAId == core2.Id)) &&
                    link.InterfaceAId.HasValue && link.InterfaceBId.HasValue &&
                    evidence.Any(item => item.PhysicalLinkId == link.Id &&
                        item.Kind == NetLoom.Domain.Topology.PhysicalLinkEvidenceKind.Lldp))
                .OrderBy(link => link.Id).First();
            var sharedPort = observed.DeviceAId == core1.Id ? observed.InterfaceAId : observed.InterfaceBId;
            var targetPort = topology.GetInterfaces().Single(port => port.DeviceId == gateway.Id && port.IfIndex == 2);
            var manual = new ManualTopologyService(topology, new SqliteManualTopologyAuditStore(factory));
            var manualId = manual.CreateLink(core1.Id, sharedPort, gateway.Id, targetPort.Id,
                observed.MediaTypeResolved, "Ручная схема: кабель ядра подключён к ether2 шлюза.");
            return new TopologyConflictKey(manualId, observed.Id);
        }

        private static void FocusTopologyConflictFieldContext(MainWindow window, TopologyConflict conflict)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var canvas = (Canvas)window.FindName("MapCanvas");
            var selected = canvas.Children.OfType<Line>().Single(line => Equals(line.Tag, conflict.ManualLinkId));
            selected.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = selected });
            PumpDispatcher();
            window.UpdateLayout();
            typeof(MainWindow).GetField("_hoveredPhysicalLinkId", flags).SetValue(window, null);
            typeof(MainWindow).GetMethod("UpdateSemanticMapVisibility", flags).Invoke(window, null);
            var devices = new[] { conflict.ManualLink.DeviceAId, conflict.ManualLink.DeviceBId,
                conflict.ObservedLink.DeviceAId, conflict.ObservedLink.DeviceBId }.Distinct().ToArray();
            var bounds = canvas.Children.OfType<Border>().Where(border => border.Tag is Guid && devices.Contains((Guid)border.Tag))
                .Select(border => new Rect(Canvas.GetLeft(border), Canvas.GetTop(border),
                    border.DesiredSize.Width, border.DesiredSize.Height)).ToList();
            Assert.AreEqual(3, bounds.Count);
            bounds.AddRange(canvas.Children.OfType<TextBlock>()
                .Where(label => Equals(label.Tag, conflict.ManualLinkId) || Equals(label.Tag, conflict.ObservedLinkId))
                .Select(label => new Rect(Canvas.GetLeft(label), Canvas.GetTop(label), label.DesiredSize.Width, label.DesiredSize.Height)));
            var context = bounds.Aggregate(Rect.Union);
            context.Inflate((double)window.FindResource("NetLoom.Map.NodeWidth"),
                (double)window.FindResource("NetLoom.Map.NodeHeight"));
            bounds.Add(context);
            var minimumZoom = (double)window.FindResource("NetLoom.Map.ReadableZoomMin");
            var fit = typeof(MainWindow).GetMethod("TryFitMapBoundsToViewport", flags, null,
                new[] { typeof(IReadOnlyList<Rect>), typeof(double?) }, null);
            Assert.IsNotNull(fit);
            Assert.IsTrue((bool)fit.Invoke(window, new object[] { bounds, minimumZoom }));
            ((TabControl)window.FindName("InspectorTabControl")).SelectedItem = window.FindName("InspectorOverviewTab");
            PumpDispatcher();
            window.UpdateLayout();
        }

        // Оси: свёрнутые и раскрытые причины, обе темы и ширины 1100/1440.
        // Состояния и имена берутся из полевого стенда без подмены данных.
        [TestMethod]
        public void TopologyQualityGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");

            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint49-quality");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png"))
                    File.Delete(file);
                var findings = new List<string>();

                try
                {
                    foreach (var expanded in new[] { false, true })
                    {
                        var scenario = expanded ? "53-quality-expanded" : "52-quality-collapsed";
                        foreach (var dark in new[] { false, true })
                        {
                            var theme = dark ? "dark" : "light";
                            var bitmaps = new List<BitmapSource>();
                            foreach (var width in new[] { 1100, 1440 })
                            {
                                var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                    "netloom-s49-quality-" + Guid.NewGuid().ToString("N") + ".db");
                                MainWindow window = null;
                                File.Copy(source, database);
                                try
                                {
                                    window = CreateParallelLinksFieldWindow(database, dark);
                                    PrepareWindow(window, width, GalleryHeight);
                                    var currentWindow = window;
                                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                                    WaitForCondition(() =>
                                    {
                                        var map = (MapSnapshot)typeof(MainWindow).GetField("_lastMapSnapshot", flags)
                                            .GetValue(currentWindow);
                                        return map != null && map.Nodes.Any(node => node.Label == "core-sw-01");
                                    });
                                    var report = (TopologyQualityReport)typeof(MainWindow)
                                        .GetField("_topologyQualityReport", flags).GetValue(window);
                                    if (report.Count == 0)
                                        findings.Add(scenario + "/" + theme + "/" + width +
                                            " — Полевой стенд не содержит ожидаемых пробелов топологии (§9).");
                                    else
                                        Assert.IsTrue(((Border)window.FindName("MapQualityNotice")).IsVisible);

                                    Assert.AreEqual(report.Items.Count, report.Count);
                                    window.UpdateLayout();
                                    var mapViewer = (ScrollViewer)window.FindName("MapScrollViewer");
                                    var mapBounds = mapViewer.TransformToAncestor(window)
                                        .TransformBounds(new Rect(mapViewer.RenderSize));
                                    ((ToggleButton)window.FindName("MapQualityToggle")).IsChecked = expanded;
                                    PumpDispatcher();
                                    window.UpdateLayout();
                                    Assert.AreEqual(mapBounds, mapViewer.TransformToAncestor(window)
                                        .TransformBounds(new Rect(mapViewer.RenderSize)));
                                    var details = (Border)window.FindName("MapQualityDetails");
                                    Assert.AreEqual(expanded, details.IsVisible);
                                    if (expanded)
                                    {
                                        Assert.AreEqual(2, Grid.GetRow(details));
                                        Assert.IsTrue(Panel.GetZIndex(details) > Panel.GetZIndex(mapViewer));
                                        Assert.IsInstanceOfType(details.Effect, typeof(System.Windows.Media.Effects.DropShadowEffect));
                                        var surface = details.Background as System.Windows.Media.SolidColorBrush;
                                        Assert.IsNotNull(surface);
                                        Assert.AreEqual((byte)255, surface.Color.A);
                                        Assert.AreEqual(1.0, surface.Opacity);
                                        Assert.AreEqual(1.0, details.Opacity);
                                        Assert.IsTrue(details.ActualWidth <=
                                            mapViewer.ActualWidth - details.Margin.Left - details.Margin.Right + 0.001);
                                    }
                                    CollectTextClipping(window.Content as DependencyObject,
                                        scenario + "/" + theme + "/" + width, findings);
                                    bitmaps.Add(Capture(window.Content as FrameworkElement));
                                }
                                finally
                                {
                                    if (window != null) window.Close();
                                    PumpDispatcher();
                                    DeleteParallelLinksFieldCopy(database);
                                }
                            }
                            SaveSideBySide(bitmaps[0], bitmaps[1],
                                System.IO.Path.Combine(output, scenario + "-" + theme + ".png"));
                        }
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(4, Directory.GetFiles(output, "*.png").Length);
            });
        }

        // Один выбранный кабель в плотной части полевого стенда, обе ширины в каждом кадре.
        [TestMethod]
        public void LinkFocusGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");

            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint49-link-focus");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png"))
                    File.Delete(file);
                var findings = new List<string>();
                const string scenario = "51-link-focus-selected";

                try
                {
                    foreach (var dark in new[] { false, true })
                    {
                        var theme = dark ? "dark" : "light";
                        var bitmaps = new List<BitmapSource>();
                        foreach (var width in new[] { 1100, 1440 })
                        {
                            var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                "netloom-s49-link-focus-" + Guid.NewGuid().ToString("N") + ".db");
                            MainWindow window = null;
                            File.Copy(source, database);
                            try
                            {
                                window = CreateParallelLinksFieldWindow(database, dark);
                                PrepareWindow(window, width, GalleryHeight);
                                var currentWindow = window;
                                WaitForCondition(() =>
                                {
                                    var map = (MapSnapshot)typeof(MainWindow).GetField("_lastMapSnapshot",
                                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(currentWindow);
                                    return map != null && map.Nodes.Any(node => node.Label == "core-sw-01") &&
                                        map.Nodes.Any(node => node.Label == "core-sw-02");
                                });
                                FocusParallelLinksFieldPair(window, 0, null);
                                PumpDispatcher();
                                window.UpdateLayout();

                                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                                // Кадр показывает выбор без временного наведения реального указателя.
                                typeof(MainWindow).GetField("_hoveredPhysicalLinkId", flags).SetValue(window, null);
                                typeof(MainWindow).GetMethod("UpdateSemanticMapVisibility", flags).Invoke(window, null);
                                window.UpdateLayout();
                                var selectedId = (Guid?)typeof(MainWindow)
                                    .GetField("_selectedPhysicalLinkId", flags).GetValue(window);
                                Assert.IsTrue(selectedId.HasValue);
                                var canvas = (Canvas)window.FindName("MapCanvas");
                                var selectedLabel = canvas.Children.OfType<TextBlock>()
                                    .Single(label => Equals(label.Tag, selectedId.Value));
                                Assert.IsTrue(selectedLabel.IsVisible);
                                Assert.AreEqual(FontWeights.SemiBold, selectedLabel.FontWeight);
                                var dimmed = (double)window.FindResource("NetLoom.Map.LinkFocusDimmedOpacity");
                                var snapshot = (MapSnapshot)typeof(MainWindow)
                                    .GetField("_lastMapSnapshot", flags).GetValue(window);
                                var selectedLink = snapshot.Links.Single(link => link.PhysicalLinkId == selectedId);
                                var neighbor = snapshot.Links.Single(link => link.PhysicalLinkId.HasValue &&
                                    link.PhysicalLinkId != selectedId &&
                                    ((link.SourceNodeKey == selectedLink.SourceNodeKey &&
                                      link.TargetNodeKey == selectedLink.TargetNodeKey) ||
                                     (link.SourceNodeKey == selectedLink.TargetNodeKey &&
                                      link.TargetNodeKey == selectedLink.SourceNodeKey)));
                                var neighborLine = canvas.Children.OfType<Line>()
                                    .Single(line => Equals(line.Tag, neighbor.PhysicalLinkId.Value));
                                Assert.IsTrue(neighborLine.IsVisible);
                                Assert.IsTrue(neighborLine.Opacity <= dimmed);
                                Assert.AreEqual(neighborLine.Opacity, canvas.Children.OfType<TextBlock>()
                                    .Single(label => Equals(label.Tag, neighbor.PhysicalLinkId.Value)).Opacity, 0.0001);
                                CollectTextClipping(window.Content as DependencyObject,
                                    scenario + "/" + theme + "/" + width, findings);
                                bitmaps.Add(Capture(window.Content as FrameworkElement));
                            }
                            finally
                            {
                                if (window != null) window.Close();
                                PumpDispatcher();
                                DeleteParallelLinksFieldCopy(database);
                            }
                        }
                        SaveSideBySide(bitmaps[0], bitmaps[1],
                            System.IO.Path.Combine(output, scenario + "-" + theme + ".png"));
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(2, Directory.GetFiles(output, "*.png").Length);
            });
        }

        // Оси: вложенные размещения и наложенные ручные рамки, обе темы и ширины 1100/1440.
        // Состояния сети берутся из двух настоящих стендов; их исходные базы не меняются.
        [TestMethod]
        public void LocationFramesGallery()
        {
            var root = FindParallelLinksRepositoryRoot();
            var sources = new[]
            {
                System.IO.Path.Combine(root, "artifacts", "realistic-stand", "field-s46.db"),
                System.IO.Path.Combine(root, "artifacts", "realistic-stand", "operator-s46-pass3-visual.db")
            };
            foreach (var source in sources)
                if (!File.Exists(source)) Assert.Inconclusive("Location gallery stand is missing: " + source);

            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint49-locations");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png")) File.Delete(file);
                var findings = new List<string>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                try
                {
                    for (var scenarioIndex = 0; scenarioIndex < sources.Length; scenarioIndex++)
                    {
                        var scenario = scenarioIndex == 0 ? "55-location-frames" : "56-location-overlap";
                        foreach (var dark in new[] { false, true })
                        {
                            var theme = dark ? "dark" : "light";
                            var bitmaps = new List<BitmapSource>();
                            foreach (var width in new[] { 1100, 1440 })
                            {
                                var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                    "netloom-s49-locations-" + Guid.NewGuid().ToString("N") + ".db");
                                MainWindow window = null;
                                File.Copy(sources[scenarioIndex], database);
                                try
                                {
                                    window = CreateParallelLinksFieldWindow(database, dark);
                                    PrepareWindow(window, width, GalleryHeight);
                                    var currentWindow = window;
                                    WaitForCondition(() =>
                                    {
                                        var currentMap = (MapSnapshot)typeof(MainWindow)
                                            .GetField("_lastMapSnapshot", flags).GetValue(currentWindow);
                                        return currentMap != null && currentMap.Locations.Count > 0;
                                    });
                                    typeof(MainWindow).GetMethod("StopStartupTopologyFit", flags).Invoke(window, null);
                                    var map = (MapSnapshot)typeof(MainWindow).GetField("_lastMapSnapshot", flags).GetValue(window);
                                    var canvas = (Canvas)window.FindName("MapCanvas");
                                    List<Rect> bounds;
                                    if (scenarioIndex == 0)
                                    {
                                        var site = map.Locations.Single(location => location.Name == "Площадка А");
                                        typeof(MainWindow).GetMethod("SelectLocation", flags).Invoke(window, new object[] { site.Id });
                                        var siteBorder = canvas.Children.OfType<Border>()
                                            .Single(border => Equals(border.Tag, site.Id) && Panel.GetZIndex(border) < 0);
                                        bounds = new List<Rect>
                                        {
                                            new Rect(Canvas.GetLeft(siteBorder), Canvas.GetTop(siteBorder),
                                                siteBorder.Width, siteBorder.Height)
                                        };
                                        Assert.IsTrue(map.Locations.Any(location => location.ParentLocationId == site.Id));
                                    }
                                    else
                                    {
                                        typeof(MainWindow).GetMethod("UpdateTopologyQuality", flags).Invoke(window, null);
                                        var report = (TopologyQualityReport)typeof(MainWindow)
                                            .GetField("_topologyQualityReport", flags).GetValue(window);
                                        Assert.IsTrue(report.Items.Any(item => item.Reasons.Any(reason => reason.Kind == TopologyQualityGapKind.LocationOverlap)));
                                        ((ToggleButton)window.FindName("MapQualityToggle")).IsChecked = true;
                                        window.UpdateLayout();
                                        var ids = map.Locations.Where(location => new[] { "1", "2", "3", "4-4" }
                                            .Contains(location.Name)).Select(location => location.Id).ToArray();
                                        Assert.IsTrue(ids.Length > 1);
                                        bounds = canvas.Children.OfType<Border>()
                                            .Where(border => border.Tag is Guid && ids.Contains((Guid)border.Tag) &&
                                                Panel.GetZIndex(border) < 0)
                                            .Select(border => new Rect(Canvas.GetLeft(border), Canvas.GetTop(border),
                                                border.Width, border.Height)).ToList();
                                    }
                                    var fit = typeof(MainWindow).GetMethod("TryFitMapBoundsToViewport", flags, null,
                                        new[] { typeof(IReadOnlyList<Rect>) }, null);
                                    Assert.IsTrue((bool)fit.Invoke(window, new object[] { bounds }));
                                    PumpDispatcher();
                                    window.UpdateLayout();
                                    CollectTextClipping(window.Content as DependencyObject,
                                        scenario + "/" + theme + "/" + width, findings);
                                    bitmaps.Add(Capture(window.Content as FrameworkElement));
                                }
                                finally
                                {
                                    if (window != null) window.Close();
                                    PumpDispatcher();
                                    DeleteParallelLinksFieldCopy(database);
                                }
                            }
                            SaveSideBySide(bitmaps[0], bitmaps[1],
                                System.IO.Path.Combine(output, scenario + "-" + theme + ".png"));
                        }
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(4, Directory.GetFiles(output, "*.png").Length);
            });
        }

        // Оси: выбранный кабель пары, ширины 1100/1440, обе темы (§9).
        // Остальные состояния сети берутся из полевого стенда без синтетической замены.
        [TestMethod]
        public void ParallelLinksGallery()
        {
            var source = System.IO.Path.Combine(FindParallelLinksRepositoryRoot(),
                "artifacts", "realistic-stand", "field-s46.db");
            if (!File.Exists(source))
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");

            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var output = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ResolveOutputDirectory()), "sprint49-parallel-links");
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(output, "*.png"))
                    File.Delete(file);
                var findings = new List<string>();

                try
                {
                    // Кадры: выбор каждого кабеля; без выбора — видны обе связи пары и их порты;
                    // Без выбора на среднем масштабе — связи пары не сливаются в одну полосу (замечание владельца).
                    var scenarios = new[]
                    {
                        "50-parallel-links", "50-parallel-links-second-cable",
                        "50-parallel-links-overview", "50-parallel-links-medium"
                    };
                    for (var scenarioIndex = 0; scenarioIndex < scenarios.Length; scenarioIndex++)
                    {
                        var scenario = scenarios[scenarioIndex];
                        foreach (var dark in new[] { false, true })
                        {
                            var theme = dark ? "dark" : "light";
                            var bitmaps = new List<BitmapSource>();
                            foreach (var width in new[] { 1100, 1440 })
                            {
                                var database = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                    "netloom-s49-parallel-links-" + Guid.NewGuid().ToString("N") + ".db");
                                MainWindow window = null;
                                File.Copy(source, database);
                                try
                                {
                                    window = CreateParallelLinksFieldWindow(database, dark);
                                    PrepareWindow(window, width, GalleryHeight);
                                    var currentWindow = window;
                                    WaitForCondition(() =>
                                    {
                                        var map = (MapSnapshot)typeof(MainWindow).GetField("_lastMapSnapshot",
                                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(currentWindow);
                                        return map != null && map.Nodes.Any(node => node.Label == "core-sw-01") &&
                                            map.Nodes.Any(node => node.Label == "core-sw-02");
                                    });
                                    FocusParallelLinksFieldPair(window, scenarioIndex < 2 ? scenarioIndex : (int?)null,
                                        scenarioIndex == 3 ? MediumParallelLinksZoom : (double?)null);
                                    PumpDispatcher();
                                    window.UpdateLayout();
                                    CollectTextClipping(window.Content as DependencyObject,
                                        scenario + "/" + theme + "/" + width, findings);
                                    bitmaps.Add(Capture(window.Content as FrameworkElement));
                                }
                                finally
                                {
                                    if (window != null) window.Close();
                                    PumpDispatcher();
                                    DeleteParallelLinksFieldCopy(database);
                                }
                            }
                            SaveSideBySide(bitmaps[0], bitmaps[1],
                                System.IO.Path.Combine(output, scenario + "-" + theme + ".png"));
                        }
                    }
                }
                finally
                {
                    File.WriteAllLines(System.IO.Path.Combine(output, "findings.txt"),
                        findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray(), new UTF8Encoding(false));
                }
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(8, Directory.GetFiles(output, "*.png").Length);
            });
        }

        // Средний масштаб: устройства с именами, подписи связей ещё скрыты (ниже LinkLabelMinZoom).
        private const double MediumParallelLinksZoom = 0.8;

        private static MainWindow CreateParallelLinksFieldWindow(string database, bool dark, bool withoutSavedView = false)
        {
            var factory = new SqliteConnectionFactory(database);
            new DatabaseInitializer(factory).Initialize();
            var topology = new SqliteMaterializedTopologyRepository(factory);
            var locations = new SqliteLocationRepository(factory);
            var layouts = new SqliteMapLayoutStore(factory);
            var mapProvider = new MaterializedMapSnapshotProvider(topology, locations,
                new MaterializedTopologyMapProjector());
            var alerts = new MaterializedTopologyAlertSnapshotProvider(topology,
                new SqliteStpObservationStore(factory));
            var refresh = new MaterializedTopologyRefreshSnapshotProvider(
                new SqliteMaterializedTopologyReadSetReader(factory), mapProvider, alerts);
            return new MainWindow(refresh, new SqliteMacIpLookupReader(factory),
                withoutSavedView ? (IMapLayoutStore)new NeighborhoodStartupLayoutStore(layouts) : layouts,
                new ManualTopologyService(topology, new SqliteManualTopologyAuditStore(factory)),
                new LocationTopologyService(locations, topology), layouts,
                new GalleryMonitoringControl(), new GalleryDiscoveryControl(),
                new AccessProfileRepository(factory).GetEnabled(), new GalleryCandidateMaterializer(),
                new ParallelLinksShellStateStore(new UiShellState(null,
                    dark ? UiShellTheme.Dark : UiShellTheme.Light,
                    UiPollingSettings.Default, MapMotionMode.Off)));
        }

        private static void FocusParallelLinksFieldPair(MainWindow window, int? selectedCable, double? fixedZoom)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var snapshot = (MapSnapshot)typeof(MainWindow).GetField("_lastMapSnapshot", flags).GetValue(window);
            var first = snapshot.Nodes.Single(node => node.Label == "core-sw-01");
            var second = snapshot.Nodes.Single(node => node.Label == "core-sw-02");
            var links = snapshot.Links.Where(link =>
                (link.SourceNodeKey == first.Key && link.TargetNodeKey == second.Key) ||
                (link.SourceNodeKey == second.Key && link.TargetNodeKey == first.Key))
                .OrderBy(link => link.PhysicalLinkId).ToArray();
            Assert.AreEqual(2, links.Length, "Field stand must contain both physical core cables.");
            Assert.IsTrue(links.All(link => link.PhysicalLinkId.HasValue));
            var canvas = (Canvas)window.FindName("MapCanvas");
            if (selectedCable.HasValue)
            {
                var selected = canvas.Children.OfType<Line>()
                    .Single(line => Equals(line.Tag, links[selectedCable.Value].PhysicalLinkId.Value));
                selected.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,
                    Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = selected
                });
                PumpDispatcher();

                var selectedId = (Guid?)typeof(MainWindow).GetField("_selectedPhysicalLinkId", flags).GetValue(window);
                Assert.AreEqual(links[selectedCable.Value].PhysicalLinkId, selectedId);
            }
            var pairIds = links.Select(link => link.PhysicalLinkId.Value).ToArray();
            var bounds = canvas.Children.OfType<Border>()
                .Where(border => Equals(border.Tag, first.DeviceId) || Equals(border.Tag, second.DeviceId))
                .Select(border => new Rect(Canvas.GetLeft(border), Canvas.GetTop(border),
                    border.DesiredSize.Width, border.DesiredSize.Height)).ToList();
            Assert.AreEqual(2, bounds.Count);
            bounds.AddRange(canvas.Children.OfType<TextBlock>()
                .Where(label => label.Tag is Guid && pairIds.Contains((Guid)label.Tag))
                .Select(label => new Rect(Canvas.GetLeft(label), Canvas.GetTop(label),
                    label.DesiredSize.Width, label.DesiredSize.Height)));
            // Контекст вокруг пары: вписывать только два узла дало бы масштаб выше 200 %.
            var context = bounds.Aggregate(Rect.Union);
            context.Inflate((double)window.FindResource("NetLoom.Map.NodeWidth"),
                (double)window.FindResource("NetLoom.Map.NodeHeight"));
            bounds.Add(context);
            var minimumZoom = fixedZoom ?? Math.Max((double)window.FindResource("NetLoom.Map.ReadableZoomMin"),
                (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom"));
            if (fixedZoom.HasValue)
            {
                // Широкий контекст вписывается не мельче заданного масштаба — получается ровно он.
                context.Inflate(4000, 3000);
                bounds.Add(context);
            }
            var fit = typeof(MainWindow).GetMethod("TryFitMapBoundsToViewport", flags, null,
                new[] { typeof(IReadOnlyList<Rect>), typeof(double?) }, null);
            Assert.IsNotNull(fit);
            Assert.IsTrue((bool)fit.Invoke(window, new object[] { bounds, minimumZoom }));
            PumpDispatcher();
            window.UpdateLayout();
            var zoom = (double)typeof(MainWindow).GetField("_zoom", flags).GetValue(window);
            Assert.IsTrue(zoom >= minimumZoom - 0.001);
            var pairLines = canvas.Children.OfType<Line>()
                .Where(line => line.Tag is Guid && pairIds.Contains((Guid)line.Tag)).ToArray();
            Assert.AreEqual(2, pairLines.Length);
            // Расстояние между параллельными линиями на экране — не меньше NetLoom.Map.ParallelLinkMinScreenGap.
            var gap = ParallelLineDistance(pairLines[0], pairLines[1]) * zoom;
            Assert.IsTrue(gap >= (double)window.FindResource("NetLoom.Map.ParallelLinkMinScreenGap") - 0.5,
                "Parallel links merge on screen: " + gap.ToString("0.0") + " px at zoom " + zoom.ToString("0.00"));
            if (!fixedZoom.HasValue)
            {
                Assert.AreEqual(2, canvas.Children.OfType<TextBlock>()
                    .Count(label => label.Tag is Guid && pairIds.Contains((Guid)label.Tag) && label.IsVisible));
            }
        }

        private static double ParallelLineDistance(Line first, Line second)
        {
            var dx = first.X2 - first.X1;
            var dy = first.Y2 - first.Y1;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length < 0.001) return 0.0;
            var midX = (second.X1 + second.X2) / 2.0;
            var midY = (second.Y1 + second.Y2) / 2.0;
            return Math.Abs(((midX - first.X1) * dy) - ((midY - first.Y1) * dx)) / length;
        }

        private static string FindParallelLinksRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(System.IO.Path.Combine(directory.FullName, "NetLoom.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new InvalidOperationException("NetLoom.sln was not found above the test output directory.");
        }

        private static void DeleteParallelLinksFieldCopy(string database)
        {
            foreach (var candidate in new[] { database, database + "-wal", database + "-shm" })
            {
                try { if (File.Exists(candidate)) File.Delete(candidate); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private sealed class ParallelLinksShellStateStore : IUiShellStateStore
        {
            private UiShellState _state;
            public ParallelLinksShellStateStore(UiShellState state) { _state = state; }
            public UiShellState Load() { return _state; }
            public void Save(UiShellState state) { _state = state; }
        }
    }
}
