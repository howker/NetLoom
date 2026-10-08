using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49LinkFocusTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly DateTime Now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Guid DeviceA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid DeviceB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid DeviceC = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid LinkAB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid LinkBC = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        [TestMethod]
        public void NoFocusUsesSemanticZoomAndDoesNotDimLinks()
        {
            WithMap(window =>
            {
                AssertUnfocused(window);
                SetZoom(window, (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom"));
                AssertUnfocused(window);
                Assert.IsTrue(MapCanvas(window).Children.OfType<TextBlock>()
                    .All(label => label.Visibility == Visibility.Visible));
            });
        }

        [TestMethod]
        public void HoverShowsBothPortsAtDistantZoomAndRestoresPresentationOnLeave()
        {
            WithMap(window =>
            {
                var canvas = MapCanvas(window);
                var line = LineById(window, LinkAB);
                var label = LabelById(window, LinkAB);
                var previousForeground = label.Foreground;
                var previousWeight = label.FontWeight;
                var previousBounds = new Point(Canvas.GetLeft(label), Canvas.GetTop(label));
                RaiseHover(line, Mouse.MouseEnterEvent);
                AssertFocused(window, LinkAB, LinkBC);
                StringAssert.Contains(label.Text, "Gi0/1");
                StringAssert.Contains(label.Text, "Gi0/24");
                Assert.AreSame(window.FindResource("NetLoom.Brush.TextPrimary"), label.Foreground);
                Assert.AreEqual(previousBounds, new Point(Canvas.GetLeft(label), Canvas.GetTop(label)));
                Assert.IsTrue(canvas.Children.OfType<Border>().All(node => node.Opacity == 1.0));

                // Масштаб и новый снимок не снимают наведение и не заменяют визуальные объекты.
                SetZoom(window, (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom"));
                window.ShowMap(Snapshot());
                Assert.AreSame(line, LineById(window, LinkAB));
                Assert.AreSame(label, LabelById(window, LinkAB));
                AssertFocused(window, LinkAB, LinkBC);
                RaiseHover(line, Mouse.MouseLeaveEvent);
                AssertUnfocused(window);
                Assert.AreSame(previousForeground, label.Foreground);
                Assert.AreEqual(previousWeight, label.FontWeight);

                SetDistantZoom(window);
                RaiseHover(label, Mouse.MouseEnterEvent);
                AssertFocused(window, LinkAB, LinkBC);
                RaiseHover(label, Mouse.MouseLeaveEvent);
                AssertUnfocused(window);
            });
        }

        [TestMethod]
        public void SelectionWithoutHoverHasFocusAndHoverTemporarilyOverridesIt()
        {
            WithMap(window =>
            {
                Select(LineById(window, LinkAB));
                Assert.AreEqual(LinkAB, GetField(window, "_selectedPhysicalLinkId"));
                AssertFocused(window, LinkAB, LinkBC);
                var second = LineById(window, LinkBC);
                RaiseHover(second, Mouse.MouseEnterEvent);
                AssertFocused(window, LinkBC, LinkAB);
                RaiseHover(second, Mouse.MouseLeaveEvent);
                AssertFocused(window, LinkAB, LinkBC);

                window.ShowMap(Snapshot());
                AssertFocused(window, LinkAB, LinkBC);
                Select(MapCanvas(window));
                Assert.IsNull(GetField(window, "_selectedPhysicalLinkId"));
                AssertUnfocused(window);
            });
        }

        [TestMethod]
        public void FocusPreservesOperationalColorAndMultipliesExistingOpacity()
        {
            WithMap(window =>
            {
                var states = (IDictionary)GetField(window, "_linkOperationalStates");
                var stateType = states.GetType().GetGenericArguments()[1];
                states[LinkAB] = Enum.Parse(stateType, "Critical");
                var mode = typeof(MainWindow).GetField("_operationalFocusMode", PrivateInstance);
                mode.SetValue(window, Enum.Parse(mode.FieldType, "CriticalLinks"));
                window.ShowMap(Snapshot(MapFreshness.Aging));
                var other = LineById(window, LinkBC);
                var previousOpacity = other.Opacity;
                Assert.IsTrue(previousOpacity > 0 && previousOpacity < 1.0);
                RaiseHover(LineById(window, LinkAB), Mouse.MouseEnterEvent);
                var dimmed = (double)window.FindResource("NetLoom.Map.LinkFocusDimmedOpacity");
                Assert.AreEqual(previousOpacity * dimmed, other.Opacity, 0.0001);
                Assert.AreEqual(other.Opacity, LabelById(window, LinkBC).Opacity, 0.0001);
                Assert.AreSame(window.FindResource("NetLoom.Brush.Critical"),
                    LabelById(window, LinkAB).Foreground);
                Assert.AreSame(window.FindResource("NetLoom.Brush.Critical"),
                    LineById(window, LinkAB).Stroke);

                // Переключение движения не сбрасывает ни один множитель прозрачности.
                typeof(MainWindow).GetMethod("StopAllMotion", PrivateInstance).Invoke(window, null);
                Assert.AreEqual(previousOpacity * dimmed, other.Opacity, 0.0001);
                RaiseHover(LineById(window, LinkAB), Mouse.MouseLeaveEvent);
                Assert.AreEqual(previousOpacity, other.Opacity, 0.0001);
                Assert.AreEqual(FontWeights.Bold, LabelById(window, LinkAB).FontWeight);
            });
        }

        [TestMethod]
        public void RemovingHoveredLinkRestoresRemainingLinks()
        {
            WithMap(window =>
            {
                RaiseHover(LineById(window, LinkAB), Mouse.MouseEnterEvent);
                AssertFocused(window, LinkAB, LinkBC);
                var snapshot = Snapshot();
                window.ShowMap(new MapSnapshot(Now, snapshot.Nodes,
                    snapshot.Links.Where(link => link.PhysicalLinkId == LinkBC)));
                Assert.IsNull(GetField(window, "_hoveredPhysicalLinkId"));
                Assert.AreEqual(1.0, LineById(window, LinkBC).Opacity, 0.0001);
                Assert.AreEqual(Visibility.Collapsed, LabelById(window, LinkBC).Visibility);
            });
        }

        private static void AssertFocused(MainWindow window, Guid focused, Guid other)
        {
            var dimmed = (double)window.FindResource("NetLoom.Map.LinkFocusDimmedOpacity");
            Assert.AreEqual(1.0, LineById(window, focused).Opacity, 0.0001);
            Assert.AreEqual(Visibility.Visible, LabelById(window, focused).Visibility);
            Assert.AreEqual(FontWeights.SemiBold, LabelById(window, focused).FontWeight);
            Assert.AreEqual(dimmed, LineById(window, other).Opacity, 0.0001);
            Assert.AreEqual(dimmed, LabelById(window, other).Opacity, 0.0001);
        }

        private static void AssertUnfocused(MainWindow window)
        {
            var canvas = MapCanvas(window);
            Assert.IsTrue(canvas.Children.OfType<Line>().All(line => Math.Abs(line.Opacity - 1.0) < 0.0001));
            var zoom = (double)GetField(window, "_zoom");
            var visibility = zoom >= (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom")
                ? Visibility.Visible : Visibility.Collapsed;
            foreach (var label in canvas.Children.OfType<TextBlock>())
            {
                Assert.AreEqual(1.0, label.Opacity, 0.0001);
                Assert.AreEqual(visibility, label.Visibility);
            }
        }

        private static void RaiseHover(UIElement element, RoutedEvent routedEvent)
        {
            element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = routedEvent });
        }

        private static void Select(UIElement element)
        {
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,
                Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = element
            });
        }

        private static MapSnapshot Snapshot(MapFreshness secondFreshness = MapFreshness.Fresh)
        {
            return new MapSnapshot(Now, new[]
            {
                Node("a", "A", 60, 60, DeviceA),
                Node("b", "B", 660, 60, DeviceB),
                Node("c", "C", 1260, 60, DeviceC)
            }, new[]
            {
                new MapLink("ab", "a", "b", "Gi0/1", "Gi0/24", MapConfidence.High,
                    MapFreshness.Fresh, new MapEvidenceItem[0], LinkAB),
                new MapLink("bc", "b", "c", "Gi0/2", "Gi0/23", MapConfidence.High,
                    secondFreshness, new MapEvidenceItem[0], LinkBC)
            });
        }

        private static MapNode Node(string key, string label, double x, double y, Guid device)
        {
            return new MapNode(key, label, null, x, y, null, MapNodeOrigin.Automatic,
                MapMonitoringCapability.Unknown, MapNodeCategory.Unknown, device);
        }

        private static PhysicalLinkDiagnostic Diagnostic(Guid link, Guid first, Guid second,
            string firstName, string secondName, string firstPort, string secondPort)
        {
            return new PhysicalLinkDiagnostic(link, first, second, null, null, firstName, secondName,
                firstPort, secondPort, DiagnosticLinkStrength.Confirmed, MapFreshness.Fresh,
                "Ethernet", null, "LLDP", Now, Now, StpTreePortState.Forwarding,
                StpTreePortState.Forwarding, new DiagnosticEvidenceItem[0], false, 0, 0, 0L);
        }

        private static object GetField(MainWindow window, string name)
        {
            return typeof(MainWindow).GetField(name, PrivateInstance).GetValue(window);
        }

        private static Canvas MapCanvas(MainWindow window) => (Canvas)window.FindName("MapCanvas");
        private static Line LineById(MainWindow window, Guid id) =>
            MapCanvas(window).Children.OfType<Line>().Single(line => Equals(line.Tag, id));
        private static TextBlock LabelById(MainWindow window, Guid id) =>
            MapCanvas(window).Children.OfType<TextBlock>().Single(label => Equals(label.Tag, id));

        private static void SetDistantZoom(MainWindow window)
        {
            SetZoom(window, (double)window.FindResource("NetLoom.Map.ReadableZoomMin"));
            Assert.IsTrue((double)GetField(window, "_zoom") <
                (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom"));
        }

        private static void SetZoom(MainWindow window, double zoom)
        {
            typeof(MainWindow).GetField("_zoom", PrivateInstance).SetValue(window, zoom);
            typeof(MainWindow).GetMethod("ApplyZoomTransform", PrivateInstance).Invoke(window, null);
        }

        private static void WithMap(Action<MainWindow> action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                MainWindow window = null;
                try
                {
                    window = new MainWindow();
                    var settings = (Button)window.FindName("MapSettingsButton");
                    var motion = settings.ContextMenu.Items.OfType<MenuItem>()
                        .Single(item => item.Items.OfType<MenuItem>().Count() == 3);
                    motion.Items.OfType<MenuItem>().Last().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    typeof(MainWindow).GetField("_lastDiagnosticSnapshot", PrivateInstance).SetValue(window,
                        new NetworkDiagnosticSnapshot(Now, new DeviceDiagnostic[0], new[]
                        {
                            Diagnostic(LinkAB, DeviceA, DeviceB, "A", "B", "Gi0/1", "Gi0/24"),
                            Diagnostic(LinkBC, DeviceB, DeviceC, "B", "C", "Gi0/2", "Gi0/23")
                        }));
                    SetDistantZoom(window);
                    window.ShowMap(Snapshot());
                    action(window);
                }
                catch (Exception error) { failure = error; }
                finally { if (window != null) window.Close(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(10))) Assert.Fail("STA WPF test did not complete.");
            if (failure != null) throw failure;
        }
    }
}
