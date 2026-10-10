using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private string DeviceSinglePointText(DeviceDiagnostic device, FailurePredictionResult result)
    {
        if (result.IsDirectional && result.IsSinglePointOfFailure)
            return UiText.Format("SpofDependents",
                UiText.FormatCount("DiagnosticDeviceCount", result.CutOffCount));
        if (result.Reason != FailurePredictionReason.PollingPointUnknown ||
            !device.IsArticulationPoint) return null;
        var parts = string.Join(", ", device.FailurePartDeviceCounts.Select(
            count => UiText.FormatCount("DiagnosticDeviceCount", count)));
        return UiText.Format("SpofStructural", parts,
            UiText.Get(PollingReasonKey() ?? "MapNeighborhoodPollingNotFound"));
    }

    private string LinkSinglePointText(PhysicalLinkDiagnostic link, FailurePredictionResult result)
    {
        if (result.IsDirectional && result.IsSinglePointOfFailure)
            return UiText.Format("SpofDependents",
                UiText.FormatCount("DiagnosticDeviceCount", result.CutOffCount));
        if (result.Reason != FailurePredictionReason.PollingPointUnknown || !link.IsBridge)
            return null;
        var parts = string.Join(", ", new[] { link.SideADeviceCount, link.SideBDeviceCount }
            .Select(count => UiText.FormatCount("DiagnosticDeviceCount", count)));
        return UiText.Format("SpofStructural", parts,
            UiText.Get(PollingReasonKey() ?? "MapNeighborhoodPollingNotFound"));
    }

    private void RefreshSinglePointsOfFailureTargets()
    {
        if (_lastDiagnosticSnapshot == null || _lastMapSnapshot == null) return;
        var pollingPoint = FailurePollingPointId;
        var devices = pollingPoint.HasValue
            ? FailurePrediction.SinglePointsOfFailureDevices(_lastDiagnosticSnapshot.Links, pollingPoint)
            : _lastDiagnosticSnapshot.Devices.Where(item => item.IsArticulationPoint)
                .Select(item => item.DeviceId).ToArray();
        var links = pollingPoint.HasValue
            ? FailurePrediction.SinglePointsOfFailureLinks(_lastDiagnosticSnapshot.Links, pollingPoint)
            : _lastDiagnosticSnapshot.Links.Where(item => item.IsBridge)
                .Select(item => item.PhysicalLinkId).ToArray();
        var mapDevices = new HashSet<Guid>(_lastMapSnapshot.Nodes
            .Where(item => item.DeviceId.HasValue).Select(item => item.DeviceId.Value));
        var mapLinks = new HashSet<Guid>(_lastMapSnapshot.Links
            .Where(item => item.PhysicalLinkId.HasValue).Select(item => item.PhysicalLinkId.Value));
        foreach (var id in devices.Where(mapDevices.Contains)) _operationalFocusDeviceIds.Add(id);
        foreach (var id in links.Where(mapLinks.Contains)) _operationalFocusPhysicalLinkIds.Add(id);
        UpdateSinglePointsOfFailureNotice();
    }

    private void ApplySinglePointNodePresentation(MapNodeVisual visual, Guid? deviceId)
    {
        if (_operationalFocusMode != MapOperationalFocusMode.SinglePointsOfFailure ||
            !deviceId.HasValue || !_operationalFocusDeviceIds.Contains(deviceId.Value)) return;
        var icon = visual.FailureImpactIcon;
        if (icon == null) return;
        icon.Text = "⊘";
        icon.SetResourceReference(TextBlock.ForegroundProperty, "NetLoom.Brush.Warning");
        icon.Visibility = Visibility.Visible;
        visual.StatusIcon.Visibility = Visibility.Collapsed;
    }

    private void ApplySinglePointLinkPresentation(MapLinkVisual visual, Guid? physicalLinkId)
    {
        if (_operationalFocusMode != MapOperationalFocusMode.SinglePointsOfFailure ||
            !physicalLinkId.HasValue ||
            !_operationalFocusPhysicalLinkIds.Contains(physicalLinkId.Value)) return;
        visual.Line.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,
            "NetLoom.Brush.Warning");
        visual.Line.StrokeThickness = LinkSelectedStrokeThickness(LinkOperationalState(physicalLinkId));
    }

    private void UpdateSinglePointsOfFailureNotice()
    {
        if (MapSinglePointsNotice == null) return;
        var active = _operationalFocusMode == MapOperationalFocusMode.SinglePointsOfFailure &&
            _shellSection == ShellSection.Map;
        MapSinglePointsNotice.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (!active) return;
        var devices = UiText.FormatCount("DiagnosticDeviceCount", _operationalFocusDeviceIds.Count);
        var links = UiText.FormatCount("MapLinkCount", _operationalFocusPhysicalLinkIds.Count);
        var summary = FailurePollingPointId.HasValue
            ? _operationalFocusDeviceIds.Count + _operationalFocusPhysicalLinkIds.Count == 0
                ? UiText.Get("SpofStripNone")
                : UiText.Format("SpofStrip", devices, links)
            : UiText.Format("SpofStripStructural", devices, links,
                UiText.Get(PollingReasonKey() ?? "MapNeighborhoodPollingNotFound"));
        MapSinglePointsSummaryText.Text = summary;
        AutomationProperties.SetName(MapSinglePointsNotice,
            UiText.Get("MapOperationalFocusSinglePoints"));
        var reset = UiText.Get("SpofStripReset");
        MapSinglePointsResetButton.Content = reset;
        AutomationProperties.SetName(MapSinglePointsResetButton, reset);
    }

    private void OnSinglePointsResetClick(object sender, RoutedEventArgs e)
    {
        RecordMapView(false);
        SetOperationalFocusMode(MapOperationalFocusMode.None);
        MapFitAllButton.Focus();
    }
}
