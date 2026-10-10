using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NetLoom.Application.Alerts;
using NetLoom.Application.Locations;
using NetLoom.Application.Lookup;
using NetLoom.Application.MapLayout;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyMap;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private void RedrawCurrentMap()
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        ShowMap(
            _lastMapSnapshot);

        var state =
            _refreshStateTracker.Current;

        if (state.Kind ==
                TopologyRefreshStateKind.Stale &&
            state.LastSuccessUtc.HasValue)
        {
            MapStatusText.Text =
                UiText.Format(
                    "MapRefreshStale",
                    state.LastSuccessUtc.Value
                        .ToLocalTime());
        }
    }

    private void LoadPersistedMapLayout()
    {
        _hasPersistedViewport = false;

        try
        {
            var snapshot =
                _mapLayoutStore.Load(
                    _mapLayoutId);

            if (snapshot == null)
            {
                return;
            }

            // Сохранённый вид «Вся площадка» может быть мельче порога читаемости (Sprint 49): восстанавливаем как есть.
            _zoom =
                ClampZoom(
                    snapshot.Viewport.Zoom);

            _pendingPanX =
                snapshot.Viewport.PanX;

            _pendingPanY =
                snapshot.Viewport.PanY;

            // Вид без сохранения (новый объект) — стартовое вписывание всей площадки (M3).
            _hasPersistedViewport =
                snapshot.Viewport.IsSaved;

            _persistedDeviceLayouts.Clear();

            foreach (var item in snapshot.Devices)
            {
                _persistedDeviceLayouts[item.DeviceId] =
                    item;
            }

            _persistedLocationLayouts.Clear();

            foreach (var item in snapshot.Locations)
            {
                _persistedLocationLayouts[item.LocationId] =
                    item;
            }
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            _zoom = 1.0;
            _pendingPanX = 0.0;
            _pendingPanY = 0.0;
            _hasPersistedViewport = false;
            _persistedDeviceLayouts.Clear();
            _persistedLocationLayouts.Clear();
        }
    }

    private void RestoreViewportOffsets()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                ApplyPersistedViewportOffsets));
    }

    private void ApplyPersistedViewportOffsets()
    {
        MapScrollViewer
            .ScrollToHorizontalOffset(
                MapVirtualWorkspace
                    .ToScrollOffset(
                        _pendingPanX,
                        _virtualOriginX,
                        _zoom));

        MapScrollViewer
            .ScrollToVerticalOffset(
                MapVirtualWorkspace
                    .ToScrollOffset(
                        _pendingPanY,
                        _virtualOriginY,
                        _zoom));
    }

    private void ApplyZoomTransform()
    {
        MapCanvas.LayoutTransform =
            new ScaleTransform(
                _zoom,
                _zoom);

        UpdateMapFocusRingScale();
        UpdateSemanticMapVisibility();
        UpdateParallelLinksForZoom();
    }

    // Замечание владельца (Sprint 49): на средних и мелких масштабах связи одной пары не сливаются в одну
    // Полосу — расстояние между полосами не меньше NetLoom.Map.ParallelLinkMinScreenGap экранных пикселей.
    private double ParallelLinkSpacingAtZoom()
    {
        var zoom =
            _zoom > 0.0
                ? _zoom
                : 1.0;

        return Math.Max(
            _parallelLinkSpacing,
            GetDoubleResource(
                "NetLoom.Map.ParallelLinkMinScreenGap") /
            zoom);
    }

    private void UpdateParallelLinksForZoom()
    {
        var spacing =
            ParallelLinkSpacingAtZoom();

        if (Math.Abs(spacing - _lastParallelLinkSpacing) < 0.01)
        {
            return;
        }

        _lastParallelLinkSpacing =
            spacing;

        if (_lastMapSnapshot == null ||
            !_lastMapSnapshot.Links
                .GroupBy(link => string.CompareOrdinal(link.SourceNodeKey, link.TargetNodeKey) <= 0
                    ? link.SourceNodeKey + "|" + link.TargetNodeKey
                    : link.TargetNodeKey + "|" + link.SourceNodeKey)
                .Any(group => group.Count() > 1))
        {
            return;
        }

        UpdateLinksForCurrentNodePositions();
    }

    private double _lastParallelLinkSpacing;

    // §8: кольцо фокуса элементов холста масштабируется вместе с картой; толщина делится на масштаб,
    // Чтобы на экране кольцо оставалось 2 px при любом масштабе (ресурсы читает NetLoom.Style.MapFocusVisual).
    private void UpdateMapFocusRingScale()
    {
        var zoom =
            _zoom > 0.0
                ? _zoom
                : 1.0;

        var ring =
            GetThicknessResource(
                "NetLoom.Thickness.FocusRing");

        var offset =
            GetThicknessResource(
                "NetLoom.Thickness.FocusRingOffset");

        MapScrollViewer.Resources["NetLoom.Thickness.MapFocusRing"] =
            new Thickness(
                ring.Left / zoom,
                ring.Top / zoom,
                ring.Right / zoom,
                ring.Bottom / zoom);

        MapScrollViewer.Resources["NetLoom.Thickness.MapFocusRingOffset"] =
            new Thickness(
                offset.Left / zoom,
                offset.Top / zoom,
                offset.Right / zoom,
                offset.Bottom / zoom);
    }

    private void UpdateSemanticMapVisibility()
    {
        var next = MapSemanticLevels.For(_zoom, _readableZoomMin,
            _linkLabelMinZoom, _semanticDetailMinZoom);
        var changed = next != _semanticLevel;
        _semanticLevel = next;
        foreach (var visual in _nodeVisualsByIdentity.Values)
            ApplyNodeSemanticPresentation(visual);
        foreach (var visual in _locationVisualsById.Values)
            ApplyLocationSemanticPresentation(visual);
        ApplyNeighborhoodVisibility();
        ApplyFarLabelDeclutter();

        // Вторая строка меняет высоту карточки: обновляем геометрию существующих связей.
        if (changed && _lastMapSnapshot != null)
            ReconcileLinks(_lastMapSnapshot.Links,
                _lastMapSnapshot.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal));
        else
            ApplyLinkFocusPresentation();

        System.Windows.Automation.AutomationProperties.SetHelpText(MapZoomValueText,
            UiText.Get("MapSemantic" + _semanticLevel + "Help"));
    }

    private double ClampZoom(
        double value)
    {
        return Math.Max(
            _zoomMin,
            Math.Min(
                _zoomMax,
                value));
    }

    private void UpdateZoomText()
    {
        MapZoomValueText.Text =
            Math.Round(
                    _zoom * 100.0)
                .ToString(
                    "0",
                    CultureInfo.CurrentCulture) +
            "%";
    }

    private void ChangeZoom(
        double requestedZoom)
    {
        var next =
            ClampZoom(
                requestedZoom);

        if (Math.Abs(next - _zoom) < 0.0001)
        {
            return;
        }

        ApplyZoomCenteredOnSelection(
            next,
            null);
    }

    private void FocusSelectedMapAtNativeZoom(
        Action completed)
    {
        ApplyZoomCenteredOnSelection(
            ClampZoom(1.0),
            completed);
    }

    private void ApplyZoomCenteredOnSelection(
        double next,
        Action completed)
    {
        var viewportWidth =
            MapScrollViewer.ViewportWidth;

        var viewportHeight =
            MapScrollViewer.ViewportHeight;

        double logicalCenterX;
        double logicalCenterY;

        if (!TryGetSelectedMapLogicalCenter(
                out logicalCenterX,
                out logicalCenterY))
        {
            if (_pendingZoomCenter.HasValue)
            {
                logicalCenterX =
                    _pendingZoomCenter.Value.X;

                logicalCenterY =
                    _pendingZoomCenter.Value.Y;
            }
            else
            {
                logicalCenterX =
                    (_zoom <= 0.0)
                        ? 0.0
                        : (MapScrollViewer.HorizontalOffset +
                           (viewportWidth / 2.0)) / _zoom;

                logicalCenterY =
                    (_zoom <= 0.0)
                        ? 0.0
                        : (MapScrollViewer.VerticalOffset +
                           (viewportHeight / 2.0)) / _zoom;
            }
        }

        var pendingCenter =
            new Point(
                logicalCenterX,
                logicalCenterY);

        _pendingZoomCenter =
            pendingCenter;

        _zoom = next;
        ApplyZoomTransform();
        UpdateZoomText();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    MapScrollViewer
                        .ScrollToHorizontalOffset(
                            Math.Max(
                                0.0,
                                (logicalCenterX * _zoom) -
                                (viewportWidth / 2.0)));

                    MapScrollViewer
                        .ScrollToVerticalOffset(
                            Math.Max(
                                0.0,
                                (logicalCenterY * _zoom) -
                                (viewportHeight / 2.0)));

                    // Сбрасываем только свой центр: более позднее нажатие уже поставило новый.
                    if (_pendingZoomCenter == pendingCenter)
                    {
                        _pendingZoomCenter = null;
                    }

                    TrySaveViewportLayout();
                    completed?.Invoke();
                }));
    }

    private bool TryGetSelectedMapLogicalCenter(
        out double logicalCenterX,
        out double logicalCenterY)
    {
        logicalCenterX = 0.0;
        logicalCenterY = 0.0;

        if (_selectedDeviceId.HasValue)
        {
            var node =
                _nodeVisualsByIdentity.Values
                    .FirstOrDefault(
                        item =>
                            item.DeviceId.HasValue &&
                            item.DeviceId.Value ==
                                _selectedDeviceId.Value);

            if (node != null)
            {
                var bounds =
                    NodeBounds(
                        node);

                logicalCenterX =
                    bounds.Left +
                    (bounds.Width / 2.0);

                logicalCenterY =
                    bounds.Top +
                    (bounds.Height / 2.0);

                return true;
            }
        }

        if (_selectedPhysicalLinkId.HasValue)
        {
            var link =
                _linkVisualsByIdentity.Values
                    .FirstOrDefault(
                        item =>
                            item.Line.Tag is Guid &&
                            (Guid)item.Line.Tag ==
                                _selectedPhysicalLinkId.Value);

            if (link != null)
            {
                logicalCenterX =
                    (link.Line.X1 +
                     link.Line.X2) / 2.0;

                logicalCenterY =
                    (link.Line.Y1 +
                     link.Line.Y2) / 2.0;

                return true;
            }
        }

        if (_selectedLocationId.HasValue)
        {
            var location =
                LocationVisual(
                    _selectedLocationId.Value);

            if (location != null)
            {
                var bounds =
                    LocationVisibleBounds(
                        location);

                logicalCenterX =
                    bounds.Left +
                    (bounds.Width / 2.0);

                logicalCenterY =
                    bounds.Top +
                    (bounds.Height / 2.0);

                return true;
            }
        }

        return false;
    }

    private void ScheduleStartupTopologyFit()
    {
        _startupTopologyFitPending = true;
        _startupPersistedViewportApplied = false;
        _startupFallbackFitActive = false;

        MapScrollViewer.SizeChanged -=
            OnStartupMapViewportSizeChanged;

        MapScrollViewer.SizeChanged +=
            OnStartupMapViewportSizeChanged;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                TryCompleteStartupTopologyFit));
    }

    private void StopStartupTopologyFit()
    {
        _startupTopologyFitPending = false;

        MapScrollViewer.SizeChanged -=
            OnStartupMapViewportSizeChanged;
    }

    private void OnStartupMapViewportSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (!_startupTopologyFitPending)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                TryCompleteStartupTopologyFit));
    }

    private void TryCompleteStartupTopologyFit()
    {
        if (!_startupTopologyFitPending ||
            _lifetimeCancellation
                .IsCancellationRequested)
        {
            return;
        }

        MapScrollViewer.UpdateLayout();
        MapCanvas.UpdateLayout();

        if (MapScrollViewer.ViewportWidth <= 0.0 ||
            MapScrollViewer.ViewportHeight <= 0.0)
        {
            return;
        }

        if (_startupFallbackFitActive ||
            !_hasPersistedViewport)
        {
            _startupFallbackFitActive = true;
            TryFitTopologyToViewport();
            return;
        }

        if (!_startupPersistedViewportApplied)
        {
            ApplyZoomTransform();
            UpdateZoomText();
            ApplyPersistedViewportOffsets();
            MapScrollViewer.UpdateLayout();
            _startupPersistedViewportApplied = true;
        }

        if (IsCurrentStartupViewportMeaningful())
        {
            return;
        }

        _startupFallbackFitActive = true;
        TryFitTopologyToViewport();
    }

    private bool IsCurrentStartupViewportMeaningful()
    {
        if (_zoom <= 0.0 ||
            MapScrollViewer.ViewportWidth <= 0.0 ||
            MapScrollViewer.ViewportHeight <= 0.0)
        {
            return false;
        }

        var bounds =
            new List<Rect>();

        bounds.AddRange(
            _nodeVisualsByIdentity.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    NodeBounds));

        bounds.AddRange(
            _locationVisualsById.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    LocationVisibleBounds));

        if (bounds.Count == 0)
        {
            return true;
        }

        var viewport =
            new Rect(
                MapScrollViewer.HorizontalOffset /
                    _zoom,
                MapScrollViewer.VerticalOffset /
                    _zoom,
                MapScrollViewer.ViewportWidth /
                    _zoom,
                MapScrollViewer.ViewportHeight /
                    _zoom);

        return bounds.Any(
            item =>
                item.IntersectsWith(
                    viewport));
    }

    private void FitTopologyToViewport()
    {
        var bounds =
            new List<Rect>();

        bounds.AddRange(
            _nodeVisualsByIdentity.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    NodeBounds));

        bounds.AddRange(
            _locationVisualsById.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    LocationVisibleBounds));

        // Явное действие вписывает всю площадку до ZoomMin; текст меняется по уровню детализации.
        TryFitMapBoundsToViewport(
            bounds);
    }

    private bool TryFitTopologyToViewport()
    {
        var bounds =
            new List<Rect>();

        bounds.AddRange(
            _nodeVisualsByIdentity.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    NodeBounds));

        bounds.AddRange(
            _locationVisualsById.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    LocationVisibleBounds));

        // Без сохранённого вида показываем всю площадку; читаемость задаёт детализация.
        // Вписывание всей площадки не увеличивает выше 100 %: маленький объект не раздувается.
        return TryFitMapBoundsToViewport(
            bounds,
            _zoomMin,
            1.0);
    }

    private void FitMapBoundsToViewport(
        IReadOnlyList<Rect> bounds)
    {
        TryFitMapBoundsToViewport(
            bounds);
    }

    private bool TryFitMapBoundsToViewport(
        IReadOnlyList<Rect> bounds)
    {
        return TryFitMapBoundsToViewport(
            bounds,
            null);
    }

    private bool TryFitMapBoundsToViewport(
        IReadOnlyList<Rect> bounds,
        double? minimumZoom)
    {
        return TryFitMapBoundsToViewport(bounds, minimumZoom, null);
    }

    private bool TryFitMapBoundsToViewport(
        IReadOnlyList<Rect> bounds,
        double? minimumZoom,
        double? maximumZoom)
    {
        if (bounds == null)
        {
            throw new ArgumentNullException(
                nameof(bounds));
        }

        if (bounds.Count == 0)
        {
            return false;
        }

        MapScrollViewer.UpdateLayout();
        MapCanvas.UpdateLayout();

        var viewportWidth =
            MapScrollViewer.ViewportWidth;

        var viewportHeight =
            MapScrollViewer.ViewportHeight;

        if (viewportWidth <= 0.0 ||
            viewportHeight <= 0.0)
        {
            return false;
        }

        var minX =
            bounds.Min(
                item => item.Left);

        var minY =
            bounds.Min(
                item => item.Top);

        var maxX =
            bounds.Max(
                item => item.Right);

        var maxY =
            bounds.Max(
                item => item.Bottom);

        var contentWidth =
            Math.Max(
                1.0,
                maxX -
                minX);

        var contentHeight =
            Math.Max(
                1.0,
                maxY -
                minY);

        var availableWidth =
            Math.Max(
                1.0,
                viewportWidth -
                (_fitPadding * 2.0));

        var availableHeight =
            Math.Max(
                1.0,
                viewportHeight -
                (_fitPadding * 2.0));

        var fitZoom =
            ClampZoom(Math.Min(maximumZoom ?? _zoomMax,
                Math.Min(
                    availableWidth / contentWidth,
                    availableHeight / contentHeight)));

        var minimumReadableZoom =
            minimumZoom.HasValue
                ? ClampZoom(
                    minimumZoom.Value)
                : fitZoom;

        var clippedForReadability =
            minimumZoom.HasValue &&
            fitZoom <
                minimumReadableZoom;

        _zoom =
            clippedForReadability
                ? minimumReadableZoom
                : fitZoom;

        var centerX =
            (minX +
             maxX) /
            2.0;

        var centerY =
            (minY +
             maxY) /
            2.0;

        if (clippedForReadability)
        {
            double selectedCenterX;
            double selectedCenterY;

            if (TryGetSelectedMapLogicalCenter(
                    out selectedCenterX,
                    out selectedCenterY))
            {
                centerX =
                    selectedCenterX;
                centerY =
                    selectedCenterY;
            }
        }

        ApplyZoomTransform();
        UpdateZoomText();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    MapScrollViewer
                        .ScrollToHorizontalOffset(
                            Math.Max(
                                0.0,
                                (centerX * _zoom) -
                                (viewportWidth / 2.0)));

                    MapScrollViewer
                        .ScrollToVerticalOffset(
                            Math.Max(
                                0.0,
                                (centerY * _zoom) -
                                (viewportHeight / 2.0)));

                    TrySaveViewportLayout();
                }));

        return true;
    }

    private void TrySaveViewportLayout()
    {
        try
        {
            _mapLayoutStore.SaveViewport(
                _mapLayoutId,
                new MapViewportLayout(
                    _zoom,
                    MapVirtualWorkspace
                        .ToLogicalPan(
                            MapScrollViewer
                                .HorizontalOffset,
                            _virtualOriginX,
                            _zoom),
                    MapVirtualWorkspace
                        .ToLogicalPan(
                            MapScrollViewer
                                .VerticalOffset,
                            _virtualOriginY,
                            _zoom)));
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            MapStatusText.Text =
                UiText.Get(
                    "MapLayoutSaveFailed");
        }
    }

    private void TrySaveDeviceLayout(
        MapNodeVisual visual)
    {
        // Временная раскладка не попадает в хранилище ни из одного обработчика карты.
        if (IsNeighborhoodLayoutActive || HasNeighborhoodLayoutPositions) return;

        if (visual == null ||
            !visual.DeviceId.HasValue)
        {
            return;
        }

        var layout =
            new MapDeviceLayout(
                visual.DeviceId.Value,
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        NodeLeft(
                            visual),
                        _virtualOriginX),
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        NodeTop(
                            visual),
                        _virtualOriginY),
                visual.IsLocked);

        try
        {
            _mapLayoutStore.SaveDevice(
                _mapLayoutId,
                layout);

            _persistedDeviceLayouts[
                layout.DeviceId] =
                layout;
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            MapStatusText.Text =
                UiText.Get(
                    "MapLayoutSaveFailed");
        }
    }

    private void TrySaveLocationLayout(
        MapLocationVisual visual)
    {
        // Рамки скрыты в окрестности; отложенный обработчик также не сохраняет этот вид.
        if (IsNeighborhoodLayoutActive || HasNeighborhoodLayoutPositions) return;

        if (visual == null ||
            visual.LocationId == Guid.Empty)
        {
            return;
        }

        var layout =
            new MapLocationLayout(
                visual.LocationId,
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        LocationLeft(
                            visual),
                        _virtualOriginX),
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        LocationTop(
                            visual),
                        _virtualOriginY),
                Math.Max(
                    _locationMinWidth,
                    visual.ExpandedWidth),
                Math.Max(
                    _locationMinHeight,
                    visual.ExpandedHeight),
                visual.IsCollapsed,
                visual.IsLocked);

        try
        {
            _mapLocationLayoutStore.SaveLocation(
                _mapLayoutId,
                layout);

            _persistedLocationLayouts[
                layout.LocationId] =
                layout;
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            MapStatusText.Text =
                UiText.Get(
                    "MapLayoutSaveFailed");
        }
    }

    private void InitializeMapSettingsMenu()
    {
        _motionNormalMenuItem =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionNormalAction"),
                IsCheckable = true,
                ToolTip =
                    UiText.Get(
                        "MapMotionNormalHint")
            };

        _motionNormalMenuItem.Click +=
            OnMapMotionNormalClick;

        _motionReducedMenuItem =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionReducedAction"),
                IsCheckable = true,
                ToolTip =
                    UiText.Get(
                        "MapMotionReducedHint")
            };

        _motionReducedMenuItem.Click +=
            OnMapMotionReducedClick;

        _motionOffMenuItem =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionOffAction"),
                IsCheckable = true,
                ToolTip =
                    UiText.Get(
                        "MapMotionOffHint")
            };

        _motionOffMenuItem.Click +=
            OnMapMotionOffClick;

        var motionMenu =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionSettings")
            };

        motionMenu.Items.Add(
            _motionNormalMenuItem);

        motionMenu.Items.Add(
            _motionReducedMenuItem);

        motionMenu.Items.Add(
            _motionOffMenuItem);

        var settingsMenu =
            new ContextMenu
            {
                Placement =
                    PlacementMode.Bottom,
                PlacementTarget =
                    MapSettingsButton
            };

        settingsMenu.Items.Add(
            motionMenu);

        settingsMenu.Items.Add(
            new Separator());

        settingsMenu.Items.Add(
            CreateOperationalFocusMenu());

        settingsMenu.Items.Add(
            new Separator());

        var exportMenuItem =
            new MenuItem
            {
                Header =
                    ExportUiText.Get(
                        "SiteExportAction"),
                ToolTip =
                    ExportUiText.Get(
                        "SiteExportHint")
            };

        exportMenuItem.Click +=
            OnSiteExportClick;

        settingsMenu.Items.Add(
            exportMenuItem);

        MapSettingsButton.ContextMenu =
            settingsMenu;

        MapSettingsButton.ToolTip =
            UiText.Get(
                "MapSettingsHint");
    }

    private void UpdateMotionModeText()
    {
        if (_motionNormalMenuItem == null ||
            _motionReducedMenuItem == null ||
            _motionOffMenuItem == null)
        {
            return;
        }

        _motionNormalMenuItem.IsChecked =
            _motionMode ==
            MapMotionMode.Normal;

        _motionReducedMenuItem.IsChecked =
            _motionMode ==
            MapMotionMode.Reduced;

        _motionOffMenuItem.IsChecked =
            _motionMode ==
            MapMotionMode.Off;
    }

    private void SetMotionMode(
        MapMotionMode mode)
    {
        _motionMode = mode;

        if (_motionMode ==
            MapMotionMode.Off)
        {
            StopAllMotion();
        }

        UpdateMotionModeText();
    }

    private void UpdateSelectedLayoutControl()
    {
        _suppressLockSelectedChange = true;

        try
        {
            if (_selectedLocationId.HasValue)
            {
                var locationVisual =
                    LocationVisual(
                        _selectedLocationId.Value);

                MapLockSelectedCheckBox.IsEnabled =
                    IsMapEditMode &&
                    locationVisual != null;

                MapLockSelectedCheckBox.IsChecked =
                    locationVisual != null &&
                    locationVisual.IsLocked;

                return;
            }

            MapNodeVisual visual = null;

            if (_selectedDeviceId.HasValue)
            {
                visual =
                    _nodeVisualsByIdentity.Values
                        .FirstOrDefault(
                            item =>
                                item.DeviceId.HasValue &&
                                item.DeviceId.Value ==
                                    _selectedDeviceId.Value);
            }

            MapLockSelectedCheckBox.IsEnabled =
                IsMapEditMode &&
                visual != null;

            MapLockSelectedCheckBox.IsChecked =
                visual != null &&
                visual.IsLocked;
        }
        finally
        {
            _suppressLockSelectedChange = false;
        }

        UpdateShellEquipmentPresentation(
            _lastMapSnapshot);
    }

    private void AnimateAppearance(
        UIElement element)
    {
        if (element == null)
        {
            return;
        }

        var targetOpacity =
            element.Opacity;

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                MapMotionKind.Appearance);

        if (duration == TimeSpan.Zero)
        {
            element.Opacity =
                targetOpacity;
            return;
        }

        var start =
            targetOpacity *
            MapMotionPolicy.PulseOpacity(
                _motionMode);

        element.Opacity = start;

        var animation =
            new DoubleAnimation(
                start,
                targetOpacity,
                new Duration(duration));

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity =
                    targetOpacity;
            };

        element.BeginAnimation(
            UIElement.OpacityProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateRemoval(
        UIElement element,
        Action remove)
    {
        if (element == null)
        {
            remove?.Invoke();
            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                MapMotionKind.Disappearance);

        if (duration == TimeSpan.Zero)
        {
            remove?.Invoke();
            return;
        }

        var animation =
            new DoubleAnimation(
                element.Opacity,
                0.0,
                new Duration(duration));

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity = 1.0;
                remove?.Invoke();
            };

        element.BeginAnimation(
            UIElement.OpacityProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateDiscoveryFocus(
        Guid deviceId)
    {
        var visual =
            _nodeVisualsByIdentity.Values
                .FirstOrDefault(
                    item =>
                        item.DeviceId.HasValue &&
                        item.DeviceId.Value ==
                            deviceId);

        if (visual == null)
        {
            return;
        }

        var element =
            visual.Border;
        var halo =
            visual.PulseHalo;

        element.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        element.Opacity =
            1.0;

        StopNodeFocusHalo(
            halo);

        if (_motionMode ==
            MapMotionMode.Off)
        {
            return;
        }

        var width =
            element.ActualWidth > 0.0
                ? element.ActualWidth
                : _nodeWidth;
        var height =
            element.ActualHeight > 0.0
                ? element.ActualHeight
                : NodeVisualHeight(
                    visual);

        halo.Width =
            width;
        halo.Height =
            height;

        Canvas.SetLeft(
            halo,
            NodeLeft(
                visual));

        Canvas.SetTop(
            halo,
            NodeTop(
                visual));

        halo.Visibility =
            Visibility.Visible;
        halo.Opacity =
            0.72;

        var scale =
            halo.RenderTransform
                as ScaleTransform;

        if (scale == null)
        {
            scale =
                new ScaleTransform(
                    1.0,
                    1.0);

            halo.RenderTransform =
                scale;
        }

        scale.ScaleX =
            1.0;
        scale.ScaleY =
            1.0;

        if (_motionMode ==
            MapMotionMode.Reduced)
        {
            var hold =
                MapMotionPolicy
                    .AlertFocusStaticDuration(
                        _motionMode);

            var holdAnimation =
                new DoubleAnimation(
                    0.72,
                    0.72,
                    new Duration(
                        hold));

            holdAnimation.Completed +=
                (sender, args) =>
                    StopNodeFocusHalo(
                        halo);

            halo.BeginAnimation(
                UIElement.OpacityProperty,
                holdAnimation,
                HandoffBehavior.SnapshotAndReplace);

            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                MapMotionKind.AlertPulse);
        var repeat =
            new RepeatBehavior(
                MapMotionPolicy
                    .AlertFocusPulseCount(
                        _motionMode));

        var opacityAnimation =
            new DoubleAnimation(
                0.72,
                0.0,
                new Duration(
                    duration))
            {
                RepeatBehavior =
                    repeat
            };

        var scaleXAnimation =
            new DoubleAnimation(
                1.0,
                1.28,
                new Duration(
                    duration))
            {
                RepeatBehavior =
                    repeat
            };

        var scaleYAnimation =
            new DoubleAnimation(
                1.0,
                1.28,
                new Duration(
                    duration))
            {
                RepeatBehavior =
                    repeat
            };

        opacityAnimation.Completed +=
            (sender, args) =>
                StopNodeFocusHalo(
                    halo);

        halo.BeginAnimation(
            UIElement.OpacityProperty,
            opacityAnimation,
            HandoffBehavior.SnapshotAndReplace);

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            scaleXAnimation,
            HandoffBehavior.SnapshotAndReplace);

        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            scaleYAnimation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void StopNodeFocusHalo(
        Rectangle halo)
    {
        if (halo == null)
        {
            return;
        }

        halo.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        var scale =
            halo.RenderTransform
                as ScaleTransform;

        if (scale != null)
        {
            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                null);
            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                null);
            scale.ScaleX =
                1.0;
            scale.ScaleY =
                1.0;
        }

        halo.Opacity =
            1.0;
        halo.Visibility =
            Visibility.Collapsed;
    }

    private void AnimatePulse(
        UIElement element,
        MapMotionKind kind,
        double targetOpacity = 1.0,
        int repeatCount = 1)
    {
        if (element == null)
        {
            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                kind);

        if (duration == TimeSpan.Zero ||
            (kind == MapMotionKind.AlertPulse &&
             _motionMode == MapMotionMode.Reduced))
        {
            element.BeginAnimation(
                UIElement.OpacityProperty,
                null);

            element.Opacity =
                targetOpacity;
            return;
        }

        var halfDuration =
            TimeSpan.FromTicks(
                Math.Max(
                    1L,
                    duration.Ticks / 2L));

        var pulseOpacity =
            targetOpacity *
            MapMotionPolicy.PulseOpacity(
                _motionMode);

        var animation =
            new DoubleAnimation(
                targetOpacity,
                pulseOpacity,
                new Duration(
                    halfDuration))
            {
                AutoReverse = true,
                RepeatBehavior =
                    new RepeatBehavior(
                        Math.Max(
                            1,
                            repeatCount))
            };

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity =
                    targetOpacity;
            };

        element.BeginAnimation(
            UIElement.OpacityProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void StopAllMotion()
    {
        foreach (var visual in
            _nodeVisualsByIdentity.Values)
        {
            StopMotion(
                visual.Border);

            StopNodeFocusHalo(
                visual.PulseHalo);
        }

        foreach (var visual in
            _linkVisualsByIdentity.Values)
        {
            StopMotion(
                visual.Line);

            StopMotion(
                visual.Label);
        }

        ApplyLinkFocusPresentation();

        StopMotion(
            AlertTransitionText);
    }

    private static void StopMotion(
        UIElement element)
    {
        if (element == null)
        {
            return;
        }

        element.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        element.Opacity = 1.0;
    }

    private double GetDoubleResource(
        string key)
    {
        var value =
            FindResource(key);

        if (!(value is double))
        {
            throw new InvalidOperationException(
                "WPF design resource is not a double: " +
                key);
        }

        return (double)value;
    }

    private Thickness GetThicknessResource(
        string key)
    {
        var value =
            FindResource(key);

        if (!(value is Thickness))
        {
            throw new InvalidOperationException(
                "WPF design resource is not a Thickness: " +
                key);
        }

        return (Thickness)value;
    }

    private Style GetStyleResource(
        string key)
    {
        var value =
            FindResource(key);

        var style =
            value as Style;

        if (style == null)
        {
            throw new InvalidOperationException(
                "WPF design resource is not a Style: " +
                key);
        }

        return style;
    }

    private static MapSnapshot EmptySnapshot()
    {
        return new MapSnapshot(
            DateTime.UtcNow,
            new MapNode[0],
            new MapLink[0]);
    }

    private static NetworkDiagnosticSnapshot
        EmptyDiagnosticSnapshot()
    {
        return new NetworkDiagnosticSnapshot(
            DateTime.UtcNow,
            new DeviceDiagnostic[0],
            new PhysicalLinkDiagnostic[0]);
    }

    public void ShowMap(MapSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        // Обычная раскладка и обновление данных всегда работают с настоящими координатами.
        RestoreNeighborhoodLayout();

        _lastMapSnapshot =
            snapshot;

        UpdateShellEquipmentPresentation(
            snapshot);

        RefreshOperationalFocusTargets();

        var nodes =
            snapshot.Nodes.ToDictionary(
                node => node.Key,
                StringComparer.Ordinal);

        var locations =
            snapshot.Locations.ToDictionary(
                location => location.Id);

        ReconcileNodes(
            snapshot.Nodes,
            locations);

        ReconcileLocations(
            snapshot.Locations,
            snapshot.Nodes);

        NormalizeLocationHierarchy(
            snapshot.Locations,
            snapshot.Nodes);

        UpdateLocationHierarchyVisibility();
        UpdateTopologyQuality();

        ReconcileLinks(
            snapshot.Links,
            nodes);

        RefreshNeighborhoodSnapshot();
        if (IsFailurePredictionLayoutActive)
            ApplyFailurePredictionLayout();
        ApplyFarLabelDeclutter();
        UpdateSelectedLayoutControl();

        if (snapshot.Nodes.Count == 0 &&
            snapshot.Locations.Count == 0)
        {
            MapStatusText.Text =
                UiText.Get("MapNotLoaded");

            return;
        }

        MapStatusText.Text =
            UiText.Format(
                "MapSummary",
                UiText.FormatCount(
                    "MapNodeCount",
                    snapshot.Nodes.Count),
                UiText.FormatCount(
                    "MapLinkCount",
                    snapshot.Links.Count),
                UiText.FormatCount(
                    "MapLocationCount",
                    snapshot.Locations.Count));
    }

}
