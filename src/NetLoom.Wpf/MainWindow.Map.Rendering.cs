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
    private Guid? _hoveredPhysicalLinkId;

    private Guid? FocusedPhysicalLinkId =>
        _hoveredPhysicalLinkId ?? _selectedPhysicalLinkId;

    private void ApplyLinkFocusPresentation()
    {
        foreach (var visual in _linkVisualsByIdentity.Values)
        {
            ApplyLinkFocusPresentation(visual);
        }

        // На дальнем уровне видимые подписи связей не должны наезжать на ярлыки имён.
        if (_semanticLevel == MapSemanticLevel.Far)
        {
            ApplyFarLabelDeclutter();
        }
    }

    private void ApplyLinkFocusPresentation(MapLinkVisual visual)
    {
        ApplyNeighborhoodLinkVisibility(visual);
        var physicalLinkId = visual.Line.Tag as Guid?;
        // Sprint 49: связи показанного пути оформляются как фокусная связь (подпись, толщина, ореол).
        var onPath = physicalLinkId.HasValue && _pathLinkIds.Contains(physicalLinkId.Value);
        var focused = (FocusedPhysicalLinkId.HasValue &&
            physicalLinkId == FocusedPhysicalLinkId) || onPath;
        var opacity = LinkPresentationOpacity(
            physicalLinkId, visual.LastFreshness ?? MapFreshness.Fresh);

        // Старый импульс не должен перекрывать новую прозрачность фокуса.
        if (visual.LastPresentationOpacity.HasValue && visual.LastPresentationOpacity.Value != opacity)
        {
            visual.Line.BeginAnimation(UIElement.OpacityProperty, null);
            visual.Label.BeginAnimation(UIElement.OpacityProperty, null);
        }
        visual.Line.Opacity = opacity;
        visual.Label.Opacity = opacity;
        visual.LastPresentationOpacity = opacity;

        ApplyLinkOperationalPresentation(visual, physicalLinkId);
        var selected = physicalLinkId.HasValue && physicalLinkId == _selectedPhysicalLinkId;
        if (selected || onPath)
        {
            visual.Line.StrokeThickness = LinkSelectedStrokeThickness(
                LinkOperationalState(physicalLinkId));
        }
        visual.SelectionHalo.Visibility = (selected || onPath) && visual.Line.Visibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (focused)
        {
            visual.Label.FontWeight = FontWeights.SemiBold;
            if (LinkOperationalBrushKey(LinkOperationalState(physicalLinkId)) == null)
            {
                visual.Label.SetResourceReference(TextBlock.ForegroundProperty,
                    "NetLoom.Brush.TextPrimary");
            }
        }

        var conflict = HasTopologyConflict(physicalLinkId);
        var prefix = UiText.Get("TopologyConflictLabelPrefix");
        var label = visual.Link == null ? visual.Label.Text ?? string.Empty : SemanticLinkLabel(visual.Link);
        // Один путь для фокусной подписи и расхождения; префикс не накапливается при обновлении.
        if (label.StartsWith(prefix, StringComparison.Ordinal)) label = label.Substring(prefix.Length);
        visual.Label.Text = conflict ? prefix + label : label;
        if (conflict)
        {
            visual.Label.SetResourceReference(TextBlock.ForegroundProperty,
                OperatorStatusBrushKey(OperatorStatusSemantic.Warning));
        }

        // ADR-079: подпись расхождения видна при любом масштабе.
        // Ниже масштаба 1 видимая подпись обратно масштабируется до экранного размера, текст остаётся полным.
        var far = _semanticLevel == MapSemanticLevel.Far;
        visual.Label.RenderTransformOrigin = new Point(0.5, 0.5);
        visual.Label.RenderTransform = _zoom > 0.0 && _zoom < 1.0
            ? new ScaleTransform(1 / _zoom, 1 / _zoom)
            : Transform.Identity;
        visual.Label.Visibility = visual.Line.Visibility == Visibility.Visible &&
            // На уровне «Издалека» — только подписи, которые оператор явно выделил (фокус, путь)
            // Или которые требуют решения (расхождение); все они экранного размера.
            (far
                ? conflict || focused
                : conflict || focused || _semanticLevel == MapSemanticLevel.Close ||
                  _semanticLevel == MapSemanticLevel.Detailed) &&
            !string.IsNullOrWhiteSpace(visual.Label.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OnMapLinkMouseEnter(object sender, MouseEventArgs e)
    {
        var element = sender as FrameworkElement;
        if (element == null || !(element.Tag is Guid) ||
            _hoveredPhysicalLinkId == (Guid)element.Tag)
        {
            return;
        }
        _hoveredPhysicalLinkId = (Guid)element.Tag;
        ApplyLinkFocusPresentation();
    }

    private void OnMapLinkMouseLeave(MapLinkVisual visual)
    {
        if (!(visual.Line.Tag is Guid) ||
            _hoveredPhysicalLinkId != (Guid)visual.Line.Tag ||
            visual.Line.IsMouseOver || visual.Label.IsMouseOver)
        {
            return;
        }
        _hoveredPhysicalLinkId = null;
        ApplyLinkFocusPresentation();
    }

    private void ReconcileNodes(
        IReadOnlyList<MapNode> nodes,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var desiredIdentities =
            new HashSet<string>(
                StringComparer.Ordinal);

        var placementStep = GetDoubleResource("NetLoom.Map.NewNodePlacementStep");
        var snapshotIdentities = new HashSet<string>(nodes.Select(NodeIdentity), StringComparer.Ordinal);
        var occupied = _nodeVisualsByIdentity.Where(pair => snapshotIdentities.Contains(pair.Key))
            .Select(pair => NodeBounds(pair.Value)).ToList();
        foreach (var node in nodes.Where(item => item.DeviceId.HasValue &&
            _persistedDeviceLayouts.ContainsKey(item.DeviceId.Value)))
        {
            var layout = _persistedDeviceLayouts[node.DeviceId.Value];
            occupied.Add(new Rect(MapVirtualWorkspace.ToCanvasCoordinate(layout.X, _virtualOriginX),
                MapVirtualWorkspace.ToCanvasCoordinate(layout.Y, _virtualOriginY), _nodeWidth, _nodeHeight));
        }

        foreach (var node in nodes.OrderByDescending(item => item.DeviceId.HasValue &&
            _persistedDeviceLayouts.ContainsKey(item.DeviceId.Value)))
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

                UpdateNodeVisual(visual, node, locations);
                if (node.DeviceId.HasValue && !_persistedDeviceLayouts.ContainsKey(node.DeviceId.Value))
                {
                    var free = MapFreePlacement.FindFreeSpot(NodeBounds(visual), occupied,
                        placementStep, _linkLabelCollisionMargin);
                    Canvas.SetLeft(visual.Border, free.X);
                    Canvas.SetTop(visual.Border, free.Y);
                }
                occupied.Add(NodeBounds(visual));

                Panel.SetZIndex(
                    visual.PulseHalo,
                    1);

                Panel.SetZIndex(
                    visual.Border,
                    2);

                MapCanvas.Children.Add(
                    visual.PulseHalo);

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

            visual.PulseHalo.Visibility =
                Visibility.Collapsed;

            AnimateRemoval(
                visual.Border,
                () =>
                {
                    MapCanvas.Children.Remove(
                        visual.PulseHalo);
                    MapCanvas.Children.Remove(
                        visual.Border);
                });
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

        var locationOrder = 0;

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
                ReferenceEquals(
                    visual.Border,
                    _focusRaisedLocationFrame)
                    ? FocusRaisedLocationZIndex
                    : -100 +
                      LocationDepth(
                          location,
                          byId));

            // Порядок Tab не зависит от Z-порядка (рамка с фокусом поднимается наверх): по глубине и порядку снимка.
            KeyboardNavigation.SetTabIndex(
                visual.CollapseButton,
                (LocationDepth(
                     location,
                     byId) *
                 10000) +
                locationOrder++);

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

    // §8: вкладка с фокусом клавиатуры не должна лежать под чужой рамкой (сохранённые рамки могут
    // Накладываться) — её размещение поднимается над остальными рамками, но остаётся под связями и узлами.
    private const int FocusRaisedLocationZIndex = -1;
    private UIElement _focusRaisedLocationFrame;
    private int _focusRaisedLocationOriginalZIndex;

    private void OnMapLocationToggleGotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        DependencyObject current = sender as DependencyObject;
        while (current != null &&
               !ReferenceEquals(VisualTreeHelper.GetParent(current), MapCanvas))
        {
            current = VisualTreeHelper.GetParent(current);
        }

        var frame = current as UIElement;
        if (frame == null ||
            ReferenceEquals(frame, _focusRaisedLocationFrame))
        {
            return;
        }

        RestoreFocusRaisedLocationFrame();
        _focusRaisedLocationFrame = frame;
        _focusRaisedLocationOriginalZIndex = Panel.GetZIndex(frame);
        Panel.SetZIndex(frame, FocusRaisedLocationZIndex);
    }

    private void OnMapLocationToggleLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        RestoreFocusRaisedLocationFrame();
    }

    private void RestoreFocusRaisedLocationFrame()
    {
        if (_focusRaisedLocationFrame == null)
        {
            return;
        }

        Panel.SetZIndex(_focusRaisedLocationFrame, _focusRaisedLocationOriginalZIndex);
        _focusRaisedLocationFrame = null;
    }

    private void NormalizeLocationHierarchy(
        IReadOnlyList<MapLocation> locations,
        IReadOnlyList<MapNode> nodes)
    {
        var byId = locations.ToDictionary(item => item.Id);
        // Только несохранённый родитель растёт вокруг содержимого; ручная геометрия не меняется.
        foreach (var location in locations.OrderByDescending(item => LocationDepth(item, byId)))
        {
            MapLocationVisual visual;
            if (_persistedLocationLayouts.ContainsKey(location.Id) ||
                !_locationVisualsById.TryGetValue(location.Id, out visual)) continue;
            SeparateAutomaticChildLocations(location.Id, locations, nodes);
            var contents = locations.Where(item => item.ParentLocationId == location.Id &&
                    _locationVisualsById.ContainsKey(item.Id))
                .Select(item => ExpandedLocationBounds(_locationVisualsById[item.Id])).ToList();
            contents.AddRange(nodes.Where(item => item.LocationId == location.Id &&
                    _nodeVisualsByIdentity.ContainsKey(NodeIdentity(item)))
                .Select(item => NodeBounds(_nodeVisualsByIdentity[NodeIdentity(item)])));
            if (contents.Count == 0) continue;
            var contentBounds = contents.Aggregate(Rect.Union);
            var required = new Rect(contentBounds.Left - _locationContentPadding,
                contentBounds.Top - _locationContentPadding - _locationHeaderHeight,
                contentBounds.Width + 2 * _locationContentPadding,
                contentBounds.Height + 2 * _locationContentPadding + _locationHeaderHeight);
            var expanded = Rect.Union(ExpandedLocationBounds(visual), required);
            // Соседей этой рамки разведёт проход её родителя (SeparateAutomaticChildLocations).
            Canvas.SetLeft(visual.Border, expanded.Left);
            Canvas.SetTop(visual.Border, expanded.Top);
            visual.ExpandedWidth = Math.Max(_locationMinWidth, expanded.Width);
            visual.ExpandedHeight = Math.Max(_locationMinHeight, expanded.Height);
            UpdateLocationVisualState(visual);
        }

        SeparateAutomaticChildLocations(null, locations, nodes);

        // Затем сверху вниз — внутрь физического родителя, но только то, что можно двигать:
        // Элемент без сохранённой позиции (новый) или элемент, родителя которого оператор сменил в этом сеансе
        // (перенос в редакторе — явное действие). Сохранённую ручную геометрию без действия оператора
        // Не двигаем: выход за родителя показывает строка качества данных (Sprint 49, M2).
        foreach (var location in locations.OrderBy(item => LocationDepth(item, byId)))
        {
            MapLocationVisual visual;
            if (!_locationVisualsById.TryGetValue(location.Id, out visual)) continue;
            Guid? shownParent;
            var reparented = _shownLocationParents.TryGetValue(location.Id, out shownParent) &&
                shownParent != location.ParentLocationId;
            if (visual.ParentLocationId.HasValue &&
                (!_persistedLocationLayouts.ContainsKey(location.Id) || reparented))
            {
                var left = LocationLeft(visual);
                var top = LocationTop(visual);
                var constrained = ConstrainLocationPositionToParent(visual, left, top);
                if (Math.Abs(constrained.X - left) > 0.001 || Math.Abs(constrained.Y - top) > 0.001)
                    MoveLocationSubtreeByDelta(location.Id, constrained.X - left, constrained.Y - top);
            }

            foreach (var node in nodes.Where(item => item.LocationId == location.Id))
            {
                MapNodeVisual nodeVisual;
                if (!_nodeVisualsByIdentity.TryGetValue(NodeIdentity(node), out nodeVisual)) continue;
                var identity = NodeIdentity(node);
                Guid? shownLocation;
                var reassigned = _shownNodeLocations.TryGetValue(identity, out shownLocation) &&
                    shownLocation != node.LocationId;
                var persisted = node.DeviceId.HasValue && _persistedDeviceLayouts.ContainsKey(node.DeviceId.Value);
                if (persisted && !reassigned) continue;
                var left = NodeLeft(nodeVisual);
                var top = NodeTop(nodeVisual);
                var constrained = ConstrainNodePositionToLocation(nodeVisual, left, top);
                if (Math.Abs(constrained.X - left) <= 0.001 && Math.Abs(constrained.Y - top) <= 0.001) continue;
                Canvas.SetLeft(nodeVisual.Border, constrained.X);
                Canvas.SetTop(nodeVisual.Border, constrained.Y);
                if (persisted) TrySaveDeviceLayout(nodeVisual);
            }
        }

        _shownLocationParents.Clear();
        foreach (var location in locations) _shownLocationParents[location.Id] = location.ParentLocationId;
        _shownNodeLocations.Clear();
        foreach (var node in nodes) _shownNodeLocations[NodeIdentity(node)] = node.LocationId;
    }

    // Несохранённые соседние рамки внутри родителя разводятся без наложений (Sprint 49, M2): по порядку
    // Названий каждая следующая ставится на ближайшее свободное место. Рамку, в поддереве которой есть
    // Сохранённая ручная геометрия (рамка или узел), не двигаем.
    private void SeparateAutomaticChildLocations(
        Guid? parentId,
        IReadOnlyList<MapLocation> locations,
        IReadOnlyList<MapNode> nodes)
    {
        var placed = new List<Rect>();
        var children = locations.Where(item => item.ParentLocationId == parentId &&
                _locationVisualsById.ContainsKey(item.Id))
            .OrderBy(item => _persistedLocationLayouts.ContainsKey(item.Id) ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture)
            .ThenBy(item => item.Id)
            .ToArray();
        foreach (var child in children)
        {
            var bounds = ExpandedLocationBounds(_locationVisualsById[child.Id]);
            if (!HasPersistedGeometryInSubtree(child.Id, locations, nodes) &&
                placed.Any(item => !Rect.Intersect(item, bounds).IsEmpty))
            {
                var free = MapFreePlacement.FindFreeSpot(bounds, placed, GetDoubleResource("NetLoom.Map.NewLocationPlacementStep"),
                    _linkLabelCollisionMargin);
                MoveLocationSubtreeByDelta(child.Id, free.X - bounds.Left, free.Y - bounds.Top);
                bounds = ExpandedLocationBounds(_locationVisualsById[child.Id]);
            }
            placed.Add(bounds);
        }
    }

    private bool HasPersistedGeometryInSubtree(
        Guid locationId,
        IReadOnlyList<MapLocation> locations,
        IReadOnlyList<MapNode> nodes)
    {
        var subtree = new HashSet<Guid> { locationId };
        var grown = true;
        while (grown)
        {
            grown = false;
            foreach (var item in locations)
                if (item.ParentLocationId.HasValue && subtree.Contains(item.ParentLocationId.Value) &&
                    subtree.Add(item.Id)) grown = true;
        }
        return subtree.Any(id => _persistedLocationLayouts.ContainsKey(id)) ||
            nodes.Any(node => node.LocationId.HasValue && subtree.Contains(node.LocationId.Value) &&
                node.DeviceId.HasValue && _persistedDeviceLayouts.ContainsKey(node.DeviceId.Value));
    }

    // Родитель размещения и размещение устройства, показанные последним снимком этого сеанса.
    private readonly Dictionary<Guid, Guid?> _shownLocationParents = new Dictionary<Guid, Guid?>();
    private readonly Dictionary<string, Guid?> _shownNodeLocations =
        new Dictionary<string, Guid?>(StringComparer.Ordinal);

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

            if (hidden)
            {
                visual.PulseHalo.Visibility =
                    Visibility.Collapsed;
            }
        }
        ApplyNeighborhoodVisibility();
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
        var title = new TextBlock
        {
            Style = GetStyleResource("NetLoom.Style.MapLocationTitle"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var lockBadge = new TextBlock
        {
            Style = GetStyleResource("NetLoom.Style.MapLocationLockBadge"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var collapseButton = new Button
        {
            Style = GetStyleResource("NetLoom.Style.MapLocationToggle"),
            VerticalAlignment = VerticalAlignment.Center
        };
        collapseButton.Click += OnMapLocationCollapseClick;
        collapseButton.GotKeyboardFocus += OnMapLocationToggleGotKeyboardFocus;
        collapseButton.LostKeyboardFocus += OnMapLocationToggleLostKeyboardFocus;
        var headerGrid = new Grid { Cursor = Cursors.SizeAll };
        var statusIcon = new Path
        {
            Style = GetStyleResource("NetLoom.Style.MapNodeStatusIcon"),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(statusIcon, 1);
        Grid.SetColumn(lockBadge, 2);
        Grid.SetColumn(collapseButton, 3);
        headerGrid.Children.Add(statusIcon);
        headerGrid.Children.Add(title);
        headerGrid.Children.Add(lockBadge);
        headerGrid.Children.Add(collapseButton);
        var header = new Border
        {
            Style = GetStyleResource("NetLoom.Style.MapLocationTab"),
            Child = headerGrid
        };
        var fill = new Border { Style = GetStyleResource("NetLoom.Style.MapLocationFill") };
        var frame = new Border
        {
            Style = GetStyleResource("NetLoom.Style.MapLocationContainer"),
            Child = fill
        };
        var resizeThumb = new Thumb
        {
            Width = _locationResizeThumbSize,
            Height = _locationResizeThumbSize,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNWSE,
            Focusable = false
        };
        resizeThumb.DragDelta += OnMapLocationResizeDragDelta;
        resizeThumb.DragCompleted += OnMapLocationResizeDragCompleted;
        var root = new Grid();
        root.Children.Add(frame);
        root.Children.Add(header);
        root.Children.Add(resizeThumb);
        var border = new Border { Child = root, Focusable = false };
        border.ContextMenu = CreateLocationContextMenu(border);
        header.MouseLeftButtonDown += OnMapLocationMouseLeftButtonDown;
        header.MouseMove += OnMapLocationMouseMove;
        header.MouseLeftButtonUp += OnMapLocationMouseLeftButtonUp;
        header.MouseRightButtonDown += OnMapLocationMouseRightButtonDown;
        border.MouseLeftButtonDown += OnMapLocationBodyMouseLeftButtonDown;
        return new MapLocationVisual(border, frame, header, title, collapseButton, lockBadge, resizeThumb)
        {
            StatusIcon = statusIcon
        };
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
        visual.LocationName = location.Name;

        var hasDescription =
            !string.IsNullOrWhiteSpace(
                location.Description);

        visual.Border.Tag =
            location.Id;

        visual.Header.Tag =
            location.Id;

        visual.CollapseButton.Tag =
            location.Id;

        visual.ResizeThumb.Tag =
            location.Id;

        visual.Border.ToolTip =
            hasDescription
                ? UiText.Format(
                    "MapLocationToolTip",
                    BuildLocationPath(
                        location,
                        locations),
                    location.Description)
                : BuildLocationPath(
                    location,
                    locations);

        visual.Header.ToolTip = visual.Border.ToolTip;
        visual.Title.ToolTip = visual.Border.ToolTip;
        UpdateLocationVisualState(visual);
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
        MapLocation location, int order, IReadOnlyList<MapNode> nodes, IReadOnlyList<MapLocation> locations)
    {
        var desired = CreateDesiredLocationLayout(location, order, nodes, locations);
        var occupied = new List<Rect>();
        foreach (var sibling in locations.Where(item => item.Id != location.Id &&
            item.ParentLocationId == location.ParentLocationId))
        {
            Rect bounds;
            if ((_persistedLocationLayouts.ContainsKey(sibling.Id) ||
                 (_locationVisualsById.ContainsKey(sibling.Id) && _locationVisualsById[sibling.Id].LocationId == sibling.Id)) &&
                TryGetExpandedLocationBounds(sibling.Id, out bounds)) occupied.Add(bounds);
        }
        var free = MapFreePlacement.FindFreeSpot(new Rect(
            MapVirtualWorkspace.ToCanvasCoordinate(desired.X, _virtualOriginX),
            MapVirtualWorkspace.ToCanvasCoordinate(desired.Y, _virtualOriginY), desired.Width, desired.Height),
            occupied, GetDoubleResource("NetLoom.Map.NewLocationPlacementStep"), _linkLabelCollisionMargin);
        return new MapLocationLayout(location.Id,
            MapVirtualWorkspace.ToLogicalCoordinate(free.X, _virtualOriginX),
            MapVirtualWorkspace.ToLogicalCoordinate(free.Y, _virtualOriginY),
            desired.Width, desired.Height, false, false);
    }

    private MapLocationLayout CreateDesiredLocationLayout(
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

    private Rect LocationVisibleBounds(MapLocationVisual visual)
    {
        return new Rect(LocationLeft(visual), LocationTop(visual), visual.Border.Width,
            visual.IsCollapsed ? _locationHeaderHeight : visual.ExpandedHeight);
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

        visual.ResizeThumb.Visibility =
            IsMapEditMode &&
            !visual.IsCollapsed &&
            !visual.IsLocked
                ? Visibility.Visible
                : Visibility.Collapsed;

        visual.Header.Cursor =
            !IsMapEditMode ||
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

        System.Windows.Automation.AutomationProperties.SetName(visual.CollapseButton,
            UiText.Format(visual.IsCollapsed ? "MapLocationExpandName" : "MapLocationCollapseName", visual.LocationName));

        visual.LockBadge.Text =
            visual.IsLocked
                ? UiText.Get(
                    "MapLocationLockedBadge")
                : string.Empty;

        visual.Header.MaxWidth = visual.ExpandedWidth;
        visual.Frame.Visibility = visual.IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
        visual.Header.SetResourceReference(Border.CornerRadiusProperty, visual.IsCollapsed
            ? "NetLoom.Radius.MapLocation" : "NetLoom.Radius.MapLocationTab");
        visual.Header.SetResourceReference(Border.BorderThicknessProperty, visual.IsCollapsed
            ? "NetLoom.Thickness.BorderThin" : "NetLoom.Thickness.MapLocationTabBorder");

        visual.Header.Measure(new Size(visual.ExpandedWidth, _locationHeaderHeight));
        visual.Border.Width = visual.IsCollapsed ? visual.Header.DesiredSize.Width : visual.ExpandedWidth;
        ApplyLocationSemanticPresentation(visual);

        UpdateLocationSelectionPresentation();
        UpdateTopologyQuality();
    }

    private void UpdateLocationSelectionPresentation()
    {
        var selectionBrush =
            FindResource(
                "NetLoom.Brush.Selection")
                as Brush;

        var normalBrush =
            FindResource(
                "NetLoom.Brush.MapLocationBorder")
                as Brush;

        foreach (var visual in
            _locationVisualsById.Values)
        {
            var selected =
                _selectedLocationId.HasValue &&
                visual.LocationId ==
                    _selectedLocationId.Value;

            visual.Frame.BorderBrush =
                selected
                    ? selectionBrush
                    : normalBrush;

            visual.Frame.BorderThickness = GetThicknessResource(selected
                ? "NetLoom.Thickness.BorderFocus" : "NetLoom.Thickness.BorderThin");
            visual.Header.BorderBrush = selected ? selectionBrush : normalBrush;
        }
    }

    private void ReconcileLinks(
        IReadOnlyList<MapLink> links,
        IReadOnlyDictionary<string, MapNode> nodes)
    {
        var desiredIdentities =
            new HashSet<string>(
                StringComparer.Ordinal);

        var endpoints = links
            .Where(link => nodes.ContainsKey(link.SourceNodeKey) && nodes.ContainsKey(link.TargetNodeKey))
            .Select(link => new ParallelLinkEndpoints(
                LinkIdentity(link),
                NodeIdentity(nodes[link.SourceNodeKey]),
                NodeIdentity(nodes[link.TargetNodeKey])))
            .ToArray();
        var slots = ParallelLinkLayout.Slots(endpoints);
        var groupByIdentity = endpoints.ToDictionary(
            item => item.LinkIdentity,
            item => StringComparer.Ordinal.Compare(item.SourceNodeIdentity, item.TargetNodeIdentity) <= 0
                ? Tuple.Create(item.SourceNodeIdentity, item.TargetNodeIdentity)
                : Tuple.Create(item.TargetNodeIdentity, item.SourceNodeIdentity),
            StringComparer.Ordinal);
        var labelBoundsByGroup = new Dictionary<Tuple<string, string>, List<Rect>>();

        // Подписи учитывают только ранее размещённые подписи той же пары узлов.
        var orderedLinks = links
            .Where(link => slots.ContainsKey(LinkIdentity(link)))
            .GroupBy(link => groupByIdentity[LinkIdentity(link)])
            .SelectMany(group => group.OrderBy(link => slots[LinkIdentity(link)].Slot));

        foreach (var link in orderedLinks)
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
                    visual.SelectionHalo,
                    -1);

                Panel.SetZIndex(
                    visual.Line,
                    0);

                Panel.SetZIndex(
                    visual.Label,
                    1);

                MapCanvas.Children.Add(
                    visual.SelectionHalo);

                MapCanvas.Children.Add(
                    visual.Line);

                MapCanvas.Children.Add(
                    visual.Label);
            }

            var slot = slots[identity];
            var group = groupByIdentity[identity];
            List<Rect> labelBounds;
            if (!labelBoundsByGroup.TryGetValue(group, out labelBounds))
            {
                labelBounds = new List<Rect>();
                labelBoundsByGroup.Add(group, labelBounds);
            }

            UpdateLinkVisual(
                visual,
                link,
                sourceVisual,
                targetVisual,
                slot,
                slot.GroupSize > 1 ? labelBounds : null);

            var linkVisible =
                sourceVisual.Border.Visibility ==
                    Visibility.Visible &&
                targetVisual.Border.Visibility ==
                    Visibility.Visible;

            visual.Line.Visibility =
                linkVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (!linkVisible)
            {
                visual.SelectionHalo.Visibility =
                    Visibility.Collapsed;
            }

            if (slot.GroupSize > 1 && linkVisible)
            {
                labelBounds.Add(new Rect(
                    Canvas.GetLeft(visual.Label),
                    Canvas.GetTop(visual.Label),
                    visual.Label.DesiredSize.Width,
                    visual.Label.DesiredSize.Height));
            }

            ApplyLinkFocusPresentation(visual);

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
                visual.SelectionHalo,
                () =>
                    MapCanvas.Children.Remove(
                        visual.SelectionHalo));

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

        if (_hoveredPhysicalLinkId.HasValue &&
            !_linkVisualsByIdentity.Values.Any(visual =>
                Equals(visual.Line.Tag, _hoveredPhysicalLinkId.Value)))
        {
            _hoveredPhysicalLinkId = null;
        }
        ApplyLinkFocusPresentation();
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
        var categoryIcon =
            new Path
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeCategoryIcon"),
                Tag =
                    "NodeCategoryIcon",
                IsHitTestVisible = false
            };

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
                Visibility =
                    Visibility.Collapsed
            };

        var stateStripe =
            new Border
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeStateStripe"),
                Tag =
                    "NodeStateStripe",
                IsHitTestVisible = false
            };

        var statusIcon =
            new Path
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeStatusIcon"),
                Tag =
                    "NodeStatusIcon",
                IsHitTestVisible = false
            };

        var lockBadge =
            new Path
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeLockBadge"),
                ToolTip =
                    UiText.Get(
                        "MapNodeLockedHint"),
                Visibility =
                    Visibility.Collapsed,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Top,
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0),
                IsHitTestVisible = false
            };

        var header =
            new Grid();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1.0,
                        GridUnitType.Star)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        Grid.SetColumn(
            title,
            0);

        Grid.SetColumn(
            statusIcon,
            1);

        header.Children.Add(
            title);

        header.Children.Add(
            statusIcon);

        var textContent =
            new StackPanel
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        textContent.Children.Add(
            header);

        textContent.Children.Add(
            secondary);

        categoryIcon.Margin =
            new Thickness(
                0);

        var iconHost =
            new StackPanel
            {
                Margin =
                    GetThicknessResource(
                        "NetLoom.Thickness.MapNodeIcon"),
                VerticalAlignment =
                    VerticalAlignment.Center,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        iconHost.Children.Add(
            categoryIcon);
        iconHost.Children.Add(
            lockBadge);

        var content =
            new Grid();

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1.0,
                        GridUnitType.Star)
            });

        Grid.SetColumn(
            iconHost,
            0);

        Grid.SetColumn(
            textContent,
            1);

        content.Children.Add(
            iconHost);

        content.Children.Add(
            textContent);

        var body =
            new Border
            {
                Padding =
                    GetThicknessResource(
                        "NetLoom.Thickness.MapNodePadding"),
                Child = content
            };

        var cardContent =
            new Grid();

        cardContent.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        cardContent.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1.0,
                        GridUnitType.Star)
            });

        Grid.SetColumn(
            stateStripe,
            0);

        Grid.SetColumn(
            body,
            1);

        cardContent.Children.Add(
            stateStripe);

        cardContent.Children.Add(
            body);

        var haloRadius =
            (CornerRadius)FindResource(
                "NetLoom.Radius.MapNode");

        var pulseHalo =
            new Rectangle
            {
                Width =
                    _nodeWidth,
                StrokeThickness =
                    2.0,
                RadiusX =
                    haloRadius.TopLeft,
                RadiusY =
                    haloRadius.TopLeft,
                Fill =
                    Brushes.Transparent,
                Visibility =
                    Visibility.Collapsed,
                IsHitTestVisible =
                    false,
                RenderTransformOrigin =
                    new Point(
                        0.5,
                        0.5),
                RenderTransform =
                    new ScaleTransform(
                        1.0,
                        1.0),
                Tag =
                    "NodeFocusHalo"
            };

        pulseHalo.SetResourceReference(
            Shape.StrokeProperty,
            "NetLoom.Brush.Selection");

        var semanticTitle = new TextBlock
        {
            FontSize = GetDoubleResource("NetLoom.FontSize.Caption"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        semanticTitle.SetResourceReference(TextBlock.ForegroundProperty, "NetLoom.Brush.TextPrimary");
        var semanticLabel = new Border
        {
            Child = semanticTitle,
            Padding = GetThicknessResource("NetLoom.Thickness.MapSemanticLabelPadding"),
            BorderThickness = GetThicknessResource("NetLoom.Thickness.BorderThin"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed
        };
        semanticLabel.SetResourceReference(Border.BackgroundProperty, "NetLoom.Brush.Surface");
        semanticLabel.SetResourceReference(Border.BorderBrushProperty, "NetLoom.Brush.Border");
        var root = new Grid();
        root.Children.Add(cardContent);
        root.Children.Add(semanticLabel);
        var border =
            new Border
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeCard"),
                Child = root,
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
            pulseHalo,
            stateStripe,
            title,
            secondary,
            categoryIcon,
            statusIcon,
            lockBadge) { SemanticLabel = semanticLabel };
    }

    private void UpdateNodeVisual(
        MapNodeVisual visual,
        MapNode node,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        visual.Node = node;
        visual.Title.Text =
            DisplayNodeLabel(
                node);

        visual.Secondary.Text =
            node.IsUnconfirmed
                ? UiText.Get("DeviceUnconfirmedMark")
                : string.Empty;

        visual.Secondary.Visibility =
            node.IsUnconfirmed
                ? Visibility.Visible
                : Visibility.Collapsed;

        visual.Secondary.ToolTip =
            node.IsUnconfirmed
                ? UiText.Get("DeviceUnconfirmedHint")
                : null;

        visual.CategoryIcon.Data =
            FindResource(
                NodeCategoryIconGeometryKey(
                    node.Category))
                as Geometry;

        visual.CategoryIcon.SetResourceReference(
            Path.StrokeProperty,
            NodeCategoryIconBrushKey(
                node.Category));

        visual.CategoryIcon.ToolTip =
            NodeCategoryIconToolTip(
                node.Category);

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
                  _selectedDeviceId.Value) ||
             // Sprint 49: узлы показанного пути выделяются так же, как выбранный.
             _pathDeviceIds.Contains(
                 node.DeviceId.Value));

        ApplyNodeDegradationPresentation(
            visual,
            node.DeviceId);

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
            visual.Border.ClearValue(
                Border.BorderThicknessProperty);

            visual.Border.ClearValue(
                Border.BorderBrushProperty);
        }

        ApplyNodeOperationalFocusPresentation(
            visual,
            node.DeviceId);
    }


    private MapLinkVisual
        CreateLinkVisual()
    {
        var selectionHaloGeometry =
            new LineGeometry();

        var selectionHalo =
            new Path
            {
                Data = selectionHaloGeometry,
                StrokeThickness =
                    GetDoubleResource(
                        "NetLoom.Map.LinkSelectionHaloThickness"),
                Opacity =
                    GetDoubleResource(
                        "NetLoom.Map.LinkSelectionHaloOpacity"),
                StrokeStartLineCap =
                    PenLineCap.Round,
                StrokeEndLineCap =
                    PenLineCap.Round,
                IsHitTestVisible = false,
                Focusable = false,
                Visibility = Visibility.Collapsed
            };

        selectionHalo.SetResourceReference(
            Shape.StrokeProperty,
            "NetLoom.Brush.Selection");

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

        var visual = new MapLinkVisual(
            selectionHalo,
            selectionHaloGeometry,
            line,
            label);

        line.MouseEnter += OnMapLinkMouseEnter;
        label.MouseEnter += OnMapLinkMouseEnter;
        line.MouseLeave += (sender, e) => OnMapLinkMouseLeave(visual);
        label.MouseLeave += (sender, e) => OnMapLinkMouseLeave(visual);

        return visual;
    }

    private void UpdateLinkVisual(
        MapLinkVisual visual,
        MapLink link,
        MapNodeVisual source,
        MapNodeVisual target,
        ParallelLinkSlot slot,
        IReadOnlyList<Rect> additionalLabelObstacles)
    {
        visual.Link = link;
        var freshnessChanged =
            visual.LastFreshness.HasValue &&
            visual.LastFreshness.Value !=
                link.Freshness;

        var operationalOpacity =
            LinkPresentationOpacity(
                link.PhysicalLinkId,
                link.Freshness);

        var confidenceDashPattern =
            LinkConfidenceDashPattern(
                link.Confidence);

        visual.Line.StrokeDashArray =
            confidenceDashPattern == null
                ? null
                : new DoubleCollection(
                    confidenceDashPattern);

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

        ParallelLinkLayout.Offset(
            x1, y1, x2, y2,
            slot.Slot,
            ParallelLinkLayout.HalfSpacing(slot.GroupSize, ParallelLinkSpacingAtZoom(), 0.75 * _nodeHeight),
            slot.SourceIsCanonicalFirst,
            out x1, out y1, out x2, out y2);

        visual.Line.X1 = x1;
        visual.Line.Y1 = y1;
        visual.Line.X2 = x2;
        visual.Line.Y2 = y2;

        visual.SelectionHaloGeometry.StartPoint =
            new Point(
                x1,
                y1);
        visual.SelectionHaloGeometry.EndPoint =
            new Point(
                x2,
                y2);

        var linkState =
            LinkOperationalState(
                link.PhysicalLinkId);

        var linkLabel =
            BuildLinkLabel(
                link);

        visual.Label.Text =
            linkLabel;

        visual.Label.ToolTip =
            OperatorStatusLabel(
                LinkStatusSemantic(
                    linkState));

        visual.SelectionHalo.Tag =
            link.PhysicalLinkId;

        visual.Line.Tag =
            link.PhysicalLinkId;

        visual.Label.Tag =
            link.PhysicalLinkId;

        visual.LastFreshness = link.Freshness;
        ApplyLinkFocusPresentation(visual);

        // Скрытая масштабом подпись должна измеряться до размещения соседнего кабеля.
        if (slot.GroupSize > 1 && visual.Label.Visibility == Visibility.Collapsed)
        {
            visual.Label.Visibility = Visibility.Hidden;
        }

        PlaceLinkLabel(
            visual.Label,
            x1,
            y1,
            x2,
            y2,
            additionalLabelObstacles);

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
        double y2,
        IReadOnlyList<Rect> additionalObstacles = null)
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
                .Concat(additionalObstacles ?? Array.Empty<Rect>())
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
                _nodeHeight,
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
        if (string.IsNullOrWhiteSpace(
                link.SourcePortLabel) &&
            string.IsNullOrWhiteSpace(
                link.TargetPortLabel))
        {
            return string.Empty;
        }

        return
            (link.SourcePortLabel ?? "?") +
            " ↔ " +
            (link.TargetPortLabel ?? "?");
    }

    private void RefreshMapInteractionModeVisuals()
    {
        foreach (var visual in
            _nodeVisualsByIdentity.Values)
        {
            UpdateNodeLockPresentation(
                visual);
        }

        foreach (var visual in
            _locationVisualsById.Values)
        {
            UpdateLocationVisualState(
                visual);
        }

        UpdateSelectedLayoutControl();
    }

    private void UpdateNodeLockPresentation(
        MapNodeVisual visual)
    {
        visual.Border.Cursor =
            !IsMapEditMode ||
            visual.IsLocked
                ? Cursors.Hand
                : Cursors.SizeAll;

        visual.LockBadge.Visibility =
            visual.IsLocked && _semanticLevel != MapSemanticLevel.Far
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
