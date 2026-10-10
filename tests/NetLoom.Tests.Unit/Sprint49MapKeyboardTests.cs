using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MapLayout;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    // Sprint 49, пункт 9 (K4, §8): карта с клавиатуры. Окно строится на снимке цепочки из восьми устройств
    // Sprint49NeighborhoodFixture с размещениями; позиции устройств сохранены, поэтому раскладка устойчива.
    public sealed partial class Sprint46ShellFoundationTests
    {
        private const BindingFlags S49KbFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestMethod]
        public void TabEntersMapOnOneStopAndNextTabLeavesIt()
        {
            S49KbWithWindow(new S49KbLayoutStore(), (window, ids, store) =>
            {
                var canvas = (Canvas)window.FindName("MapCanvas");
                var start = (Button)window.FindName("MapFitAllButton");

                // До первого Tab ни один элемент карты не стоит в обходе: вход выбирается в момент нажатия.
                Assert.AreEqual(0, S49KbTabStops(canvas).Count);

                var onMap = S49KbTabToMap(window, start);
                Assert.IsNotNull(onMap, "Tab from the map toolbar must reach the map.");
                Assert.IsTrue(onMap is MapKeyboardBorder && onMap.Tag is Guid,
                    "With nothing selected the entry is a device card.");
                Assert.AreEqual(1, S49KbTabStops(canvas).Count, "The map is one tab stop.");
                Assert.AreSame(onMap, S49KbTabStops(canvas).Single());
                Assert.AreSame(onMap, window.MapKeyboardTabStop);

                S49KbPress(window, Key.Tab);
                var after = Keyboard.FocusedElement as DependencyObject;
                Assert.IsNotNull(after);
                Assert.IsFalse(canvas.IsAncestorOf(after), "The next Tab must leave the map.");
            });
        }

        [TestMethod]
        public void TabEntersMapOnSelectedElement()
        {
            S49KbWithWindow(new S49KbLayoutStore(), (window, ids, store) =>
            {
                var canvas = (Canvas)window.FindName("MapCanvas");
                var start = (Button)window.FindName("MapFitAllButton");
                SelectDevice(window, ids[4]);

                var onMap = S49KbTabToMap(window, start);
                Assert.AreSame(DeviceBorder(window, ids[4]), onMap);

                // Ссылку на связь, размещение и выбор «ничего» тоже обслуживает вход: выбор размещения.
                Keyboard.Focus(start);
                PumpDispatcher();
                S49NavLeftDown(S49NavHeader(S49NavLocationBorder(window, Sprint49NeighborhoodFixture.Child),
                    Sprint49NeighborhoodFixture.Child), 1);
                var onLocation = S49KbTabToMap(window, start);
                Assert.IsNotNull(onLocation);
                Assert.IsTrue(onLocation is MapKeyboardBorder && Equals(onLocation.Tag, Sprint49NeighborhoodFixture.Child));
                Assert.AreEqual(1, S49KbTabStops(canvas).Count);
            });
        }

        [TestMethod]
        public void ArrowsWalkBetweenDevicesAndScrollWithoutChangingZoom()
        {
            S49KbWithWindow(new S49KbLayoutStore(), (window, ids, store) =>
            {
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var canvas = (Canvas)window.FindName("MapCanvas");
                var zoom = NeighborhoodZoom(window);
                var first = DeviceBorder(window, ids[0]);
                Keyboard.Focus(first);
                PumpDispatcher();
                Assert.AreSame(first, window.MapKeyboardTabStop);

                // Вправо: по цепочке (между устройствами могут встать подписи связей) доходим до второго устройства.
                Assert.IsTrue(S49KbPressUntilFocused(window, Key.Right, DeviceBorder(window, ids[1]), 10));
                Assert.AreSame(DeviceBorder(window, ids[1]), window.MapKeyboardTabStop);
                Assert.IsFalse(KeyboardNavigation.GetIsTabStop(first), "Only the current element is a tab stop.");
                Assert.AreEqual(1, S49KbTabStops(canvas).Count);

                // Влево: возвращаемся к первому.
                Assert.IsTrue(S49KbPressUntilFocused(window, Key.Left, first, 10));

                // Правое устройство цепочки лежит за краем области: стрелки выводят его в область без смены масштаба.
                var last = DeviceBorder(window, ids[7]);
                var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                Assert.IsFalse(viewport.Contains(last.TransformToAncestor(viewer).TransformBounds(new Rect(last.RenderSize))),
                    "The last device must start outside the viewport for this check.");
                Assert.IsTrue(S49KbPressUntilFocused(window, Key.Right, last, 40));
                PumpDispatcher();
                window.UpdateLayout();
                viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                Assert.IsTrue(viewport.Contains(last.TransformToAncestor(viewer).TransformBounds(new Rect(last.RenderSize))),
                    "The focused device must be scrolled into view.");
                Assert.IsTrue(viewer.HorizontalOffset > 0.0);
                Assert.AreEqual(zoom, NeighborhoodZoom(window), 0.0001);

                // Дальше вправо ничего нет: клавиша не поглощается (стрелку получает область прокрутки).
                Assert.IsFalse(window.HandleMapElementKey(Key.Right, ModifierKeys.None, false));
            });
        }

        [TestMethod]
        public void FocusedLinkLabelMakesTheLinkFocusedAndEnterSelectsIt()
        {
            S49KbWithWindow(new S49KbLayoutStore(), (window, ids, store) =>
            {
                var canvas = (Canvas)window.FindName("MapCanvas");
                var map = S49NavField<MapSnapshot>(window, "_lastMapSnapshot");
                var minimum = (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom");
                typeof(MainWindow).GetMethod("ChangeZoom", S49KbFlags).Invoke(window, new object[] { minimum + 0.2 });
                PumpDispatcher();
                typeof(MainWindow).GetField("_hoveredPhysicalLinkId", S49KbFlags).SetValue(window, null);

                var labels = canvas.Children.OfType<TextBlock>().Where(label => label.Tag is Guid && label.IsVisible).ToArray();
                Assert.IsTrue(labels.Length >= 2, "Link labels must be visible at the close level.");
                var label0 = labels[0];
                Assert.IsTrue(label0 is MapKeyboardLabel);
                Assert.IsTrue(label0.Focusable);

                Keyboard.Focus(label0);
                PumpDispatcher();
                Assert.AreSame(label0, Keyboard.FocusedElement);
                Assert.AreEqual((Guid)label0.Tag, S49NavField<Guid?>(window, "_keyboardFocusedPhysicalLinkId"));
                Assert.AreEqual(System.Windows.FontWeights.SemiBold, label0.FontWeight);
                var dimmed = (double)window.FindResource("NetLoom.Map.LinkFocusDimmedOpacity");
                foreach (var line in canvas.Children.OfType<Line>().Where(line => line.Tag is Guid && line.IsVisible))
                {
                    if (Equals(line.Tag, label0.Tag))
                        Assert.IsTrue(line.Opacity > dimmed + 0.01, "The focused link must not be dimmed.");
                    else
                        Assert.IsTrue(line.Opacity <= dimmed + 0.0001, "Other links must be dimmed.");
                }

                // Enter выбирает связь: инспектор показывает её, как после щелчка мышью.
                Assert.IsTrue(window.HandleMapElementKey(Key.Enter, ModifierKeys.None, false));
                PumpDispatcher();
                Assert.AreEqual((Guid)label0.Tag, S49NavField<Guid?>(window, "_selectedPhysicalLinkId"));
                Assert.IsNull(S49NavField<Guid?>(window, "_selectedDeviceId"));

                // Фокус ушёл с подписи: связь больше не фокусная по клавиатуре.
                Keyboard.Focus(DeviceBorder(window, ids[0]));
                PumpDispatcher();
                Assert.IsNull(S49NavField<Guid?>(window, "_keyboardFocusedPhysicalLinkId"));
                Assert.IsTrue(map.Links.Count > 0);
            });
        }

        [TestMethod]
        public void EnterAndSpaceSelectDeviceAndLocationLikeAClick()
        {
            S49KbWithWindow(new S49KbLayoutStore(), (window, ids, store) =>
            {
                var title = (TextBlock)window.FindName("DiagnosticElementTitleText");
                Keyboard.Focus(DeviceBorder(window, ids[3]));
                PumpDispatcher();

                Assert.IsTrue(window.HandleMapElementKey(Key.Enter, ModifierKeys.None, false));
                PumpDispatcher();
                Assert.AreEqual(ids[3], S49NavField<Guid?>(window, "_selectedDeviceId"));
                StringAssert.Contains(title.Text, "neighbor-sw-3");
                Assert.AreSame(DeviceBorder(window, ids[3]), Keyboard.FocusedElement, "The focus stays on the card.");

                // Пробел на выбранном элементе остаётся панорамированием, на невыбранном — выбирает.
                Assert.IsFalse(window.HandleMapElementKey(Key.Space, ModifierKeys.None, false));
                Keyboard.Focus(DeviceBorder(window, ids[2]));
                PumpDispatcher();
                Assert.IsTrue(window.HandleMapElementKey(Key.Space, ModifierKeys.None, false));
                PumpDispatcher();
                Assert.AreEqual(ids[2], S49NavField<Guid?>(window, "_selectedDeviceId"));

                var header = S49NavHeader(S49NavLocationBorder(window, Sprint49NeighborhoodFixture.Child),
                    Sprint49NeighborhoodFixture.Child);
                Assert.IsTrue(header.Focusable);
                Keyboard.Focus(header);
                PumpDispatcher();
                Assert.IsTrue(window.HandleMapElementKey(Key.Enter, ModifierKeys.None, false));
                PumpDispatcher();
                Assert.AreEqual(Sprint49NeighborhoodFixture.Child, S49NavField<Guid?>(window, "_selectedLocationId"));
                Assert.IsNull(S49NavField<Guid?>(window, "_selectedDeviceId"));

                // Кнопка сворачивания остаётся обычной кнопкой: Enter и пробел нажимают её сами.
                var toggle = S49NavDescendants(S49NavLocationBorder(window, Sprint49NeighborhoodFixture.Child))
                    .OfType<Button>().First();
                Keyboard.Focus(toggle);
                PumpDispatcher();
                Assert.IsFalse(window.HandleMapElementKey(Key.Enter, ModifierKeys.None, false));
                Assert.IsFalse(window.HandleMapElementKey(Key.Space, ModifierKeys.None, false));
            });
        }

        [TestMethod]
        public void CtrlArrowMovesAndSavesDeviceInEditModeOnly()
        {
            var store = new S49KbLayoutStore();
            S49KbWithWindow(store, (window, ids, layouts) =>
            {
                var border = DeviceBorder(window, ids[1]);
                Keyboard.Focus(border);
                PumpDispatcher();
                var left = Canvas.GetLeft(border);
                var top = Canvas.GetTop(border);
                var step = (double)window.FindResource("NetLoom.Map.KeyboardMoveStep");
                var fine = (double)window.FindResource("NetLoom.Map.KeyboardMoveFineStep");
                Assert.AreEqual(24.0, step);
                Assert.AreEqual(4.0, fine);

                // В режиме просмотра Ctrl+стрелка ничего не двигает и не сохраняет.
                Assert.IsFalse(window.HandleMapElementKey(Key.Right, ModifierKeys.Control, false));
                Assert.AreEqual(left, Canvas.GetLeft(border), 0.001);
                Assert.AreEqual(0, layouts.SavedDevices.Count);

                Click((Button)window.FindName("MapEditModeButton"));
                Keyboard.Focus(border);
                PumpDispatcher();

                Assert.IsTrue(window.HandleMapElementKey(Key.Right, ModifierKeys.Control, false));
                Assert.AreEqual(left + step, Canvas.GetLeft(border), 0.001);
                Assert.AreEqual(1, layouts.SavedDevices.Count);
                Assert.AreEqual(ids[1], layouts.SavedDevices.Last().DeviceId);

                // Узел вплотную вписан в автоматическую рамку по вертикали (правило Sprint 38: не выходит из
                // Размещения), поэтому точный шаг проверяем по горизонтали.
                Assert.IsTrue(window.HandleMapElementKey(Key.Right, ModifierKeys.Control | ModifierKeys.Shift, false));
                Assert.AreEqual(left + step + fine, Canvas.GetLeft(border), 0.001);
                Assert.AreEqual(top, Canvas.GetTop(border), 0.001);
                Assert.AreEqual(2, layouts.SavedDevices.Count);

                // Сохранено то же, что дал бы конец перетаскивания: логические координаты сдвинуты на шаг.
                var originX = S49NavField<double>(window, "_virtualOriginX");
                Assert.AreEqual(Canvas.GetLeft(border) - originX, layouts.SavedDevices.Last().X, 0.001);
            });
        }

        [TestMethod]
        public void LockedDeviceDoesNotMoveWithKeyboard()
        {
            var locked = Sprint49NeighborhoodFixture.Devices[2];
            var store = new S49KbLayoutStore(locked);
            S49KbWithWindow(store, (window, ids, layouts) =>
            {
                Click((Button)window.FindName("MapEditModeButton"));
                var border = DeviceBorder(window, locked);
                Keyboard.Focus(border);
                PumpDispatcher();
                var left = Canvas.GetLeft(border);
                var top = Canvas.GetTop(border);

                // Закреплённый узел не двигается и не сохраняется, как и при перетаскивании мышью.
                Assert.IsTrue(window.HandleMapElementKey(Key.Left, ModifierKeys.Control, false));
                Assert.IsTrue(window.HandleMapElementKey(Key.Up, ModifierKeys.Control, false));
                Assert.AreEqual(left, Canvas.GetLeft(border), 0.001);
                Assert.AreEqual(top, Canvas.GetTop(border), 0.001);
                Assert.AreEqual(0, layouts.SavedDevices.Count);
            });
        }

        [TestMethod]
        public void CtrlArrowMovesLocationTabWithItsDevicesAndSavesThem()
        {
            var store = new S49KbLayoutStore();
            S49KbWithWindow(store, (window, ids, layouts) =>
            {
                Click((Button)window.FindName("MapEditModeButton"));
                var frame = S49NavLocationBorder(window, Sprint49NeighborhoodFixture.Empty);
                var header = S49NavHeader(frame, Sprint49NeighborhoodFixture.Empty);
                var device = DeviceBorder(window, ids[7]);
                Keyboard.Focus(header);
                PumpDispatcher();
                var frameLeft = Canvas.GetLeft(frame);
                var deviceLeft = Canvas.GetLeft(device);
                var step = (double)window.FindResource("NetLoom.Map.KeyboardMoveStep");

                Assert.IsTrue(window.HandleMapElementKey(Key.Left, ModifierKeys.Control, false));
                PumpDispatcher();

                Assert.AreEqual(frameLeft - step, Canvas.GetLeft(frame), 0.001);
                Assert.AreEqual(deviceLeft - step, Canvas.GetLeft(device), 0.001);
                Assert.IsTrue(layouts.SavedLocations.Any(item => item.LocationId == Sprint49NeighborhoodFixture.Empty));
                Assert.IsTrue(layouts.SavedDevices.Any(item => item.DeviceId == ids[7]));
            });
        }

        [TestMethod]
        public void ShiftF10AndMenuKeyOpenContextMenuOfFocusedElement()
        {
            S49KbWithWindow(new S49KbLayoutStore(), (window, ids, store) =>
            {
                var border = DeviceBorder(window, ids[0]);
                Keyboard.Focus(border);
                PumpDispatcher();

                // F10 без Shift меню не открывает.
                Assert.IsFalse(window.HandleMapElementKey(Key.F10, ModifierKeys.None, false));
                Assert.IsFalse(border.ContextMenu.IsOpen);

                Assert.IsTrue(window.HandleMapElementKey(Key.F10, ModifierKeys.Shift, false));
                PumpDispatcher();
                Assert.IsTrue(border.ContextMenu.IsOpen, "Shift+F10 must open the device menu.");
                Assert.AreEqual(ids[0], S49NavField<Guid?>(window, "_selectedDeviceId"), "The menu belongs to the selected element.");
                border.ContextMenu.IsOpen = false;
                PumpDispatcher();

                Keyboard.Focus(border);
                PumpDispatcher();
                Assert.IsTrue(window.HandleMapElementKey(Key.Apps, ModifierKeys.None, false));
                PumpDispatcher();
                Assert.IsTrue(border.ContextMenu.IsOpen, "The menu key must open the device menu.");
                border.ContextMenu.IsOpen = false;
                PumpDispatcher();
            });
        }

        [TestMethod]
        public void MapElementsHaveAutomationNamesAndPeers()
        {
            S49KbWithWindow(new S49KbLayoutStore(), (window, ids, store) =>
            {
                var canvas = (Canvas)window.FindName("MapCanvas");
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                Assert.AreEqual(UiText.Get("MapKeyboardHelp"), AutomationProperties.GetHelpText(viewer));

                var border = DeviceBorder(window, ids[0]);
                var nodeName = AutomationProperties.GetName(border);
                StringAssert.StartsWith(nodeName, "neighbor-sw-0, ");
                var peer = UIElementAutomationPeer.CreatePeerForElement(border);
                Assert.IsNotNull(peer, "A device card needs a UI Automation node.");
                Assert.AreEqual(nodeName, peer.GetName());
                Assert.IsTrue(peer.IsKeyboardFocusable());

                var header = S49NavHeader(S49NavLocationBorder(window, Sprint49NeighborhoodFixture.Child),
                    Sprint49NeighborhoodFixture.Child);
                Assert.AreEqual("Distribution", AutomationProperties.GetName(header));
                Assert.AreEqual("Distribution", UIElementAutomationPeer.CreatePeerForElement(header).GetName());

                var minimum = (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom");
                typeof(MainWindow).GetMethod("ChangeZoom", S49KbFlags).Invoke(window, new object[] { minimum + 0.2 });
                PumpDispatcher();
                var map = S49NavField<NetLoom.Contracts.TopologyMap.MapSnapshot>(window, "_lastMapSnapshot");
                var link = map.Links.First(item => item.SourceNodeKey == ids[0].ToString("D"));
                var label = canvas.Children.OfType<TextBlock>().First(item => Equals(item.Tag, link.PhysicalLinkId.Value));
                Assert.AreEqual(UiText.Format("MapLinkAutomationName", "neighbor-sw-0", "neighbor-sw-1", "Gi0/1 ↔ Gi0/2"),
                    AutomationProperties.GetName(label));
                var labelPeer = UIElementAutomationPeer.CreatePeerForElement(label);
                Assert.IsNotNull(labelPeer);
                Assert.AreEqual(AutomationProperties.GetName(label), labelPeer.GetName());
            });
        }

        // Окно на снимке цепочки с сохранёнными позициями устройств; режим движения выключен, чтобы кадры не плыли.
        private static void S49KbWithWindow(
            S49KbLayoutStore store,
            Action<MainWindow, Guid[], S49KbLayoutStore> body)
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(
                    new FixedRefreshProvider(Sprint49NeighborhoodFixture.Snapshot()),
                    new EmptyLookupReader(),
                    store)
                {
                    Width = 1400.0,
                    Height = 900.0
                };
                try
                {
                    typeof(MainWindow).GetMethod("SetMotionMode", S49KbFlags)
                        .Invoke(window, new object[] { MapMotionMode.Off });
                    window.Show();
                    window.Activate();
                    WaitForCondition(() => DeviceBorder(window, ids[7]) != null &&
                        S49NavField<NetworkDiagnosticSnapshot>(window, "_lastDiagnosticSnapshot") != null);
                    typeof(MainWindow).GetMethod("StopStartupTopologyFit", S49KbFlags).Invoke(window, null);
                    PumpDispatcher();
                    window.UpdateLayout();
                    body(window, ids, store);
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            });
        }

        // Клавиша подаётся как от клавиатуры: PreviewKeyDown через InputManager, фокус берётся из Keyboard.FocusedElement.
        private static void S49KbPress(MainWindow window, Key key)
        {
            var source = PresentationSource.FromVisual(window);
            InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            PumpDispatcher();
        }

        // Tab с кнопки панели инструментов, пока фокус не окажется внутри карты.
        private static FrameworkElement S49KbTabToMap(MainWindow window, Button start)
        {
            var canvas = (Canvas)window.FindName("MapCanvas");
            Keyboard.Focus(start);
            PumpDispatcher();

            for (var press = 0; press < 8; press++)
            {
                S49KbPress(window, Key.Tab);
                var focused = Keyboard.FocusedElement as FrameworkElement;
                if (focused != null && canvas.IsAncestorOf(focused))
                    return focused;
            }

            return null;
        }

        private static bool S49KbPressUntilFocused(MainWindow window, Key key, UIElement expected, int limit)
        {
            for (var press = 0; press < limit; press++)
            {
                S49KbPress(window, key);
                if (ReferenceEquals(Keyboard.FocusedElement, expected))
                    return true;
            }

            return false;
        }

        // Элементы карты, участвующие в обходе Tab.
        private static List<FrameworkElement> S49KbTabStops(Canvas canvas)
        {
            return S49NavDescendants(canvas).OfType<FrameworkElement>()
                .Where(item => (item is MapKeyboardBorder || item is MapKeyboardLabel || item is Button) &&
                    item.Focusable && KeyboardNavigation.GetIsTabStop(item))
                .ToList();
        }

        private sealed class S49KbLayoutStore : IMapLayoutStore, IMapLocationLayoutStore
        {
            private readonly Guid? _lockedDevice;

            public S49KbLayoutStore(Guid? lockedDevice = null)
            {
                _lockedDevice = lockedDevice;
            }

            public List<MapDeviceLayout> SavedDevices { get; } = new List<MapDeviceLayout>();

            public List<MapLocationLayout> SavedLocations { get; } = new List<MapLocationLayout>();

            public MapLayoutSnapshot Load(Guid mapId)
            {
                var devices = Sprint49NeighborhoodFixture.Devices
                    .Select((id, index) => new MapDeviceLayout(id, 100 + index * 450, 100, id == _lockedDevice))
                    .ToArray();
                return new MapLayoutSnapshot(mapId, new MapViewportLayout(1, 0, 0), devices, new MapLocationLayout[0]);
            }

            public void SaveViewport(Guid mapId, MapViewportLayout viewport) { }

            public void SaveDevice(Guid mapId, MapDeviceLayout layout) { SavedDevices.Add(layout); }

            public void SaveLocation(Guid mapId, MapLocationLayout layout) { SavedLocations.Add(layout); }
        }
    }
}
