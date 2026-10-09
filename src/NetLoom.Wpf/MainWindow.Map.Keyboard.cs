using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;
using NetLoom.Wpf.Shell;

namespace NetLoom.Wpf;

// Sprint 49, K4 (UI_DESIGN_RULES §8): карта с клавиатуры.
// Карта — составной элемент и одна остановка Tab (MapCanvas: TabNavigation=Once). Остановка перемещаемая:
// IsTabStop=true только у «текущего» элемента (roving tabindex), остальные Focusable, но не в обходе Tab.
// Стрелки переходят к ближайшему видимому элементу (MapSpatialNavigation), Enter и пробел выбирают,
// Shift+F10 и клавиша меню открывают контекстное меню, Ctrl+стрелка в режиме правки сдвигает узел или вкладку.
public partial class MainWindow
{
    private enum MapKeyboardTargetKind
    {
        Node = 0,
        Link = 1,
        Location = 2,
        Toggle = 3
    }

    // Элемент карты, доступный с клавиатуры: ключ, вид, сам элемент и его прямоугольник в координатах холста.
    private sealed class MapKeyboardTarget
    {
        public MapKeyboardTarget(
            string key,
            MapKeyboardTargetKind kind,
            FrameworkElement element,
            Rect bounds)
        {
            Key = key;
            Kind = kind;
            Element = element;
            Bounds = bounds;
        }

        public string Key { get; }

        public MapKeyboardTargetKind Kind { get; }

        public FrameworkElement Element { get; }

        public Rect Bounds { get; }

        public MapNodeVisual Node { get; set; }

        public MapLinkVisual Link { get; set; }

        public MapLocationVisual Location { get; set; }
    }

    // Единственный элемент карты, который сейчас участвует в обходе Tab.
    private FrameworkElement _mapTabStop;

    // Tab с клавиатуры входит на карту снаружи: первый фокус внутри холста направляется на нужный элемент.
    private bool _mapTabEntryPending;

    private bool _mapEntryRedirecting;

    // Элемент карты, который сейчас единственная остановка Tab (для тестов).
    internal FrameworkElement MapKeyboardTabStop
    {
        get { return _mapTabStop; }
    }

    private void InitializeMapKeyboard()
    {
        AutomationProperties.SetHelpText(
            MapScrollViewer,
            UiText.Get("MapKeyboardHelp"));
    }

    // Подпись связи масштабируется обратно (ниже 100 %), поэтому кольцу нужны обычные экранные значения,
    // А не толщина, делённая на масштаб карты (UpdateMapFocusRingScale).
    private void UpdateLinkLabelFocusRing(TextBlock label)
    {
        const string ringKey = "NetLoom.Thickness.MapFocusRing";
        const string offsetKey = "NetLoom.Thickness.MapFocusRingOffset";

        if (_zoom > 0.0 && _zoom < 1.0)
        {
            if (!label.Resources.Contains(ringKey))
            {
                label.Resources[ringKey] = GetThicknessResource("NetLoom.Thickness.FocusRing");
                label.Resources[offsetKey] = GetThicknessResource("NetLoom.Thickness.FocusRingOffset");
            }
        }
        else if (label.Resources.Contains(ringKey))
        {
            label.Resources.Remove(ringKey);
            label.Resources.Remove(offsetKey);
        }
    }

    // Сбор элементов карты.

    private bool IsMapCanvasDescendant(DependencyObject element)
    {
        var current = element;

        while (current != null)
        {
            if (ReferenceEquals(current, MapCanvas))
            {
                return true;
            }

            current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool TryGetMapElementBounds(FrameworkElement element, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (element == null ||
            !element.IsVisible ||
            element.ActualWidth <= 0.0 ||
            element.ActualHeight <= 0.0)
        {
            return false;
        }

        try
        {
            bounds = element
                .TransformToAncestor(MapCanvas)
                .TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // Видимые элементы карты: карточки устройств, подписи видимых связей, вкладки размещений и их кнопки.
    private List<MapKeyboardTarget> CollectMapKeyboardTargets()
    {
        var targets = new List<MapKeyboardTarget>();
        Rect bounds;

        foreach (var pair in _nodeVisualsByIdentity)
        {
            if (TryGetMapElementBounds(pair.Value.Border, out bounds))
            {
                targets.Add(new MapKeyboardTarget(
                    "node:" + pair.Key,
                    MapKeyboardTargetKind.Node,
                    pair.Value.Border,
                    bounds)
                {
                    Node = pair.Value
                });
            }
        }

        foreach (var pair in _linkVisualsByIdentity)
        {
            if (pair.Value.Line.Tag is Guid &&
                TryGetMapElementBounds(pair.Value.Label, out bounds))
            {
                targets.Add(new MapKeyboardTarget(
                    "link:" + pair.Key,
                    MapKeyboardTargetKind.Link,
                    pair.Value.Label,
                    bounds)
                {
                    Link = pair.Value
                });
            }
        }

        foreach (var pair in _locationVisualsById)
        {
            if (TryGetMapElementBounds(pair.Value.Header, out bounds))
            {
                targets.Add(new MapKeyboardTarget(
                    "location:" + pair.Key.ToString("N"),
                    MapKeyboardTargetKind.Location,
                    pair.Value.Header,
                    bounds)
                {
                    Location = pair.Value
                });
            }

            if (TryGetMapElementBounds(pair.Value.CollapseButton, out bounds))
            {
                targets.Add(new MapKeyboardTarget(
                    "toggle:" + pair.Key.ToString("N"),
                    MapKeyboardTargetKind.Toggle,
                    pair.Value.CollapseButton,
                    bounds)
                {
                    Location = pair.Value
                });
            }
        }

        return targets;
    }

    // Соседи текущего элемента: узел — связанные узлы и подписи своих связей, связь — оба конца,
    // Вкладка — устройства своего размещения. При равной оценке перехода они выигрывают.
    private HashSet<string> MapKeyboardRelatedKeys(MapKeyboardTarget current)
    {
        var related = new HashSet<string>(StringComparer.Ordinal);

        if (_lastMapSnapshot == null)
        {
            return related;
        }

        var identityByKey = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var node in _lastMapSnapshot.Nodes)
        {
            identityByKey[node.Key] = NodeIdentity(node);
        }

        string identity;

        switch (current.Kind)
        {
            case MapKeyboardTargetKind.Node:
                var nodeKey = current.Node.Node == null ? null : current.Node.Node.Key;

                if (nodeKey == null)
                {
                    break;
                }

                foreach (var link in _lastMapSnapshot.Links)
                {
                    var other = string.Equals(link.SourceNodeKey, nodeKey, StringComparison.Ordinal)
                        ? link.TargetNodeKey
                        : string.Equals(link.TargetNodeKey, nodeKey, StringComparison.Ordinal)
                            ? link.SourceNodeKey
                            : null;

                    if (other == null)
                    {
                        continue;
                    }

                    if (identityByKey.TryGetValue(other, out identity))
                    {
                        related.Add("node:" + identity);
                    }

                    related.Add("link:" + LinkIdentity(link));
                }

                break;

            case MapKeyboardTargetKind.Link:
                var shown = current.Link.Link;

                if (shown == null)
                {
                    break;
                }

                if (identityByKey.TryGetValue(shown.SourceNodeKey, out identity))
                {
                    related.Add("node:" + identity);
                }

                if (identityByKey.TryGetValue(shown.TargetNodeKey, out identity))
                {
                    related.Add("node:" + identity);
                }

                break;

            case MapKeyboardTargetKind.Location:
            case MapKeyboardTargetKind.Toggle:
                foreach (var node in _lastMapSnapshot.Nodes)
                {
                    if (node.LocationId == current.Location.LocationId)
                    {
                        related.Add("node:" + NodeIdentity(node));
                    }
                }

                break;
        }

        return related;
    }

    // Вход на карту по Tab.

    private Point MapViewportCenterInCanvas()
    {
        var center = new Point(
            MapScrollViewer.ViewportWidth / 2.0,
            MapScrollViewer.ViewportHeight / 2.0);

        try
        {
            return MapScrollViewer.TransformToDescendant(MapCanvas).Transform(center);
        }
        catch (InvalidOperationException)
        {
            return center;
        }
    }

    // Выбранный элемент, а если ничего не выбрано или он не виден — узел ближе всего к центру видимой области.
    private MapKeyboardTarget ChooseMapEntryTarget(IReadOnlyList<MapKeyboardTarget> targets)
    {
        MapKeyboardTarget selected = null;

        if (_selectedDeviceId.HasValue)
        {
            selected = targets.FirstOrDefault(
                item => item.Kind == MapKeyboardTargetKind.Node &&
                        item.Node.DeviceId == _selectedDeviceId);
        }
        else if (_selectedPhysicalLinkId.HasValue)
        {
            selected = targets.FirstOrDefault(
                item => item.Kind == MapKeyboardTargetKind.Link &&
                        Equals(item.Link.Line.Tag, _selectedPhysicalLinkId.Value));
        }
        else if (_selectedLocationId.HasValue)
        {
            selected = targets.FirstOrDefault(
                item => item.Kind == MapKeyboardTargetKind.Location &&
                        item.Location.LocationId == _selectedLocationId.Value);
        }

        if (selected != null)
        {
            return selected;
        }

        var center = MapViewportCenterInCanvas();

        Func<MapKeyboardTarget, double> distance = item =>
        {
            var itemCenter = new Point(
                item.Bounds.Left + (item.Bounds.Width / 2.0),
                item.Bounds.Top + (item.Bounds.Height / 2.0));

            return (itemCenter - center).Length;
        };

        return targets
                   .Where(item => item.Kind == MapKeyboardTargetKind.Node)
                   .OrderBy(distance)
                   .FirstOrDefault() ??
               targets
                   .Where(item => item.Kind == MapKeyboardTargetKind.Location)
                   .OrderBy(distance)
                   .FirstOrDefault();
    }

    private void SetMapTabStop(FrameworkElement element)
    {
        if (!ReferenceEquals(_mapTabStop, element) && _mapTabStop != null)
        {
            KeyboardNavigation.SetIsTabStop(_mapTabStop, false);
        }

        _mapTabStop = element;

        if (element != null)
        {
            KeyboardNavigation.SetIsTabStop(element, true);
        }

        // TabNavigation="Once": WPF входит в группу через запомненный «активный» элемент; если там остался
        // Прежний элемент, переставший быть остановкой, Tab проскакивает карту.
        // Свойство внутреннее (одинаково в .NET Framework 4.8 и .NET 8); нет его — остаётся поведение WPF.
        var activeElement = typeof(KeyboardNavigation).GetField(
            "TabOnceActiveElementProperty",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
            .GetValue(null) as DependencyProperty;
        if (activeElement != null)
        {
            MapCanvas.SetValue(activeElement, element == null ? null : new WeakReference(element));
        }
    }

    // Tab снаружи карты: до разбора клавиши выбираем единственную остановку, на которую придёт фокус.
    private void PrepareMapTabEntry()
    {
        if (_shellSection != ShellSection.Map)
        {
            return;
        }

        var focused = Keyboard.FocusedElement as DependencyObject;

        if (focused != null && IsMapCanvasDescendant(focused))
        {
            return;
        }

        var entry = ChooseMapEntryTarget(CollectMapKeyboardTargets());

        if (entry == null)
        {
            return;
        }

        SetMapTabStop(entry.Element);

        // Флаг живёт только на время обработки этого нажатия.
        _mapTabEntryPending = true;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => _mapTabEntryPending = false));
    }

    private void OnMapCanvasPreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_mapTabEntryPending || _mapEntryRedirecting)
        {
            return;
        }

        var newFocus = e.NewFocus as DependencyObject;
        var oldFocus = e.OldFocus as DependencyObject;

        if (newFocus == null ||
            !IsMapCanvasDescendant(newFocus) ||
            (oldFocus != null && IsMapCanvasDescendant(oldFocus)))
        {
            return;
        }

        _mapTabEntryPending = false;

        var entry = ChooseMapEntryTarget(CollectMapKeyboardTargets());

        if (entry == null || ReferenceEquals(entry.Element, newFocus))
        {
            return;
        }

        // WPF мог вернуть последний элемент группы; вход на карту всегда на выбранном или центральном.
        e.Handled = true;
        _mapEntryRedirecting = true;

        try
        {
            SetMapTabStop(entry.Element);
            Keyboard.Focus(entry.Element);
        }
        finally
        {
            _mapEntryRedirecting = false;
        }
    }

    private void OnMapCanvasGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var element = e.NewFocus as FrameworkElement;

        if (element == null || !IsMapCanvasDescendant(element))
        {
            return;
        }

        if (element is MapKeyboardBorder || element is MapKeyboardLabel || element is Button)
        {
            SetMapTabStop(element);
        }

        var label = element as MapKeyboardLabel;

        if (label != null && label.Tag is Guid)
        {
            // Связь с подписью в фокусе становится фокусной, как по наведению (правило пункта 2).
            _keyboardFocusedPhysicalLinkId = (Guid)label.Tag;
            ApplyLinkFocusPresentation();
        }

        ScrollFocusedMapElementIntoViewLater(element);
    }

    private void OnMapCanvasLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!(e.OldFocus is MapKeyboardLabel) ||
            e.NewFocus is MapKeyboardLabel ||
            !_keyboardFocusedPhysicalLinkId.HasValue)
        {
            return;
        }

        _keyboardFocusedPhysicalLinkId = null;
        ApplyLinkFocusPresentation();
    }

    // Клавиши.

    private static bool TryMapArrowDirection(Key key, out MapNavigationDirection direction)
    {
        direction = MapNavigationDirection.Left;

        switch (key)
        {
            case Key.Left:
                direction = MapNavigationDirection.Left;
                return true;
            case Key.Right:
                direction = MapNavigationDirection.Right;
                return true;
            case Key.Up:
                direction = MapNavigationDirection.Up;
                return true;
            case Key.Down:
                direction = MapNavigationDirection.Down;
                return true;
            default:
                return false;
        }
    }

    // Обработка клавиш на элементе карты в фокусе. Возвращает true, если клавиша обработана.
    // Вызывается из PreviewKeyDown окна раньше навигации по карте: пробел на элементе выбирает его, а не панорамирует.
    internal bool HandleMapElementKey(Key key, ModifierKeys modifiers, bool isRepeat)
    {
        if (_shellSection != ShellSection.Map ||
            _activeTopologyEditor != null ||
            ShellGlobalSearchPopup.IsOpen)
        {
            return false;
        }

        var focused = Keyboard.FocusedElement as FrameworkElement;

        if (focused == null || !IsMapCanvasDescendant(focused))
        {
            return false;
        }

        var targets = CollectMapKeyboardTargets();
        var current = targets.FirstOrDefault(item => ReferenceEquals(item.Element, focused));

        if (current == null)
        {
            return false;
        }

        var control = (modifiers & ModifierKeys.Control) != ModifierKeys.None;
        var shift = (modifiers & ModifierKeys.Shift) != ModifierKeys.None;
        var alt = (modifiers & ModifierKeys.Alt) != ModifierKeys.None;
        MapNavigationDirection direction;

        if (TryMapArrowDirection(key, out direction))
        {
            if (alt)
            {
                return false;
            }

            return control
                ? MoveMapElementWithKeyboard(current, direction, shift)
                : NavigateMapByArrow(current, targets, direction);
        }

        switch (key)
        {
            case Key.Enter:
            case Key.Space:
                // На кнопке сворачивания Enter и пробел нажимают саму кнопку.
                if (control || alt || shift || current.Kind == MapKeyboardTargetKind.Toggle)
                {
                    return false;
                }

                // Пробел на уже выбранном элементе (например, после щелчка мышью) остаётся панорамированием.
                if (key == Key.Space && IsMapTargetSelected(current))
                {
                    return false;
                }

                return isRepeat || ActivateMapTarget(current);

            case Key.F10:
                return shift && !control && !alt && OpenMapContextMenu(current);

            case Key.Apps:
                return OpenMapContextMenu(current);

            default:
                return false;
        }
    }

    private bool IsMapTargetSelected(MapKeyboardTarget target)
    {
        switch (target.Kind)
        {
            case MapKeyboardTargetKind.Node:
                return target.Node.DeviceId.HasValue &&
                       target.Node.DeviceId == _selectedDeviceId;
            case MapKeyboardTargetKind.Link:
                return target.Link.Line.Tag is Guid &&
                       _selectedPhysicalLinkId.HasValue &&
                       (Guid)target.Link.Line.Tag == _selectedPhysicalLinkId.Value;
            case MapKeyboardTargetKind.Location:
                return _selectedLocationId.HasValue &&
                       target.Location.LocationId == _selectedLocationId.Value;
            default:
                return false;
        }
    }

    private bool NavigateMapByArrow(
        MapKeyboardTarget current,
        IReadOnlyList<MapKeyboardTarget> targets,
        MapNavigationDirection direction)
    {
        var next = MapSpatialNavigation.Next(
            new MapSpatialItem(current.Key, current.Bounds),
            targets.Select(item => new MapSpatialItem(item.Key, item.Bounds)),
            direction,
            MapKeyboardRelatedKeys(current));

        if (next == null)
        {
            // Дальше в этом направлении ничего нет: стрелку получает область прокрутки.
            return false;
        }

        var target = targets.First(item => string.Equals(item.Key, next.Key, StringComparison.Ordinal));

        ScrollMapElementIntoView(target.Element);
        SetMapTabStop(target.Element);
        Keyboard.Focus(target.Element);
        return true;
    }

    // Прокручивает карту (без смены масштаба), чтобы элемент стал виден целиком, насколько позволяет область.
    private void ScrollMapElementIntoView(FrameworkElement element)
    {
        Rect rect;

        try
        {
            rect = element
                .TransformToAncestor(MapScrollViewer)
                .TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var margin = GetDoubleResource("NetLoom.Map.KeyboardScrollMargin");
        var deltaX = MapScrollDelta(rect.Left, rect.Right, MapScrollViewer.ViewportWidth, margin);
        var deltaY = MapScrollDelta(rect.Top, rect.Bottom, MapScrollViewer.ViewportHeight, margin);

        if (Math.Abs(deltaX) < 0.5 && Math.Abs(deltaY) < 0.5)
        {
            return;
        }

        MapScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, MapScrollViewer.HorizontalOffset + deltaX));
        MapScrollViewer.ScrollToVerticalOffset(Math.Max(0.0, MapScrollViewer.VerticalOffset + deltaY));

        // Рабочий вид сохраняется в базу только в «Карте»: «Предупреждения» его не меняют (A4).
        if (_shellSection == ShellSection.Map && !_mapViewportHeldForAlerts)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(TrySaveViewportLayout));
        }
    }

    // §8: элемент карты с фокусом клавиатуры виден целиком вместе с кольцом — при любом пути фокуса
    // (Tab, стрелки, «Показать на карте»), а не только при переходе стрелками.
    private void ScrollFocusedMapElementIntoViewLater(FrameworkElement element)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (element.IsKeyboardFocused)
                {
                    ScrollMapElementIntoView(element);
                }
            }));
    }

    private static double MapScrollDelta(double start, double end, double viewport, double margin)
    {
        // Виден — только с запасом под кольцо фокуса.
        if (start >= margin && end <= viewport - margin)
        {
            return 0.0;
        }

        // Выше или левее области, либо элемент не помещается с запасом: выравниваем по началу.
        if (start < margin || (end - start) + (2.0 * margin) >= viewport)
        {
            return start - margin;
        }

        return end - viewport + margin;
    }

    // Выбор с клавиатуры тем же путём, что щелчок мышью.

    private bool ActivateMapTarget(MapKeyboardTarget target)
    {
        var element = target.Element;

        switch (target.Kind)
        {
            case MapKeyboardTargetKind.Node:
                if (!target.Node.DeviceId.HasValue)
                {
                    return false;
                }

                SelectMapDeviceFromKeyboard(target.Node.DeviceId.Value);
                break;

            case MapKeyboardTargetKind.Link:
                if (!(target.Link.Line.Tag is Guid))
                {
                    return false;
                }

                SelectMapLinkFromKeyboard((Guid)target.Link.Line.Tag);
                break;

            case MapKeyboardTargetKind.Location:
                SelectLocation(target.Location.LocationId);
                break;

            default:
                return false;
        }

        // Перерисовка карты оставляет те же элементы; фокус возвращается, если выбор его сдвинул.
        if (element.IsVisible && !ReferenceEquals(Keyboard.FocusedElement, element))
        {
            Keyboard.Focus(element);
        }

        return true;
    }

    private void SelectMapDeviceFromKeyboard(Guid deviceId)
    {
        StopStartupTopologyFit();

        // Как обычный щелчок: показанный путь сбрасывается.
        ClearMapPathState(false);

        _highlightedDeviceId = null;
        _selectedDeviceId = deviceId;
        _selectedInterfaceId = null;
        _selectedPhysicalLinkId = null;
        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
        RevealInspectorForExplicitSelection();
    }

    private void SelectMapLinkFromKeyboard(Guid physicalLinkId)
    {
        StopStartupTopologyFit();
        ClearMapPathState(false);

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedInterfaceId = null;
        _selectedPhysicalLinkId = physicalLinkId;
        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        ApplyLinkFocusPresentation();
        UpdateSelectedLayoutControl();
        RevealInspectorForExplicitSelection();
    }

    // Контекстное меню элемента в фокусе (Shift+F10, клавиша меню): узел, связь, размещение.
    private bool OpenMapContextMenu(MapKeyboardTarget target)
    {
        ContextMenu menu;

        switch (target.Kind)
        {
            case MapKeyboardTargetKind.Node:
                OnMapNodeMouseRightButtonDown(target.Element, null);
                menu = target.Element.ContextMenu;
                break;

            case MapKeyboardTargetKind.Link:
                OnMapLinkMouseRightButtonDown(target.Element, null);
                menu = target.Element.ContextMenu;
                break;

            case MapKeyboardTargetKind.Location:
                OnMapLocationMouseRightButtonDown(target.Element, null);
                menu = target.Location.Border.ContextMenu;
                break;

            default:
                return false;
        }

        if (menu == null)
        {
            return false;
        }

        // Выбор мог перерисовать карту; меню открывается у того же элемента.
        menu.PlacementTarget = target.Element;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        return true;
    }

    // Режим «Правка»: Ctrl+стрелка сдвигает узел или вкладку (с потомками), Ctrl+Shift+стрелка — мелким шагом.
    // Позиция сохраняется так же, как по окончании перетаскивания; закреплённый элемент не двигается.
    private bool MoveMapElementWithKeyboard(
        MapKeyboardTarget target,
        MapNavigationDirection direction,
        bool fine)
    {
        if (!IsMapEditMode ||
            (target.Kind != MapKeyboardTargetKind.Node && target.Kind != MapKeyboardTargetKind.Location))
        {
            return false;
        }

        var step = GetDoubleResource(
            fine
                ? "NetLoom.Map.KeyboardMoveFineStep"
                : "NetLoom.Map.KeyboardMoveStep");

        var deltaX = direction == MapNavigationDirection.Left
            ? -step
            : direction == MapNavigationDirection.Right
                ? step
                : 0.0;

        var deltaY = direction == MapNavigationDirection.Up
            ? -step
            : direction == MapNavigationDirection.Down
                ? step
                : 0.0;

        if (target.Kind == MapKeyboardTargetKind.Node)
        {
            MoveNodeWithKeyboard(target.Node, deltaX, deltaY);
        }
        else
        {
            MoveLocationWithKeyboard(target.Location, deltaX, deltaY);
        }

        ScrollMapElementIntoView(target.Element);
        return true;
    }

    private void MoveNodeWithKeyboard(MapNodeVisual visual, double deltaX, double deltaY)
    {
        if (visual.IsLocked)
        {
            return;
        }

        var canvasWidth = MapCanvas.ActualWidth > 0.0
            ? MapCanvas.ActualWidth
            : MapCanvas.Width;

        var canvasHeight = MapCanvas.ActualHeight > 0.0
            ? MapCanvas.ActualHeight
            : MapCanvas.Height;

        var left = Math.Max(
            0.0,
            Math.Min(
                Math.Max(0.0, canvasWidth - _nodeWidth),
                NodeLeft(visual) + deltaX));

        var top = Math.Max(
            0.0,
            Math.Min(
                Math.Max(0.0, canvasHeight - NodeVisualHeight(visual)),
                NodeTop(visual) + deltaY));

        var constrained = ConstrainNodePositionToLocation(visual, left, top);

        if (Math.Abs(constrained.X - NodeLeft(visual)) < 0.001 &&
            Math.Abs(constrained.Y - NodeTop(visual)) < 0.001)
        {
            return;
        }

        Canvas.SetLeft(visual.Border, constrained.X);
        Canvas.SetTop(visual.Border, constrained.Y);

        UpdateLinksForCurrentNodePositions();
        TrySaveDeviceLayout(visual);
    }

    private void MoveLocationWithKeyboard(MapLocationVisual visual, double deltaX, double deltaY)
    {
        if (visual.IsLocked)
        {
            return;
        }

        var canvasWidth = MapCanvas.ActualWidth > 0.0
            ? MapCanvas.ActualWidth
            : MapCanvas.Width;

        var canvasHeight = MapCanvas.ActualHeight > 0.0
            ? MapCanvas.ActualHeight
            : MapCanvas.Height;

        var startLeft = LocationLeft(visual);
        var startTop = LocationTop(visual);

        var left = Math.Max(
            0.0,
            Math.Min(
                Math.Max(0.0, canvasWidth - visual.Border.Width),
                startLeft + deltaX));

        var top = Math.Max(
            0.0,
            Math.Min(
                Math.Max(0.0, canvasHeight - visual.Border.Height),
                startTop + deltaY));

        // Те же шаги, что у перетаскивания рамки: потомки и устройства двигаются вместе с ней.
        _locationDragStartLeft = startLeft;
        _locationDragStartTop = startTop;
        _locationDragLocationStarts.Clear();
        _locationDragDeviceStarts.Clear();
        CaptureLocationSubtreeStarts(visual.LocationId);
        CaptureLocationDeviceStarts(visual.LocationId);

        ApplyLocationDrag(visual, left, top);

        if (Math.Abs(LocationLeft(visual) - startLeft) >= 0.001 ||
            Math.Abs(LocationTop(visual) - startTop) >= 0.001)
        {
            TrySaveLocationLayout(visual);

            foreach (var child in _locationDragLocationStarts.Keys)
            {
                TrySaveLocationLayout(child);
            }

            foreach (var node in _locationDragDeviceStarts.Keys)
            {
                TrySaveDeviceLayout(node);
            }
        }

        _locationDragLocationStarts.Clear();
        _locationDragDeviceStarts.Clear();
    }
}
