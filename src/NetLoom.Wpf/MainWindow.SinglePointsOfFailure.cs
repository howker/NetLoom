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
        if (!active)
        {
            MapSinglePointsToggle.IsChecked = false;
            return;
        }
        var devices = UiText.FormatCount("DiagnosticDeviceCount", _operationalFocusDeviceIds.Count);
        var links = UiText.FormatCount("MapLinkCount", _operationalFocusPhysicalLinkIds.Count);
        var summary = FailurePollingPointId.HasValue
            ? _operationalFocusDeviceIds.Count + _operationalFocusPhysicalLinkIds.Count == 0
                ? UiText.Get("SpofStripNone")
                : UiText.Format("SpofStrip", devices, links)
            : UiText.Format("SpofStripStructural", devices, links,
                UiText.Get(PollingReasonKey() ?? "MapNeighborhoodPollingNotFound"));
        MapSinglePointsSummaryText.Text = summary;
        AutomationProperties.SetName(MapSinglePointsToggle, summary);
        var rows = SinglePointRows();
        var previous = MapSinglePointsItems.ItemsSource as IReadOnlyList<SinglePointRow>;
        if (previous == null || previous.Count != rows.Count ||
            !previous.Zip(rows, (oldItem, newItem) =>
                oldItem.DeviceId == newItem.DeviceId && oldItem.LinkId == newItem.LinkId &&
                oldItem.Count == newItem.Count && oldItem.Name == newItem.Name).All(equal => equal))
            MapSinglePointsItems.ItemsSource = rows;
        AutomationProperties.SetName(MapSinglePointsNotice,
            UiText.Get("MapOperationalFocusSinglePoints"));
        var reset = UiText.Get("SpofStripReset");
        MapSinglePointsResetButton.Content = reset;
        AutomationProperties.SetName(MapSinglePointsResetButton, reset);
    }

    private IReadOnlyList<SinglePointRow> SinglePointRows()
    {
        if (_lastDiagnosticSnapshot == null) return new SinglePointRow[0];
        var rows = new List<SinglePointRow>();
        foreach (var id in _operationalFocusDeviceIds)
        {
            var device = _lastDiagnosticSnapshot.Devices.FirstOrDefault(item => item.DeviceId == id);
            if (device == null) continue;
            var count = FailurePollingPointId.HasValue ? PredictDevice(id).CutOffCount :
                device.FailurePartDeviceCounts.DefaultIfEmpty(0).Max();
            rows.Add(new SinglePointRow(id, null, DisplayDeviceName(device.DisplayName), count));
        }
        foreach (var id in _operationalFocusPhysicalLinkIds)
        {
            var link = _lastDiagnosticSnapshot.Links.FirstOrDefault(item => item.PhysicalLinkId == id);
            if (link == null) continue;
            var count = FailurePollingPointId.HasValue ? PredictLink(id).CutOffCount :
                Math.Min(link.SideADeviceCount, link.SideBDeviceCount);
            rows.Add(new SinglePointRow(null, id,
                DisplayDeviceName(link.DeviceAName) + " ↔ " + DisplayDeviceName(link.DeviceBName), count));
        }
        return rows.OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture).ToArray();
    }

    private void OnSinglePointShowClick(object sender, RoutedEventArgs e)
    {
        var row = (sender as Button)?.Tag as SinglePointRow;
        if (row == null) return;
        if (row.DeviceId.HasValue) SelectAlertDeviceContext(row.DeviceId.Value);
        else if (row.LinkId.HasValue) SelectAlertPhysicalContext(row.LinkId.Value);
        if (FailurePollingPointId.HasValue) OnFailurePredictionShowClick(sender, e);
        else if (row.DeviceId.HasValue)
            FocusSelectedMapAtNativeZoom(() => AnimateDiscoveryFocus(row.DeviceId.Value));
        else if (row.LinkId.HasValue)
            FocusAlertContextToViewport(new[] { row.LinkId.Value }, null);
        MapSinglePointsToggle.IsChecked = false;
        e.Handled = true;
    }

    private sealed class SinglePointRow
    {
        public SinglePointRow(Guid? deviceId, Guid? linkId, string name, int count)
        {
            DeviceId = deviceId; LinkId = linkId; Name = name; Count = count;
            ShowText = UiText.Get("SpofShow");
            ShowName = ShowText + " " + name;
            Dependents = UiText.Format("SpofDependentCount",
                UiText.FormatCount("DiagnosticDeviceCount", count));
        }
        public Guid? DeviceId { get; }
        public Guid? LinkId { get; }
        public string Name { get; }
        public int Count { get; }
        public string ShowText { get; }
        public string ShowName { get; }
        public string Dependents { get; }
    }

    private void OnSinglePointsResetClick(object sender, RoutedEventArgs e)
    {
        RecordMapView(false);
        SetOperationalFocusMode(MapOperationalFocusMode.None);
        MapFitAllButton.Focus();
    }
}
