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
        try
        {
            var snapshot =
                _mapLayoutStore.Load(
                    _mapLayoutId);

            if (snapshot == null)
            {
                return;
            }

            _zoom =
                ClampZoom(
                    snapshot.Viewport.Zoom);

            _pendingPanX =
                snapshot.Viewport.PanX;

            _pendingPanY =
                snapshot.Viewport.PanY;

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
            _persistedDeviceLayouts.Clear();
            _persistedLocationLayouts.Clear();
        }
    }

    private void RestoreViewportOffsets()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
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
                }));
    }

    private void ApplyZoomTransform()
    {
        MapCanvas.LayoutTransform =
            new ScaleTransform(
                _zoom,
                _zoom);
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

        var viewportWidth =
            MapScrollViewer.ViewportWidth;

        var viewportHeight =
            MapScrollViewer.ViewportHeight;

        var logicalCenterX =
            (_zoom <= 0.0)
                ? 0.0
                : (MapScrollViewer.HorizontalOffset +
                   (viewportWidth / 2.0)) / _zoom;

        var logicalCenterY =
            (_zoom <= 0.0)
                ? 0.0
                : (MapScrollViewer.VerticalOffset +
                   (viewportHeight / 2.0)) / _zoom;

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

                    TrySaveViewportLayout();
                }));
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

        if (bounds.Count == 0)
        {
            return;
        }

        var viewportWidth =
            MapScrollViewer.ViewportWidth;

        var viewportHeight =
            MapScrollViewer.ViewportHeight;

        if (viewportWidth <= 0.0 ||
            viewportHeight <= 0.0)
        {
            return;
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

        _zoom =
            ClampZoom(
                Math.Min(
                    availableWidth /
                    contentWidth,
                    availableHeight /
                    contentHeight));

        var centerX =
            (minX +
             maxX) /
            2.0;

        var centerY =
            (minY +
             maxY) /
            2.0;

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
                visual != null;

            MapLockSelectedCheckBox.IsChecked =
                visual != null &&
                visual.IsLocked;
        }
        finally
        {
            _suppressLockSelectedChange = false;
        }
    }

    private void AnimateAppearance(
        UIElement element)
    {
        if (element == null)
        {
            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                MapMotionKind.Appearance);

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 1.0;
            return;
        }

        var start =
            MapMotionPolicy.PulseOpacity(
                _motionMode);

        element.Opacity = start;

        var animation =
            new DoubleAnimation(
                start,
                1.0,
                new Duration(duration));

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity = 1.0;
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

    private void AnimatePulse(
        UIElement element,
        MapMotionKind kind)
    {
        if (element == null)
        {
            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                kind);

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 1.0;
            return;
        }

        var halfDuration =
            TimeSpan.FromTicks(
                Math.Max(
                    1L,
                    duration.Ticks / 2L));

        var animation =
            new DoubleAnimation(
                1.0,
                MapMotionPolicy.PulseOpacity(
                    _motionMode),
                new Duration(
                    halfDuration))
            {
                AutoReverse = true
            };

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity = 1.0;
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
        }

        foreach (var visual in
            _linkVisualsByIdentity.Values)
        {
            StopMotion(
                visual.Line);

            StopMotion(
                visual.Label);
        }

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

        _lastMapSnapshot =
            snapshot;

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

        ReconcileLinks(
            snapshot.Links,
            nodes);

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
