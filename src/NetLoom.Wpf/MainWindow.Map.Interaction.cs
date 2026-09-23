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
    private async void OnMapNodeMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        var deviceId =
            (Guid)element.Tag;

        StopStartupTopologyFit();

        _highlightedDeviceId = null;

        _selectedDeviceId =
            deviceId;

        _selectedPhysicalLinkId =
            null;

        _selectedLocationId =
            null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();

        if (e.ClickCount >= 2)
        {
            _dragNodeVisual = null;
            _dragMoved = false;

            if (element.IsMouseCaptured)
            {
                element.ReleaseMouseCapture();
            }

            if (IsManualDevice(deviceId))
            {
                e.Handled = true;

                await OpenManualTopologyEditorAsync(
                    deviceId,
                    null);
            }

            return;
        }

        var visual =
            _nodeVisualsByIdentity.Values
                .FirstOrDefault(
                    item =>
                        ReferenceEquals(
                            item.Border,
                            element));

        if (visual != null &&
            !visual.IsLocked)
        {
            _dragNodeVisual =
                visual;

            _dragStartPoint =
                e.GetPosition(
                    MapCanvas);

            _dragStartLeft =
                NodeLeft(visual);

            _dragStartTop =
                NodeTop(visual);

            _dragMoved = false;

            visual.Border.CaptureMouse();
        }

        e.Handled = true;
    }

    private void OnMapNodeMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        StopStartupTopologyFit();

        _highlightedDeviceId = null;
        _selectedDeviceId = (Guid)element.Tag;
        _selectedPhysicalLinkId = null;


        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private void OnMapNodeContextMenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        var menu =
            sender as ContextMenu;

        var target =
            menu == null
                ? null
                : menu.Tag as FrameworkElement;

        var canChange =
            target != null &&
            target.Tag is Guid &&
            IsManualDevice(
                (Guid)target.Tag);

        UpdateMapElementContextMenuState(
            menu,
            canChange);
    }

    private async void OnMapNodeContextEditClick(
        object sender,
        RoutedEventArgs e)
    {
        var deviceId =
            MapElementIdFromMenuItem(sender);

        if (!deviceId.HasValue ||
            !IsManualDevice(
                deviceId.Value))
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualDevice");
            return;
        }

        await OpenManualTopologyEditorAsync(
            deviceId.Value,
            null);
    }

    private async void OnMapNodeContextDeleteClick(
        object sender,
        RoutedEventArgs e)
    {
        var deviceId =
            MapElementIdFromMenuItem(sender);

        if (!deviceId.HasValue)
        {
            return;
        }

        await DeleteManualDeviceFromMapAsync(
            deviceId.Value);
    }

    private async void OnMainWindowPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Delete ||
            e.OriginalSource is TextBoxBase ||
            e.OriginalSource is PasswordBox)
        {
            return;
        }

        if (_selectedLocationId.HasValue)
        {
            e.Handled = true;

            await DeleteLocationFromMapAsync(
                _selectedLocationId.Value);

            return;
        }

        if (_selectedPhysicalLinkId.HasValue)
        {
            e.Handled = true;

            await DeleteManualLinkFromMapAsync(
                _selectedPhysicalLinkId.Value);

            return;
        }

        if (_selectedDeviceId.HasValue)
        {
            e.Handled = true;

            await DeleteManualDeviceFromMapAsync(
                _selectedDeviceId.Value);
        }
    }

    private async void OnMapNodeKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
        {
            return;
        }

        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        e.Handled = true;

        await DeleteManualDeviceFromMapAsync(
            (Guid)element.Tag);
    }

    private void OnMapNodeMouseMove(
        object sender,
        MouseEventArgs e)
    {
        var element =
            sender as Border;

        if (_dragNodeVisual == null ||
            element == null ||
            !ReferenceEquals(
                _dragNodeVisual.Border,
                element) ||
            e.LeftButton !=
                MouseButtonState.Pressed)
        {
            return;
        }

        var current =
            e.GetPosition(
                MapCanvas);

        var deltaX =
            current.X -
            _dragStartPoint.X;

        var deltaY =
            current.Y -
            _dragStartPoint.Y;

        if (!_dragMoved &&
            Math.Abs(deltaX) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(deltaY) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragMoved = true;

        var canvasWidth =
            MapCanvas.ActualWidth > 0.0
                ? MapCanvas.ActualWidth
                : MapCanvas.Width;

        var canvasHeight =
            MapCanvas.ActualHeight > 0.0
                ? MapCanvas.ActualHeight
                : MapCanvas.Height;

        var left =
            Math.Max(
                0.0,
                Math.Min(
                    Math.Max(
                        0.0,
                        canvasWidth -
                        _nodeWidth),
                    _dragStartLeft +
                    deltaX));

        var top =
            Math.Max(
                0.0,
                Math.Min(
                    Math.Max(
                        0.0,
                        canvasHeight -
                        NodeVisualHeight(
                            _dragNodeVisual)),
                    _dragStartTop +
                    deltaY));

        var constrained =
            ConstrainNodePositionToLocation(
                _dragNodeVisual,
                left,
                top);

        Canvas.SetLeft(
            _dragNodeVisual.Border,
            constrained.X);

        Canvas.SetTop(
            _dragNodeVisual.Border,
            constrained.Y);

        UpdateLinksForCurrentNodePositions();
        e.Handled = true;
    }

    private void OnMapNodeMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_dragNodeVisual == null)
        {
            return;
        }

        var visual =
            _dragNodeVisual;

        _dragNodeVisual = null;

        if (visual.Border.IsMouseCaptured)
        {
            visual.Border.ReleaseMouseCapture();
        }

        if (_dragMoved)
        {
            TrySaveDeviceLayout(
                visual);
        }

        _dragMoved = false;
        e.Handled = true;
    }

    private void UpdateLinksForCurrentNodePositions()
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        var nodes =
            _lastMapSnapshot.Nodes
                .ToDictionary(
                    node => node.Key,
                    StringComparer.Ordinal);

        ReconcileLinks(
            _lastMapSnapshot.Links,
            nodes);
    }

    private void OnMapZoomOutClick(
        object sender,
        RoutedEventArgs e)
    {
        StopStartupTopologyFit();

        ChangeZoom(
            _zoom -
            _zoomStep);
    }

    private void OnMapZoomInClick(
        object sender,
        RoutedEventArgs e)
    {
        StopStartupTopologyFit();

        ChangeZoom(
            _zoom +
            _zoomStep);
    }

    private void OnMapFitAllClick(
        object sender,
        RoutedEventArgs e)
    {
        StopStartupTopologyFit();
        FitTopologyToViewport();
    }

    private async void OnManualTopologyClick(
        object sender,
        RoutedEventArgs e)
    {
        await OpenManualTopologyEditorAsync(
            null,
            null);
    }

    private async Task OpenManualTopologyEditorAsync(
        Guid? initialDeviceId,
        Guid? initialLinkId)
    {
        var editor =
            new ManualTopologyWindow(
                _manualTopologyService,
                initialDeviceId,
                initialLinkId)
            {
                Owner = this
            };

        editor.ShowDialog();

        if (editor.HasChanges)
        {
            await RefreshTopologyAsync();
        }
    }

    private async void OnLocationsClick(
        object sender,
        RoutedEventArgs e)
    {
        await OpenLocationTopologyEditorAsync(
            null);
    }

    private async Task OpenLocationTopologyEditorAsync(
        Guid? initialLocationId)
    {
        var editor =
            new LocationTopologyWindow(
                _locationTopologyService,
                initialLocationId)
            {
                Owner = this
            };

        editor.ShowDialog();

        if (editor.HasChanges)
        {
            await RefreshTopologyAsync();
        }
    }

    private MapLocationVisual LocationVisual(
        Guid locationId)
    {
        MapLocationVisual visual;

        return _locationVisualsById.TryGetValue(
            locationId,
            out visual)
            ? visual
            : null;
    }

    private Guid? LocationIdFromElement(
        object sender)
    {
        var element =
            sender as FrameworkElement;

        return element != null &&
               element.Tag is Guid
            ? (Guid?)((Guid)element.Tag)
            : null;
    }

    private Guid? LocationIdFromMenuItem(
        object sender)
    {
        var menuItem =
            sender as MenuItem;

        var target =
            menuItem == null
                ? null
                : menuItem.Tag
                    as FrameworkElement;

        return target != null &&
               target.Tag is Guid
            ? (Guid?)((Guid)target.Tag)
            : null;
    }

    private void SelectLocation(
        Guid locationId)
    {
        StopStartupTopologyFit();

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedPhysicalLinkId = null;
        _selectedLocationId =
            locationId;

        UpdateLocationSelectionPresentation();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private async void OnMapLocationMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        var visual =
            LocationVisual(
                locationId.Value);

        if (visual == null)
        {
            return;
        }

        SelectLocation(
            locationId.Value);

        if (e.ClickCount >= 2)
        {
            _dragLocationVisual = null;
            _locationDragMoved = false;

            if (visual.Header.IsMouseCaptured)
            {
                visual.Header.ReleaseMouseCapture();
            }

            e.Handled = true;

            await OpenLocationTopologyEditorAsync(
                locationId.Value);

            return;
        }

        if (visual.IsLocked)
        {
            e.Handled = true;
            return;
        }

        _dragLocationVisual =
            visual;

        _locationDragStartPoint =
            e.GetPosition(
                MapCanvas);

        _locationDragStartLeft =
            LocationLeft(
                visual);

        _locationDragStartTop =
            LocationTop(
                visual);

        _locationDragMoved = false;

        _locationDragLocationStarts.Clear();
        _locationDragDeviceStarts.Clear();

        CaptureLocationSubtreeStarts(
            locationId.Value);

        CaptureLocationDeviceStarts(
            locationId.Value);

        visual.Header.CaptureMouse();
        e.Handled = true;
    }

    private async void OnMapLocationBodyMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        SelectLocation(
            locationId.Value);

        if (e.ClickCount >= 2)
        {
            await OpenLocationTopologyEditorAsync(
                locationId.Value);
        }

        e.Handled = true;
    }

    private void OnMapLocationMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        SelectLocation(
            locationId.Value);
    }

    private void OnMapLocationMouseMove(
        object sender,
        MouseEventArgs e)
    {
        var visual =
            _dragLocationVisual;

        if (visual == null ||
            e.LeftButton !=
                MouseButtonState.Pressed)
        {
            return;
        }

        var current =
            e.GetPosition(
                MapCanvas);

        var deltaX =
            current.X -
            _locationDragStartPoint.X;

        var deltaY =
            current.Y -
            _locationDragStartPoint.Y;

        if (!_locationDragMoved &&
            Math.Abs(deltaX) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(deltaY) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _locationDragMoved = true;

        var canvasWidth =
            MapCanvas.ActualWidth > 0.0
                ? MapCanvas.ActualWidth
                : MapCanvas.Width;

        var canvasHeight =
            MapCanvas.ActualHeight > 0.0
                ? MapCanvas.ActualHeight
                : MapCanvas.Height;

        var left =
            Math.Max(
                0.0,
                Math.Min(
                    Math.Max(
                        0.0,
                        canvasWidth -
                        visual.Border.Width),
                    _locationDragStartLeft +
                    deltaX));

        var top =
            Math.Max(
                0.0,
                Math.Min(
                    Math.Max(
                        0.0,
                        canvasHeight -
                        visual.Border.Height),
                    _locationDragStartTop +
                    deltaY));

        ApplyLocationDrag(
            visual,
            left,
            top);

        e.Handled = true;
    }

    private void OnMapLocationMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        var visual =
            _dragLocationVisual;

        if (visual == null)
        {
            return;
        }

        _dragLocationVisual = null;

        if (visual.Header.IsMouseCaptured)
        {
            visual.Header.ReleaseMouseCapture();
        }

        if (_locationDragMoved)
        {
            TrySaveLocationLayout(
                visual);

            foreach (var child in
                _locationDragLocationStarts.Keys)
            {
                TrySaveLocationLayout(
                    child);
            }

            foreach (var node in
                _locationDragDeviceStarts.Keys)
            {
                TrySaveDeviceLayout(
                    node);
            }
        }

        _locationDragMoved = false;
        _locationDragLocationStarts.Clear();
        _locationDragDeviceStarts.Clear();

        e.Handled = true;
    }

    private void CaptureLocationDeviceStarts(
        Guid locationId)
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        var includedLocations =
            DescendantLocationIds(
                locationId);

        includedLocations.Add(
            locationId);

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            if (!node.DeviceId.HasValue ||
                !node.LocationId.HasValue ||
                !includedLocations.Contains(
                    node.LocationId.Value))
            {
                continue;
            }

            MapNodeVisual visual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out visual))
            {
                continue;
            }

            _locationDragDeviceStarts[
                visual] =
                new Point(
                    NodeLeft(
                        visual),
                    NodeTop(
                        visual));
        }
    }

    private HashSet<Guid> DescendantLocationIds(
        Guid locationId)
    {
        var result =
            new HashSet<Guid>();

        if (_lastMapSnapshot == null)
        {
            return result;
        }

        var pending =
            new Queue<Guid>();

        pending.Enqueue(
            locationId);

        while (pending.Count > 0)
        {
            var parentId =
                pending.Dequeue();

            foreach (var child in
                _lastMapSnapshot.Locations
                    .Where(
                        item =>
                            item.ParentLocationId ==
                            parentId))
            {
                if (result.Add(
                        child.Id))
                {
                    pending.Enqueue(
                        child.Id);
                }
            }
        }

        return result;
    }

    private void ApplyLocationDrag(
        MapLocationVisual visual,
        double desiredLeft,
        double desiredTop)
    {
        if (visual == null)
        {
            return;
        }

        var constrained =
            ConstrainLocationPositionToParent(
                visual,
                desiredLeft,
                desiredTop);

        var actualDeltaX =
            constrained.X -
            _locationDragStartLeft;

        var actualDeltaY =
            constrained.Y -
            _locationDragStartTop;

        Canvas.SetLeft(
            visual.Border,
            constrained.X);

        Canvas.SetTop(
            visual.Border,
            constrained.Y);

        foreach (var pair in
            _locationDragLocationStarts)
        {
            Canvas.SetLeft(
                pair.Key.Border,
                pair.Value.X +
                actualDeltaX);

            Canvas.SetTop(
                pair.Key.Border,
                pair.Value.Y +
                actualDeltaY);
        }

        foreach (var pair in
            _locationDragDeviceStarts)
        {
            Canvas.SetLeft(
                pair.Key.Border,
                pair.Value.X +
                actualDeltaX);

            Canvas.SetTop(
                pair.Key.Border,
                pair.Value.Y +
                actualDeltaY);
        }

        UpdateLinksForCurrentNodePositions();
    }

    private void CaptureLocationSubtreeStarts(
        Guid locationId)
    {
        foreach (var descendantId in
            DescendantLocationIds(
                locationId))
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    descendantId,
                    out visual))
            {
                continue;
            }

            _locationDragLocationStarts[
                visual] =
                new Point(
                    LocationLeft(
                        visual),
                    LocationTop(
                        visual));
        }
    }

    private Point ConstrainLocationPositionToParent(
        MapLocationVisual visual,
        double desiredLeft,
        double desiredTop)
    {
        if (visual == null ||
            !visual.ParentLocationId.HasValue)
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        MapLocationVisual parent;

        if (!_locationVisualsById.TryGetValue(
                visual.ParentLocationId.Value,
                out parent))
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        var parentBounds =
            ExpandedLocationBounds(
                parent);

        var width =
            Math.Max(
                _locationMinWidth,
                visual.ExpandedWidth);

        var height =
            Math.Max(
                _locationMinHeight,
                visual.ExpandedHeight);

        var minimumLeft =
            parentBounds.Left +
            _locationContentPadding;

        var minimumTop =
            parentBounds.Top +
            _locationHeaderHeight +
            _locationContentPadding;

        var maximumLeft =
            Math.Max(
                minimumLeft,
                parentBounds.Right -
                _locationContentPadding -
                width);

        var maximumTop =
            Math.Max(
                minimumTop,
                parentBounds.Bottom -
                _locationContentPadding -
                height);

        return new Point(
            Math.Max(
                minimumLeft,
                Math.Min(
                    maximumLeft,
                    desiredLeft)),
            Math.Max(
                minimumTop,
                Math.Min(
                    maximumTop,
                    desiredTop)));
    }

    private Point ConstrainNodePositionToLocation(
        MapNodeVisual visual,
        double desiredLeft,
        double desiredTop)
    {
        if (visual == null ||
            !visual.LocationId.HasValue)
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        MapLocationVisual location;

        if (!_locationVisualsById.TryGetValue(
                visual.LocationId.Value,
                out location))
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        var bounds =
            ExpandedLocationBounds(
                location);

        var nodeHeight =
            NodeVisualHeight(
                visual);

        var minimumLeft =
            bounds.Left +
            _locationContentPadding;

        var minimumTop =
            bounds.Top +
            _locationHeaderHeight +
            _locationContentPadding;

        var maximumLeft =
            Math.Max(
                minimumLeft,
                bounds.Right -
                _locationContentPadding -
                _nodeWidth);

        var maximumTop =
            Math.Max(
                minimumTop,
                bounds.Bottom -
                _locationContentPadding -
                nodeHeight);

        return new Point(
            Math.Max(
                minimumLeft,
                Math.Min(
                    maximumLeft,
                    desiredLeft)),
            Math.Max(
                minimumTop,
                Math.Min(
                    maximumTop,
                    desiredTop)));
    }

    private Size MinimumLocationExpandedSize(
        Guid locationId)
    {
        var visual =
            LocationVisual(
                locationId);

        if (visual == null)
        {
            return new Size(
                _locationMinWidth,
                _locationMinHeight);
        }

        var left =
            LocationLeft(
                visual);

        var top =
            LocationTop(
                visual);

        var requiredRight =
            left +
            _locationMinWidth;

        var requiredBottom =
            top +
            _locationMinHeight;

        foreach (var child in
            _locationVisualsById.Values
                .Where(
                    item =>
                        item.ParentLocationId ==
                        locationId))
        {
            var childBounds =
                ExpandedLocationBounds(
                    child);

            requiredRight =
                Math.Max(
                    requiredRight,
                    childBounds.Right +
                    _locationContentPadding);

            requiredBottom =
                Math.Max(
                    requiredBottom,
                    childBounds.Bottom +
                    _locationContentPadding);
        }

        if (_lastMapSnapshot != null)
        {
            foreach (var node in
                _lastMapSnapshot.Nodes
                    .Where(
                        item =>
                            item.LocationId ==
                            locationId))
            {
                MapNodeVisual nodeVisual;

                if (!_nodeVisualsByIdentity.TryGetValue(
                        NodeIdentity(node),
                        out nodeVisual))
                {
                    continue;
                }

                var nodeBounds =
                    NodeBounds(
                        nodeVisual);

                requiredRight =
                    Math.Max(
                        requiredRight,
                        nodeBounds.Right +
                        _locationContentPadding);

                requiredBottom =
                    Math.Max(
                        requiredBottom,
                        nodeBounds.Bottom +
                        _locationContentPadding);
            }
        }

        return new Size(
            Math.Max(
                _locationMinWidth,
                requiredRight - left),
            Math.Max(
                _locationMinHeight,
                requiredBottom - top));
    }

    private Size MaximumLocationExpandedSizeInParent(
        MapLocationVisual visual)
    {
        if (visual == null ||
            !visual.ParentLocationId.HasValue)
        {
            return new Size(
                double.PositiveInfinity,
                double.PositiveInfinity);
        }

        MapLocationVisual parent;

        if (!_locationVisualsById.TryGetValue(
                visual.ParentLocationId.Value,
                out parent))
        {
            return new Size(
                double.PositiveInfinity,
                double.PositiveInfinity);
        }

        var parentBounds =
            ExpandedLocationBounds(
                parent);

        return new Size(
            Math.Max(
                _locationMinWidth,
                parentBounds.Right -
                _locationContentPadding -
                LocationLeft(
                    visual)),
            Math.Max(
                _locationMinHeight,
                parentBounds.Bottom -
                _locationContentPadding -
                LocationTop(
                    visual)));
    }

    private void OnMapLocationResizeDragDelta(
        object sender,
        DragDeltaEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        var visual =
            LocationVisual(
                locationId.Value);

        if (visual == null ||
            visual.IsLocked ||
            visual.IsCollapsed)
        {
            return;
        }

        StopStartupTopologyFit();

        var minimum =
            MinimumLocationExpandedSize(
                locationId.Value);

        var maximum =
            MaximumLocationExpandedSizeInParent(
                visual);

        visual.ExpandedWidth =
            Math.Max(
                minimum.Width,
                Math.Min(
                    maximum.Width,
                    visual.ExpandedWidth +
                    e.HorizontalChange));

        visual.ExpandedHeight =
            Math.Max(
                minimum.Height,
                Math.Min(
                    maximum.Height,
                    visual.ExpandedHeight +
                    e.VerticalChange));

        UpdateLocationVisualState(
            visual);

        e.Handled = true;
    }

    private void OnMapLocationResizeDragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        var visual =
            LocationVisual(
                locationId.Value);

        if (visual != null &&
            !visual.IsLocked &&
            !visual.IsCollapsed)
        {
            TrySaveLocationLayout(
                visual);
        }

        e.Handled = true;
    }

    private void OnMapLocationCollapseClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        ToggleLocationCollapsed(
            locationId.Value);

        e.Handled = true;
    }

    private void ToggleLocationCollapsed(
        Guid locationId)
    {
        var visual =
            LocationVisual(
                locationId);

        if (visual == null)
        {
            return;
        }

        visual.IsCollapsed =
            !visual.IsCollapsed;

        UpdateLocationVisualState(
            visual);

        UpdateLocationHierarchyVisibility();
        UpdateLinksForCurrentNodePositions();

        TrySaveLocationLayout(
            visual);
    }

    private void ToggleLocationLocked(
        Guid locationId,
        bool? locked = null)
    {
        var visual =
            LocationVisual(
                locationId);

        if (visual == null)
        {
            return;
        }

        visual.IsLocked =
            locked ??
            !visual.IsLocked;

        UpdateLocationVisualState(
            visual);

        TrySaveLocationLayout(
            visual);

        UpdateSelectedLayoutControl();
    }

    private void OnMapLocationContextMenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        var menu =
            sender as ContextMenu;

        var target =
            menu == null
                ? null
                : menu.Tag
                    as FrameworkElement;

        if (target == null ||
            !(target.Tag is Guid) ||
            menu.Items.Count < 6)
        {
            return;
        }

        var visual =
            LocationVisual(
                (Guid)target.Tag);

        if (visual == null)
        {
            return;
        }

        var edit =
            menu.Items[0]
                as MenuItem;

        var collapse =
            menu.Items[2]
                as MenuItem;

        var lockItem =
            menu.Items[3]
                as MenuItem;

        var delete =
            menu.Items[5]
                as MenuItem;

        if (edit != null)
        {
            edit.Header =
                UiText.Get(
                    "MapLocationEdit");
        }

        if (collapse != null)
        {
            collapse.Header =
                UiText.Get(
                    visual.IsCollapsed
                        ? "MapLocationExpand"
                        : "MapLocationCollapse");
        }

        if (lockItem != null)
        {
            lockItem.Header =
                UiText.Get(
                    visual.IsLocked
                        ? "MapLocationUnlock"
                        : "MapLocationLock");
        }

        if (delete != null)
        {
            delete.Header =
                UiText.Get(
                    "LocationTopologyDelete");
        }
    }

    private async void OnMapLocationContextEditClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            await OpenLocationTopologyEditorAsync(
                locationId.Value);
        }
    }

    private void OnMapLocationContextCollapseClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            ToggleLocationCollapsed(
                locationId.Value);
        }
    }

    private void OnMapLocationContextLockClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            ToggleLocationLocked(
                locationId.Value);
        }
    }

    private async void OnMapLocationContextDeleteClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            await DeleteLocationFromMapAsync(
                locationId.Value);
        }
    }

    private async Task DeleteLocationFromMapAsync(
        Guid locationId)
    {
        var location =
            _lastMapSnapshot == null
                ? null
                : _lastMapSnapshot.Locations
                    .FirstOrDefault(
                        item =>
                            item.Id ==
                            locationId);

        if (location == null)
        {
            return;
        }

        var answer =
            MessageBox.Show(
                this,
                UiText.Format(
                    "LocationTopologyDeleteConfirm",
                    location.Name),
                UiText.Get(
                    "LocationTopologyWindowTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

        if (answer !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _locationTopologyService.DeleteLocation(
                locationId);

            _selectedLocationId = null;
            _persistedLocationLayouts.Remove(
                locationId);

            await RefreshTopologyAsync();
        }
        catch (Exception error)
        {
            Trace.TraceError(
                "LOCATION_MAP_DELETE_FAILED " +
                error);

            MessageBox.Show(
                this,
                UiText.Get(
                    "LocationTopologyDeleteBlocked"),
                UiText.Get(
                    "LocationTopologyWindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static Guid? MapElementIdFromMenuItem(
        object sender)
    {
        var menuItem =
            sender as MenuItem;

        var target =
            menuItem == null
                ? null
                : menuItem.Tag as FrameworkElement;

        return target != null &&
               target.Tag is Guid
            ? (Guid?)((Guid)target.Tag)
            : null;
    }

    private bool IsManualDevice(
        Guid deviceId)
    {
        try
        {
            return _manualTopologyService
                .GetSnapshot()
                .Devices
                .Any(
                    item =>
                        item.DeviceId == deviceId &&
                        item.IsManual);
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "MANUAL_TOPOLOGY_MAP_DEVICE_LOOKUP_FAILED " +
                exception);

            return false;
        }
    }

    private bool IsManualLink(
        Guid physicalLinkId)
    {
        try
        {
            return _manualTopologyService
                .GetSnapshot()
                .Links
                .Any(
                    item =>
                        item.PhysicalLinkId ==
                            physicalLinkId &&
                        item.IsManual);
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "MANUAL_TOPOLOGY_MAP_LINK_LOOKUP_FAILED " +
                exception);

            return false;
        }
    }

    private async Task DeleteManualDeviceFromMapAsync(
        Guid deviceId)
    {
        ManualTopologyEditorSnapshot snapshot;

        try
        {
            snapshot =
                _manualTopologyService
                    .GetSnapshot();
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
            return;
        }

        var device =
            snapshot.Devices
                .FirstOrDefault(
                    item =>
                        item.DeviceId == deviceId);

        if (device == null ||
            !device.IsManual)
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualDevice");
            return;
        }

        if (snapshot.Links.Any(
                link =>
                    link.DeviceAId == deviceId ||
                    link.DeviceBId == deviceId))
        {
            ShowManualTopologyWarning(
                "ManualTopologyDeleteConnectedDevice");
            return;
        }

        if (!ConfirmManualTopologyDelete(
                "ManualTopologyConfirmDeleteDevice",
                device.DisplayName))
        {
            return;
        }

        try
        {
            _manualTopologyService.DeleteDevice(
                deviceId);

            if (_selectedDeviceId == deviceId)
            {
                _selectedDeviceId = null;
            }

            await RefreshTopologyAsync();
        }
        catch (InvalidOperationException exception)
        {
            Trace.TraceError(
                "MANUAL_TOPOLOGY_MAP_DELETE_DEVICE_BLOCKED " +
                exception);

            ShowManualTopologyWarning(
                "ManualTopologyDeleteConnectedDevice");
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
        }
    }

    private async Task DeleteManualLinkFromMapAsync(
        Guid physicalLinkId)
    {
        ManualTopologyEditorSnapshot snapshot;

        try
        {
            snapshot =
                _manualTopologyService
                    .GetSnapshot();
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
            return;
        }

        var link =
            snapshot.Links
                .FirstOrDefault(
                    item =>
                        item.PhysicalLinkId ==
                            physicalLinkId);

        if (link == null ||
            !link.IsManual)
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualLink");
            return;
        }

        if (!ConfirmManualTopologyDelete(
                "ManualTopologyConfirmDeleteLink",
                ManualTopologyLinkDisplayName(
                    snapshot,
                    link)))
        {
            return;
        }

        try
        {
            _manualTopologyService.DeleteLink(
                physicalLinkId);

            if (_selectedPhysicalLinkId ==
                physicalLinkId)
            {
                _selectedPhysicalLinkId = null;
            }

            await RefreshTopologyAsync();
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
        }
    }

    private static string ManualTopologyLinkDisplayName(
        ManualTopologyEditorSnapshot snapshot,
        ManualTopologyLinkItem link)
    {
        var sideA =
            snapshot.Devices
                .FirstOrDefault(
                    item =>
                        item.DeviceId ==
                            link.DeviceAId);

        var sideB =
            snapshot.Devices
                .FirstOrDefault(
                    item =>
                        item.DeviceId ==
                            link.DeviceBId);

        return
            (sideA == null
                ? link.DeviceAId.ToString("D")
                : sideA.DisplayName) +
            " ↔ " +
            (sideB == null
                ? link.DeviceBId.ToString("D")
                : sideB.DisplayName);
    }

    private bool ConfirmManualTopologyDelete(
        string messageKey,
        string displayName)
    {
        return MessageBox.Show(
            this,
            UiText.Format(
                messageKey,
                displayName),
            UiText.Get(
                "ManualTopologyConfirmDeleteTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) ==
            MessageBoxResult.Yes;
    }

    private void ShowManualTopologyWarning(
        string messageKey)
    {
        MessageBox.Show(
            this,
            UiText.Get(
                messageKey),
            UiText.Get(
                "ManualTopologyWarningTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void ShowManualTopologyOperationFailure(
        Exception exception)
    {
        Trace.TraceError(
            "MANUAL_TOPOLOGY_MAP_OPERATION_FAILED " +
            exception);

        MessageBox.Show(
            this,
            UiText.Get(
                "ManualTopologyOperationFailed"),
            UiText.Get(
                "ManualTopologyErrorTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnMapHelpClick(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            UiText.Get(
                "MapHelpBody"),
            UiText.Get(
                "MapHelpTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnMapSettingsClick(
        object sender,
        RoutedEventArgs e)
    {
        if (MapSettingsButton.ContextMenu == null)
        {
            return;
        }

        MapSettingsButton.ContextMenu.PlacementTarget =
            MapSettingsButton;

        MapSettingsButton.ContextMenu.IsOpen =
            true;
    }

    private void OnMapMotionNormalClick(
        object sender,
        RoutedEventArgs e)
    {
        SetMotionMode(
            MapMotionMode.Normal);
    }

    private void OnMapMotionReducedClick(
        object sender,
        RoutedEventArgs e)
    {
        SetMotionMode(
            MapMotionMode.Reduced);
    }

    private void OnMapMotionOffClick(
        object sender,
        RoutedEventArgs e)
    {
        SetMotionMode(
            MapMotionMode.Off);
    }

    private void OnMapLockSelectedChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (_suppressLockSelectedChange)
        {
            return;
        }

        if (_selectedLocationId.HasValue)
        {
            ToggleLocationLocked(
                _selectedLocationId.Value,
                MapLockSelectedCheckBox.IsChecked ==
                    true);

            return;
        }

        if (!_selectedDeviceId.HasValue)
        {
            return;
        }

        var visual =
            _nodeVisualsByIdentity.Values
                .FirstOrDefault(
                    item =>
                        item.DeviceId.HasValue &&
                        item.DeviceId.Value ==
                            _selectedDeviceId.Value);

        if (visual == null)
        {
            return;
        }

        visual.IsLocked =
            MapLockSelectedCheckBox.IsChecked ==
            true;

        UpdateNodeLockPresentation(
            visual);

        TrySaveDeviceLayout(
            visual);
    }

    private void OnMapPreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        StopStartupTopologyFit();

        ChangeZoom(
            _zoom +
            (e.Delta > 0
                ? _zoomStep
                : -_zoomStep));

        e.Handled = true;
    }

    private void OnMapPreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Middle)
        {
            return;
        }

        StopStartupTopologyFit();

        _isPanning = true;
        _panStartPoint =
            e.GetPosition(
                MapScrollViewer);

        _panStartHorizontalOffset =
            MapScrollViewer.HorizontalOffset;

        _panStartVerticalOffset =
            MapScrollViewer.VerticalOffset;

        MapScrollViewer.Cursor =
            Cursors.SizeAll;

        MapScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void OnMapPreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!_isPanning ||
            e.MiddleButton !=
                MouseButtonState.Pressed)
        {
            return;
        }

        var current =
            e.GetPosition(
                MapScrollViewer);

        MapScrollViewer
            .ScrollToHorizontalOffset(
                Math.Max(
                    0.0,
                    _panStartHorizontalOffset -
                    (current.X -
                     _panStartPoint.X)));

        MapScrollViewer
            .ScrollToVerticalOffset(
                Math.Max(
                    0.0,
                    _panStartVerticalOffset -
                    (current.Y -
                     _panStartPoint.Y)));

        e.Handled = true;
    }

    private void OnMapPreviewMouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_isPanning ||
            e.ChangedButton !=
                MouseButton.Middle)
        {
            return;
        }

        _isPanning = false;

        if (MapScrollViewer.IsMouseCaptured)
        {
            MapScrollViewer.ReleaseMouseCapture();
        }

        MapScrollViewer.Cursor = null;

        TrySaveViewportLayout();
        e.Handled = true;
    }

    private async void OnMapLinkMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        var physicalLinkId =
            (Guid)element.Tag;

        StopStartupTopologyFit();

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedPhysicalLinkId =
            physicalLinkId;

        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();

        if (e.ClickCount >= 2 &&
            IsManualLink(
                physicalLinkId))
        {
            e.Handled = true;

            await OpenManualTopologyEditorAsync(
                null,
                physicalLinkId);

            return;
        }

        e.Handled = true;
    }

    private void OnMapLinkMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        StopStartupTopologyFit();

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedPhysicalLinkId =
            (Guid)element.Tag;

        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private void OnMapLinkContextMenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        var menu =
            sender as ContextMenu;

        var target =
            menu == null
                ? null
                : menu.Tag as FrameworkElement;

        var canChange =
            target != null &&
            target.Tag is Guid &&
            IsManualLink(
                (Guid)target.Tag);

        UpdateMapElementContextMenuState(
            menu,
            canChange);
    }

    private async void OnMapLinkContextEditClick(
        object sender,
        RoutedEventArgs e)
    {
        var physicalLinkId =
            MapElementIdFromMenuItem(sender);

        if (!physicalLinkId.HasValue ||
            !IsManualLink(
                physicalLinkId.Value))
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualLink");
            return;
        }

        await OpenManualTopologyEditorAsync(
            null,
            physicalLinkId.Value);
    }

    private async void OnMapLinkContextDeleteClick(
        object sender,
        RoutedEventArgs e)
    {
        var physicalLinkId =
            MapElementIdFromMenuItem(sender);

        if (!physicalLinkId.HasValue)
        {
            return;
        }

        await DeleteManualLinkFromMapAsync(
            physicalLinkId.Value);
    }

    private async void OnMapLinkKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
        {
            return;
        }

        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        e.Handled = true;

        await DeleteManualLinkFromMapAsync(
            (Guid)element.Tag);
    }

    private void OnMapCanvasMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(
            e.OriginalSource,
            MapCanvas))
        {
            return;
        }

        StopStartupTopologyFit();

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedPhysicalLinkId = null;
        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

}
