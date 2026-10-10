using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private Guid? _failurePredictionDeviceId;
    private Guid? _failurePredictionLinkId;
    private FailurePredictionResult _failurePredictionResult;
    private bool IsFailurePredictionLayoutActive =>
        _operationalFocusMode == MapOperationalFocusMode.FailurePrediction &&
        _shellSection == ShellSection.Map && !IsMapEditMode && _failurePredictionResult != null;

    private void ApplyFailurePredictionLayout()
    {
        RestoreNeighborhoodLayout();
        if (!IsFailurePredictionLayoutActive || _lastMapSnapshot == null) return;
        var selected = _failurePredictionDeviceId;
        if (!selected.HasValue && _failurePredictionLinkId.HasValue)
        {
            var link = _lastDiagnosticSnapshot.Links.FirstOrDefault(item =>
                item.PhysicalLinkId == _failurePredictionLinkId.Value);
            if (link != null)
            {
                var distances = PollingDistances();
                int a;
                int b;
                selected = distances != null && distances.TryGetValue(link.DeviceAId, out a) &&
                    distances.TryGetValue(link.DeviceBId, out b) && b < a
                    ? link.DeviceBId : link.DeviceAId;
            }
        }
        if (selected.HasValue)
        {
            // Ряды прогноза — как группы инспектора: цель, «будут отрезаны», «только через резерв STP»,
            // «Обход не подтверждён». Так пострадавшие помещаются в читаемый масштаб чаще, чем рядами по расстоянию.
            var rows = new Dictionary<Guid, int>();
            foreach (var id in _operationalFocusDeviceIds)
            {
                FailureImpactCategory category;
                rows[id] = !_failurePredictionResult.AffectedDevices.TryGetValue(id, out category) ? 0
                    : category == FailureImpactCategory.CutOff ? 1
                    : category == FailureImpactCategory.StandbyOnly ? 2 : 3;
            }
            ApplyCompactDeviceLayout(_operationalFocusDeviceIds, selected.Value, rows);
        }
        UpdateLocationHierarchyVisibility();
        ApplyNeighborhoodVisibility();
        ReconcileLinks(_lastMapSnapshot.Links,
            _lastMapSnapshot.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal));
    }

    private Guid? FailurePollingPointId => _pollingPoint != null &&
        _pollingPoint.Status == EnginePollingPointStatus.Determined &&
        _pollingPoint.DeviceId.HasValue && _lastDiagnosticSnapshot != null &&
        _lastDiagnosticSnapshot.Links.Any(link =>
            link.DeviceAId == _pollingPoint.DeviceId.Value ||
            link.DeviceBId == _pollingPoint.DeviceId.Value)
            ? _pollingPoint.DeviceId : null;

    private FailurePredictionResult PredictDevice(Guid deviceId) =>
        FailurePrediction.PredictDeviceFailure(_lastDiagnosticSnapshot.Links,
            FailurePollingPointId, deviceId);

    private FailurePredictionResult PredictLink(Guid linkId) =>
        FailurePrediction.PredictLinkFailure(_lastDiagnosticSnapshot.Links,
            FailurePollingPointId, linkId);

    // Одна строка итога в той же формулировке, что полоса прогноза (замечание владельца перед слиянием Sprint 50).
    private IEnumerable<string> FailureCategoryLines(FailurePredictionResult result)
    {
        var parts = FailureSummaryParts(result);
        if (parts.Count > 0) yield return string.Join("; ", parts) + ".";
    }

    private static List<string> FailureSummaryParts(FailurePredictionResult result)
    {
        var parts = new List<string>();
        if (result.CutOffCount > 0)
            parts.Add(UiText.FormatCount("ImpactStripCutOff", result.CutOffCount));
        if (result.StandbyOnlyCount > 0)
            parts.Add(UiText.FormatCount("ImpactStripStandby", result.StandbyOnlyCount));
        if (result.UnconfirmedCount > 0)
            parts.Add(UiText.FormatCount("ImpactStripUnconfirmed", result.UnconfirmedCount));
        return parts;
    }

    private DiagnosticTextRow[] DeviceFailurePredictionRows(DeviceDiagnostic device)
    {
        var result = PredictDevice(device.DeviceId);
        var rows = FailurePredictionLines(result).ToList();
        var spof = DeviceSinglePointText(device, result);
        // Без направления строка единой точки отказа уже называет причину: она заменяет строку причины, а не повторяет её.
        if (spof != null && !result.IsDirectional && rows.Count > 0) rows[0] = spof;
        else if (spof != null) rows.Insert(0, spof);
        if (!result.IsDirectional)
        {
            rows.Add(device.IsArticulationPoint
                ? UiText.Format("ImpactDeviceParts", device.FailurePartDeviceCounts.Count,
                    string.Join(", ", device.FailurePartDeviceCounts.Select(
                        count => UiText.FormatCount("DiagnosticDeviceCount", count))))
                : UiText.Get("ImpactDeviceNoParts"));
        }
        rows.Add(UiText.Get("ImpactPredictionNote"));
        return rows.Select((text, index) => new DiagnosticTextRow(text, index == 0 && spof != null)).ToArray();
    }

    private DiagnosticTextRow[] LinkFailurePredictionRows(PhysicalLinkDiagnostic link)
    {
        var result = PredictLink(link.PhysicalLinkId);
        var rows = FailurePredictionLines(result).ToList();
        var spof = LinkSinglePointText(link, result);
        // Без направления строка единой точки отказа уже называет причину: она заменяет строку причины, а не повторяет её.
        if (spof != null && !result.IsDirectional && rows.Count > 0) rows[0] = spof;
        else if (spof != null) rows.Insert(0, spof);
        if (!result.IsDirectional)
        {
            if (link.IsBridge)
            {
                rows.Add(UiText.Get("DiagnosticImpactSinglePath"));
                rows.Add(UiText.Format("DiagnosticImpactSideA", DisplayDeviceName(link.DeviceAName),
                    UiText.FormatCount("DiagnosticDeviceCount", link.SideADeviceCount)));
                rows.Add(UiText.Format("DiagnosticImpactSideB", DisplayDeviceName(link.DeviceBName),
                    UiText.FormatCount("DiagnosticDeviceCount", link.SideBDeviceCount)));
                rows.Add(UiText.Format("DiagnosticImpactPairs", link.SeparatedDevicePairCount));
            }
            else rows.Add(UiText.Get("DiagnosticImpactAlternativePath"));
        }
        rows.Add(UiText.Get("ImpactPredictionNote"));
        return rows.Select((text, index) => new DiagnosticTextRow(text, index == 0 && spof != null)).ToArray();
    }

    private IEnumerable<string> FailurePredictionLines(FailurePredictionResult result)
    {
        if (!result.IsDirectional)
        {
            yield return result.Reason == FailurePredictionReason.TargetIsPollingPoint
                ? UiText.Get("ImpactTargetIsPollingPoint")
                : result.Reason == FailurePredictionReason.TargetNotConnectedToPollingPoint
                    ? UiText.Get("ImpactNotConnected")
                    : UiText.Format("ImpactNoDirection", UiText.Get(PollingReasonKey() ?? "MapNeighborhoodPollingNotFound"));
            yield break;
        }
        if (result.AffectedDevices.Count == 0)
        {
            yield return UiText.Get("ImpactNoneAffected");
            yield break;
        }
        foreach (var line in FailureCategoryLines(result)) yield return line;
    }

    private void UpdateFailurePredictionInspectorAction(Guid? deviceId, Guid? linkId)
    {
        var result = deviceId.HasValue ? PredictDevice(deviceId.Value) : PredictLink(linkId.Value);
        InspectorFailureImpactGroups.ItemsSource = FailureImpactGroups(result);
        var show = result.IsDirectional && result.AffectedDevices.Count > 0;
        InspectorFailurePredictionShowButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        var label = UiText.Get("ImpactShowOnMap");
        InspectorFailurePredictionShowButton.Content = label;
        AutomationProperties.SetName(InspectorFailurePredictionShowButton, label);
        InspectorFailurePredictionShowButton.Tag = show
            ? (object)(deviceId ?? linkId.Value) : null;
    }

    private IReadOnlyList<FailureImpactGroupRow> FailureImpactGroups(FailurePredictionResult result)
    {
        var groups = new List<FailureImpactGroupRow>();
        AddFailureImpactGroup(groups, result, FailureImpactCategory.CutOff, "ImpactGroupCutOff");
        AddFailureImpactGroup(groups, result, FailureImpactCategory.StandbyOnly, "ImpactGroupStandby");
        AddFailureImpactGroup(groups, result, FailureImpactCategory.Unconfirmed, "ImpactGroupUnconfirmed");
        return groups;
    }

    private void AddFailureImpactGroup(List<FailureImpactGroupRow> groups,
        FailurePredictionResult result, FailureImpactCategory category, string titleKey)
    {
        var devices = result.AffectedDevices.Where(pair => pair.Value == category)
            .Select(pair => new FailureImpactDeviceRow(pair.Key,
                DisplayDeviceName(_lastDiagnosticSnapshot.Devices.FirstOrDefault(
                    device => device.DeviceId == pair.Key)?.DisplayName)))
            .OrderBy(item => item.Name, StringComparer.CurrentCulture)
            .ThenBy(item => item.Id).ToArray();
        if (devices.Length > 0)
            groups.Add(new FailureImpactGroupRow(UiText.Format(titleKey, devices.Length), devices));
    }

    private void OnFailureImpactDeviceClick(object sender, RoutedEventArgs e)
    {
        if (!(sender is Button button) || !(button.Tag is Guid id)) return;
        SelectAlertDeviceContext(id);
        e.Handled = true;
    }

    private sealed class FailureImpactGroupRow
    {
        public FailureImpactGroupRow(string title, IReadOnlyList<FailureImpactDeviceRow> devices)
        { Title = title; Devices = devices; }
        public string Title { get; }
        public IReadOnlyList<FailureImpactDeviceRow> Devices { get; }
    }

    private sealed class FailureImpactDeviceRow
    {
        public FailureImpactDeviceRow(Guid id, string name) { Id = id; Name = name; }
        public Guid Id { get; }
        public string Name { get; }
    }

    private void OnFailurePredictionShowClick(object sender, RoutedEventArgs e)
    {
        var deviceId = _selectedDeviceId;
        var linkId = deviceId.HasValue ? null : _selectedPhysicalLinkId;
        if (!deviceId.HasValue && !linkId.HasValue) return;
        var result = deviceId.HasValue ? PredictDevice(deviceId.Value) : PredictLink(linkId.Value);
        if (!result.IsDirectional || result.AffectedDevices.Count == 0) return;
        if (_shellSection != ShellSection.Map && _shellSection != ShellSection.Alerts)
            ShowShellSection(ShellSection.Map);
        StopStartupTopologyFit();
        RecordMapView(true);
        ClearMapPathState(false);
        _failurePredictionDeviceId = deviceId;
        _failurePredictionLinkId = linkId;
        SetOperationalFocusMode(MapOperationalFocusMode.FailurePrediction);
    }

    private void ClearFailurePredictionTarget()
    {
        _failurePredictionDeviceId = null;
        _failurePredictionLinkId = null;
        _failurePredictionResult = null;
        UpdateFailurePredictionNotice();
    }

    private void RefreshFailurePredictionFocusTargets()
    {
        if (_lastDiagnosticSnapshot == null || _lastMapSnapshot == null) return;
        var device = _failurePredictionDeviceId.HasValue &&
            _lastDiagnosticSnapshot.Devices.Any(item => item.DeviceId == _failurePredictionDeviceId.Value) &&
            MapDeviceExists(_failurePredictionDeviceId);
        var link = _failurePredictionLinkId.HasValue &&
            _lastDiagnosticSnapshot.Links.Any(item => item.PhysicalLinkId == _failurePredictionLinkId.Value) &&
            _lastMapSnapshot.Links.Any(item => item.PhysicalLinkId == _failurePredictionLinkId.Value);
        if (!device && !link)
        {
            RestoreNeighborhoodLayout();
            _operationalFocusMode = MapOperationalFocusMode.None;
            ClearFailurePredictionTarget();
            UpdateOperationalFocusMenuState();
            return;
        }
        _failurePredictionResult = device
            ? PredictDevice(_failurePredictionDeviceId.Value)
            : PredictLink(_failurePredictionLinkId.Value);
        if (!_failurePredictionResult.IsDirectional)
        {
            RestoreNeighborhoodLayout();
            _operationalFocusMode = MapOperationalFocusMode.None;
            ClearFailurePredictionTarget();
            UpdateOperationalFocusMenuState();
            return;
        }
        if (device) _operationalFocusDeviceIds.Add(_failurePredictionDeviceId.Value);
        if (link)
        {
            _operationalFocusPhysicalLinkIds.Add(_failurePredictionLinkId.Value);
            var failedLink = _lastDiagnosticSnapshot.Links.First(item =>
                item.PhysicalLinkId == _failurePredictionLinkId.Value);
            _operationalFocusDeviceIds.Add(failedLink.DeviceAId);
            _operationalFocusDeviceIds.Add(failedLink.DeviceBId);
        }
        foreach (var affected in _failurePredictionResult.AffectedDevices.Keys)
            _operationalFocusDeviceIds.Add(affected);
        var nodes = _lastMapSnapshot.Nodes.ToDictionary(item => item.Key, StringComparer.Ordinal);
        foreach (var mapLink in _lastMapSnapshot.Links)
        {
            MapNode a;
            MapNode b;
            if (!mapLink.PhysicalLinkId.HasValue ||
                !nodes.TryGetValue(mapLink.SourceNodeKey, out a) ||
                !nodes.TryGetValue(mapLink.TargetNodeKey, out b)) continue;
            if (a.DeviceId.HasValue && b.DeviceId.HasValue &&
                _operationalFocusDeviceIds.Contains(a.DeviceId.Value) &&
                _operationalFocusDeviceIds.Contains(b.DeviceId.Value))
                _operationalFocusPhysicalLinkIds.Add(mapLink.PhysicalLinkId.Value);
        }
        UpdateFailurePredictionNotice();
    }

    private void RefreshFailurePredictionAfterPollingPoint()
    {
        if (_operationalFocusMode == MapOperationalFocusMode.FailurePrediction ||
            _operationalFocusMode == MapOperationalFocusMode.SinglePointsOfFailure)
        {
            RefreshOperationalFocusTargets();
            ReapplyOperationalFocusPresentation();
            UpdateNeighborhoodMenuState();
            if (IsFailurePredictionLayoutActive)
            {
                ApplyFailurePredictionLayout();
                FitOperationalFocusToViewport();
            }
        }
        if (_selectedDeviceId.HasValue || _selectedPhysicalLinkId.HasValue)
            ShowSelectedDiagnostic();
    }

    private void ApplyFailurePredictionNodePresentation(MapNodeVisual visual, Guid? deviceId)
    {
        var icon = visual.FailureImpactIcon;
        if (icon == null) return;
        FailureImpactCategory category = FailureImpactCategory.Unconfirmed;
        var affected = _operationalFocusMode == MapOperationalFocusMode.FailurePrediction &&
            deviceId.HasValue && _failurePredictionResult != null &&
            _failurePredictionResult.AffectedDevices.TryGetValue(deviceId.Value, out category);
        icon.Visibility = affected ? Visibility.Visible : Visibility.Collapsed;
        if (!affected)
        {
            if (deviceId.HasValue && deviceId != _selectedDeviceId && deviceId != _highlightedDeviceId &&
                !_pathDeviceIds.Contains(deviceId.Value))
            {
                visual.Border.ClearValue(Border.BorderThicknessProperty);
                visual.Border.ClearValue(Border.BorderBrushProperty);
            }
            return;
        }
        icon.Text = category == FailureImpactCategory.CutOff ? "⊘" :
            category == FailureImpactCategory.StandbyOnly ? "↺" : "?";
        var brush = category == FailureImpactCategory.CutOff
            ? "NetLoom.Brush.Warning" : "NetLoom.Brush.TextSecondary";
        icon.SetResourceReference(TextBlock.ForegroundProperty, brush);
        visual.Border.BorderThickness = GetThicknessResource("NetLoom.Thickness.BorderFocus");
        visual.Border.SetResourceReference(Border.BorderBrushProperty, brush);
    }

    private void UpdateFailurePredictionNotice()
    {
        if (MapFailurePredictionNotice == null) return;
        var active = _operationalFocusMode == MapOperationalFocusMode.FailurePrediction &&
            _failurePredictionResult != null && _shellSection == ShellSection.Map;
        string summary = null;
        if (active)
        {
            var parts = FailureSummaryParts(_failurePredictionResult);
            var categories = parts.Count == 0 ? UiText.Get("ImpactNoneAffected").TrimEnd('.')
                : string.Join("; ", parts);
            if (_failurePredictionDeviceId.HasValue)
                summary = UiText.Format("ImpactStripForDevice",
                    MapDeviceLabel(_failurePredictionDeviceId.Value), categories);
            else if (_failurePredictionLinkId.HasValue)
            {
                var link = _lastDiagnosticSnapshot.Links.FirstOrDefault(item =>
                    item.PhysicalLinkId == _failurePredictionLinkId.Value);
                if (link != null) summary = UiText.Format("ImpactStripForLink",
                    DisplayDeviceName(link.DeviceAName), DisplayDeviceName(link.DeviceBName), categories);
            }
        }
        MapFailurePredictionSummaryText.Text = summary ?? string.Empty;
        MapFailurePredictionNotice.Visibility = summary == null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(MapFailurePredictionNotice,
            UiText.Get("MapOperationalFocusFailurePrediction"));
        var reset = UiText.Get("ImpactStripReset");
        MapFailurePredictionResetButton.Content = reset;
        AutomationProperties.SetName(MapFailurePredictionResetButton, reset);
    }

    private void OnFailurePredictionResetClick(object sender, RoutedEventArgs e)
    {
        RecordMapView(false);
        SetOperationalFocusMode(MapOperationalFocusMode.None);
        MapFitAllButton.Focus();
    }
}
