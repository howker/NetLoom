using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private Guid? _neighborhoodSelectedDeviceId;
    private HashSet<Guid> _neighborhoodDeviceIds = new HashSet<Guid>();
    private MenuItem _neighborhoodMenuItem;

    private MenuItem CreateNeighborhoodMenuItem()
    {
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = UiText.Get("MapNeighborhoodMenu") });
        var reason = new TextBlock { Text = UiText.Get("MapNeighborhoodSelectDevice") };
        reason.SetResourceReference(FrameworkElement.StyleProperty, "NetLoom.Style.MutedText");
        reason.Visibility = _selectedDeviceId.HasValue ? Visibility.Collapsed : Visibility.Visible;
        header.Children.Add(reason);
        _neighborhoodMenuItem = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsEnabled = _selectedDeviceId.HasValue,
            IsChecked = _neighborhoodSelectedDeviceId.HasValue
        };
        AutomationProperties.SetName(_neighborhoodMenuItem, UiText.Get("MapNeighborhoodMenu"));
        _neighborhoodMenuItem.Click += (sender, args) => EnableNeighborhood();
        return _neighborhoodMenuItem;
    }

    private IReadOnlyList<MapNeighborhoodLink> NeighborhoodLinks()
    {
        if (_lastMapSnapshot == null) return new MapNeighborhoodLink[0];
        var nodes = _lastMapSnapshot.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal);
        var diagnostics = _lastDiagnosticSnapshot?.Links.ToDictionary(link => link.PhysicalLinkId);
        var links = new List<MapNeighborhoodLink>();
        foreach (var link in _lastMapSnapshot.Links)
        {
            MapNode a;
            MapNode b;
            if (!nodes.TryGetValue(link.SourceNodeKey, out a) || !nodes.TryGetValue(link.TargetNodeKey, out b) ||
                !a.DeviceId.HasValue || !b.DeviceId.HasValue) continue;
            PhysicalLinkDiagnostic diagnostic = null;
            if (link.PhysicalLinkId.HasValue && diagnostics != null)
                diagnostics.TryGetValue(link.PhysicalLinkId.Value, out diagnostic);
            var uplink = diagnostic?.StpUplink ?? DiagnosticStpUplink.Unknown;
            // Концы диагностической связи и направление линии карты могут идти в обратном порядке.
            if (diagnostic != null && diagnostic.DeviceAId != a.DeviceId.Value)
                uplink = uplink == DiagnosticStpUplink.SideAIsUpstream ? DiagnosticStpUplink.SideBIsUpstream
                    : uplink == DiagnosticStpUplink.SideBIsUpstream ? DiagnosticStpUplink.SideAIsUpstream : uplink;
            links.Add(new MapNeighborhoodLink(link.PhysicalLinkId ?? Guid.Empty,
                a.DeviceId.Value, b.DeviceId.Value, uplink));
        }
        return links;
    }

    private void EnableNeighborhood()
    {
        if (!_selectedDeviceId.HasValue || _lastMapSnapshot == null ||
            !_lastMapSnapshot.Nodes.Any(node => node.DeviceId == _selectedDeviceId)) return;
        StopStartupTopologyFit();
        // Операционный фокус и окрестность — взаимоисключающие режимы показа.
        _operationalFocusMode = MapOperationalFocusMode.None;
        RefreshOperationalFocusTargets();
        _neighborhoodSelectedDeviceId = _selectedDeviceId;
        _neighborhoodDeviceIds = MapNeighborhood.Initial(_selectedDeviceId.Value, NeighborhoodLinks());
        RefreshNeighborhoodPresentation();
        FitNeighborhoodToViewport();
    }

    private void DisableNeighborhood()
    {
        _neighborhoodSelectedDeviceId = null;
        _neighborhoodDeviceIds.Clear();
        UpdateNeighborhoodMenuState();
        if (_lastMapSnapshot == null) return;
        UpdateLocationHierarchyVisibility();
        ReconcileLinks(_lastMapSnapshot.Links,
            _lastMapSnapshot.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal));
        UpdateSemanticMapVisibility();
    }

    private void ShowWholeSite()
    {
        StopStartupTopologyFit();
        SetOperationalFocusMode(MapOperationalFocusMode.None);
        // Вся площадка допускает ZoomMin; читаемость обеспечивает семантический масштаб.
        FitTopologyToViewport();
    }

    private void RefreshNeighborhoodSnapshot()
    {
        if (_neighborhoodSelectedDeviceId.HasValue)
        {
            var existing = new HashSet<Guid>(_lastMapSnapshot.Nodes.Where(node => node.DeviceId.HasValue)
                .Select(node => node.DeviceId.Value));
            if (!existing.Contains(_neighborhoodSelectedDeviceId.Value))
            {
                DisableNeighborhood();
                ReapplyOperationalFocusPresentation();
            }
            else
            {
                // Обновление сохраняет уже раскрытое множество, удаляя только исчезнувшие узлы.
                _neighborhoodDeviceIds.IntersectWith(existing);
                ApplyNeighborhoodVisibility();
                ApplyLinkFocusPresentation();
            }
        }
        UpdateNeighborhoodMenuState();
    }

    private void RefreshNeighborhoodPresentation()
    {
        UpdateLocationHierarchyVisibility();
        ApplyNeighborhoodVisibility();
        ReconcileLinks(_lastMapSnapshot.Links,
            _lastMapSnapshot.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal));
        ReapplyOperationalFocusPresentation();
        UpdateOperationalFocusMenuState();
    }

    private void RefreshNeighborhoodForSection()
    {
        // Карта общая с предупреждениями: там видны все участники, а рабочая окрестность сохраняется.
        if (_neighborhoodSelectedDeviceId.HasValue && _lastMapSnapshot != null)
            RefreshNeighborhoodPresentation();
        else
            UpdateNeighborhoodMenuState();
    }

    private void ApplyNeighborhoodVisibility()
    {
        if (!_neighborhoodSelectedDeviceId.HasValue || _lastMapSnapshot == null ||
            _shellSection != ShellSection.Map) return;
        var locations = _lastMapSnapshot.Locations.ToDictionary(location => location.Id);
        var includedLocations = new HashSet<Guid>();
        foreach (var node in _lastMapSnapshot.Nodes)
        {
            MapNodeVisual visual;
            if (!_nodeVisualsByIdentity.TryGetValue(NodeIdentity(node), out visual)) continue;
            if (!node.DeviceId.HasValue || !_neighborhoodDeviceIds.Contains(node.DeviceId.Value))
            {
                visual.Border.Visibility = Visibility.Collapsed;
                visual.PulseHalo.Visibility = Visibility.Collapsed;
                visual.SemanticLabel.Visibility = Visibility.Collapsed;
                continue;
            }
            if (visual.Border.Visibility != Visibility.Visible) continue;
            if (!node.LocationId.HasValue) includedLocations.Add(Guid.Empty);
            var locationId = node.LocationId;
            while (locationId.HasValue && includedLocations.Add(locationId.Value))
            {
                MapLocation location;
                locationId = locations.TryGetValue(locationId.Value, out location) ? location.ParentLocationId : null;
            }
        }
        foreach (var visual in _locationVisualsById.Values)
            if (!includedLocations.Contains(visual.LocationId)) visual.Border.Visibility = Visibility.Collapsed;
    }

    private void ApplyNeighborhoodLinkVisibility(MapLinkVisual visual)
    {
        if (!_neighborhoodSelectedDeviceId.HasValue || _lastMapSnapshot == null ||
            _shellSection != ShellSection.Map) return;
        var link = visual.Link;
        if (link == null) return;
        var a = _lastMapSnapshot.Nodes.FirstOrDefault(node => node.Key == link.SourceNodeKey);
        var b = _lastMapSnapshot.Nodes.FirstOrDefault(node => node.Key == link.TargetNodeKey);
        if (a?.DeviceId == null || b?.DeviceId == null ||
            !_neighborhoodDeviceIds.Contains(a.DeviceId.Value) || !_neighborhoodDeviceIds.Contains(b.DeviceId.Value))
        {
            visual.Line.Visibility = Visibility.Collapsed;
            visual.Label.Visibility = Visibility.Collapsed;
            visual.SelectionHalo.Visibility = Visibility.Collapsed;
        }
    }

    private void FitNeighborhoodToViewport()
    {
        var bounds = _nodeVisualsByIdentity.Values.Where(visual => visual.DeviceId.HasValue &&
            _neighborhoodDeviceIds.Contains(visual.DeviceId.Value) && visual.Border.Visibility == Visibility.Visible)
            .Select(NodeBounds).ToArray();
        TryFitMapBoundsToViewport(bounds, _readableZoomMin, 1.0);
    }

    private void UpdateNeighborhoodMenuState()
    {
        var actions = new[] { MapNeighborhoodUpButton, MapNeighborhoodDownButton,
            MapNeighborhoodOtherButton, MapNeighborhoodWholeSiteButton };
        var keys = new[] { "MapNeighborhoodExpandUp", "MapNeighborhoodExpandDown",
            "MapNeighborhoodExpandOther", "MapNeighborhoodWholeSite" };
        for (var i = 0; i < actions.Length; i++)
        {
            actions[i].Content = UiText.Get(keys[i]);
            AutomationProperties.SetName(actions[i], UiText.Get(keys[i]));
        }
        MapNeighborhoodDownReason.Text = UiText.Get("MapNeighborhoodNoMoreDown");
        var active = _neighborhoodSelectedDeviceId.HasValue;
        if (_neighborhoodMenuItem != null) _neighborhoodMenuItem.IsChecked = active;
        var caption = UiText.Get(active ? "MapNeighborhoodShow" : "ShellMapFocusAction");
        MapOperationalFocusButton.Content = caption;
        AutomationProperties.SetName(MapOperationalFocusButton, caption);
        MapNeighborhoodNotice.Visibility = active && _shellSection == ShellSection.Map
            ? Visibility.Visible : Visibility.Collapsed;
        if (!active || _lastMapSnapshot == null) return;
        var selected = _lastMapSnapshot.Nodes.FirstOrDefault(node => node.DeviceId == _neighborhoodSelectedDeviceId);
        MapNeighborhoodSummaryText.Text = UiText.Format("MapNeighborhoodSummary", selected?.Label,
            _neighborhoodDeviceIds.Count, _lastMapSnapshot.Nodes.Count(node => node.DeviceId.HasValue));
        var links = NeighborhoodLinks();
        MapNeighborhoodUpButton.IsEnabled = MapNeighborhood.CanExpandUp(_neighborhoodDeviceIds, links);
        MapNeighborhoodDownButton.IsEnabled = MapNeighborhood.CanExpandDown(_neighborhoodDeviceIds, links);
        MapNeighborhoodUpReason.Text = UiText.Get(links.Any(link => link.StpUplink != DiagnosticStpUplink.Unknown)
            ? "MapNeighborhoodNoMoreUp" : "MapNeighborhoodNoStpUp");
        MapNeighborhoodUpReason.Visibility = MapNeighborhoodUpButton.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        MapNeighborhoodDownReason.Visibility = MapNeighborhoodDownButton.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        MapNeighborhoodOtherButton.Visibility = MapNeighborhood.CanExpandUndirected(_neighborhoodDeviceIds, links)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMapNeighborhoodExpandClick(object sender, RoutedEventArgs e)
    {
        if (!_neighborhoodSelectedDeviceId.HasValue) return;
        var button = (Button)sender;
        var links = NeighborhoodLinks();
        _neighborhoodDeviceIds = button == MapNeighborhoodUpButton ? MapNeighborhood.ExpandUp(_neighborhoodDeviceIds, links)
            : button == MapNeighborhoodDownButton ? MapNeighborhood.ExpandDown(_neighborhoodDeviceIds, links)
            : MapNeighborhood.ExpandUndirected(_neighborhoodDeviceIds, links);
        RefreshNeighborhoodPresentation();
        FitNeighborhoodToViewport();
        // После исчезновения или блокировки действия фокус переходит на следующую кнопку полосы.
        var buttons = new[] { MapNeighborhoodUpButton, MapNeighborhoodDownButton,
            MapNeighborhoodOtherButton, MapNeighborhoodWholeSiteButton };
        var index = Array.IndexOf(buttons, button);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var target = button.IsEnabled && button.IsVisible ? button
                : buttons.Skip(index + 1).FirstOrDefault(item => item.IsEnabled && item.IsVisible);
            target?.Focus();
        }));
    }

    private void OnMapNeighborhoodWholeSiteClick(object sender, RoutedEventArgs e)
    {
        ShowWholeSite();
        MapFitAllButton.Focus();
    }
}
