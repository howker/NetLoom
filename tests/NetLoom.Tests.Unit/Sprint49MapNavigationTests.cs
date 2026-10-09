using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    // Sprint 49, пункт 8: навигация по карте (F, Esc, Alt+←, пробел, двойной щелчок по размещению, путь).
    // Окно строится на снимке цепочки из восьми устройств Sprint49NeighborhoodFixture.
    public sealed partial class Sprint46ShellFoundationTests
    {
        private const BindingFlags S49NavFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestMethod]
        public void FocusKeyFitsSelectedWithNeighborsAndEscapeReturnsPreviousZoomAndOffsets()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var zoomBefore = NeighborhoodZoom(window);
                var offsetXBefore = viewer.HorizontalOffset;
                var offsetYBefore = viewer.VerticalOffset;

                // Ничего не выбрано: F ничего не делает и клавишу не поглощает.
                Assert.IsFalse(S49NavKey(window, Key.F).Handled);
                Assert.AreEqual(0, window.MapViewHistoryCount);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.0001);

                SelectDevice(window, ids[4]);
                var focus = S49NavKey(window, Key.F);
                Assert.IsTrue(focus.Handled);
                Assert.AreEqual(1, window.MapViewHistoryCount);
                var zoomFocus = NeighborhoodZoom(window);
                Assert.IsTrue(zoomFocus > zoomBefore + 0.01, "F must enlarge the view to the selected device and its neighbors.");
                Assert.IsTrue(zoomFocus <= 1.0 + 0.0001);
                var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                foreach (var index in new[] { 3, 4, 5 })
                {
                    var border = DeviceBorder(window, ids[index]);
                    Assert.IsTrue(viewport.Contains(border.TransformToAncestor(viewer)
                        .TransformBounds(new Rect(border.RenderSize))), "The device and its neighbors must fit the viewport.");
                }

                var escape = S49NavKey(window, Key.Escape);
                Assert.IsTrue(escape.Handled);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.001);
                Assert.AreEqual(offsetXBefore, viewer.HorizontalOffset, 1.0);
                Assert.AreEqual(offsetYBefore, viewer.VerticalOffset, 1.0);

                // Вид уже прежний: повторный Esc ведёт себя как раньше и не поглощается.
                Assert.IsFalse(S49NavKey(window, Key.Escape).Handled);
            });
        }

        [TestMethod]
        public void FocusOnLinkFitsBothEndsAndOnLocationFitsItsSubtree()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var zoomBefore = NeighborhoodZoom(window);
                var map = S49NavField<MapSnapshot>(window, "_lastMapSnapshot");
                var link = map.Links.First(item => item.SourceNodeKey == ids[5].ToString("D") ||
                    item.TargetNodeKey == ids[5].ToString("D"));

                var linkLine = ((Canvas)window.FindName("MapCanvas")).Children.OfType<Line>()
                    .First(line => Equals(line.Tag, link.PhysicalLinkId.Value));
                linkLine.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = linkLine
                });
                PumpDispatcher();

                Assert.IsTrue(S49NavKey(window, Key.F).Handled);
                Assert.IsTrue(NeighborhoodZoom(window) > zoomBefore + 0.01);
                var ends = new[] { link.SourceNodeKey, link.TargetNodeKey };
                var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                foreach (var node in map.Nodes.Where(item => ends.Contains(item.Key)))
                {
                    var border = DeviceBorder(window, node.DeviceId.Value);
                    Assert.IsTrue(viewport.Contains(border.TransformToAncestor(viewer)
                        .TransformBounds(new Rect(border.RenderSize))), "Both link ends must fit the viewport.");
                }
                Assert.IsTrue(S49NavKey(window, Key.Escape).Handled);

                // Размещение: его поддерево (рамка и устройства внутри) целиком в области карты.
                var locationBorder = S49NavLocationBorder(window, Sprint49NeighborhoodFixture.Child);
                S49NavLeftDown(S49NavHeader(locationBorder, Sprint49NeighborhoodFixture.Child), 1);
                Assert.IsTrue(S49NavKey(window, Key.F).Handled);
                foreach (var index in new[] { 0, 5 })
                {
                    var border = DeviceBorder(window, ids[index]);
                    Assert.IsTrue(viewport.Contains(border.TransformToAncestor(viewer)
                        .TransformBounds(new Rect(border.RenderSize))), "The location subtree must fit the viewport.");
                }
            });
        }

        [TestMethod]
        public void AltLeftStepsBackThroughTwoActionsOneByOne()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var zoom0 = NeighborhoodZoom(window);

                SelectDevice(window, ids[4]);
                Assert.IsTrue(S49NavKey(window, Key.F).Handled);
                var zoom1 = NeighborhoodZoom(window);
                var offsetX1 = viewer.HorizontalOffset;
                var offsetY1 = viewer.VerticalOffset;
                Assert.IsTrue(Math.Abs(zoom1 - zoom0) > 0.01);

                // Второе действие — окрестность другого устройства.
                SelectDevice(window, ids[1]);
                NeighborhoodMenu(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                PumpDispatcher();
                Assert.AreEqual(2, window.MapViewHistoryCount);
                AssertNeighborhoodDevices(window, 0, 1, 2, 3);

                // Шаг назад: вид после первого действия, окрестность снята.
                Assert.IsTrue(S49NavHandle(window, Key.Left, ModifierKeys.Alt, false, window));
                PumpDispatcher();
                Assert.AreEqual(1, window.MapViewHistoryCount);
                AssertNeighborhoodDevices(window, Enumerable.Range(0, 8).ToArray());
                Assert.AreEqual(zoom1, NeighborhoodZoom(window), 0.001);
                Assert.AreEqual(offsetX1, viewer.HorizontalOffset, 1.0);
                Assert.AreEqual(offsetY1, viewer.VerticalOffset, 1.0);

                // Второй шаг назад: вид до первого действия.
                Assert.IsTrue(S49NavHandle(window, Key.Left, ModifierKeys.Alt, false, window));
                PumpDispatcher();
                Assert.AreEqual(0, window.MapViewHistoryCount);
                Assert.AreEqual(zoom0, NeighborhoodZoom(window), 0.001);

                // История пуста: Alt+← ничего не делает; Left без Alt и Ctrl+Alt+← тоже не затрагивается.
                Assert.IsFalse(S49NavHandle(window, Key.Left, ModifierKeys.Alt, false, window));
                Assert.IsFalse(S49NavHandle(window, Key.Left, ModifierKeys.None, false, window));
                Assert.IsFalse(S49NavHandle(window, Key.Left, ModifierKeys.Alt | ModifierKeys.Control, false, window));
            });
        }

        [TestMethod]
        public void NavigationKeysIgnoreSearchFieldAndPlainKStaysUntouched()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var zoomBefore = NeighborhoodZoom(window);
                SelectDevice(window, ids[4]);

                // Ctrl+K открывает поиск прежним обработчиком (модификатор состояния клавиатуры в тесте не задать),
                // А обычное K окном не перехватывается.
                Assert.IsFalse(S49NavKey(window, Key.K).Handled);

                typeof(MainWindow).GetMethod("FocusAdr083GlobalSearch", S49NavFlags).Invoke(window, null);
                var search = (TextBox)window.FindName("ShellGlobalSearchTextBox");

                // Фокус в поле поиска: F, пробел и Esc принадлежат полю.
                Assert.IsFalse(S49NavHandle(window, Key.F, ModifierKeys.None, false, search));
                Assert.IsFalse(S49NavHandle(window, Key.Space, ModifierKeys.None, false, search));
                Assert.IsFalse(window.IsMapSpacePanActive);
                var typed = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(search),
                    Environment.TickCount, Key.F) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                search.RaiseEvent(typed);
                PumpDispatcher();
                Assert.IsFalse(typed.Handled);
                Assert.AreEqual(0, window.MapViewHistoryCount);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.0001);

                // Вне поля та же клавиша работает.
                Assert.IsTrue(S49NavHandle(window, Key.F, ModifierKeys.None, false, window));
                Assert.AreEqual(1, window.MapViewHistoryCount);

                // Повтор автоклавиши F историю не пополняет, а Ctrl+F и Alt+F вообще не фокус.
                Assert.IsFalse(S49NavHandle(window, Key.F, ModifierKeys.None, true, window));
                Assert.IsFalse(S49NavHandle(window, Key.F, ModifierKeys.Control, false, window));
                Assert.AreEqual(1, window.MapViewHistoryCount);
            });
        }

        [TestMethod]
        public void SpaceWithLeftButtonPansOverNodeWithoutSelectingItAndReleaseRestoresNormalMode()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var border = DeviceBorder(window, ids[2]);
                Assert.IsNull(S49NavField<Guid?>(window, "_selectedDeviceId"));

                Assert.IsTrue(S49NavHandle(window, Key.Space, ModifierKeys.None, false, window));
                Assert.IsTrue(window.IsMapSpacePanActive);
                Assert.AreSame(Cursors.Hand, viewer.Cursor);
                Assert.IsTrue(viewer.ForceCursor);

                // Автоповтор пробела режим не меняет и не прокручивает страницу.
                Assert.IsTrue(S49NavHandle(window, Key.Space, ModifierKeys.None, true, window));
                Assert.IsTrue(window.IsMapSpacePanActive);

                var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent };
                border.RaiseEvent(down);
                PumpDispatcher();
                Assert.IsTrue(down.Handled, "The left button must start panning even over a node.");
                Assert.IsTrue(S49NavField<bool>(window, "_isPanning"));
                Assert.IsNull(S49NavField<Guid?>(window, "_selectedDeviceId"));

                var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseUpEvent };
                viewer.RaiseEvent(up);
                PumpDispatcher();
                Assert.IsFalse(S49NavField<bool>(window, "_isPanning"));
                Assert.AreSame(Cursors.Hand, viewer.Cursor);

                var release = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window),
                    Environment.TickCount, Key.Space) { RoutedEvent = Keyboard.PreviewKeyUpEvent };
                window.RaiseEvent(release);
                PumpDispatcher();
                Assert.IsTrue(release.Handled);
                Assert.IsFalse(window.IsMapSpacePanActive);
                Assert.IsNull(viewer.Cursor);
                Assert.IsFalse(viewer.ForceCursor);

                // Без пробела левая кнопка панорамирование не начинает.
                var plain = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent };
                border.RaiseEvent(plain);
                PumpDispatcher();
                Assert.IsFalse(plain.Handled);
                Assert.IsFalse(S49NavField<bool>(window, "_isPanning"));
            });
        }

        [TestMethod]
        public void SpaceOnFocusedButtonPressesTheButtonInsteadOfPanning()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var button = (Button)window.FindName("MapZoomInButton");
                button.Focus();

                Assert.IsFalse(S49NavHandle(window, Key.Space, ModifierKeys.None, false, button));
                Assert.IsFalse(window.IsMapSpacePanActive);
                Assert.IsNull(((ScrollViewer)window.FindName("MapScrollViewer")).Cursor);

                var press = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(button),
                    Environment.TickCount, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                button.RaiseEvent(press);
                PumpDispatcher();
                Assert.IsFalse(press.Handled, "Space on a button must stay with the button.");
                Assert.IsFalse(window.IsMapSpacePanActive);
            });
        }

        [TestMethod]
        public void DoubleClickOnLocationFitsItInViewModeAndOpensEditorInEditMode()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var zoomBefore = NeighborhoodZoom(window);
                var locationBorder = S49NavLocationBorder(window, Sprint49NeighborhoodFixture.Child);
                var header = S49NavHeader(locationBorder, Sprint49NeighborhoodFixture.Child);

                S49NavLeftDown(header, 1);
                Assert.AreEqual(0, window.MapViewHistoryCount);
                S49NavLeftDown(header, 2);
                Assert.AreEqual(1, window.MapViewHistoryCount);
                Assert.AreEqual(Sprint49NeighborhoodFixture.Child, S49NavField<Guid?>(window, "_selectedLocationId"));
                var zoomFit = NeighborhoodZoom(window);
                Assert.IsTrue(zoomFit > zoomBefore + 0.01 && zoomFit <= 1.0 + 0.0001);
                var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                foreach (var index in new[] { 0, 5 })
                {
                    var border = DeviceBorder(window, ids[index]);
                    Assert.IsTrue(viewport.Contains(border.TransformToAncestor(viewer)
                        .TransformBounds(new Rect(border.RenderSize))), "The location subtree must fit the viewport.");
                }
                Assert.IsTrue(S49NavKey(window, Key.Escape).Handled);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.001);

                // Двойной щелчок по рамке (не по заголовку) в просмотре действует так же.
                S49NavLeftDown(locationBorder, 2);
                Assert.AreEqual(2, window.MapViewHistoryCount);
                Assert.IsTrue(S49NavKey(window, Key.Escape).Handled);

                // В режиме правки двойной щелчок по-прежнему открывает редактор размещения (ADR-080), а не вписывает.
                Click((Button)window.FindName("MapEditModeButton"));
                var host = (ContentControl)window.FindName("ShellWorkspaceEditorHost");
                S49NavLeftDown(header, 2);
                WaitForCondition(() => host.Content != null &&
                    string.Equals("LocationTopologyEditorControl", host.Content.GetType().Name, StringComparison.Ordinal));
                Assert.AreEqual(2, window.MapViewHistoryCount);
            });
        }

        [TestMethod]
        public void EscapeInEditModeLeavesEditModeBeforeItReturnsTheMapView()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var zoomBefore = NeighborhoodZoom(window);
                SelectDevice(window, ids[4]);
                Assert.IsTrue(S49NavKey(window, Key.F).Handled);
                var zoomFocus = NeighborhoodZoom(window);

                Click((Button)window.FindName("MapEditModeButton"));
                Assert.AreEqual("Edit", S49NavField<object>(window, "_mapInteractionMode").ToString());

                // Первый Esc — выход из режима правки, масштаб остаётся.
                Assert.IsTrue(S49NavKey(window, Key.Escape).Handled);
                Assert.AreEqual("View", S49NavField<object>(window, "_mapInteractionMode").ToString());
                Assert.AreEqual(zoomFocus, NeighborhoodZoom(window), 0.0001);

                // Второй Esc — возврат к карте в прежнем масштабе.
                Assert.IsTrue(S49NavKey(window, Key.Escape).Handled);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.001);
            });
        }

        [TestMethod]
        public void ShiftClickOnTwoDevicesHighlightsPathShowsCountAndEscapeResets()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                var canvas = (Canvas)window.FindName("MapCanvas");
                var notice = (Border)window.FindName("MapPathNotice");
                var summary = (TextBlock)window.FindName("MapPathSummaryText");
                var zoomBefore = NeighborhoodZoom(window);
                var offsetXBefore = viewer.HorizontalOffset;
                var offsetYBefore = viewer.VerticalOffset;
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);

                // Первый Shift+щелчок выбирает начало и отмечает его.
                window.HandleMapDeviceShiftClick(ids[0]);
                PumpDispatcher();
                Assert.AreEqual(ids[0], S49NavField<Guid?>(window, "_selectedDeviceId"));
                Assert.IsTrue(window.HasMapPathForTests);
                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual(UiText.Format("MapPathStart", "neighbor-sw-0"), summary.Text);
                Assert.AreEqual(0, S49NavField<HashSet<Guid>>(window, "_pathLinkIds").Count);

                // Второй Shift+щелчок строит путь 0-1-3-4 по трём связям (связь 1-3 короче цепочки).
                window.HandleMapDeviceShiftClick(ids[4]);
                PumpDispatcher();
                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual(UiText.Format("MapPathSummary", "neighbor-sw-0", "neighbor-sw-4",
                    UiText.FormatCount("MapLinkCount", 3)), summary.Text);
                Assert.AreEqual(1, window.MapViewHistoryCount);
                Assert.IsTrue(NeighborhoodZoom(window) > zoomBefore + 0.01);

                var pathLinks = S49NavField<HashSet<Guid>>(window, "_pathLinkIds");
                Assert.AreEqual(3, pathLinks.Count);
                // Реальный указатель может стоять над окном теста — убираем случайное наведение на связь.
                typeof(MainWindow).GetField("_hoveredPhysicalLinkId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, null);
                typeof(MainWindow).GetMethod("ApplyLinkFocusPresentation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                    Type.EmptyTypes, null).Invoke(window, null);
                var dimmed = (double)window.FindResource("NetLoom.Map.LinkFocusDimmedOpacity");
                var lines = canvas.Children.OfType<Line>().Where(line => line.Tag is Guid && line.IsVisible).ToArray();
                Assert.AreEqual(3, lines.Count(line => pathLinks.Contains((Guid)line.Tag)));
                foreach (var line in lines)
                {
                    if (pathLinks.Contains((Guid)line.Tag))
                        Assert.IsTrue(line.Opacity > dimmed + 0.01, "Path links must not be dimmed.");
                    else
                        Assert.IsTrue(line.Opacity <= dimmed + 0.0001, "Other links must be dimmed.");
                }
                var labels = canvas.Children.OfType<TextBlock>()
                    .Where(label => label.Tag is Guid && pathLinks.Contains((Guid)label.Tag)).ToArray();
                Assert.AreEqual(3, labels.Length);
                Assert.IsTrue(labels.All(label => label.IsVisible), "Path link labels must be visible.");

                var focusThickness = (Thickness)window.FindResource("NetLoom.Thickness.BorderFocus");
                foreach (var index in new[] { 0, 1, 3, 4 })
                    Assert.AreEqual(focusThickness, DeviceBorder(window, ids[index]).BorderThickness);
                Assert.AreNotEqual(focusThickness, DeviceBorder(window, ids[2]).BorderThickness);
                var viewport = new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight);
                foreach (var index in new[] { 0, 1, 3, 4 })
                {
                    var border = DeviceBorder(window, ids[index]);
                    Assert.IsTrue(viewport.Contains(border.TransformToAncestor(viewer)
                        .TransformBounds(new Rect(border.RenderSize))), "The whole path must fit the viewport.");
                }

                // Esc сбрасывает путь и возвращает вид.
                Assert.IsTrue(S49NavKey(window, Key.Escape).Handled);
                Assert.IsFalse(window.HasMapPathForTests);
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.001);
                Assert.AreEqual(offsetXBefore, viewer.HorizontalOffset, 1.0);
                Assert.AreEqual(offsetYBefore, viewer.VerticalOffset, 1.0);
                Assert.AreEqual(0, S49NavField<HashSet<Guid>>(window, "_pathLinkIds").Count);
                Assert.AreNotEqual(focusThickness, DeviceBorder(window, ids[1]).BorderThickness);
                foreach (var line in canvas.Children.OfType<Line>().Where(line => line.Tag is Guid && line.IsVisible))
                    Assert.IsTrue(line.Opacity > dimmed + 0.01, "Links must not stay dimmed after the path is reset.");
            });
        }

        [TestMethod]
        public void ResetPathLinkAndPlainClickClearThePath()
        {
            S49NavWithWindow(Sprint49NeighborhoodFixture.Snapshot(), (window, ids) =>
            {
                var notice = (Border)window.FindName("MapPathNotice");
                var reset = (Button)window.FindName("MapPathResetButton");
                var zoomBefore = NeighborhoodZoom(window);

                window.HandleMapDeviceShiftClick(ids[0]);
                window.HandleMapDeviceShiftClick(ids[4]);
                PumpDispatcher();
                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual(UiText.Get("MapPathReset"), reset.Content);

                Click(reset);
                Assert.IsFalse(window.HasMapPathForTests);
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.001);

                // Обычный щелчок без Shift сбрасывает путь, но вид оставляет как есть.
                window.HandleMapDeviceShiftClick(ids[0]);
                window.HandleMapDeviceShiftClick(ids[4]);
                PumpDispatcher();
                Assert.IsTrue(notice.IsVisible);
                var zoomPath = NeighborhoodZoom(window);
                SelectDevice(window, ids[2]);
                Assert.IsFalse(window.HasMapPathForTests);
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Assert.AreEqual(zoomPath, NeighborhoodZoom(window), 0.0001);
                Assert.AreEqual(ids[2], S49NavField<Guid?>(window, "_selectedDeviceId"));

                // Второй Shift+щелчок по тому же устройству путь не строит.
                window.HandleMapDeviceShiftClick(ids[1]);
                window.HandleMapDeviceShiftClick(ids[1]);
                PumpDispatcher();
                Assert.AreEqual(UiText.Format("MapPathStart", "neighbor-sw-1"),
                    ((TextBlock)window.FindName("MapPathSummaryText")).Text);
            });
        }

        [TestMethod]
        public void ShiftClickWithoutKnownPathShowsNeutralTextAndEscapeClearsItInPlace()
        {
            var source = Sprint49NeighborhoodFixture.Snapshot();
            var ids = Sprint49NeighborhoodFixture.Devices;
            // Связь 3-4 убрана: цепочка распадается на две части 0-1-2-3 и 4-5-6-7.
            var removed = source.DiagnosticSnapshot.Links.Single(link =>
                (link.DeviceAId == ids[3] && link.DeviceBId == ids[4]) ||
                (link.DeviceAId == ids[4] && link.DeviceBId == ids[3])).PhysicalLinkId;
            var map = source.MapSnapshot;
            var split = new TopologyRefreshSnapshot(
                new MapSnapshot(DateTime.UtcNow, map.Nodes, map.Links.Where(link => link.PhysicalLinkId != removed),
                    map.Locations),
                source.AlertSnapshot,
                new NetworkDiagnosticSnapshot(DateTime.UtcNow, source.DiagnosticSnapshot.Devices,
                    source.DiagnosticSnapshot.Links.Where(link => link.PhysicalLinkId != removed)));

            S49NavWithWindow(split, (window, devices) =>
            {
                var notice = (Border)window.FindName("MapPathNotice");
                var zoomBefore = NeighborhoodZoom(window);

                window.HandleMapDeviceShiftClick(devices[0]);
                window.HandleMapDeviceShiftClick(devices[6]);
                PumpDispatcher();

                Assert.IsTrue(notice.IsVisible);
                Assert.AreEqual(UiText.Format("MapPathNone", "neighbor-sw-0", "neighbor-sw-6"),
                    ((TextBlock)window.FindName("MapPathSummaryText")).Text);
                Assert.AreEqual(0, S49NavField<HashSet<Guid>>(window, "_pathLinkIds").Count);
                // Пути нет — вид не менялся и в историю ничего не попало.
                Assert.AreEqual(0, window.MapViewHistoryCount);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.0001);

                Assert.IsTrue(S49NavKey(window, Key.Escape).Handled);
                Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
                Assert.IsFalse(window.HasMapPathForTests);
                Assert.AreEqual(zoomBefore, NeighborhoodZoom(window), 0.0001);
            });
        }

        private static T S49NavField<T>(MainWindow window, string name)
        {
            return (T)typeof(MainWindow).GetField(name, S49NavFlags).GetValue(window);
        }

        // Окно на снимке; стартовое вписывание завершено и остановлено, чтобы вид был устойчивым.
        private static void S49NavWithWindow(TopologyRefreshSnapshot snapshot, Action<MainWindow, Guid[]> body)
        {
            RunOnSta(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var ids = Sprint49NeighborhoodFixture.Devices;
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader())
                {
                    Width = 1400.0,
                    Height = 900.0
                };
                try
                {
                    window.Show();
                    var viewer = (ScrollViewer)window.FindName("MapScrollViewer");
                    Func<bool> ready = () => DeviceBorder(window, ids[7]) != null &&
                        S49NavField<NetworkDiagnosticSnapshot>(window, "_lastDiagnosticSnapshot") != null &&
                        new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight).Contains(
                            DeviceBorder(window, ids[7]).TransformToAncestor(viewer)
                                .TransformBounds(new Rect(DeviceBorder(window, ids[7]).RenderSize)));
                    var deadline = DateTime.UtcNow.AddSeconds(12);
                    while (!ready() && DateTime.UtcNow < deadline)
                    {
                        PumpDispatcher();
                        Thread.Sleep(10);
                    }
                    Assert.IsTrue(ready(), "The whole site must be fitted at startup.");
                    typeof(MainWindow).GetMethod("StopStartupTopologyFit", S49NavFlags).Invoke(window, null);
                    PumpDispatcher();
                    body(window, ids);
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            });
        }

        // Клавиша поднимается как событие окна; фокус клавиатуры сброшен, чтобы он не влиял на результат.
        private static KeyEventArgs S49NavKey(MainWindow window, Key key)
        {
            Keyboard.ClearFocus();
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window),
                Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            window.RaiseEvent(args);
            PumpDispatcher();
            return args;
        }

        // Прямой вызов обработчика навигации: Alt и Ctrl состояния клавиатуры в тесте не задать.
        private static bool S49NavHandle(MainWindow window, Key key, ModifierKeys modifiers, bool repeat, object source)
        {
            Keyboard.ClearFocus();
            var handled = window.HandleMapNavigationKey(key, modifiers, repeat, source);
            PumpDispatcher();
            return handled;
        }

        private static void S49NavLeftDown(UIElement target, int clickCount)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = target
            };
            // Число щелчков у синтетического события задаётся закрытым сеттером.
            var setter = typeof(MouseButtonEventArgs).GetProperty("ClickCount").GetSetMethod(true);
            if (setter != null)
                setter.Invoke(args, new object[] { clickCount });
            else
                typeof(MouseButtonEventArgs).GetField("_count", S49NavFlags).SetValue(args, clickCount);
            target.RaiseEvent(args);
            PumpDispatcher();
        }

        private static Border S49NavLocationBorder(MainWindow window, Guid locationId)
        {
            return ((Canvas)window.FindName("MapCanvas")).Children.OfType<Border>()
                .Single(border => Equals(border.Tag, locationId) && Panel.GetZIndex(border) < 0);
        }

        private static Border S49NavHeader(Border locationBorder, Guid locationId)
        {
            return S49NavDescendants(locationBorder).OfType<Border>()
                .First(border => !ReferenceEquals(border, locationBorder) && Equals(border.Tag, locationId));
        }

        private static IEnumerable<DependencyObject> S49NavDescendants(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (var nested in S49NavDescendants(child)) yield return nested;
            }
        }
    }
}
