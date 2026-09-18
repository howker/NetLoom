using System;
using System.Collections.Generic;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private enum MapLinkOperationalState
    {
        Normal = 0,
        Transition = 1,
        Blocked = 2,
        Degraded = 3,
        Critical = 4
    }

    private readonly Dictionary<Guid, MapLinkOperationalState>
        _linkOperationalStates =
            new Dictionary<Guid, MapLinkOperationalState>();

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

            visual.Label.ClearValue(
                System.Windows.Controls.TextBlock.ForegroundProperty);
        }
        else
        {
            visual.Line.SetResourceReference(
                System.Windows.Shapes.Shape.StrokeProperty,
                brushKey);

            visual.Label.SetResourceReference(
                System.Windows.Controls.TextBlock.ForegroundProperty,
                brushKey);
        }

        visual.Line.ClearValue(
            System.Windows.Shapes.Shape.StrokeThicknessProperty);
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

    private enum MapNodeDegradationState
    {
        Normal = 0,
        Degraded = 1
    }

    private void ApplyNodeDegradationPresentation(
        MapNodeVisual visual,
        Guid? deviceId)
    {
        var state =
            NodeDegradationState(
                deviceId);

        var brushKey =
            NodeDegradationBrushKey(
                state);

        if (brushKey == null)
        {
            visual.Border.ClearValue(
                System.Windows.Controls.Border.BorderThicknessProperty);

            visual.Border.ClearValue(
                System.Windows.Controls.Border.BorderBrushProperty);

            return;
        }

        visual.Border.BorderThickness =
            GetThicknessResource(
                "NetLoom.Thickness.BorderFocus");

        visual.Border.SetResourceReference(
            System.Windows.Controls.Border.BorderBrushProperty,
            brushKey);
    }

    private MapNodeDegradationState
        NodeDegradationState(
            Guid? deviceId)
    {
        if (!deviceId.HasValue ||
            _lastDiagnosticSnapshot == null)
        {
            return MapNodeDegradationState.Normal;
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

        return MapNodeDegradationState.Normal;
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

        foreach (var status in statuses)
        {
            if (status ==
                DiagnosticDegradationStatus.Degraded)
            {
                return MapNodeDegradationState.Degraded;
            }
        }

        return MapNodeDegradationState.Normal;
    }

    private static string NodeDegradationBrushKey(
        MapNodeDegradationState state)
    {
        switch (state)
        {
            case MapNodeDegradationState.Degraded:
                return "NetLoom.Brush.Warning";

            case MapNodeDegradationState.Normal:
            default:
                return null;
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
            case MapLinkOperationalState.Transition:
                return "NetLoom.Brush.Warning";

            case MapLinkOperationalState.Blocked:
                return "NetLoom.Brush.Accent";

            case MapLinkOperationalState.Normal:
            default:
                return null;
        }
    }
}
