using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

// Sprint 49, навигация по карте.
// История видов, фокус на выбранном (F), возврат (Esc, Alt+←), панорамирование пробелом
// И кратчайший известный физический путь (Shift+щелчок).
public partial class MainWindow
{
    private enum MapViewDisplayMode
    {
        WholeSite = 0,
        Neighborhood = 1,
        OperationalFocus = 2
    }

    // Вид карты: масштаб, смещения прокрутки, режим показа и выбранный элемент.
    private sealed class MapViewState
    {
        public double Zoom { get; set; }

        public double HorizontalOffset { get; set; }

        public double VerticalOffset { get; set; }

        public MapViewDisplayMode Mode { get; set; }

        public Guid? NeighborhoodAnchor { get; set; }

        public HashSet<Guid> NeighborhoodDeviceIds { get; set; }

        public MapOperationalFocusMode OperationalFocus { get; set; }

        public Guid? SelectedDeviceId { get; set; }

        public Guid? SelectedPhysicalLinkId { get; set; }

        public Guid? SelectedLocationId { get; set; }

        // Sprint 50: выбранное кольцо и кольцо режима «Кольцо».
        public string SelectedRingKey { get; set; }

        public string OperationalFocusRingKey { get; set; }

        // Вид, из которого был сделан первый вход в «фокусный» вид (null — вид сам не фокусный).
        public MapViewState ViewBeforeFocus { get; set; }
    }

    private MapViewHistory<MapViewState> _mapViewHistory;

    // Вид, запомненный при первом входе в «фокусный» вид (окрестность, F, путь, вписывание размещения).
    private MapViewState _mapViewBeforeFocus;

    private bool _mapSpacePanActive;

    private bool _panWithLeftButton;

    // Показанный путь: устройства и связи пути, начало и конец, признак «пути нет».
    private HashSet<Guid> _pathDeviceIds = new HashSet<Guid>();

    private HashSet<Guid> _pathLinkIds = new HashSet<Guid>();

    private Guid? _pathStartDeviceId;

    private Guid? _pathEndDeviceId;

    private bool _pathNotFound;

    private MapViewHistory<MapViewState> MapViewHistoryStack
    {
        get
        {
            if (_mapViewHistory == null)
            {
                _mapViewHistory = new MapViewHistory<MapViewState>(
                    Math.Max(1, (int)Math.Round(GetDoubleResource("NetLoom.Map.ViewHistoryLimit"))));
            }
            return _mapViewHistory;
        }
    }

    // Число записей истории видов (для тестов).
    internal int MapViewHistoryCount
    {
        get { return _mapViewHistory == null ? 0 : _mapViewHistory.Count; }
    }

    // Пробел зажат и карта ждёт нажатия левой кнопки для панорамирования (для тестов).
    internal bool IsMapSpacePanActive
    {
        get { return _mapSpacePanActive; }
    }

    // Показан ли путь или отмечено его начало (для тестов).
    internal bool HasMapPathForTests
    {
        get { return HasMapPath; }
    }

    private bool HasMapPath
    {
        get { return _pathStartDeviceId.HasValue || _pathDeviceIds.Count > 0 || _pathLinkIds.Count > 0; }
    }

    private void OnMainWindowPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && _mapSpacePanActive)
        {
            EndMapSpacePan();
            e.Handled = true;
        }
    }

    private void OnMainWindowDeactivated(object sender, EventArgs e)
    {
        // Отпускание пробела в другом окне не приходит: без этого режим «залип» бы.
        EndMapSpacePan();
    }

    // Обработка клавиш навигации по карте. Возвращает true, если клавиша обработана.
    // Работает только в разделе «Карта», когда фокус не в поле ввода, не открыты поиск, редактор и выпадающие списки.
    internal bool HandleMapNavigationKey(Key key, ModifierKeys modifiers, bool isRepeat, object source)
    {
        if (!MapNavigationKeysAllowed(source))
        {
            return false;
        }

        var ctrlOrAlt = (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.None;

        switch (key)
        {
            case Key.Escape:
                return TryNavigateBackToMap();
            case Key.F:
                return !ctrlOrAlt && !isRepeat && FocusSelectedOnMap();
            case Key.Left:
                return (modifiers & ModifierKeys.Alt) != ModifierKeys.None &&
                       (modifiers & ModifierKeys.Control) == ModifierKeys.None &&
                       NavigateToPreviousMapView();
            case Key.Space:
                if (ctrlOrAlt || IsSpaceActivatedElement(source) || IsSpaceActivatedElement(Keyboard.FocusedElement))
                {
                    return false;
                }
                if (!isRepeat)
                {
                    BeginMapSpacePan();
                }
                return true;
            default:
                return false;
        }
    }

    private bool MapNavigationKeysAllowed(object source)
    {
        if (_shellSection != ShellSection.Map ||
            _activeTopologyEditor != null ||
            ShellGlobalSearchPopup.IsOpen)
        {
            return false;
        }

        return !IsMapKeyboardBlocker(source) && !IsMapKeyboardBlocker(Keyboard.FocusedElement);
    }

    // Поле ввода, открытый выпадающий список и меню сами используют эти клавиши.
    private static bool IsMapKeyboardBlocker(object element)
    {
        var current = element as DependencyObject;

        while (current != null)
        {
            if (current is TextBoxBase ||
                current is PasswordBox ||
                current is MenuItem ||
                current is ContextMenu)
            {
                return true;
            }

            var combo = current as ComboBox;

            if (combo != null && combo.IsDropDownOpen)
            {
                return true;
            }

            current = ParentOfElement(current);
        }

        return false;
    }

    // Пробел нажимает кнопку, флажок и переключатель, выбирает элемент списка: там он не панорамирует.
    private static bool IsSpaceActivatedElement(object element)
    {
        var current = element as DependencyObject;

        while (current != null)
        {
            if (current is ButtonBase ||
                current is ComboBox ||
                current is ListBoxItem ||
                current is TreeViewItem ||
                current is DataGridCell)
            {
                return true;
            }

            current = ParentOfElement(current);
        }

        return false;
    }

    private static DependencyObject ParentOfElement(DependencyObject element)
    {
        if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(element);

            if (visualParent != null)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(element);
    }

    // История видов и память о входе в фокусный вид.

    private MapViewState CaptureMapViewState()
    {
        return new MapViewState
        {
            Zoom = _zoom,
            HorizontalOffset = MapScrollViewer.HorizontalOffset,
            VerticalOffset = MapScrollViewer.VerticalOffset,
            Mode = _neighborhoodSelectedDeviceId.HasValue
                ? MapViewDisplayMode.Neighborhood
                : _operationalFocusMode != MapOperationalFocusMode.None
                    ? MapViewDisplayMode.OperationalFocus
                    : MapViewDisplayMode.WholeSite,
            NeighborhoodAnchor = _neighborhoodSelectedDeviceId,
            NeighborhoodDeviceIds = new HashSet<Guid>(_neighborhoodDeviceIds),
            OperationalFocus = _operationalFocusMode,
            SelectedDeviceId = _selectedDeviceId,
            SelectedPhysicalLinkId = _selectedPhysicalLinkId,
            SelectedLocationId = _selectedLocationId,
            SelectedRingKey = _selectedRingKey,
            OperationalFocusRingKey = _operationalFocusRingKey,
            ViewBeforeFocus = _mapViewBeforeFocus
        };
    }

    // Кладёт текущий вид в историю ПЕРЕД его изменением.
    // Параметр enterFocusView: действие приводит к «фокусному» виду, и Esc вернёт вид до первого такого входа.
    // Для остальных действий фокусный вид считается завершённым.
    private void RecordMapView(bool enterFocusView)
    {
        var current = CaptureMapViewState();

        MapViewHistoryStack.Push(current);

        if (!enterFocusView)
        {
            _mapViewBeforeFocus = null;

            // Действие вне фокусных видов завершает и показанный путь.
            ClearMapPathState(true);
        }
        else if (_mapViewBeforeFocus == null)
        {
            _mapViewBeforeFocus = current;
        }
    }

    private bool MapDeviceExists(Guid? deviceId)
    {
        return deviceId.HasValue &&
               _lastMapSnapshot != null &&
               _lastMapSnapshot.Nodes.Any(node => node.DeviceId == deviceId);
    }

    // Применяет вид: режим показа, выбранный элемент, масштаб и смещения. Историю не пишет.
    private void ApplyMapViewState(MapViewState state)
    {
        StopStartupTopologyFit();
        ClearMapPathState(false);

        _mapViewBeforeFocus = state.ViewBeforeFocus;

        _highlightedDeviceId = null;
        _selectedInterfaceId = null;
        _selectedDeviceId = MapDeviceExists(state.SelectedDeviceId) ? state.SelectedDeviceId : null;
        _selectedPhysicalLinkId = _lastMapSnapshot != null && state.SelectedPhysicalLinkId.HasValue &&
            _lastMapSnapshot.Links.Any(link => link.PhysicalLinkId == state.SelectedPhysicalLinkId)
                ? state.SelectedPhysicalLinkId
                : null;
        _selectedLocationId = _lastMapSnapshot != null && state.SelectedLocationId.HasValue &&
            _lastMapSnapshot.Locations.Any(location => location.Id == state.SelectedLocationId.Value)
                ? state.SelectedLocationId
                : null;
        _selectedRingKey = RingByKey(state.SelectedRingKey) != null ? state.SelectedRingKey : null;

        if (state.Mode == MapViewDisplayMode.Neighborhood && MapDeviceExists(state.NeighborhoodAnchor))
        {
            // Окрестность возвращается с тем же множеством устройств, которое было раскрыто.
            _operationalFocusMode = MapOperationalFocusMode.None;
            RefreshOperationalFocusTargets();
            _neighborhoodSelectedDeviceId = state.NeighborhoodAnchor;
            _neighborhoodDeviceIds = new HashSet<Guid>(state.NeighborhoodDeviceIds);
            RefreshNeighborhoodPresentation();
        }
        else if (state.Mode == MapViewDisplayMode.OperationalFocus)
        {
            // Sprint 50: режим кольца возвращается вместе с ключом кольца.
            _operationalFocusRingKey = state.OperationalFocusRingKey;
            SetOperationalFocusMode(state.OperationalFocus);
        }
        else
        {
            SetOperationalFocusMode(MapOperationalFocusMode.None);
        }

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
        ApplyLinkFocusPresentation();
        UpdateNeighborhoodMenuState();

        RestoreMapViewport(state.Zoom, state.HorizontalOffset, state.VerticalOffset);
    }

    // Масштаб применяется сразу, смещения — после раскладки.
    // Отложенная прокрутка встаёт в очередь после прокруток вписывания при смене режима показа и остаётся последней.
    private void RestoreMapViewport(double zoom, double horizontalOffset, double verticalOffset)
    {
        _pendingZoomCenter = null;
        _zoom = ClampZoom(zoom);
        ApplyZoomTransform();
        UpdateZoomText();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    MapScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, horizontalOffset));
                    MapScrollViewer.ScrollToVerticalOffset(Math.Max(0.0, verticalOffset));
                    TrySaveViewportLayout();
                }));
    }

    // Esc: вернуться к карте в прежнем масштабе, если текущий вид получен фокусным действием.
    private bool TryNavigateBackToMap()
    {
        if (HasMapPath && _pathLinkIds.Count == 0)
        {
            // Путь без смены вида (начало пути или «пути нет») сбрасывается на месте.
            ClearMapPathState(true);
            return true;
        }

        var before = _mapViewBeforeFocus;

        if (before != null)
        {
            _mapViewBeforeFocus = null;
            ApplyMapViewState(before);
            return true;
        }

        if (HasMapPath)
        {
            ClearMapPathState(true);
            return true;
        }

        return false;
    }

    // Alt+←: предыдущий вид из истории.
    private bool NavigateToPreviousMapView()
    {
        MapViewState state;

        if (!MapViewHistoryStack.TryPop(out state))
        {
            return false;
        }

        ApplyMapViewState(state);
        return true;
    }

    // Фокус на выбранном (F).

    private bool FocusSelectedOnMap()
    {
        var bounds = new List<Rect>();
        var participants = new List<MapNodeVisual>();

        if (!TryCollectFocusTargets(bounds, participants))
        {
            return false;
        }

        StopStartupTopologyFit();
        RecordMapView(true);
        FitMapBoundsWithFarLabels(bounds, participants);
        return true;
    }

    // Устройство — оно и его непосредственные соседи; связь — оба конца; размещение — его поддерево.
    private bool TryCollectFocusTargets(List<Rect> bounds, List<MapNodeVisual> participants)
    {
        if (_lastMapSnapshot == null)
        {
            return false;
        }

        if (_selectedDeviceId.HasValue)
        {
            AddVisibleDeviceTargets(
                MapNeighborhood.Initial(_selectedDeviceId.Value, NeighborhoodLinks()),
                bounds,
                participants);

            return bounds.Count > 0;
        }

        if (_selectedPhysicalLinkId.HasValue)
        {
            var link = _lastMapSnapshot.Links.FirstOrDefault(
                item => item.PhysicalLinkId == _selectedPhysicalLinkId);

            if (link == null)
            {
                return false;
            }

            var ends = new HashSet<Guid>();

            foreach (var key in new[] { link.SourceNodeKey, link.TargetNodeKey })
            {
                var node = _lastMapSnapshot.Nodes.FirstOrDefault(item => item.Key == key);

                if (node != null && node.DeviceId.HasValue)
                {
                    ends.Add(node.DeviceId.Value);
                }
            }

            AddVisibleDeviceTargets(ends, bounds, participants);
            return bounds.Count > 0;
        }

        if (_selectedLocationId.HasValue)
        {
            return TryGetLocationSubtreeBounds(_selectedLocationId.Value, bounds, participants);
        }

        return false;
    }

    private void AddVisibleDeviceTargets(
        IEnumerable<Guid> deviceIds,
        List<Rect> bounds,
        List<MapNodeVisual> participants)
    {
        var wanted = new HashSet<Guid>(deviceIds);

        foreach (var visual in _nodeVisualsByIdentity.Values)
        {
            if (visual.DeviceId.HasValue &&
                wanted.Contains(visual.DeviceId.Value) &&
                visual.Border.Visibility == Visibility.Visible)
            {
                bounds.Add(NodeBounds(visual));
                participants.Add(visual);
            }
        }
    }

    // Рамка размещения со всеми потомками: вложенные рамки и устройства поддерева.
    private bool TryGetLocationSubtreeBounds(
        Guid locationId,
        List<Rect> bounds,
        List<MapNodeVisual> participants)
    {
        var visual = LocationVisual(locationId);

        if (visual == null || visual.Border.Visibility != Visibility.Visible)
        {
            return false;
        }

        bounds.Add(LocationVisibleBounds(visual));

        var included = DescendantLocationIds(locationId);

        foreach (var childId in included)
        {
            var child = LocationVisual(childId);

            if (child != null && child.Border.Visibility == Visibility.Visible)
            {
                bounds.Add(LocationVisibleBounds(child));
            }
        }

        included.Add(locationId);

        foreach (var nodeVisual in _nodeVisualsByIdentity.Values)
        {
            if (nodeVisual.DeviceId.HasValue &&
                nodeVisual.LocationId.HasValue &&
                included.Contains(nodeVisual.LocationId.Value) &&
                nodeVisual.Border.Visibility == Visibility.Visible)
            {
                bounds.Add(NodeBounds(nodeVisual));
                participants.Add(nodeVisual);
            }
        }

        return true;
    }

    // Вписывает участников целиком не мельче ZoomMin и не крупнее 100 %, как окрестность.
    // На уровне «Издалека» ярлык имени стоит над карточкой: вписываем заново вместе с ярлыками.
    private bool FitMapBoundsWithFarLabels(
        IReadOnlyList<Rect> bounds,
        IReadOnlyCollection<MapNodeVisual> participants)
    {
        if (!TryFitMapBoundsToViewport(bounds, _zoomMin, 1.0))
        {
            return false;
        }

        if (_semanticLevel != MapSemanticLevel.Far)
        {
            return true;
        }

        var labelHeight = 0.0;
        var labelWidth = 0.0;

        foreach (var visual in participants.Where(
            item => item.SemanticLabel != null && item.SemanticLabel.DesiredSize.Height > 0.0))
        {
            labelHeight = Math.Max(labelHeight, visual.SemanticLabel.DesiredSize.Height);
            labelWidth = Math.Max(labelWidth, visual.SemanticLabel.DesiredSize.Width);
        }

        if (labelHeight <= 0.0)
        {
            return true;
        }

        var zoom = _zoom > 0.0 ? _zoom : 1.0;
        var upward = (labelHeight + _linkLabelCollisionMargin) / zoom;
        var withLabels = bounds
            .Select(item => new Rect(
                item.Left,
                item.Top - upward,
                Math.Max(item.Width, labelWidth / zoom),
                item.Height + upward))
            .ToArray();

        TryFitMapBoundsToViewport(withLabels, _zoomMin, 1.0);
        return true;
    }

    // Двойной щелчок по размещению в режиме просмотра: вписать его поддерево в экран целиком.
    private void FitLocationSubtree(Guid locationId)
    {
        var bounds = new List<Rect>();
        var participants = new List<MapNodeVisual>();

        if (!TryGetLocationSubtreeBounds(locationId, bounds, participants))
        {
            return;
        }

        StopStartupTopologyFit();
        RecordMapView(true);
        FitMapBoundsWithFarLabels(bounds, participants);
    }

    // Временное панорамирование пробелом.

    private void BeginMapSpacePan()
    {
        _mapSpacePanActive = true;
        MapScrollViewer.ForceCursor = true;
        MapScrollViewer.Cursor = Cursors.Hand;
    }

    private void EndMapSpacePan()
    {
        if (!_mapSpacePanActive)
        {
            return;
        }

        _mapSpacePanActive = false;

        // Идущее панорамирование заканчивается отпусканием кнопки мыши.
        if (!_isPanning)
        {
            MapScrollViewer.Cursor = null;
            MapScrollViewer.ForceCursor = false;
        }
    }

    // Кратчайший известный физический путь (Shift+щелчок по двум устройствам).

    // Первый Shift+щелчок выбирает начало пути, второй по другому устройству строит путь.
    internal void HandleMapDeviceShiftClick(Guid deviceId)
    {
        StopStartupTopologyFit();

        if (_pathStartDeviceId.HasValue && !_pathEndDeviceId.HasValue)
        {
            if (_pathStartDeviceId.Value != deviceId)
            {
                BuildMapPath(_pathStartDeviceId.Value, deviceId);
            }

            return;
        }

        // Новое начало: прежний путь (если был) сбрасывается без возврата вида.
        ClearMapPathState(false);
        _pathStartDeviceId = deviceId;
        _pathDeviceIds.Add(deviceId);

        _highlightedDeviceId = null;
        _selectedDeviceId = deviceId;
        _selectedInterfaceId = null;
        _selectedPhysicalLinkId = null;
        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
        ApplyLinkFocusPresentation();
        RevealInspectorForExplicitSelection();
        UpdateMapPathNotice();
    }

    // Связи, видимые на карте: скрытые окрестностью или свёрнутым размещением в путь не входят.
    private IReadOnlyList<MapNeighborhoodLink> VisiblePhysicalLinks()
    {
        if (_lastDiagnosticSnapshot == null)
        {
            return new MapNeighborhoodLink[0];
        }

        var visible = new HashSet<Guid>(
            _linkVisualsByIdentity.Values
                .Where(visual => visual.Line.Tag is Guid && visual.Line.Visibility == Visibility.Visible)
                .Select(visual => (Guid)visual.Line.Tag));

        return _lastDiagnosticSnapshot.Links
            .Where(link => visible.Contains(link.PhysicalLinkId))
            .Select(link => new MapNeighborhoodLink(link.PhysicalLinkId, link.DeviceAId, link.DeviceBId))
            .ToArray();
    }

    private void BuildMapPath(Guid start, Guid end)
    {
        var result = MapShortestPath.Find(start, end, VisiblePhysicalLinks());

        if (result.Found)
        {
            RecordMapView(true);
        }

        _highlightedDeviceId = null;
        _selectedDeviceId = end;
        _selectedInterfaceId = null;
        _selectedPhysicalLinkId = null;
        _selectedLocationId = null;

        _pathEndDeviceId = end;
        _pathNotFound = !result.Found;
        _pathDeviceIds.Clear();
        _pathLinkIds.Clear();

        if (result.Found)
        {
            foreach (var deviceId in result.DeviceIds)
            {
                _pathDeviceIds.Add(deviceId);
            }

            foreach (var linkId in result.LinkIds)
            {
                _pathLinkIds.Add(linkId);
            }
        }
        else
        {
            _pathDeviceIds.Add(start);
        }

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
        ApplyLinkFocusPresentation();
        RevealInspectorForExplicitSelection();
        UpdateMapPathNotice();

        if (result.Found)
        {
            FitMapPathToViewport();
        }
    }

    private void FitMapPathToViewport()
    {
        var bounds = new List<Rect>();
        var participants = new List<MapNodeVisual>();

        AddVisibleDeviceTargets(_pathDeviceIds, bounds, participants);

        if (bounds.Count > 0)
        {
            FitMapBoundsWithFarLabels(bounds, participants);
        }
    }

    // Сбрасывает путь и отметку его начала. redraw: перерисовать карту (вызывающий без перерисовки сам её выполнит).
    private void ClearMapPathState(bool redraw)
    {
        if (!HasMapPath && !_pathEndDeviceId.HasValue)
        {
            return;
        }

        _pathStartDeviceId = null;
        _pathEndDeviceId = null;
        _pathNotFound = false;
        _pathDeviceIds.Clear();
        _pathLinkIds.Clear();

        UpdateMapPathNotice();

        if (redraw && _lastMapSnapshot != null)
        {
            RedrawCurrentMap();
            ApplyLinkFocusPresentation();
        }
    }

    private string MapDeviceLabel(Guid deviceId)
    {
        var node = _lastMapSnapshot == null
            ? null
            : _lastMapSnapshot.Nodes.FirstOrDefault(item => item.DeviceId == deviceId);

        return node == null || string.IsNullOrWhiteSpace(node.Label)
            ? deviceId.ToString("D")
            : node.Label;
    }

    private void UpdateMapPathNotice()
    {
        if (MapPathNotice == null)
        {
            return;
        }

        var reset = UiText.Get("MapPathReset");

        MapPathResetButton.Content = reset;
        System.Windows.Automation.AutomationProperties.SetName(MapPathResetButton, reset);

        string text = null;

        if (_pathStartDeviceId.HasValue && _shellSection == ShellSection.Map)
        {
            var start = MapDeviceLabel(_pathStartDeviceId.Value);

            if (!_pathEndDeviceId.HasValue)
            {
                text = UiText.Format("MapPathStart", start);
            }
            else
            {
                var end = MapDeviceLabel(_pathEndDeviceId.Value);

                text = _pathNotFound
                    ? UiText.Format("MapPathNone", start, end)
                    : UiText.Format(
                        "MapPathSummary",
                        start,
                        end,
                        UiText.FormatCount("MapLinkCount", _pathLinkIds.Count));
            }
        }

        MapPathSummaryText.Text = text ?? string.Empty;
        MapPathNotice.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnMapPathResetClick(object sender, RoutedEventArgs e)
    {
        TryNavigateBackToMap();
        MapFitAllButton.Focus();
    }
}
