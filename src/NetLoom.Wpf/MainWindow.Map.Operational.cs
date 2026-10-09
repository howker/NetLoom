using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private enum MapLinkOperationalState
    {
        Normal = 0,
        Forwarding = 1,
        Transition = 2,
        Blocked = 3,
        Degraded = 4,
        Critical = 5
    }

    private enum MapOperationalFocusMode
    {
        None = 0,
        AllProblems = 1,
        CriticalLinks = 2,
        DegradedLinks = 3,
        BlockedLinks = 4,
        TransitionLinks = 5,
        DegradedNodes = 6
    }

    private const double OperationalFocusDimmedOpacity = 0.12;

    private readonly Dictionary<Guid, MapLinkOperationalState>
        _linkOperationalStates =
            new Dictionary<Guid, MapLinkOperationalState>();

    private readonly HashSet<Guid>
        _operationalFocusPhysicalLinkIds =
            new HashSet<Guid>();

    private readonly HashSet<Guid>
        _operationalFocusDeviceIds =
            new HashSet<Guid>();

    private readonly Dictionary<MapOperationalFocusMode, MenuItem>
        _operationalFocusMenuItems =
            new Dictionary<MapOperationalFocusMode, MenuItem>();

    private MapOperationalFocusMode
        _operationalFocusMode =
            MapOperationalFocusMode.None;

    private void UpdateLinkOperationalStates(
        NetworkDiagnosticSnapshot diagnosticSnapshot,
        TopologyAlertSnapshot alertSnapshot)
    {
        if (diagnosticSnapshot == null)
        {
            throw new ArgumentNullException(
                nameof(diagnosticSnapshot));
        }

        if (alertSnapshot == null)
        {
            throw new ArgumentNullException(
                nameof(alertSnapshot));
        }

        _linkOperationalStates.Clear();

        foreach (var link in diagnosticSnapshot.Links)
        {
            SetLinkOperationalState(
                link.PhysicalLinkId,
                ClassifyStpOperationalState(
                    link.StpStateA,
                    link.StpStateB));
        }

        foreach (var alert in alertSnapshot.Alerts)
        {
            var state =
                ClassifyAlertOperationalState(
                    alert.Kind);

            foreach (var physicalLinkId in
                alert.PhysicalLinkIds)
            {
                SetLinkOperationalState(
                    physicalLinkId,
                    state);
            }
        }
    }

    private void SetLinkOperationalState(
        Guid physicalLinkId,
        MapLinkOperationalState state)
    {
        if (physicalLinkId == Guid.Empty ||
            state == MapLinkOperationalState.Normal)
        {
            return;
        }

        MapLinkOperationalState current;

        if (_linkOperationalStates.TryGetValue(
                physicalLinkId,
                out current) &&
            current >= state)
        {
            return;
        }

        _linkOperationalStates[physicalLinkId] =
            state;
    }

    private void ApplyLinkOperationalPresentation(
        MapLinkVisual visual,
        Guid? physicalLinkId)
    {
        var state =
            LinkOperationalState(
                physicalLinkId);

        var brushKey =
            LinkOperationalBrushKey(
                state);

        if (brushKey == null)
        {
            visual.Line.ClearValue(
                System.Windows.Shapes.Shape.StrokeProperty);

            visual.Line.ClearValue(
                System.Windows.Shapes.Shape.StrokeThicknessProperty);

            visual.Label.ClearValue(
                TextBlock.ForegroundProperty);

            visual.Label.ClearValue(
                TextBlock.FontWeightProperty);

            return;
        }

        visual.Line.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            brushKey);

        visual.Line.StrokeThickness =
            GetDoubleResource(
                "NetLoom.Map.LinkStrokeThickness") *
            LinkOperationalStrokeScale(
                state);

        visual.Label.SetResourceReference(
            TextBlock.ForegroundProperty,
            brushKey);

        visual.Label.FontWeight =
            LinkOperationalLabelWeight(
                state);
    }

    private double LinkSelectedStrokeThickness(
        MapLinkOperationalState state)
    {
        return Math.Max(
            GetDoubleResource(
                "NetLoom.Map.LinkSelectedStrokeThickness"),
            GetDoubleResource(
                "NetLoom.Map.LinkStrokeThickness") *
            (LinkOperationalStrokeScale(state) + 0.25));
    }

    private MapLinkOperationalState LinkOperationalState(
        Guid? physicalLinkId)
    {
        if (!physicalLinkId.HasValue)
        {
            return MapLinkOperationalState.Normal;
        }

        MapLinkOperationalState state;

        return _linkOperationalStates.TryGetValue(
                   physicalLinkId.Value,
                   out state)
            ? state
            : MapLinkOperationalState.Normal;
    }

    private static MapLinkOperationalState
        ClassifyStpOperationalState(
            StpTreePortState stateA,
            StpTreePortState stateB)
    {
        var first =
            ClassifyStpEndpointState(
                stateA);

        var second =
            ClassifyStpEndpointState(
                stateB);

        return first >= second
            ? first
            : second;
    }

    private static MapLinkOperationalState
        ClassifyStpEndpointState(
            StpTreePortState state)
    {
        switch (state)
        {
            case StpTreePortState.Broken:
                return MapLinkOperationalState.Critical;

            case StpTreePortState.Disabled:
                return MapLinkOperationalState.Degraded;

            case StpTreePortState.Blocking:
                return MapLinkOperationalState.Blocked;

            case StpTreePortState.Listening:
            case StpTreePortState.Learning:
                return MapLinkOperationalState.Transition;

            case StpTreePortState.Forwarding:
                return MapLinkOperationalState.Forwarding;

            case StpTreePortState.Unknown:
            default:
                return MapLinkOperationalState.Normal;
        }
    }

    private static MapLinkOperationalState
        ClassifyAlertOperationalState(
            TopologyAlertKind kind)
    {
        switch (kind)
        {
            case TopologyAlertKind.ForwardingCycle:
                return MapLinkOperationalState.Critical;

            case TopologyAlertKind.RingProtectionDegraded:
                return MapLinkOperationalState.Degraded;

            default:
                return MapLinkOperationalState.Normal;
        }
    }

    private MenuItem CreateOperationalFocusMenu()
    {
        var focusMenu =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapOperationalFocusSettings")
            };

        // Окрестность — режим существующего меню «Показать», без новой верхней панели.
        focusMenu.Items.Add(CreateNeighborhoodMenuItem());
        focusMenu.Items.Add(new Separator());

        focusMenu.Items.Add(
            CreateOperationalFocusMenuItem(
                MapOperationalFocusMode.AllProblems,
                "MapOperationalFocusAllProblems"));

        focusMenu.Items.Add(
            new Separator());

        focusMenu.Items.Add(
            CreateOperationalFocusMenuItem(
                MapOperationalFocusMode.CriticalLinks,
                "MapOperationalFocusCriticalLinks"));

        focusMenu.Items.Add(
            CreateOperationalFocusMenuItem(
                MapOperationalFocusMode.DegradedLinks,
                "MapOperationalFocusDegradedLinks"));

        focusMenu.Items.Add(
            CreateOperationalFocusMenuItem(
                MapOperationalFocusMode.BlockedLinks,
                "MapOperationalFocusBlockedLinks"));

        focusMenu.Items.Add(
            CreateOperationalFocusMenuItem(
                MapOperationalFocusMode.TransitionLinks,
                "MapOperationalFocusTransitionLinks"));

        focusMenu.Items.Add(
            CreateOperationalFocusMenuItem(
                MapOperationalFocusMode.DegradedNodes,
                "MapOperationalFocusDegradedNodes"));

        focusMenu.Items.Add(
            new Separator());

        focusMenu.Items.Add(
            CreateOperationalFocusMenuItem(
                MapOperationalFocusMode.None,
                "MapOperationalFocusClear"));

        UpdateOperationalFocusMenuState();

        return focusMenu;
    }

    private MenuItem CreateOperationalFocusMenuItem(
        MapOperationalFocusMode mode,
        string resourceKey)
    {
        var item =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        resourceKey),
                IsCheckable = true,
                Tag = mode
            };

        item.Click +=
            OnOperationalFocusMenuItemClick;

        _operationalFocusMenuItems[mode] =
            item;

        return item;
    }

    private void OnOperationalFocusMenuItemClick(
        object sender,
        RoutedEventArgs e)
    {
        var item =
            sender as MenuItem;

        if (item == null ||
            !(item.Tag is MapOperationalFocusMode))
        {
            return;
        }

        // Sprint 49: смена режима показа — действие, прежний вид которого попадает в историю.
        RecordMapView(false);

        SetOperationalFocusMode(
            (MapOperationalFocusMode)item.Tag);
    }

    private void SetOperationalFocusMode(
        MapOperationalFocusMode mode)
    {
        DisableNeighborhood();
        _operationalFocusMode =
            mode;

        RefreshOperationalFocusTargets();
        UpdateOperationalFocusMenuState();
        ReapplyOperationalFocusPresentation();
        FitOperationalFocusToViewport();
    }

    private void UpdateOperationalFocusMenuState()
    {
        foreach (var pair in
            _operationalFocusMenuItems)
        {
            pair.Value.IsChecked =
                pair.Key ==
                _operationalFocusMode && !_neighborhoodSelectedDeviceId.HasValue;
        }
        UpdateNeighborhoodMenuState();
    }

    private void RefreshOperationalFocusTargets()
    {
        _operationalFocusPhysicalLinkIds.Clear();
        _operationalFocusDeviceIds.Clear();

        if (_operationalFocusMode ==
                MapOperationalFocusMode.None ||
            _lastMapSnapshot == null)
        {
            return;
        }

        var nodesByKey =
            _lastMapSnapshot.Nodes
                .ToDictionary(
                    item => item.Key,
                    StringComparer.Ordinal);

        foreach (var link in
            _lastMapSnapshot.Links)
        {
            var state =
                LinkOperationalState(
                    link.PhysicalLinkId);

            if (!OperationalFocusMatchesLink(
                    _operationalFocusMode,
                    state))
            {
                continue;
            }

            if (link.PhysicalLinkId.HasValue)
            {
                _operationalFocusPhysicalLinkIds.Add(
                    link.PhysicalLinkId.Value);
            }

            AddOperationalFocusEndpoint(
                nodesByKey,
                link.SourceNodeKey);

            AddOperationalFocusEndpoint(
                nodesByKey,
                link.TargetNodeKey);
        }

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            if (!node.DeviceId.HasValue)
            {
                continue;
            }

            if (OperationalFocusMatchesNode(
                    _operationalFocusMode,
                    NodeDegradationState(
                        node.DeviceId)))
            {
                _operationalFocusDeviceIds.Add(
                    node.DeviceId.Value);
            }
        }
    }

    private void AddOperationalFocusEndpoint(
        IReadOnlyDictionary<string, MapNode> nodesByKey,
        string nodeKey)
    {
        MapNode node;

        if (nodesByKey.TryGetValue(
                nodeKey,
                out node) &&
            node.DeviceId.HasValue)
        {
            _operationalFocusDeviceIds.Add(
                node.DeviceId.Value);
        }
    }

    private void ReapplyOperationalFocusPresentation()
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            MapNodeVisual visual;

            if (_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out visual))
            {
                ApplyNodeOperationalFocusPresentation(
                    visual,
                    node.DeviceId);
            }
        }

        foreach (var visual in _locationVisualsById.Values)
            ApplyLocationSemanticPresentation(visual);
        ApplyFarLabelDeclutter();
        ApplyLinkFocusPresentation();
    }

    private void ApplyNodeOperationalFocusPresentation(
        MapNodeVisual visual,
        Guid? deviceId)
    {
        visual.Border.Opacity =
            NodeOperationalFocusOpacity(
                deviceId);
        ApplyNodeSemanticPresentation(visual);
    }

    private double NodeOperationalFocusOpacity(
        Guid? deviceId)
    {
        if (_operationalFocusMode ==
                MapOperationalFocusMode.None ||
            (deviceId.HasValue &&
             ((_selectedDeviceId.HasValue &&
               _selectedDeviceId.Value == deviceId.Value) ||
              (_highlightedDeviceId.HasValue &&
               _highlightedDeviceId.Value == deviceId.Value) ||
              _operationalFocusDeviceIds.Contains(
                  deviceId.Value))))
        {
            return 1.0;
        }

        return OperationalFocusDimmedOpacity;
    }

    private double LinkPresentationOpacity(
        Guid? physicalLinkId,
        MapFreshness freshness)
    {
        var focusOpacity =
            1.0;

        if (_operationalFocusMode !=
            MapOperationalFocusMode.None)
        {
            var selected =
                physicalLinkId.HasValue &&
                _selectedPhysicalLinkId.HasValue &&
                physicalLinkId.Value ==
                    _selectedPhysicalLinkId.Value;

            var focused =
                physicalLinkId.HasValue &&
                _operationalFocusPhysicalLinkIds.Contains(
                    physicalLinkId.Value);

            if (!selected &&
                !focused)
            {
                focusOpacity =
                    OperationalFocusDimmedOpacity;
            }
        }

        // Sprint 49: пока показан путь, остальные связи приглушены так же, как при фокусной связи.
        var pathActive = _pathLinkIds.Count > 0;
        var onPath = pathActive && physicalLinkId.HasValue && _pathLinkIds.Contains(physicalLinkId.Value);
        var linkFocusOpacity = !onPath && (pathActive ||
            (FocusedPhysicalLinkId.HasValue && physicalLinkId != FocusedPhysicalLinkId))
                ? GetDoubleResource("NetLoom.Map.LinkFocusDimmedOpacity")
                : 1.0;

        return LinkFreshnessOpacity(
                   freshness) *
               focusOpacity *
               linkFocusOpacity;
    }

    private void FitOperationalFocusToViewport()
    {
        if (_operationalFocusMode ==
            MapOperationalFocusMode.None)
        {
            return;
        }

        var bounds =
            new List<Rect>();

        foreach (var visual in
            _nodeVisualsByIdentity.Values)
        {
            if (visual.DeviceId.HasValue &&
                _operationalFocusDeviceIds.Contains(
                    visual.DeviceId.Value))
            {
                bounds.Add(
                    NodeBounds(visual));
            }
        }

        foreach (var visual in
            _linkVisualsByIdentity.Values)
        {
            if (!(visual.Line.Tag is Guid) ||
                !_operationalFocusPhysicalLinkIds.Contains(
                    (Guid)visual.Line.Tag))
            {
                continue;
            }

            bounds.Add(
                new Rect(
                    new Point(
                        Math.Min(
                            visual.Line.X1,
                            visual.Line.X2),
                        Math.Min(
                            visual.Line.Y1,
                            visual.Line.Y2)),
                    new Point(
                        Math.Max(
                            visual.Line.X1,
                            visual.Line.X2),
                        Math.Max(
                            visual.Line.Y1,
                            visual.Line.Y2))));
        }

        // Участники должны помещаться целиком; мелкий текст убирает семантический масштаб.
        TryFitMapBoundsToViewport(bounds, _zoomMin);
    }

    private static bool OperationalFocusMatchesLink(
        MapOperationalFocusMode mode,
        MapLinkOperationalState state)
    {
        switch (mode)
        {
            case MapOperationalFocusMode.AllProblems:
                return state ==
                           MapLinkOperationalState.Transition ||
                       state ==
                           MapLinkOperationalState.Blocked ||
                       state ==
                           MapLinkOperationalState.Degraded ||
                       state ==
                           MapLinkOperationalState.Critical;

            case MapOperationalFocusMode.CriticalLinks:
                return state ==
                    MapLinkOperationalState.Critical;

            case MapOperationalFocusMode.DegradedLinks:
                return state ==
                    MapLinkOperationalState.Degraded;

            case MapOperationalFocusMode.BlockedLinks:
                return state ==
                    MapLinkOperationalState.Blocked;

            case MapOperationalFocusMode.TransitionLinks:
                return state ==
                    MapLinkOperationalState.Transition;

            case MapOperationalFocusMode.None:
            case MapOperationalFocusMode.DegradedNodes:
            default:
                return false;
        }
    }

    private static bool OperationalFocusMatchesNode(
        MapOperationalFocusMode mode,
        MapNodeDegradationState state)
    {
        switch (mode)
        {
            case MapOperationalFocusMode.AllProblems:
            case MapOperationalFocusMode.DegradedNodes:
                return state ==
                    MapNodeDegradationState.Degraded;

            default:
                return false;
        }
    }

    private enum MapNodeDegradationState
    {
        Unknown = 0,
        Normal = 1,
        Degraded = 2,
        Critical = 3
    }

    private void ApplyNodeDegradationPresentation(
        MapNodeVisual visual,
        Guid? deviceId)
    {
        var state =
            NodeDegradationState(
                deviceId);

        var semantic =
            NodeStatusSemantic(
                state);

        var hasProblem =
            state == MapNodeDegradationState.Degraded ||
            state == MapNodeDegradationState.Critical;

        var brushKey =
            hasProblem
                ? OperatorStatusBrushKey(
                    semantic)
                : "NetLoom.Brush.BorderStrong";

        visual.StateStripe.SetResourceReference(
            System.Windows.Controls.Border.BackgroundProperty,
            brushKey);

        visual.StatusIcon.Visibility =
            hasProblem
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (hasProblem)
        {
            visual.StatusIcon.Data =
                FindResource(
                    OperatorStatusIconGeometryKey(
                        semantic))
                as Geometry;

            visual.StatusIcon.ToolTip =
                OperatorStatusLabel(
                    semantic);

            visual.StatusIcon.SetResourceReference(
                Path.StrokeProperty,
                brushKey);
        }
        else
        {
            visual.StatusIcon.Data =
                null;
            visual.StatusIcon.ToolTip =
                null;
            visual.StatusIcon.ClearValue(
                Path.StrokeProperty);
        }
        // Sprint 49, K4: имя карточки для UI Automation — подпись и состояние, как в «Оборудовании».
        if (visual.Node != null)
        {
            System.Windows.Automation.AutomationProperties.SetName(
                visual.Border,
                UiText.Format(
                    "MapNodeAutomationName",
                    DisplayNodeLabel(
                        visual.Node),
                    OperatorStatusLabel(
                        semantic)));
        }

        ApplyNodeSemanticPresentation(visual);
        // Изменение проблемы вне обновления схемы должно сразу попасть в сводку размещения.
        foreach (var location in _locationVisualsById.Values)
            ApplyLocationSemanticPresentation(location);
    }

    private MapNodeDegradationState
        NodeDegradationState(
            Guid? deviceId)
    {
        if (!deviceId.HasValue ||
            _lastDiagnosticSnapshot == null)
        {
            return MapNodeDegradationState.Unknown;
        }

        var linkedState =
            ConnectedNodeOperationalState(
                deviceId.Value);

        if (linkedState ==
            MapLinkOperationalState.Critical)
        {
            return MapNodeDegradationState.Critical;
        }

        // Sprint 47: «Устройство не отвечает» — предупреждение на карточке, как в инспекторе и списке.
        if (_lastAlertSnapshot != null &&
            _lastAlertSnapshot.Alerts.Any(
                alert =>
                    alert.DeviceIds.Contains(
                        deviceId.Value)))
        {
            return MapNodeDegradationState.Degraded;
        }

        if (linkedState ==
                MapLinkOperationalState.Degraded ||
            linkedState ==
                MapLinkOperationalState.Blocked ||
            linkedState ==
                MapLinkOperationalState.Transition)
        {
            return MapNodeDegradationState.Degraded;
        }

        foreach (var device in
            _lastDiagnosticSnapshot.Devices)
        {
            if (device.DeviceId !=
                deviceId.Value)
            {
                continue;
            }

            var statuses =
                new List<DiagnosticDegradationStatus>();

            foreach (var item in
                device.Interfaces)
            {
                statuses.Add(
                    item.DegradationStatus);
            }

            return ClassifyNodeDegradationState(
                statuses);
        }

        return MapNodeDegradationState.Unknown;
    }

    private MapLinkOperationalState
        ConnectedNodeOperationalState(
            Guid deviceId)
    {
        if (_lastMapSnapshot == null)
        {
            return MapLinkOperationalState.Normal;
        }

        var node =
            _lastMapSnapshot.Nodes
                .FirstOrDefault(
                    item =>
                        item.DeviceId.HasValue &&
                        item.DeviceId.Value ==
                            deviceId);

        if (node == null)
        {
            return MapLinkOperationalState.Normal;
        }

        var state =
            MapLinkOperationalState.Normal;

        foreach (var link in
            _lastMapSnapshot.Links)
        {
            if (!string.Equals(
                    link.SourceNodeKey,
                    node.Key,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    link.TargetNodeKey,
                    node.Key,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var candidate =
                LinkOperationalState(
                    link.PhysicalLinkId);

            if (candidate > state)
            {
                state =
                    candidate;
            }
        }

        return state;
    }

    private static MapNodeDegradationState
        ClassifyNodeDegradationState(
            IEnumerable<DiagnosticDegradationStatus> statuses)
    {
        if (statuses == null)
        {
            throw new ArgumentNullException(
                nameof(statuses));
        }

        var hasHealthy = false;
        var hasUnknown = false;

        foreach (var status in statuses)
        {
            switch (status)
            {
                case DiagnosticDegradationStatus.Degraded:
                    return MapNodeDegradationState.Degraded;

                case DiagnosticDegradationStatus.Healthy:
                    hasHealthy = true;
                    break;

                case DiagnosticDegradationStatus.Unknown:
                default:
                    hasUnknown = true;
                    break;
            }
        }

        return hasHealthy && !hasUnknown
            ? MapNodeDegradationState.Normal
            : MapNodeDegradationState.Unknown;
    }

    private static string NodeDegradationBrushKey(
        MapNodeDegradationState state)
    {
        switch (state)
        {
            case MapNodeDegradationState.Critical:
                return "NetLoom.Brush.Critical";

            case MapNodeDegradationState.Degraded:
                return "NetLoom.Brush.Warning";

            case MapNodeDegradationState.Normal:
                return "NetLoom.Brush.Success";

            case MapNodeDegradationState.Unknown:
            default:
                return "NetLoom.Brush.TextDisabled";
        }
    }

    private static double LinkOperationalStrokeScale(
        MapLinkOperationalState state)
    {
        switch (state)
        {
            case MapLinkOperationalState.Transition:
                return 1.10;

            case MapLinkOperationalState.Blocked:
                return 1.20;

            case MapLinkOperationalState.Degraded:
                return 1.35;

            case MapLinkOperationalState.Critical:
                return 1.55;

            case MapLinkOperationalState.Forwarding:
            case MapLinkOperationalState.Normal:
            default:
                return 1.0;
        }
    }

    private static string LinkOperationalBrushKey(
        MapLinkOperationalState state)
    {
        switch (state)
        {
            case MapLinkOperationalState.Critical:
                return "NetLoom.Brush.Critical";

            case MapLinkOperationalState.Degraded:
                return "NetLoom.Brush.Warning";

            case MapLinkOperationalState.Blocked:
                return "NetLoom.Brush.AccentPressed";

            case MapLinkOperationalState.Transition:
                return "NetLoom.Brush.AccentHover";

            case MapLinkOperationalState.Forwarding:
            case MapLinkOperationalState.Normal:
                return null;

            default:
                return null;
        }
    }

    private static FontWeight LinkOperationalLabelWeight(
        MapLinkOperationalState state)
    {
        switch (state)
        {
            case MapLinkOperationalState.Critical:
            case MapLinkOperationalState.Degraded:
                return FontWeights.Bold;

            case MapLinkOperationalState.Blocked:
            case MapLinkOperationalState.Transition:
                return FontWeights.SemiBold;

            case MapLinkOperationalState.Forwarding:
            case MapLinkOperationalState.Normal:
            default:
                return FontWeights.Normal;
        }
    }
}
