using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void Sprint49SemanticZoomChangesExistingVisualsAcrossAllLevels()
        {
            var location = Guid.NewGuid();
            var child = Guid.NewGuid();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var cable = Guid.NewGuid();
            var map = new MapSnapshot(Now, new[]
            {
                new MapNode("first", "Первый", null, 160, 170, location, deviceId: first),
                new MapNode("second", "Второй", null, 560, 170, child, deviceId: second, isUnconfirmed: true)
            }, new[]
            {
                new MapLink("cable", "first", "second", "G1/1", "G1/2", MapConfidence.High,
                    MapFreshness.Fresh, new MapEvidenceItem[0], cable)
            }, new[] { new MapLocation(location, null, "Серверная", null),
                new MapLocation(child, location, "Стойка", null) });
            var store = new LocationFrameLayoutStore(locations: new[]
            {
                new MapLocationLayout(location, 100, 100, 900, 500, false, false),
                new MapLocationLayout(child, 500, 140, 450, 330, false, false)
            });
            WithLocationFrameWindow(map, store, window =>
            {
                var diagnostics = new NetworkDiagnosticSnapshot(Now, new[]
                {
                    new DeviceDiagnostic(first, "Первый", "MOXA EDS-518A", null, Now, Now,
                        new InterfaceDiagnostic[0], "10.48.228.14"),
                    new DeviceDiagnostic(second, "Второй", null, null, Now, Now,
                        new InterfaceDiagnostic[0], "10.48.228.15", "Модель ЙЁ")
                }, new[]
                {
                    new PhysicalLinkDiagnostic(cable, first, second, null, null, "Первый", "Второй",
                        "G1/1", "G1/2", DiagnosticLinkStrength.Confirmed, MapFreshness.Fresh,
                        "Ethernet", 1000000000L, "LLDP", Now, Now, StpTreePortState.Forwarding,
                        StpTreePortState.Forwarding, new DiagnosticEvidenceItem[0], false, 0, 0, 0L)
                });
                typeof(MainWindow).GetField("_lastDiagnosticSnapshot", LocationFrameFlags).SetValue(window, diagnostics);
                window.ShowMap(map);
                var firstVisual = SemanticVisual(window, "_nodeVisualsByIdentity", "device:" + first.ToString("D"));
                var secondVisual = SemanticVisual(window, "_nodeVisualsByIdentity", "device:" + second.ToString("D"));
                var locationVisual = SemanticVisual(window, "_locationVisualsById", location);
                var title = SemanticProperty<TextBlock>(firstVisual, "Title");
                var secondary = SemanticProperty<TextBlock>(firstVisual, "Secondary");
                var label = ((Canvas)window.FindName("MapCanvas")).Children.OfType<TextBlock>()
                    .Single(text => Equals(text.Tag, cable));
                var border = DeviceBorder(window, first);
                foreach (var theme in new[] { UiShellTheme.Light, UiShellTheme.Dark })
                {
                    typeof(MainWindow).GetMethod("ApplyShellTheme", LocationFrameFlags).Invoke(window, new object[] { theme });
                    var readable = (double)window.FindResource("NetLoom.Map.ReadableZoomMin");
                    var labelMin = (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom");
                    var detailMin = (double)window.FindResource("NetLoom.Map.SemanticDetailMinZoom");
                    foreach (var zoom in new[] { 0.5, readable, labelMin, detailMin, 0.5 })
                    {
                        SemanticZoom(window, zoom);
                        var level = MapSemanticLevels.For(zoom, readable, labelMin, detailMin);
                        Assert.AreSame(border, DeviceBorder(window, first));
                        Assert.AreSame(firstVisual, SemanticVisual(window, "_nodeVisualsByIdentity", "device:" + first.ToString("D")));
                        Assert.AreEqual(level == MapSemanticLevel.Far ? Visibility.Collapsed : Visibility.Visible, title.Visibility);
                        Assert.AreEqual(level == MapSemanticLevel.Far ? Visibility.Collapsed : Visibility.Visible,
                            SemanticProperty<System.Windows.Shapes.Path>(firstVisual, "CategoryIcon").Visibility);
                        Assert.AreEqual(level == MapSemanticLevel.Close || level == MapSemanticLevel.Detailed
                            ? Visibility.Visible : Visibility.Collapsed, label.Visibility);
                        Assert.AreEqual(level == MapSemanticLevel.Detailed ? Visibility.Visible : Visibility.Collapsed, secondary.Visibility);
                        Assert.AreEqual(UiText.Get("MapSemantic" + level + "Help"),
                            AutomationProperties.GetHelpText((TextBlock)window.FindName("MapZoomValueText")));
                        if (level == MapSemanticLevel.Detailed)
                        {
                            StringAssert.Contains(secondary.Text, "10.48.228.14 · MOXA EDS-518A");
                            StringAssert.Contains(label.Text, UiText.Format("DiagnosticSpeedGbps", 1.0));
                            StringAssert.StartsWith(SemanticProperty<TextBlock>(secondVisual, "Secondary").Text,
                                UiText.Get("DeviceUnconfirmedMark") + " · 10.48.228.15 · Модель ЙЁ");
                            var geometry = ((Canvas)window.FindName("MapCanvas")).Children
                                .OfType<System.Windows.Shapes.Line>().Single(item => Equals(item.Tag, cable));
                            Assert.AreEqual(Canvas.GetTop(border) + border.ActualHeight / 2, geometry.Y1, 0.01);
                        }
                        else
                            Assert.AreEqual("G1/1 ↔ G1/2", label.Text);
                        var header = SemanticProperty<Border>(locationVisual, "Header");
                        var locationTitle = SemanticProperty<TextBlock>(locationVisual, "Title");
                        if (level == MapSemanticLevel.Far)
                        {
                            Assert.AreEqual("Серверная · 2", locationTitle.Text);
                            var scale = (ScaleTransform)header.RenderTransform;
                            Assert.IsTrue(locationTitle.FontSize * zoom * scale.ScaleX >=
                                (double)window.FindResource("NetLoom.FontSize.Caption"));
                            Assert.IsTrue(header.ActualWidth * scale.ScaleX <=
                                900 + header.ActualHeight * scale.ScaleY + 0.01);
                        }
                        else
                            Assert.IsTrue(header.RenderTransform.Value.IsIdentity);
                        window.ShowMap(map);
                        window.UpdateLayout();
                        Assert.AreEqual(level == MapSemanticLevel.Far ? Visibility.Collapsed : Visibility.Visible, title.Visibility);
                    }
                }

                // Выбор узла сохраняет читаемое имя при любом дальнем масштабе.
                border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = border });
                SemanticZoom(window, 0.25);
                var nameLabel = SemanticProperty<Border>(firstVisual, "SemanticLabel");
                Assert.AreEqual(Visibility.Visible, nameLabel.Visibility);
                Assert.AreEqual("Первый", ((TextBlock)nameLabel.Child).Text);
                Assert.AreEqual(4.0, ((TransformGroup)nameLabel.RenderTransform).Children.OfType<ScaleTransform>().Single().ScaleX);
                SemanticZoom(window, (double)window.FindResource("NetLoom.Map.ReadableZoomMin"));
                Assert.AreEqual(Visibility.Collapsed, nameLabel.Visibility);
                Assert.IsTrue(nameLabel.RenderTransform.Value.IsIdentity);
                Assert.AreEqual(0, store.DeviceWrites);
                Assert.AreEqual(0, store.LocationWrites);

                // Проблема и участник операционного фокуса получают имя без выбора узла.
                typeof(MainWindow).GetField("_selectedDeviceId", LocationFrameFlags).SetValue(window, null);
                typeof(MainWindow).GetField("_highlightedDeviceId", LocationFrameFlags).SetValue(window, null);
                var states = (IDictionary)typeof(MainWindow).GetField("_linkOperationalStates", LocationFrameFlags).GetValue(window);
                states[cable] = Enum.Parse(states.GetType().GetGenericArguments()[1], "Critical");
                SemanticZoom(window, 0.5);
                window.ShowMap(map);
                window.UpdateLayout();
                var status = SemanticProperty<System.Windows.Shapes.Path>(secondVisual, "StatusIcon");
                Assert.AreEqual(Visibility.Visible, status.Visibility);
                Assert.AreEqual(2.0, ((ScaleTransform)status.RenderTransform).ScaleX);
                Assert.AreEqual(Visibility.Visible, SemanticProperty<Border>(secondVisual, "SemanticLabel").Visibility);
                Assert.AreEqual(Visibility.Visible,
                    SemanticProperty<System.Windows.Shapes.Path>(locationVisual, "StatusIcon").Visibility);
                Assert.AreSame(status.Stroke,
                    SemanticProperty<System.Windows.Shapes.Path>(locationVisual, "StatusIcon").Stroke);
                states.Clear();
                window.ShowMap(map);
                window.UpdateLayout();
                Assert.AreEqual(Visibility.Collapsed, status.Visibility);
                Assert.AreEqual(Visibility.Collapsed, SemanticProperty<Border>(secondVisual, "SemanticLabel").Visibility);
                var mode = typeof(MainWindow).GetField("_operationalFocusMode", LocationFrameFlags);
                mode.SetValue(window, Enum.Parse(mode.FieldType, "AllProblems"));
                var participants = (System.Collections.Generic.HashSet<Guid>)typeof(MainWindow)
                    .GetField("_operationalFocusDeviceIds", LocationFrameFlags).GetValue(window);
                participants.Add(second);
                typeof(MainWindow).GetMethod("ReapplyOperationalFocusPresentation", LocationFrameFlags).Invoke(window, null);
                Assert.AreEqual(Visibility.Visible, SemanticProperty<Border>(secondVisual, "SemanticLabel").Visibility);
                SemanticZoom(window, (double)window.FindResource("NetLoom.Map.ReadableZoomMin"));
                Assert.IsTrue(status.RenderTransform.Value.IsIdentity);
                mode.SetValue(window, Enum.Parse(mode.FieldType, "None"));
                participants.Clear();

                // Фокусная подпись остаётся на среднем уровне, но скрывается на дальнем.
                var line = ((Canvas)window.FindName("MapCanvas")).Children.OfType<System.Windows.Shapes.Line>()
                    .Single(item => Equals(item.Tag, cable));
                line.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = Mouse.MouseEnterEvent });
                Assert.AreEqual(Visibility.Visible, label.Visibility);
                SemanticZoom(window, 0.5);
                Assert.AreEqual(Visibility.Collapsed, label.Visibility);
            });
        }

        private static object SemanticVisual(MainWindow window, string field, object key)
        {
            return ((IDictionary)typeof(MainWindow).GetField(field, LocationFrameFlags).GetValue(window))[key];
        }

        private static T SemanticProperty<T>(object visual, string name)
        {
            return (T)visual.GetType().GetProperty(name).GetValue(visual);
        }

        private static void SemanticZoom(MainWindow window, double zoom)
        {
            typeof(MainWindow).GetMethod("ChangeZoom", LocationFrameFlags).Invoke(window, new object[] { zoom });
            PumpDispatcher();
            window.UpdateLayout();
        }
    }
}
