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

                // Фокусная подпись видна на всех уровнях; на дальнем — в экранном размере (не мельче Caption).
                var line = ((Canvas)window.FindName("MapCanvas")).Children.OfType<System.Windows.Shapes.Line>()
                    .Single(item => Equals(item.Tag, cable));
                line.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = Mouse.MouseEnterEvent });
                Assert.AreEqual(Visibility.Visible, label.Visibility);
                SemanticZoom(window, 0.5);
                Assert.AreEqual(Visibility.Visible, label.Visibility);
                var screenScale = label.TransformToAncestor(window).TransformBounds(new Rect(0, 0, 1, 1)).Height;
                Assert.IsTrue(label.FontSize * screenScale >= (double)window.FindResource("NetLoom.FontSize.Caption") - 0.05,
                    "The focused link label must stay readable at the far level.");
            });
        }

        // Критерий владельца: на уровне «Издалека» подписи (ярлыки устройств и вкладки размещений) не накладываются.
        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void Sprint49FarLabelsDoNotOverlapAndSelectedLabelStaysVisible()
        {
            var location = Guid.NewGuid();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var cable = Guid.NewGuid();
            // Два устройства стоят рядом: их ярлыки на малом масштабе заведомо пересекаются.
            var map = new MapSnapshot(Now, new[]
            {
                new MapNode("first", "Первый", null, 160, 170, location, deviceId: first),
                new MapNode("second", "Второй", null, 200, 190, location, deviceId: second)
            }, new[]
            {
                new MapLink("cable", "first", "second", "G1/1", "G1/2", MapConfidence.High,
                    MapFreshness.Fresh, new MapEvidenceItem[0], cable)
            }, new[] { new MapLocation(location, null, "Серверная", null) });
            // Позиции сохранены оператором: карта их не разводит (пункт 5), поэтому ярлыки пересекаются.
            var store = new LocationFrameLayoutStore(
                devices: new[]
                {
                    new MapDeviceLayout(first, 160, 170, false),
                    new MapDeviceLayout(second, 200, 190, false)
                },
                locations: new[]
                {
                    new MapLocationLayout(location, 100, 100, 900, 500, false, false)
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
                var states = (IDictionary)typeof(MainWindow).GetField("_linkOperationalStates", LocationFrameFlags).GetValue(window);
                states[cable] = Enum.Parse(states.GetType().GetGenericArguments()[1], "Critical");
                window.ShowMap(map);
                var firstVisual = SemanticVisual(window, "_nodeVisualsByIdentity", "device:" + first.ToString("D"));
                var secondVisual = SemanticVisual(window, "_nodeVisualsByIdentity", "device:" + second.ToString("D"));
                SemanticZoom(window, 0.5);
                Assert.AreEqual(Visibility.Visible,
                    SemanticProperty<System.Windows.Shapes.Path>(firstVisual, "StatusIcon").Visibility);
                Assert.AreEqual(Visibility.Visible,
                    SemanticProperty<System.Windows.Shapes.Path>(secondVisual, "StatusIcon").Visibility);

                // Два близких проблемных устройства: виден ровно один ярлык из двух.
                var firstLabel = SemanticProperty<Border>(firstVisual, "SemanticLabel");
                var secondLabel = SemanticProperty<Border>(secondVisual, "SemanticLabel");
                Assert.AreEqual(1, new[] { firstLabel, secondLabel }.Count(item => item.Visibility == Visibility.Visible));
                AssertNoOverlappingFarLabels(window);

                // Выбранное устройство всегда сохраняет ярлык, даже если раньше проиграло по ключу.
                foreach (var selected in new[] { first, second, first })
                {
                    var border = DeviceBorder(window, selected);
                    border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = border });
                    SemanticZoom(window, 0.5);
                    var selectedLabel = selected == first ? firstLabel : secondLabel;
                    var otherLabel = selected == first ? secondLabel : firstLabel;
                    Assert.AreEqual(Visibility.Visible, selectedLabel.Visibility);
                    Assert.AreEqual(Visibility.Collapsed, otherLabel.Visibility);
                    Assert.AreEqual(0, ((TextBlock)otherLabel.Child).Text.Length);
                    AssertNoOverlappingFarLabels(window);
                }

                // Вне уровня «Издалека» ничего не скрывается этим механизмом.
                SemanticZoom(window, (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom"));
                Assert.AreEqual(Visibility.Collapsed, firstLabel.Visibility);
                Assert.AreEqual(Visibility.Visible, SemanticProperty<Border>(
                    SemanticVisual(window, "_locationVisualsById", location), "Header").Visibility);
            });
        }

        // Видимые ярлыки и вкладки размещений не пересекаются на экране (поле 0).
        private static void AssertNoOverlappingFarLabels(MainWindow window)
        {
            var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
            var items = new System.Collections.Generic.List<Tuple<string, Rect>>();
            foreach (DictionaryEntry pair in (IDictionary)typeof(MainWindow)
                .GetField("_nodeVisualsByIdentity", LocationFrameFlags).GetValue(window))
            {
                var label = SemanticProperty<Border>(pair.Value, "SemanticLabel");
                if (label.Visibility != Visibility.Visible || label.ActualWidth <= 0.0) continue;
                items.Add(Tuple.Create("ярлык «" + ((TextBlock)label.Child).Text + "»",
                    label.TransformToAncestor(viewer).TransformBounds(new Rect(label.RenderSize))));
            }
            foreach (DictionaryEntry pair in (IDictionary)typeof(MainWindow)
                .GetField("_locationVisualsById", LocationFrameFlags).GetValue(window))
            {
                var header = SemanticProperty<Border>(pair.Value, "Header");
                if (header.Visibility != Visibility.Visible || header.ActualWidth <= 0.0) continue;
                items.Add(Tuple.Create("вкладка «" + SemanticProperty<TextBlock>(pair.Value, "Title").Text + "»",
                    header.TransformToAncestor(viewer).TransformBounds(new Rect(header.RenderSize))));
            }
            for (var i = 0; i < items.Count; i++)
                for (var j = i + 1; j < items.Count; j++)
                {
                    var a = items[i].Item2;
                    var b = items[j].Item2;
                    Assert.IsFalse(a.Left < b.Right - 0.01 && b.Left < a.Right - 0.01 &&
                        a.Top < b.Bottom - 0.01 && b.Top < a.Bottom - 0.01,
                        items[i].Item1 + " накладывается на " + items[j].Item1 + ": " + a + " и " + b);
                }
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
