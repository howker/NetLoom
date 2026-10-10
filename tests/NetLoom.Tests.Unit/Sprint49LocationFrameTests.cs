using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
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
        public void LocationFrameHasOneOutlineAndOpaqueTabWithNeutralCollapseButton()
        {
            var id = Guid.NewGuid();
            var store = new LocationFrameLayoutStore(locations: new[]
            {
                new MapLocationLayout(id, 100, 100, 600, 400, false, false)
            });
            var map = LocationFrameMap(new MapNode[0], new MapLocation(id, null, "Серверная", "Описание комнаты"));
            WithLocationFrameWindow(map, store, window =>
            {
                foreach (var theme in new[] { UiShellTheme.Light, UiShellTheme.Dark })
                {
                    typeof(MainWindow).GetMethod("ApplyShellTheme", LocationFrameFlags).Invoke(window, new object[] { theme });
                    window.ShowMap(map);
                    window.UpdateLayout();
                    var outer = LocationFrameBorder(window, id);
                    var root = (Grid)outer.Child;
                    var frame = root.Children.OfType<Border>().Single(border => border.Child is Border);
                    var tab = root.Children.OfType<Border>().Single(border => border.Child is Grid);
                    var title = ((Grid)tab.Child).Children.OfType<TextBlock>().Single(text => text.Text == "Серверная");
                    var button = ((Grid)tab.Child).Children.OfType<Button>().Single();
                    Assert.AreEqual(new Thickness(0), outer.BorderThickness);
                    Assert.AreEqual((Thickness)window.FindResource("NetLoom.Thickness.BorderThin"), frame.BorderThickness);
                    Assert.AreEqual(outer.ActualWidth, frame.ActualWidth, 0.01);
                    Assert.IsTrue(tab.ActualWidth < frame.ActualWidth);
                    Assert.AreEqual(1.0, outer.Opacity);
                    Assert.AreEqual(1.0, tab.Opacity);
                    Assert.AreEqual(1.0, title.Opacity);
                    Assert.AreEqual((double)window.FindResource("NetLoom.Map.LocationFillOpacity"), ((Border)frame.Child).Opacity);
                    Assert.AreEqual(TextTrimming.CharacterEllipsis, title.TextTrimming);
                    StringAssert.Contains(tab.ToolTip.ToString(), "Серверная");
                    StringAssert.Contains(tab.ToolTip.ToString(), "Описание комнаты");
                    Assert.IsFalse(ConfirmationTextBlocks(outer).Any(text => text.Text == "Описание комнаты"));
                    var accent = ((SolidColorBrush)window.FindResource("NetLoom.Brush.Accent")).Color;
                    Assert.AreNotEqual(accent, ((SolidColorBrush)button.Background).Color);
                    Assert.AreNotEqual(accent, ((SolidColorBrush)button.Foreground).Color);
                    Assert.IsTrue(button.ActualWidth >= 24 && button.ActualHeight >= 24);
                    Assert.AreEqual("Свернуть размещение «Серверная»", AutomationProperties.GetName(button));
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
                    window.UpdateLayout();
                    Assert.AreEqual(Visibility.Collapsed, frame.Visibility);
                    Assert.AreEqual(tab.ActualWidth, outer.ActualWidth, 0.01);
                    Assert.AreEqual((double)window.FindResource("NetLoom.Map.LocationHeaderHeight"), outer.Height);
                    Assert.AreEqual("Развернуть размещение «Серверная»", AutomationProperties.GetName(button));
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
                    window.UpdateLayout();
                    Assert.AreEqual(Visibility.Visible, frame.Visibility);
                    Assert.AreEqual(600.0, outer.Width);
                    Assert.AreEqual(400.0, outer.Height);
                }
            });
        }

        [TestMethod]
        public void NewNodeAvoidsPersistedNodeRegardlessOfSnapshotOrderWithoutSaving()
        {
            var savedId = Guid.NewGuid();
            var newId = Guid.NewGuid();
            var store = new LocationFrameLayoutStore(devices: new[] { new MapDeviceLayout(savedId, 100, 100, false) });
            // Новый узел намеренно идёт первым; сохранённый резервирует свою позицию заранее.
            var map = LocationFrameMap(new[]
            {
                new MapNode("new", "Новый", null, 100, 100, deviceId: newId),
                new MapNode("saved", "Сохранённый", null, 900, 900, deviceId: savedId)
            });
            WithLocationFrameWindow(map, store, window =>
            {
                var saved = DeviceBorder(window, savedId);
                var added = DeviceBorder(window, newId);
                var before = LocationFrameBounds(saved);
                var addedBefore = LocationFrameBounds(added);
                Assert.AreEqual(100.0, before.X - (double)TopologyQualityField(window, "_virtualOriginX"), 0.001);
                Assert.AreEqual(100.0, before.Y - (double)TopologyQualityField(window, "_virtualOriginY"), 0.001);
                var intersection = Rect.Intersect(before, addedBefore);
                Assert.IsTrue(intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0);
                window.ShowMap(map);
                window.UpdateLayout();
                Assert.AreEqual(before, LocationFrameBounds(saved));
                Assert.AreEqual(addedBefore, LocationFrameBounds(added));
                Assert.AreEqual(0, store.DeviceWrites);
                Assert.AreEqual(0, store.LocationWrites);
            });
        }

        [TestMethod]
        public void SavedOverlapsAreReportedAndShownWithoutChangingGeometry()
        {
            var first = Guid.Parse("49494949-0005-0001-0000-000000000001");
            var second = Guid.Parse("49494949-0005-0001-0000-000000000002");
            var store = new LocationFrameLayoutStore(locations: new[]
            {
                new MapLocationLayout(first, 100, 100, 600, 400, false, false),
                new MapLocationLayout(second, 200, 200, 600, 400, false, false)
            });
            var map = LocationFrameMap(new MapNode[0], new MapLocation(first, null, "А", null),
                new MapLocation(second, null, "Б", null));
            WithLocationFrameWindow(map, store, window =>
            {
                var firstBefore = LocationFrameBounds(LocationFrameBorder(window, first));
                var secondBefore = LocationFrameBounds(LocationFrameBorder(window, second));
                Assert.AreEqual("Топология неполная: 1", ((TextBlock)window.FindName("MapQualitySummaryText")).Text);
                Assert.AreEqual(Visibility.Visible, ((Border)window.FindName("MapQualityNotice")).Visibility);
                var toggle = (ToggleButton)window.FindName("MapQualityToggle");
                toggle.IsChecked = true;
                window.UpdateLayout();
                var items = (ItemsControl)window.FindName("MapQualityItems");
                Assert.AreEqual(1, items.Items.Count);
                var texts = ConfirmationTextBlocks(items).Select(text => text.Text).ToArray();
                CollectionAssert.Contains(texts, "А");
                Assert.IsTrue(texts.Any(text => text.Contains("Накладывается на размещение «Б»")));
                var show = TopologyQualityVisualButtons(items).Single(button =>
                    ((TopologyQualityItem)button.Tag).Reasons.Any(reason =>
                        reason.Kind == TopologyQualityGapKind.LocationOverlap));
                var item = (TopologyQualityItem)show.Tag;
                Assert.AreEqual(first, item.LocationId);
                Assert.AreEqual(second, item.OtherLocationId);
                Assert.AreEqual(1, item.Reasons.Count);
                show.Focus();
                show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, show));
                PumpDispatcher();
                Assert.AreEqual(first, TopologyQualityField(window, "_selectedLocationId"));
                Assert.AreSame(toggle, Keyboard.FocusedElement);
                Assert.AreEqual(false, toggle.IsChecked);
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("MapQualityDetails")).Visibility);
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                foreach (var id in new[] { first, second })
                {
                    var border = LocationFrameBorder(window, id);
                    var visible = border.TransformToAncestor(viewer).TransformBounds(new Rect(border.RenderSize));
                    Assert.IsTrue(visible.Left >= -1 && visible.Top >= -1 &&
                        visible.Right <= viewer.ActualWidth + 1 && visible.Bottom <= viewer.ActualHeight + 1);
                }
                window.ShowMap(map);
                window.UpdateLayout();
                Assert.AreEqual(firstBefore, LocationFrameBounds(LocationFrameBorder(window, first)));
                Assert.AreEqual(secondBefore, LocationFrameBounds(LocationFrameBorder(window, second)));
                Assert.AreEqual(0, store.LocationWrites);
                Assert.AreEqual(0, store.DeviceWrites);
            });
        }

        [TestMethod]
        public void NewSiblingAvoidsSavedFrameWithoutSavingOrMovingIt()
        {
            var savedId = Guid.NewGuid();
            var newId = Guid.NewGuid();
            var store = new LocationFrameLayoutStore(locations: new[]
            {
                new MapLocationLayout(savedId, -260, -180, 520, 360, false, false)
            });
            var map = LocationFrameMap(new MapNode[0], new MapLocation(newId, null, "А", null),
                new MapLocation(savedId, null, "Б", null));
            WithLocationFrameWindow(map, store, window =>
            {
                var saved = LocationFrameBounds(LocationFrameBorder(window, savedId));
                var added = LocationFrameBounds(LocationFrameBorder(window, newId));
                var intersection = Rect.Intersect(saved, added);
                Assert.IsTrue(intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0);
                Assert.AreEqual(-260.0, saved.X - (double)TopologyQualityField(window, "_virtualOriginX"), 0.001);
                Assert.AreEqual(-180.0, saved.Y - (double)TopologyQualityField(window, "_virtualOriginY"), 0.001);
                Assert.AreEqual(0, store.LocationWrites);
            });
        }

        [TestMethod]
        public void SavedHierarchyAndDeviceGeometryIsNotNormalizedOnRefresh()
        {
            var parent = Guid.NewGuid();
            var child = Guid.NewGuid();
            var device = Guid.NewGuid();
            var store = new LocationFrameLayoutStore(
                devices: new[] { new MapDeviceLayout(device, 1200, 900, false) },
                locations: new[]
                {
                    new MapLocationLayout(parent, 100, 100, 320, 240, false, false),
                    new MapLocationLayout(child, -900, -700, 280, 220, false, false)
                });
            var map = LocationFrameMap(new[]
            {
                new MapNode("saved", "Сохранённый", null, 0, 0, child, deviceId: device)
            }, new MapLocation(parent, null, "Корпус", null), new MapLocation(child, parent, "Комната", null));
            WithLocationFrameWindow(map, store, window =>
            {
                var parentBefore = LocationFrameBounds(LocationFrameBorder(window, parent));
                var childBefore = LocationFrameBounds(LocationFrameBorder(window, child));
                var deviceBefore = LocationFrameBounds(DeviceBorder(window, device));
                var originX = (double)TopologyQualityField(window, "_virtualOriginX");
                var originY = (double)TopologyQualityField(window, "_virtualOriginY");
                Assert.AreEqual(new Rect(originX + 100, originY + 100, 320, 240), parentBefore);
                Assert.AreEqual(new Rect(originX - 900, originY - 700, 280, 220), childBefore);
                Assert.AreEqual(1200.0, deviceBefore.X - originX, 0.001);
                Assert.AreEqual(900.0, deviceBefore.Y - originY, 0.001);
                window.ShowMap(map);
                window.UpdateLayout();
                Assert.AreEqual(parentBefore, LocationFrameBounds(LocationFrameBorder(window, parent)));
                Assert.AreEqual(childBefore, LocationFrameBounds(LocationFrameBorder(window, child)));
                Assert.AreEqual(deviceBefore, LocationFrameBounds(DeviceBorder(window, device)));
                Assert.AreEqual(0, store.LocationWrites);
                Assert.AreEqual(0, store.DeviceWrites);
            });
        }

        [TestMethod]
        public void EditResizeAndDragRecalculateOverlapNotice()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var store = new LocationFrameLayoutStore(locations: new[]
            {
                new MapLocationLayout(first, 100, 100, 600, 400, false, false),
                new MapLocationLayout(second, 800, 100, 400, 400, false, false)
            });
            var map = LocationFrameMap(new MapNode[0], new MapLocation(first, null, "А", null),
                new MapLocation(second, null, "Б", null));
            WithLocationFrameWindow(map, store, window =>
            {
                Click((Button)window.FindName("MapEditModeButton"));
                window.UpdateLayout();
                var firstBorder = LocationFrameBorder(window, first);
                var thumb = ((Grid)firstBorder.Child).Children.OfType<Thumb>().Single();
                Assert.AreEqual(Visibility.Visible, thumb.Visibility);
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("MapQualityNotice")).Visibility);
                thumb.RaiseEvent(new DragDeltaEventArgs(200, 0) { RoutedEvent = Thumb.DragDeltaEvent });
                window.UpdateLayout();
                Assert.AreEqual(800.0, firstBorder.Width);
                Assert.AreEqual(Visibility.Visible, ((Border)window.FindName("MapQualityNotice")).Visibility);
                var secondBorder = LocationFrameBorder(window, second);
                typeof(MainWindow).GetField("_locationDragStartLeft", LocationFrameFlags)
                    .SetValue(window, Canvas.GetLeft(secondBorder));
                typeof(MainWindow).GetField("_locationDragStartTop", LocationFrameFlags)
                    .SetValue(window, Canvas.GetTop(secondBorder));
                var visual = typeof(MainWindow).GetMethod("LocationVisual", LocationFrameFlags)
                    .Invoke(window, new object[] { second });
                typeof(MainWindow).GetMethod("ApplyLocationDrag", LocationFrameFlags).Invoke(window,
                    new[] { visual, (object)(Canvas.GetLeft(firstBorder) + 1000), Canvas.GetTop(secondBorder) });
                window.UpdateLayout();
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("MapQualityNotice")).Visibility);
                Assert.AreEqual(0, store.LocationWrites);
            });
        }

        private const BindingFlags LocationFrameFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static MapSnapshot LocationFrameMap(MapNode[] nodes, params MapLocation[] locations)
        {
            return new MapSnapshot(Sprint49TopologyQualityFixture.Now, nodes, new MapLink[0], locations);
        }

        private static void WithLocationFrameWindow(MapSnapshot map, LocationFrameLayoutStore store,
            Action<MainWindow> action)
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var snapshot = new TopologyRefreshSnapshot(map,
                    new TopologyAlertSnapshot(map.GeneratedUtc, "cist", new TopologyAlert[0]));
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader(), store)
                {
                    Width = 1100,
                    Height = 900
                };
                try
                {
                    typeof(MainWindow).GetMethod("SetMotionMode", LocationFrameFlags)
                        .Invoke(window, new object[] { MapMotionMode.Off });
                    window.Show();
                    WaitForCondition(() => ReferenceEquals(map, TopologyQualityField(window, "_lastMapSnapshot")));
                    window.UpdateLayout();
                    action(window);
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            });
        }

        private static Border LocationFrameBorder(MainWindow window, Guid id)
        {
            return ((Canvas)window.FindName("MapCanvas")).Children.OfType<Border>()
                .Single(border => Equals(border.Tag, id) && border.Child is Grid);
        }

        private static Rect LocationFrameBounds(Border border)
        {
            return new Rect(Canvas.GetLeft(border), Canvas.GetTop(border), border.ActualWidth, border.ActualHeight);
        }

        private sealed class LocationFrameLayoutStore : IMapLayoutStore, IMapLocationLayoutStore
        {
            private readonly MapDeviceLayout[] _devices;
            private readonly MapLocationLayout[] _locations;
            public LocationFrameLayoutStore(MapDeviceLayout[] devices = null, MapLocationLayout[] locations = null)
            {
                _devices = devices ?? new MapDeviceLayout[0];
                _locations = locations ?? new MapLocationLayout[0];
            }
            public int DeviceWrites { get; private set; }
            public int LocationWrites { get; private set; }
            public MapLayoutSnapshot Load(Guid mapId)
            {
                return new MapLayoutSnapshot(mapId, new MapViewportLayout(1, 0, 0), _devices, _locations);
            }
            public void SaveViewport(Guid mapId, MapViewportLayout viewport) { }
            public void SaveDevice(Guid mapId, MapDeviceLayout layout) { DeviceWrites++; }
            public void SaveLocation(Guid mapId, MapLocationLayout layout) { LocationWrites++; }
        }
    }
}
