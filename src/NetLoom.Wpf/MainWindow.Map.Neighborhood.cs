using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using NetLoom.Application.Lookup;
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

    // ADR-085: точка опроса Engine и направление «вверх» (ближе к ней).
    // Desktop и Engine работают на одной машине, поэтому её адреса известны Desktop.
    private IEngineHostAddresses _engineHostAddresses = new SystemEngineHostAddresses();
    private EnginePollingPointResult _pollingPoint;
    private bool _pollingPointRunning;
    private bool _pollingPointRerun;

    // Источник MAC-адресов машины Engine; по умолчанию системный, тесты и галерея подставляют свой.
    public IEngineHostAddresses EngineHostAddresses
    {
        get { return _engineHostAddresses; }
        set
        {
            _engineHostAddresses = value ?? new SystemEngineHostAddresses();
            _pollingPoint = null;
            // До первого снимка пересчёт не нужен: его выполнит ApplyTopologyRefresh.
            if (_lastDiagnosticSnapshot != null && _lastDiagnosticSnapshot.Devices.Count > 0)
                RefreshEnginePollingPoint();
        }
    }

    // Текущее определение точки опроса; null — расчёт ещё не завершён.
    internal EnginePollingPointResult PollingPoint => _pollingPoint;

    private IReadOnlyList<MapNeighborhoodLink> NeighborhoodLinks()
    {
        if (_lastMapSnapshot == null) return new MapNeighborhoodLink[0];
        var nodes = _lastMapSnapshot.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal);
        var links = new List<MapNeighborhoodLink>();
        foreach (var link in _lastMapSnapshot.Links)
        {
            MapNode a;
            MapNode b;
            if (!nodes.TryGetValue(link.SourceNodeKey, out a) || !nodes.TryGetValue(link.TargetNodeKey, out b) ||
                !a.DeviceId.HasValue || !b.DeviceId.HasValue) continue;
            links.Add(new MapNeighborhoodLink(link.PhysicalLinkId ?? Guid.Empty, a.DeviceId.Value, b.DeviceId.Value));
        }
        return links;
    }

    // Расстояния до точки опроса считаются по физическим связям диагностики (неориентированный граф устройств).
    // Точка опроса не определена — null: направления «вверх/вниз» нет.
    private Dictionary<Guid, int> PollingDistances()
    {
        if (_pollingPoint == null || _pollingPoint.Status != EnginePollingPointStatus.Determined ||
            !_pollingPoint.DeviceId.HasValue || _lastDiagnosticSnapshot == null) return null;
        var links = _lastDiagnosticSnapshot.Links
            .Select(link => new MapNeighborhoodLink(link.PhysicalLinkId, link.DeviceAId, link.DeviceBId)).ToArray();
        return MapNeighborhood.Distances(_pollingPoint.DeviceId.Value, links);
    }

    // Пересчёт точки опроса: один поиск по MAC в фоновом потоке за обновление снимков.
    // Обновление, пришедшее во время расчёта, ставит один повторный расчёт, а не очередь.
    private async void RefreshEnginePollingPoint()
    {
        if (_lifetimeCancellation.IsCancellationRequested || _lastDiagnosticSnapshot == null) return;
        if (_pollingPointRunning)
        {
            _pollingPointRerun = true;
            return;
        }
        _pollingPointRunning = true;
        try
        {
            do
            {
                _pollingPointRerun = false;
                var linkInterfaces = new HashSet<Guid>();
                foreach (var link in _lastDiagnosticSnapshot.Links)
                {
                    if (link.InterfaceAId.HasValue) linkInterfaces.Add(link.InterfaceAId.Value);
                    if (link.InterfaceBId.HasValue) linkInterfaces.Add(link.InterfaceBId.Value);
                }
                var hostAddresses = _engineHostAddresses;
                var token = _lifetimeCancellation.Token;
                EnginePollingPointResult result;
                try
                {
                    var candidates = await Task.Run(() =>
                    {
                        var found = new List<MacIpLookupCandidate>();
                        foreach (var mac in hostAddresses.GetMacAddresses())
                        {
                            try
                            {
                                found.AddRange(_lookupSearchService.Search(mac, LookupCandidateLimit).Candidates);
                            }
                            catch (ArgumentException)
                            {
                                // Адрес адаптера в неподдерживаемом виде пропускаем.
                            }
                        }
                        return found;
                    }, token);
                    result = EnginePollingPoint.Resolve(candidates, linkInterfaces);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception error)
                {
                    System.Diagnostics.Trace.TraceError(error.ToString());
                    result = new EnginePollingPointResult(EnginePollingPointStatus.NotFound, null);
                }
                if (_lifetimeCancellation.IsCancellationRequested) return;
                _pollingPoint = result;
                UpdateNeighborhoodMenuState();
            }
            while (_pollingPointRerun);
        }
        finally
        {
            _pollingPointRunning = false;
        }
    }

    private string PollingReasonKey()
    {
        if (_pollingPoint == null) return "MapNeighborhoodPollingPending";
        switch (_pollingPoint.Status)
        {
            case EnginePollingPointStatus.Ambiguous: return "MapNeighborhoodPollingAmbiguous";
            case EnginePollingPointStatus.NoAccessPort: return "MapNeighborhoodPollingNoAccessPort";
            case EnginePollingPointStatus.NotFound: return "MapNeighborhoodPollingNotFound";
            default: return null;
        }
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
        var distances = PollingDistances();
        MapNeighborhoodUpButton.IsEnabled = MapNeighborhood.CanExpandUp(_neighborhoodDeviceIds, links, distances);
        MapNeighborhoodDownButton.IsEnabled = MapNeighborhood.CanExpandDown(_neighborhoodDeviceIds, links, distances);
        // Точка опроса не определена — причина называет вид неопределённости; иначе — что раскрывать нечего.
        var pollingReason = PollingReasonKey();
        MapNeighborhoodUpReason.Text = UiText.Get(pollingReason ?? "MapNeighborhoodNoMoreUp");
        MapNeighborhoodDownReason.Text = UiText.Get(pollingReason ?? "MapNeighborhoodNoMoreDown");
        MapNeighborhoodUpReason.Visibility = MapNeighborhoodUpButton.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        MapNeighborhoodDownReason.Visibility = MapNeighborhoodDownButton.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        MapNeighborhoodOtherButton.Visibility = MapNeighborhood.CanExpandUndirected(_neighborhoodDeviceIds, links, distances)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMapNeighborhoodExpandClick(object sender, RoutedEventArgs e)
    {
        if (!_neighborhoodSelectedDeviceId.HasValue) return;
        var button = (Button)sender;
        var links = NeighborhoodLinks();
        var distances = PollingDistances();
        _neighborhoodDeviceIds = button == MapNeighborhoodUpButton ? MapNeighborhood.ExpandUp(_neighborhoodDeviceIds, links, distances)
            : button == MapNeighborhoodDownButton ? MapNeighborhood.ExpandDown(_neighborhoodDeviceIds, links, distances)
            : MapNeighborhood.ExpandUndirected(_neighborhoodDeviceIds, links, distances);
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
