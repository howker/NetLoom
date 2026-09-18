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
    private void ReconcileNodes(
        IReadOnlyList<MapNode> nodes,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var desiredIdentities =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            var identity =
                NodeIdentity(node);

            if (!desiredIdentities.Add(identity))
            {
                throw new InvalidOperationException(
                    "Map snapshot contains duplicate stable node identity.");
            }

            MapNodeVisual visual;
            var created = false;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    identity,
                    out visual))
            {
                visual =
                    CreateNodeVisual();

                created = true;

                _nodeVisualsByIdentity.Add(
                    identity,
                    visual);

                var left = node.X;
                var top = node.Y;

                if (node.DeviceId.HasValue)
                {
                    MapDeviceLayout persisted;

                    if (_persistedDeviceLayouts.TryGetValue(
                            node.DeviceId.Value,
                            out persisted))
                    {
                        left = persisted.X;
                        top = persisted.Y;
                    }
                }

                Canvas.SetLeft(
                    visual.Border,
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            left,
                            _virtualOriginX));

                Canvas.SetTop(
                    visual.Border,
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            top,
                            _virtualOriginY));

                Panel.SetZIndex(
                    visual.Border,
                    2);

                MapCanvas.Children.Add(
                    visual.Border);
            }
            else
            {
                if (double.IsNaN(
                    Canvas.GetLeft(
                        visual.Border)))
                {
                    Canvas.SetLeft(
                        visual.Border,
                        MapVirtualWorkspace
                            .ToCanvasCoordinate(
                                node.X,
                                _virtualOriginX));
                }

                if (double.IsNaN(
                    Canvas.GetTop(
                        visual.Border)))
                {
                    Canvas.SetTop(
                        visual.Border,
                        MapVirtualWorkspace
                            .ToCanvasCoordinate(
                                node.Y,
                                _virtualOriginY));
                }
            }

            UpdateNodeVisual(
                visual,
                node,
                locations);

            if (created)
            {
                AnimateAppearance(
                    visual.Border);
            }
        }

        foreach (var identity in
            _nodeVisualsByIdentity.Keys
                .Where(
                    key =>
                        !desiredIdentities.Contains(
                            key))
                .ToArray())
        {
            var visual =
                _nodeVisualsByIdentity[
                    identity];

            _nodeVisualsByIdentity.Remove(
                identity);

            AnimateRemoval(
                visual.Border,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Border));
        }

        _nodeBordersByDeviceId.Clear();

        foreach (var node in nodes)
        {
            if (!node.DeviceId.HasValue)
            {
                continue;
            }

            MapNodeVisual visual;

            if (_nodeVisualsByIdentity.TryGetValue(
                NodeIdentity(node),
                out visual))
            {
                _nodeBordersByDeviceId[
                    node.DeviceId.Value] =
                    visual.Border;
            }
        }
    }

    private void ReconcileLocations(
        IReadOnlyList<MapLocation> locations,
        IReadOnlyList<MapNode> nodes)
    {
        var desiredIds =
            new HashSet<Guid>(
                locations.Select(
                    item => item.Id));

        var byId =
            locations.ToDictionary(
                item => item.Id);

        var ordered =
            locations
                .OrderBy(
                    item =>
                        LocationDepth(
                            item,
                            byId))
                .ThenBy(
                    item => item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Id)
                .ToArray();

        for (var index = 0;
             index < ordered.Length;
             index++)
        {
            var location =
                ordered[index];

            MapLocationVisual visual;
            var created = false;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                visual =
                    CreateLocationVisual();

                created = true;

                _locationVisualsById.Add(
                    location.Id,
                    visual);

                MapCanvas.Children.Add(
                    visual.Border);
            }

            MapLocationLayout layout = null;

            if (_persistedLocationLayouts.TryGetValue(
                    location.Id,
                    out layout))
            {
                ApplyLocationLayout(
                    visual,
                    layout);
            }
            else if (created)
            {
                layout =
                    CreateDefaultLocationLayout(
                        location,
                        index,
                        nodes,
                        locations);

                ApplyLocationLayout(
                    visual,
                    layout);
            }

            Panel.SetZIndex(
                visual.Border,
                -100 +
                LocationDepth(
                    location,
                    byId));

            UpdateLocationVisual(
                visual,
                location,
                byId);

            if (created)
            {
                AnimateAppearance(
                    visual.Border);
            }
        }

        foreach (var locationId in
            _locationVisualsById.Keys
                .Where(
                    id =>
                        !desiredIds.Contains(
                            id))
                .ToArray())
        {
            var visual =
                _locationVisualsById[
                    locationId];

            _locationVisualsById.Remove(
                locationId);

            AnimateRemoval(
                visual.Border,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Border));
        }

        if (_selectedLocationId.HasValue &&
            !desiredIds.Contains(
                _selectedLocationId.Value))
        {
            _selectedLocationId = null;
        }

        UpdateLocationSelectionPresentation();
    }

    private void NormalizeLocationHierarchy(
        IReadOnlyList<MapLocation> locations,
        IReadOnlyList<MapNode> nodes)
    {
        if (locations == null ||
            locations.Count == 0)
        {
            return;
        }

        var byId =
            locations.ToDictionary(
                item => item.Id);

        // Сначала увеличиваем только размер контейнеров снизу вверх.
        // Позиция родителя остаётся стабильной.
        // Иерархия не должна тянуть его к ошибочно сохранённому ребёнку.
        foreach (var location in
            locations
                .OrderByDescending(
                    item =>
                        LocationDepth(
                            item,
                            byId)))
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                continue;
            }

            var requiredWidth =
                _locationMinWidth;

            var requiredHeight =
                _locationMinHeight;

            foreach (var child in
                locations.Where(
                    item =>
                        item.ParentLocationId ==
                        location.Id))
            {
                MapLocationVisual childVisual;

                if (!_locationVisualsById.TryGetValue(
                        child.Id,
                        out childVisual))
                {
                    continue;
                }

                requiredWidth =
                    Math.Max(
                        requiredWidth,
                        Math.Max(
                            _locationMinWidth,
                            childVisual.ExpandedWidth) +
                        (2.0 *
                         _locationContentPadding));

                requiredHeight =
                    Math.Max(
                        requiredHeight,
                        Math.Max(
                            _locationMinHeight,
                            childVisual.ExpandedHeight) +
                        _locationHeaderHeight +
                        (2.0 *
                         _locationContentPadding));
            }

            foreach (var node in
                nodes.Where(
                    item =>
                        item.LocationId ==
                        location.Id))
            {
                MapNodeVisual nodeVisual;

                if (!_nodeVisualsByIdentity.TryGetValue(
                        NodeIdentity(node),
                        out nodeVisual))
                {
                    continue;
                }

                requiredWidth =
                    Math.Max(
                        requiredWidth,
                        _nodeWidth +
                        (2.0 *
                         _locationContentPadding));

                requiredHeight =
                    Math.Max(
                        requiredHeight,
                        NodeVisualHeight(
                            nodeVisual) +
                        _locationHeaderHeight +
                        (2.0 *
                         _locationContentPadding));
            }

            var widthChanged =
                visual.ExpandedWidth + 0.001 <
                requiredWidth;

            var heightChanged =
                visual.ExpandedHeight + 0.001 <
                requiredHeight;

            if (!widthChanged &&
                !heightChanged)
            {
                continue;
            }

            visual.ExpandedWidth =
                Math.Max(
                    visual.ExpandedWidth,
                    requiredWidth);

            visual.ExpandedHeight =
                Math.Max(
                    visual.ExpandedHeight,
                    requiredHeight);

            UpdateLocationVisualState(
                visual);

            if (_persistedLocationLayouts.ContainsKey(
                    location.Id))
            {
                TrySaveLocationLayout(
                    visual);
            }
        }

        // Затем сверху вниз возвращаем каждый дочерний контейнер и устройство внутрь физического родителя.
        // При сдвиге Location перемещается всё его поддерево, а не только рамка.
        foreach (var location in
            locations
                .OrderBy(
                    item =>
                        LocationDepth(
                            item,
                            byId)))
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                continue;
            }

            if (visual.ParentLocationId.HasValue)
            {
                var left =
                    LocationLeft(
                        visual);

                var top =
                    LocationTop(
                        visual);

                var constrained =
                    ConstrainLocationPositionToParent(
                        visual,
                        left,
                        top);

                var deltaX =
                    constrained.X - left;

                var deltaY =
                    constrained.Y - top;

                if (Math.Abs(deltaX) > 0.001 ||
                    Math.Abs(deltaY) > 0.001)
                {
                    MoveLocationSubtreeByDelta(
                        location.Id,
                        deltaX,
                        deltaY);
                }
            }

            foreach (var node in
                nodes.Where(
                    item =>
                        item.LocationId ==
                        location.Id))
            {
                MapNodeVisual nodeVisual;

                if (!_nodeVisualsByIdentity.TryGetValue(
                        NodeIdentity(node),
                        out nodeVisual))
                {
                    continue;
                }

                var left =
                    NodeLeft(
                        nodeVisual);

                var top =
                    NodeTop(
                        nodeVisual);

                var constrained =
                    ConstrainNodePositionToLocation(
                        nodeVisual,
                        left,
                        top);

                if (Math.Abs(
                        constrained.X - left) <= 0.001 &&
                    Math.Abs(
                        constrained.Y - top) <= 0.001)
                {
                    continue;
                }

                Canvas.SetLeft(
                    nodeVisual.Border,
                    constrained.X);

                Canvas.SetTop(
                    nodeVisual.Border,
                    constrained.Y);

                if (node.DeviceId.HasValue &&
                    _persistedDeviceLayouts.ContainsKey(
                        node.DeviceId.Value))
                {
                    TrySaveDeviceLayout(
                        nodeVisual);
                }
            }
        }
    }

    private void MoveLocationSubtreeByDelta(
        Guid locationId,
        double deltaX,
        double deltaY)
    {
        if (Math.Abs(deltaX) <= 0.001 &&
            Math.Abs(deltaY) <= 0.001)
        {
            return;
        }

        var includedLocations =
            DescendantLocationIds(
                locationId);

        includedLocations.Add(
            locationId);

        foreach (var currentLocationId in
            includedLocations)
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    currentLocationId,
                    out visual))
            {
                continue;
            }

            Canvas.SetLeft(
                visual.Border,
                LocationLeft(
                    visual) +
                deltaX);

            Canvas.SetTop(
                visual.Border,
                LocationTop(
                    visual) +
                deltaY);

            if (_persistedLocationLayouts.ContainsKey(
                    currentLocationId))
            {
                TrySaveLocationLayout(
                    visual);
            }
        }

        if (_lastMapSnapshot == null)
        {
            return;
        }

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            if (!node.LocationId.HasValue ||
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

            Canvas.SetLeft(
                visual.Border,
                NodeLeft(
                    visual) +
                deltaX);

            Canvas.SetTop(
                visual.Border,
                NodeTop(
                    visual) +
                deltaY);

            if (node.DeviceId.HasValue &&
                _persistedDeviceLayouts.ContainsKey(
                    node.DeviceId.Value))
            {
                TrySaveDeviceLayout(
                    visual);
            }
        }
    }

    private void UpdateLocationHierarchyVisibility()
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        var byId =
            _lastMapSnapshot.Locations
                .ToDictionary(
                    item => item.Id);

        foreach (var location in
            _lastMapSnapshot.Locations)
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                continue;
            }

            visual.Border.Visibility =
                HasCollapsedLocationAncestor(
                    location,
                    byId,
                    includeSelf: false)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            MapNodeVisual visual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out visual))
            {
                continue;
            }

            var hidden = false;

            if (node.LocationId.HasValue)
            {
                MapLocation location;

                if (byId.TryGetValue(
                        node.LocationId.Value,
                        out location))
                {
                    hidden =
                        HasCollapsedLocationAncestor(
                            location,
                            byId,
                            includeSelf: true);
                }
            }

            visual.Border.Visibility =
                hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }
    }

    private bool HasCollapsedLocationAncestor(
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations,
        bool includeSelf)
    {
        var current =
            includeSelf
                ? location
                : null;

        var parentId =
            includeSelf
                ? (Guid?)location.Id
                : location.ParentLocationId;

        var visited =
            new HashSet<Guid>();

        while (parentId.HasValue &&
               visited.Add(
                   parentId.Value))
        {
            MapLocation candidate;

            if (!locations.TryGetValue(
                    parentId.Value,
                    out candidate))
            {
                break;
            }

            MapLocationVisual visual;

            if (_locationVisualsById.TryGetValue(
                    candidate.Id,
                    out visual) &&
                visual.IsCollapsed)
            {
                return true;
            }

            current = candidate;
            parentId =
                current.ParentLocationId;
        }

        return false;
    }

    private MapLocationVisual CreateLocationVisual()
    {
        var title =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationTitle"),
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var lockBadge =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationLockBadge"),
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var collapseButton =
            new Button
            {
                MinWidth = 28.0,
                MinHeight = 24.0,
                Padding = new Thickness(
                    4.0,
                    0.0,
                    4.0,
                    0.0),
                Focusable = false,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        collapseButton.Click +=
            OnMapLocationCollapseClick;

        var headerGrid =
            new Grid
            {
                Height = _locationHeaderHeight,
                Cursor = Cursors.SizeAll
            };

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1.0,
                        GridUnitType.Star)
            });

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid.SetColumn(
            title,
            0);

        Grid.SetColumn(
            lockBadge,
            1);

        Grid.SetColumn(
            collapseButton,
            2);

        headerGrid.Children.Add(
            title);

        headerGrid.Children.Add(
            lockBadge);

        headerGrid.Children.Add(
            collapseButton);

        var header =
            new Border
            {
                Background =
                    FindResource(
                        "NetLoom.Brush.AccentSoft")
                        as Brush,
                Padding =
                    GetThicknessResource(
                        "NetLoom.Thickness.MapLocationHeader"),
                Child = headerGrid
            };

        var description =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationDescription"),
                Margin =
                    GetThicknessResource(
                        "NetLoom.Thickness.MapLocationDescription"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var resizeThumb =
            new Thumb
            {
                Width =
                    _locationResizeThumbSize,
                Height =
                    _locationResizeThumbSize,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                Cursor =
                    Cursors.SizeNWSE,
                Focusable = false
            };

        resizeThumb.DragDelta +=
            OnMapLocationResizeDragDelta;

        resizeThumb.DragCompleted +=
            OnMapLocationResizeDragCompleted;

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1.0,
                        GridUnitType.Star)
            });

        Grid.SetRow(
            header,
            0);

        Grid.SetRow(
            description,
            1);

        Grid.SetRowSpan(
            resizeThumb,
            2);

        root.Children.Add(
            header);

        root.Children.Add(
            description);

        root.Children.Add(
            resizeThumb);

        var border =
            new Border
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationContainer"),
                Child = root,
                Focusable = false
            };

        border.ContextMenu =
            CreateLocationContextMenu(
                border);

        header.MouseLeftButtonDown +=
            OnMapLocationMouseLeftButtonDown;

        header.MouseMove +=
            OnMapLocationMouseMove;

        header.MouseLeftButtonUp +=
            OnMapLocationMouseLeftButtonUp;

        header.MouseRightButtonDown +=
            OnMapLocationMouseRightButtonDown;

        border.MouseLeftButtonDown +=
            OnMapLocationBodyMouseLeftButtonDown;

        return new MapLocationVisual(
            border,
            header,
            title,
            description,
            collapseButton,
            lockBadge,
            resizeThumb);
    }

    private ContextMenu CreateLocationContextMenu(
        Border target)
    {
        var menu =
            new ContextMenu
            {
                Tag = target
            };

        var edit =
            new MenuItem
            {
                Tag = target
            };

        edit.Click +=
            OnMapLocationContextEditClick;

        var collapse =
            new MenuItem
            {
                Tag = target
            };

        collapse.Click +=
            OnMapLocationContextCollapseClick;

        var lockItem =
            new MenuItem
            {
                Tag = target
            };

        lockItem.Click +=
            OnMapLocationContextLockClick;

        var delete =
            new MenuItem
            {
                Tag = target
            };

        delete.Click +=
            OnMapLocationContextDeleteClick;

        menu.Items.Add(
            edit);

        menu.Items.Add(
            new Separator());

        menu.Items.Add(
            collapse);

        menu.Items.Add(
            lockItem);

        menu.Items.Add(
            new Separator());

        menu.Items.Add(
            delete);

        menu.Opened +=
            OnMapLocationContextMenuOpened;

        return menu;
    }

    private void UpdateLocationVisual(
        MapLocationVisual visual,
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        visual.LocationId =
            location.Id;

        visual.ParentLocationId =
            location.ParentLocationId;

        visual.Title.Text =
            location.Name;

        visual.Description.Text =
            string.IsNullOrWhiteSpace(
                location.Description)
                ? UiText.Get(
                    "MapLocationNoDescription")
                : location.Description;

        visual.Border.Tag =
            location.Id;

        visual.Header.Tag =
            location.Id;

        visual.CollapseButton.Tag =
            location.Id;

        visual.ResizeThumb.Tag =
            location.Id;

        visual.Border.ToolTip =
            UiText.Format(
                "MapLocationToolTip",
                BuildLocationPath(
                    location,
                    locations),
                string.IsNullOrWhiteSpace(
                    location.Description)
                    ? UiText.Get(
                        "MapLocationNoDescription")
                    : location.Description);

        UpdateLocationVisualState(
            visual);
    }

    private bool TryGetExpandedLocationBounds(
        Guid locationId,
        out Rect bounds)
    {
        MapLocationVisual visual;

        if (_locationVisualsById.TryGetValue(
                locationId,
                out visual))
        {
            bounds =
                ExpandedLocationBounds(
                    visual);

            return true;
        }

        MapLocationLayout layout;

        if (_persistedLocationLayouts.TryGetValue(
                locationId,
                out layout))
        {
            bounds =
                new Rect(
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            layout.X,
                            _virtualOriginX),
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            layout.Y,
                            _virtualOriginY),
                    Math.Max(
                        _locationMinWidth,
                        layout.Width),
                    Math.Max(
                        _locationMinHeight,
                        layout.Height));

            return true;
        }

        bounds = Rect.Empty;
        return false;
    }

    private MapLocationLayout CreateDefaultLocationLayout(
        MapLocation location,
        int order,
        IReadOnlyList<MapNode> nodes,
        IReadOnlyList<MapLocation> locations)
    {
        var bounds =
            new List<Rect>();

        foreach (var node in nodes)
        {
            if (node.LocationId !=
                location.Id)
            {
                continue;
            }

            MapNodeVisual nodeVisual;

            if (_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out nodeVisual))
            {
                bounds.Add(
                    NodeBounds(
                        nodeVisual));
            }
        }

        foreach (var child in locations)
        {
            if (child.ParentLocationId !=
                location.Id)
            {
                continue;
            }

            MapLocationVisual childVisual;

            if (_locationVisualsById.TryGetValue(
                    child.Id,
                    out childVisual))
            {
                bounds.Add(
                    ExpandedLocationBounds(
                        childVisual));
            }
        }

        if (bounds.Count > 0)
        {
            var left =
                bounds.Min(
                    item => item.Left) -
                _locationContentPadding;

            var top =
                bounds.Min(
                    item => item.Top) -
                _locationContentPadding -
                _locationHeaderHeight;

            var right =
                bounds.Max(
                    item => item.Right) +
                _locationContentPadding;

            var bottom =
                bounds.Max(
                    item => item.Bottom) +
                _locationContentPadding;

            var width =
                Math.Max(
                    _locationMinWidth,
                    right - left);

            var height =
                Math.Max(
                    _locationMinHeight,
                    bottom - top);

            return new MapLocationLayout(
                location.Id,
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        left,
                        _virtualOriginX),
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        top,
                        _virtualOriginY),
                width,
                height,
                false,
                false);
        }

        if (location.ParentLocationId.HasValue)
        {
            Rect parentBounds;

            if (TryGetExpandedLocationBounds(
                    location.ParentLocationId.Value,
                    out parentBounds))
            {
                var availableWidth =
                    Math.Max(
                        _locationMinWidth,
                        parentBounds.Width -
                        (2.0 *
                         _locationContentPadding));

                var availableHeight =
                    Math.Max(
                        _locationMinHeight,
                        parentBounds.Height -
                        _locationHeaderHeight -
                        (2.0 *
                         _locationContentPadding));

                var width =
                    Math.Min(
                        _locationDefaultWidth,
                        availableWidth);

                var height =
                    Math.Min(
                        _locationDefaultHeight,
                        availableHeight);

                var left =
                    parentBounds.Left +
                    _locationContentPadding;

                var top =
                    parentBounds.Top +
                    _locationHeaderHeight +
                    _locationContentPadding;

                return new MapLocationLayout(
                    location.Id,
                    MapVirtualWorkspace
                        .ToLogicalCoordinate(
                            left,
                            _virtualOriginX),
                    MapVirtualWorkspace
                        .ToLogicalCoordinate(
                            top,
                            _virtualOriginY),
                    width,
                    height,
                    false,
                    false);
            }
        }

        var column =
            order % 3;

        var row =
            order / 3;

        return new MapLocationLayout(
            location.Id,
            (column *
                (_locationDefaultWidth + 56.0)) -
                (_locationDefaultWidth / 2.0),
            (row *
                (_locationDefaultHeight + 56.0)) -
                (_locationDefaultHeight / 2.0),
            _locationDefaultWidth,
            _locationDefaultHeight,
            false,
            false);
    }

    private static int LocationDepth(
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var depth = 0;
        var parentId =
            location.ParentLocationId;

        var visited =
            new HashSet<Guid>();

        while (parentId.HasValue &&
               visited.Add(
                   parentId.Value))
        {
            MapLocation parent;

            if (!locations.TryGetValue(
                    parentId.Value,
                    out parent))
            {
                break;
            }

            depth++;
            parentId =
                parent.ParentLocationId;
        }

        return depth;
    }

    private static string BuildLocationPath(
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var names =
            new List<string>();

        var current =
            location;

        var visited =
            new HashSet<Guid>();

        while (current != null &&
               visited.Add(
                   current.Id))
        {
            names.Add(
                current.Name);

            if (!current.ParentLocationId.HasValue)
            {
                break;
            }

            MapLocation parent;

            if (!locations.TryGetValue(
                    current.ParentLocationId.Value,
                    out parent))
            {
                break;
            }

            current = parent;
        }

        names.Reverse();

        return string.Join(
            " / ",
            names);
    }

    private void ApplyLocationLayout(
        MapLocationVisual visual,
        MapLocationLayout layout)
    {
        visual.IsCollapsed =
            layout.IsCollapsed;

        visual.IsLocked =
            layout.IsLocked;

        visual.ExpandedWidth =
            Math.Max(
                _locationMinWidth,
                layout.Width);

        visual.ExpandedHeight =
            Math.Max(
                _locationMinHeight,
                layout.Height);

        Canvas.SetLeft(
            visual.Border,
            MapVirtualWorkspace
                .ToCanvasCoordinate(
                    layout.X,
                    _virtualOriginX));

        Canvas.SetTop(
            visual.Border,
            MapVirtualWorkspace
                .ToCanvasCoordinate(
                    layout.Y,
                    _virtualOriginY));

        visual.Border.Width =
            visual.ExpandedWidth;

        visual.Border.Height =
            visual.IsCollapsed
                ? _locationHeaderHeight
                : visual.ExpandedHeight;
    }

    private Rect ExpandedLocationBounds(
        MapLocationVisual visual)
    {
        return new Rect(
            LocationLeft(
                visual),
            LocationTop(
                visual),
            Math.Max(
                _locationMinWidth,
                visual.ExpandedWidth),
            Math.Max(
                _locationMinHeight,
                visual.ExpandedHeight));
    }

    private Rect LocationVisibleBounds(
        MapLocationVisual visual)
    {
        return new Rect(
            LocationLeft(
                visual),
            LocationTop(
                visual),
            Math.Max(
                _locationMinWidth,
                visual.Border.Width),
            Math.Max(
                _locationHeaderHeight,
                visual.Border.Height));
    }

    private static double LocationLeft(
        MapLocationVisual visual)
    {
        var value =
            Canvas.GetLeft(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private static double LocationTop(
        MapLocationVisual visual)
    {
        var value =
            Canvas.GetTop(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private void UpdateLocationVisualState(
        MapLocationVisual visual)
    {
        visual.Border.Width =
            Math.Max(
                _locationMinWidth,
                visual.ExpandedWidth);

        visual.Border.Height =
            visual.IsCollapsed
                ? _locationHeaderHeight
                : Math.Max(
                    _locationMinHeight,
                    visual.ExpandedHeight);

        visual.Description.Visibility =
            visual.IsCollapsed
                ? Visibility.Collapsed
                : Visibility.Visible;

        visual.ResizeThumb.Visibility =
            !visual.IsCollapsed &&
            !visual.IsLocked
                ? Visibility.Visible
                : Visibility.Collapsed;

        visual.Header.Cursor =
            visual.IsLocked
                ? Cursors.Hand
                : Cursors.SizeAll;

        visual.CollapseButton.Content =
            visual.IsCollapsed
                ? "+"
                : "−";

        visual.CollapseButton.ToolTip =
            UiText.Get(
                visual.IsCollapsed
                    ? "MapLocationExpand"
                    : "MapLocationCollapse");

        visual.LockBadge.Text =
            visual.IsLocked
                ? UiText.Get(
                    "MapLocationLockedBadge")
                : string.Empty;

        UpdateLocationSelectionPresentation();
    }

    private void UpdateLocationSelectionPresentation()
    {
        var selectionBrush =
            FindResource(
                "NetLoom.Brush.Selection")
                as Brush;

        var normalBrush =
            FindResource(
                "NetLoom.Brush.BorderStrong")
                as Brush;

        foreach (var visual in
            _locationVisualsById.Values)
        {
            var selected =
                _selectedLocationId.HasValue &&
                visual.LocationId ==
                    _selectedLocationId.Value;

            visual.Border.BorderBrush =
                selected
                    ? selectionBrush
                    : normalBrush;

            visual.Border.BorderThickness =
                selected
                    ? new Thickness(3.0)
                    : new Thickness(1.0);
        }
    }

    private void ReconcileLinks(
        IReadOnlyList<MapLink> links,
        IReadOnlyDictionary<string, MapNode> nodes)
    {
        var desiredIdentities =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var link in links)
        {
            MapNode source;
            MapNode target;

            if (!nodes.TryGetValue(
                    link.SourceNodeKey,
                    out source) ||
                !nodes.TryGetValue(
                    link.TargetNodeKey,
                    out target))
            {
                continue;
            }

            MapNodeVisual sourceVisual;
            MapNodeVisual targetVisual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(source),
                    out sourceVisual) ||
                !_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(target),
                    out targetVisual))
            {
                continue;
            }

            var identity =
                LinkIdentity(link);

            if (!desiredIdentities.Add(identity))
            {
                throw new InvalidOperationException(
                    "Map snapshot contains duplicate stable link identity.");
            }

            MapLinkVisual visual;
            var created = false;

            if (!_linkVisualsByIdentity.TryGetValue(
                    identity,
                    out visual))
            {
                visual =
                    CreateLinkVisual();

                created = true;

                _linkVisualsByIdentity.Add(
                    identity,
                    visual);

                Panel.SetZIndex(
                    visual.Line,
                    0);

                Panel.SetZIndex(
                    visual.Label,
                    1);

                MapCanvas.Children.Add(
                    visual.Line);

                MapCanvas.Children.Add(
                    visual.Label);
            }

            UpdateLinkVisual(
                visual,
                link,
                sourceVisual,
                targetVisual);

            var linkVisible =
                sourceVisual.Border.Visibility ==
                    Visibility.Visible &&
                targetVisual.Border.Visibility ==
                    Visibility.Visible;

            visual.Line.Visibility =
                linkVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            visual.Label.Visibility =
                linkVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (created)
            {
                AnimateAppearance(
                    visual.Line);

                AnimateAppearance(
                    visual.Label);
            }
        }

        foreach (var identity in
            _linkVisualsByIdentity.Keys
                .Where(
                    key =>
                        !desiredIdentities.Contains(
                            key))
                .ToArray())
        {
            var visual =
                _linkVisualsByIdentity[
                    identity];

            _linkVisualsByIdentity.Remove(
                identity);

            AnimateRemoval(
                visual.Line,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Line));

            AnimateRemoval(
                visual.Label,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Label));
        }
    }

    private static string NodeIdentity(
        MapNode node)
    {
        if (node.DeviceId.HasValue)
        {
            return
                "device:" +
                node.DeviceId.Value.ToString("D");
        }

        return
            "key:" +
            node.Key;
    }

    private static string LinkIdentity(
        MapLink link)
    {
        if (link.PhysicalLinkId.HasValue)
        {
            return
                "physical-link:" +
                link.PhysicalLinkId.Value.ToString("D");
        }

        return
            "key:" +
            link.Key;
    }

    private ContextMenu CreateMapElementContextMenu(
        FrameworkElement target,
        RoutedEventHandler editHandler,
        RoutedEventHandler deleteHandler,
        RoutedEventHandler openedHandler)
    {
        var edit =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "ManualTopologyEdit"),
                Tag = target
            };

        edit.Click +=
            editHandler;

        var delete =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "ManualTopologyDelete"),
                Tag = target
            };

        delete.Click +=
            deleteHandler;

        var menu =
            new ContextMenu
            {
                Tag = target
            };

        menu.Items.Add(edit);
        menu.Items.Add(new Separator());
        menu.Items.Add(delete);

        menu.Opened +=
            openedHandler;

        return menu;
    }

    private static void UpdateMapElementContextMenuState(
        ContextMenu menu,
        bool canChange)
    {
        if (menu == null ||
            menu.Items.Count < 3)
        {
            return;
        }

        var edit =
            menu.Items[0]
                as MenuItem;

        var delete =
            menu.Items[2]
                as MenuItem;

        if (edit != null)
        {
            edit.IsEnabled =
                canChange;
        }

        if (delete != null)
        {
            delete.IsEnabled =
                canChange;
        }
    }

    private MapNodeVisual
        CreateNodeVisual()
    {
        var title =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeTitle")
            };

        var secondary =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeSecondary"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var topologyMetadata =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var managementAddressText =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var locationText =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var lockBadge =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeLockBadge"),
                Text =
                    UiText.Get(
                        "MapNodeLockedBadge"),
                Visibility =
                    Visibility.Collapsed
            };

        var header =
            new DockPanel();

        DockPanel.SetDock(
            lockBadge,
            Dock.Right);

        header.Children.Add(
            lockBadge);

        header.Children.Add(
            title);

        var content =
            new StackPanel();

        content.Children.Add(header);
        content.Children.Add(secondary);
        content.Children.Add(topologyMetadata);
        content.Children.Add(managementAddressText);
        content.Children.Add(locationText);

        var border =
            new Border
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeCard"),
                Child = content,
                Cursor = Cursors.Hand,
                Focusable = false
            };

        border.ContextMenu =
            CreateMapElementContextMenu(
                border,
                OnMapNodeContextEditClick,
                OnMapNodeContextDeleteClick,
                OnMapNodeContextMenuOpened);

        border.MouseLeftButtonDown +=
            OnMapNodeMouseLeftButtonDown;

        border.MouseRightButtonDown +=
            OnMapNodeMouseRightButtonDown;

        border.MouseMove +=
            OnMapNodeMouseMove;

        border.MouseLeftButtonUp +=
            OnMapNodeMouseLeftButtonUp;

        return new MapNodeVisual(
            border,
            title,
            secondary,
            topologyMetadata,
            managementAddressText,
            locationText,
            lockBadge);
    }

    private void UpdateNodeVisual(
        MapNodeVisual visual,
        MapNode node,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        visual.Title.Text =
            DisplayNodeLabel(
                node);

        visual.Secondary.Text =
            string.IsNullOrWhiteSpace(
                node.SecondaryText)
                ? string.Empty
                : node.SecondaryText;

        visual.TopologyMetadata.Text =
            BuildTopologyMetadata(node);

        visual.ManagementAddress.Text =
            UiText.Format(
                "MapNodeIpAddress",
                string.IsNullOrWhiteSpace(
                    node.ManagementAddress)
                    ? UiText.Get(
                        "DiagnosticNotAvailable")
                    : node.ManagementAddress);

        visual.Location.Text =
            BuildLocationText(
                node,
                locations);

        visual.DeviceId =
            node.DeviceId;

        visual.LocationId =
            node.LocationId;

        visual.IsManual =
            node.Origin ==
            MapNodeOrigin.Manual;

        visual.IsLocked = false;

        if (node.DeviceId.HasValue)
        {
            MapDeviceLayout persisted;

            if (_persistedDeviceLayouts.TryGetValue(
                    node.DeviceId.Value,
                    out persisted))
            {
                visual.IsLocked =
                    persisted.IsLocked;
            }
        }

        UpdateNodeLockPresentation(
            visual);

        visual.Border.Tag =
            node.DeviceId;

        var isHighlighted =
            node.DeviceId.HasValue &&
            ((_highlightedDeviceId.HasValue &&
              node.DeviceId.Value ==
                  _highlightedDeviceId.Value) ||
             (_selectedDeviceId.HasValue &&
              node.DeviceId.Value ==
                  _selectedDeviceId.Value));

        if (isHighlighted)
        {
            visual.Border.BorderThickness =
                GetThicknessResource(
                    "NetLoom.Thickness.BorderFocus");

            visual.Border.SetResourceReference(
                Border.BorderBrushProperty,
                "NetLoom.Brush.Selection");
        }
        else
        {
            ApplyNodeDegradationPresentation(
                visual,
                node.DeviceId);
        }
    }

    private MapLinkVisual
        CreateLinkVisual()
    {
        var line =
            new Line
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLink"),
                Cursor = Cursors.Hand,
                Focusable = false
            };

        var label =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLinkLabel"),
                Cursor = Cursors.Hand,
                Focusable = false
            };

        line.ContextMenu =
            CreateMapElementContextMenu(
                line,
                OnMapLinkContextEditClick,
                OnMapLinkContextDeleteClick,
                OnMapLinkContextMenuOpened);

        label.ContextMenu =
            CreateMapElementContextMenu(
                label,
                OnMapLinkContextEditClick,
                OnMapLinkContextDeleteClick,
                OnMapLinkContextMenuOpened);

        line.MouseLeftButtonDown +=
            OnMapLinkMouseLeftButtonDown;

        label.MouseLeftButtonDown +=
            OnMapLinkMouseLeftButtonDown;

        line.MouseRightButtonDown +=
            OnMapLinkMouseRightButtonDown;

        label.MouseRightButtonDown +=
            OnMapLinkMouseRightButtonDown;

        return new MapLinkVisual(
            line,
            label);
    }

    private void UpdateLinkVisual(
        MapLinkVisual visual,
        MapLink link,
        MapNodeVisual source,
        MapNodeVisual target)
    {
        var freshnessChanged =
            visual.LastFreshness.HasValue &&
            visual.LastFreshness.Value !=
                link.Freshness;

        var operationalOpacity =
            LinkFreshnessOpacity(
                link.Freshness);

        var confidenceDashPattern =
            LinkConfidenceDashPattern(
                link.Confidence);

        visual.Line.StrokeDashArray =
            confidenceDashPattern == null
                ? null
                : new DoubleCollection(
                    confidenceDashPattern);

        visual.Line.Opacity =
            operationalOpacity;

        visual.Label.Opacity =
            operationalOpacity;

        var x1 =
            NodeLeft(source) +
            (_nodeWidth / 2.0);

        var y1 =
            NodeTop(source) +
            (NodeVisualHeight(source) / 2.0);

        var x2 =
            NodeLeft(target) +
            (_nodeWidth / 2.0);

        var y2 =
            NodeTop(target) +
            (NodeVisualHeight(target) / 2.0);

        visual.Line.X1 = x1;
        visual.Line.Y1 = y1;
        visual.Line.X2 = x2;
        visual.Line.Y2 = y2;

        visual.Label.Text =
            BuildLinkLabel(link);

        visual.Line.Tag =
            link.PhysicalLinkId;

        visual.Label.Tag =
            link.PhysicalLinkId;

        var isSelected =
            link.PhysicalLinkId.HasValue &&
            _selectedPhysicalLinkId.HasValue &&
            link.PhysicalLinkId.Value ==
                _selectedPhysicalLinkId.Value;

        if (isSelected)
        {
            visual.Line.SetResourceReference(
                Shape.StrokeProperty,
                "NetLoom.Brush.Selection");

            visual.Line.StrokeThickness =
                GetDoubleResource(
                    "NetLoom.Map.LinkSelectedStrokeThickness");

            visual.Label.SetResourceReference(
                TextBlock.ForegroundProperty,
                "NetLoom.Brush.Selection");
        }
        else
        {
            ApplyLinkOperationalPresentation(
                visual,
                link.PhysicalLinkId);
        }

        PlaceLinkLabel(
            visual.Label,
            x1,
            y1,
            x2,
            y2);

        if (freshnessChanged)
        {
            AnimatePulse(
                visual.Line,
                MapMotionKind.FreshnessChange,
                operationalOpacity);

            AnimatePulse(
                visual.Label,
                MapMotionKind.FreshnessChange,
                operationalOpacity);
        }

        visual.LastFreshness =
            link.Freshness;
    }

    private static double[] LinkConfidenceDashPattern(
        MapConfidence confidence)
    {
        switch (confidence)
        {
            case MapConfidence.High:
                return null;

            case MapConfidence.Medium:
                return new[]
                {
                    6.0,
                    3.0
                };

            case MapConfidence.Low:
            default:
                return new[]
                {
                    2.0,
                    2.0
                };
        }
    }

    private static double LinkFreshnessOpacity(
        MapFreshness freshness)
    {
        switch (freshness)
        {
            case MapFreshness.Fresh:
                return 1.0;

            case MapFreshness.Aging:
                return 0.72;

            case MapFreshness.Stale:
            default:
                return 0.45;
        }
    }

    private void PlaceLinkLabel(
        TextBlock label,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        label.Measure(
            new Size(
                double.PositiveInfinity,
                double.PositiveInfinity));

        var labelWidth =
            label.DesiredSize.Width;

        var labelHeight =
            label.DesiredSize.Height;

        var centerX =
            (x1 + x2) / 2.0;

        var centerY =
            (y1 + y2) / 2.0;

        var deltaX =
            x2 - x1;

        var deltaY =
            y2 - y1;

        var length =
            Math.Sqrt(
                (deltaX * deltaX) +
                (deltaY * deltaY));

        var normalX = 0.0;
        var normalY = -1.0;

        if (length > 0.001)
        {
            normalX =
                deltaY / length;

            normalY =
                -deltaX / length;

            if (normalY > 0.0 ||
                (Math.Abs(normalY) <= 0.001 &&
                 normalX < 0.0))
            {
                normalX = -normalX;
                normalY = -normalY;
            }
        }

        var obstacles =
            _nodeVisualsByIdentity.Values
                .Select(NodeBounds)
                .ToArray();

        const int maxPlacementSteps = 40;

        for (var step = 0;
             step <= maxPlacementSteps;
             step++)
        {
            if (step == 0)
            {
                if (TryPlaceLinkLabel(
                    label,
                    centerX,
                    centerY,
                    labelWidth,
                    labelHeight,
                    obstacles,
                    _linkLabelCollisionMargin))
                {
                    return;
                }

                continue;
            }

            var offset =
                _linkLabelPlacementStep * step;

            if (TryPlaceLinkLabel(
                label,
                centerX + (normalX * offset),
                centerY + (normalY * offset),
                labelWidth,
                labelHeight,
                obstacles,
                _linkLabelCollisionMargin))
            {
                return;
            }

            if (TryPlaceLinkLabel(
                label,
                centerX - (normalX * offset),
                centerY - (normalY * offset),
                labelWidth,
                labelHeight,
                obstacles,
                _linkLabelCollisionMargin))
            {
                return;
            }
        }

        Canvas.SetLeft(
            label,
            centerX - (labelWidth / 2.0));

        Canvas.SetTop(
            label,
            centerY - (labelHeight / 2.0));
    }

    private static bool TryPlaceLinkLabel(
        TextBlock label,
        double centerX,
        double centerY,
        double labelWidth,
        double labelHeight,
        IReadOnlyList<Rect> obstacles,
        double collisionMargin)
    {
        var left =
            centerX -
            (labelWidth / 2.0);

        var top =
            centerY -
            (labelHeight / 2.0);

        var labelBounds =
            new Rect(
                left,
                top,
                labelWidth,
                labelHeight);

        foreach (var obstacle in obstacles)
        {
            var collisionBounds =
                obstacle;

            collisionBounds.Inflate(
                collisionMargin,
                collisionMargin);

            if (collisionBounds.IntersectsWith(
                labelBounds))
            {
                return false;
            }
        }

        Canvas.SetLeft(
            label,
            left);

        Canvas.SetTop(
            label,
            top);

        return true;
    }

    private Rect NodeBounds(
        MapNodeVisual visual)
    {
        return new Rect(
            NodeLeft(visual),
            NodeTop(visual),
            _nodeWidth,
            NodeVisualHeight(visual));
    }

    private double NodeVisualHeight(
        MapNodeVisual visual)
    {
        if (visual == null ||
            visual.Border == null)
        {
            return _nodeHeight;
        }

        visual.Border.Measure(
            new Size(
                _nodeWidth,
                double.PositiveInfinity));

        var measured =
            Math.Max(
                visual.Border.ActualHeight,
                visual.Border.DesiredSize.Height);

        return measured > 0.0
            ? Math.Max(
                _nodeHeight,
                measured)
            : _nodeHeight;
    }

    private static double NodeLeft(
        MapNodeVisual visual)
    {
        var value =
            Canvas.GetLeft(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private static double NodeTop(
        MapNodeVisual visual)
    {
        var value =
            Canvas.GetTop(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private static string BuildLinkLabel(
        MapLink link)
    {
        var status =
            ConfidenceText(link.Confidence) +
            " • " +
            FreshnessText(link.Freshness) +
            " • " +
            UiText.FormatCount(
                "EvidenceCount",
                link.Evidence.Count);

        if (string.IsNullOrWhiteSpace(
                link.SourcePortLabel) &&
            string.IsNullOrWhiteSpace(
                link.TargetPortLabel))
        {
            return status;
        }

        return
            (link.SourcePortLabel ?? "?") +
            " ↔ " +
            (link.TargetPortLabel ?? "?") +
            " • " +
            status;
    }

    private static void UpdateNodeLockPresentation(
        MapNodeVisual visual)
    {
        visual.Border.Cursor =
            visual.IsLocked
                ? Cursors.Hand
                : Cursors.SizeAll;

        visual.LockBadge.Visibility =
            visual.IsLocked
                ? Visibility.Visible
                : Visibility.Collapsed;

        visual.Border.ToolTip =
            visual.IsLocked
                ? UiText.Get(
                    "MapNodeLockedHint")
                : visual.IsManual
                    ? UiText.Get(
                        "ManualTopologyMapNodeHint")
                    : null;
    }

    private static string DisplayNodeLabel(
        MapNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(
                nameof(node));
        }

        return string.Equals(
                node.Label,
                node.Key,
                StringComparison.Ordinal)
            ? UiText.Get(
                "NodeUnknownLabel")
            : node.Label;
    }

}
