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
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46UiStateGalleryTests
    {
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

                                    ((ToggleButton)window.FindName("MapQualityToggle")).IsChecked = expanded;
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
                                FocusParallelLinksFieldPair(window, 0);
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
                    // Два кадра показывают выбор каждого кабеля; каждый объединяет обе ширины.
                    for (var selectedCable = 0; selectedCable < 2; selectedCable++)
                    {
                        var scenario = selectedCable == 0 ? "50-parallel-links" : "50-parallel-links-second-cable";
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
                                    FocusParallelLinksFieldPair(window, selectedCable);
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

        private static MainWindow CreateParallelLinksFieldWindow(string database, bool dark)
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
            return new MainWindow(refresh, new SqliteMacIpLookupReader(factory), layouts,
                new ManualTopologyService(topology, new SqliteManualTopologyAuditStore(factory)),
                new LocationTopologyService(locations, topology), layouts,
                new GalleryMonitoringControl(), new GalleryDiscoveryControl(),
                new AccessProfileRepository(factory).GetEnabled(), new GalleryCandidateMaterializer(),
                new ParallelLinksShellStateStore(new UiShellState(null,
                    dark ? UiShellTheme.Dark : UiShellTheme.Light,
                    UiPollingSettings.Default, MapMotionMode.Off)));
        }

        private static void FocusParallelLinksFieldPair(MainWindow window, int selectedCable)
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
            var selected = canvas.Children.OfType<Line>()
                .Single(line => Equals(line.Tag, links[selectedCable].PhysicalLinkId.Value));
            selected.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,
                Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = selected
            });
            PumpDispatcher();

            var selectedId = (Guid?)typeof(MainWindow).GetField("_selectedPhysicalLinkId", flags).GetValue(window);
            Assert.AreEqual(links[selectedCable].PhysicalLinkId, selectedId);
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
            var minimumZoom = Math.Max((double)window.FindResource("NetLoom.Map.ReadableZoomMin"),
                (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom"));
            var fit = typeof(MainWindow).GetMethod("TryFitMapBoundsToViewport", flags, null,
                new[] { typeof(IReadOnlyList<Rect>), typeof(double?) }, null);
            Assert.IsNotNull(fit);
            Assert.IsTrue((bool)fit.Invoke(window, new object[] { bounds, minimumZoom }));
            PumpDispatcher();
            window.UpdateLayout();
            Assert.IsTrue((double)typeof(MainWindow).GetField("_zoom", flags).GetValue(window) >= minimumZoom);
            Assert.AreEqual(2, canvas.Children.OfType<TextBlock>()
                .Count(label => label.Tag is Guid && pairIds.Contains((Guid)label.Tag) && label.IsVisible));
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
