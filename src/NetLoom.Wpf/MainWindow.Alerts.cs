using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NetLoom.Application.Alerts;
using NetLoom.Contracts.Alerts;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private readonly TopologyAlertTransitionTracker
        _alertTransitionTracker;

    private static string AlertSeverityText(
        TopologyAlertSeverity severity)
    {
        return severity ==
               TopologyAlertSeverity.Critical
            ? UiText.Get(
                "AlertSeverityCritical")
            : UiText.Get(
                "AlertSeverityWarning");
    }

    private static string AlertKindText(
        TopologyAlertKind kind)
    {
        switch (kind)
        {
            case TopologyAlertKind.ForwardingCycle:
                return UiText.Get(
                    "AlertKindForwardingCycle");

            case TopologyAlertKind
                .RingProtectionDegraded:
                return UiText.Get(
                    "AlertKindRingProtectionDegraded");

            default:
                return kind.ToString();
        }
    }

    private static string AlertReasonText(
        TopologyAlertReason reason)
    {
        switch (reason)
        {
            case TopologyAlertReason
                .ConfirmedForwardingCycle:
                return UiText.Get(
                    "AlertReasonConfirmedForwardingCycle");

            case TopologyAlertReason
                .DisabledRingLink:
                return UiText.Get(
                    "AlertReasonDisabledRingLink");

            case TopologyAlertReason
                .MultipleBlockingRingLinks:
                return UiText.Get(
                    "AlertReasonMultipleBlockingRingLinks");

            default:
                return reason.ToString();
        }
    }

    private string BuildAlertScope(
        TopologyAlert alert)
    {
        var names =
            new List<string>();

        if (_lastDiagnosticSnapshot != null)
        {
            var physicalLinkIds =
                new HashSet<Guid>(
                    alert.PhysicalLinkIds);

            foreach (var link in
                     _lastDiagnosticSnapshot.Links
                         .Where(
                             item =>
                                 physicalLinkIds.Contains(
                                     item.PhysicalLinkId)))
            {
                if (!string.IsNullOrWhiteSpace(
                        link.DeviceAName))
                {
                    names.Add(
                        link.DeviceAName.Trim());
                }

                if (!string.IsNullOrWhiteSpace(
                        link.DeviceBName))
                {
                    names.Add(
                        link.DeviceBName.Trim());
                }
            }
        }

        if (names.Count == 0 &&
            _lastMapSnapshot != null)
        {
            var nodesByKey =
                _lastMapSnapshot.Nodes
                    .ToDictionary(
                        item => item.Key,
                        StringComparer.Ordinal);

            var physicalLinkIds =
                new HashSet<Guid>(
                    alert.PhysicalLinkIds);

            foreach (var link in
                     _lastMapSnapshot.Links
                         .Where(
                             item =>
                                 item.PhysicalLinkId.HasValue &&
                                 physicalLinkIds.Contains(
                                     item.PhysicalLinkId.Value)))
            {
                NetLoom.Contracts.TopologyMap.MapNode node;

                if (nodesByKey.TryGetValue(
                        link.SourceNodeKey,
                        out node))
                {
                    names.Add(
                        node.Label);
                }

                if (nodesByKey.TryGetValue(
                        link.TargetNodeKey,
                        out node))
                {
                    names.Add(
                        node.Label);
                }
            }
        }

        var distinctNames =
            names
                .Where(
                    item =>
                        !string.IsNullOrWhiteSpace(
                            item))
                .Distinct(
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        if (distinctNames.Length == 0)
        {
            return UiText.Format(
                "AlertAffectedLinks",
                alert.PhysicalLinkIds.Count);
        }

        const int maxVisibleNames = 3;

        var visibleNames =
            distinctNames
                .Take(
                    maxVisibleNames)
                .ToArray();

        var result =
            string.Join(
                " ↔ ",
                visibleNames);

        if (distinctNames.Length >
            maxVisibleNames)
        {
            result +=
                UiText.Format(
                    "AlertMoreDevices",
                    distinctNames.Length -
                    maxVisibleNames);
        }

        return result;
    }

    private static string BuildAlertReasonSummary(
        TopologyAlert alert)
    {
        return string.Join(
            " · ",
            alert.Reasons
                .Select(
                    AlertReasonText));
    }

    private static string BuildAlertTechnicalDetails(
        TopologyAlert alert)
    {
        var lines =
            new List<string>
            {
                UiText.Format(
                    "AlertInstance",
                    alert.InstanceId)
            };

        if (alert.RelatedRegionKeys.Count > 0)
        {
            lines.Add(
                UiText.Format(
                    "AlertRegions",
                    string.Join(
                        ", ",
                        alert.RelatedRegionKeys)));
        }

        lines.Add(
            UiText.Format(
                "AlertLinks",
                string.Join(
                    ", ",
                    alert.PhysicalLinkIds
                        .Select(
                            id =>
                                id.ToString("D")))));

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private AlertRow BuildAlertRow(
        TopologyAlert alert,
        DateTime generatedUtc)
    {
        return new AlertRow(
            AlertSeverityText(
                alert.Severity),
            AlertKindText(
                alert.Kind),
            BuildAlertScope(
                alert),
            BuildAlertReasonSummary(
                alert),
            generatedUtc
                .ToLocalTime()
                .ToString(
                    "HH:mm",
                    CultureInfo.CurrentCulture),
            BuildAlertTechnicalDetails(
                alert),
            alert.PhysicalLinkIds[0],
            alert.Severity ==
                TopologyAlertSeverity.Critical,
            UiText.Get(
                "AlertShowOnMapAction"),
            UiText.Get(
                "AlertTechnicalDetails"));
    }

    private static string BuildShellAlertEvent(
        AlertRow row)
    {
        return UiText.Format(
            "ShellAlertEvent",
            row.TimeText,
            row.SeverityText,
            row.Title,
            row.Scope);
    }

    private void ShowAlerts(
        TopologyAlertSnapshot snapshot,
        TopologyAlertTransitionKind transition)
    {
        var rows =
            snapshot.Alerts
                .Select(
                    alert =>
                        BuildAlertRow(
                            alert,
                            snapshot.GeneratedUtc))
                .ToArray();

        AlertList.ItemsSource =
            rows;

        if (rows.Length == 0)
        {
            AlertStatusText.Text =
                UiText.Get(
                    "AlertNone");
        }
        else
        {
            var criticalCount =
                snapshot.Alerts.Count(
                    alert =>
                        alert.Severity ==
                        TopologyAlertSeverity.Critical);

            var warningCount =
                snapshot.Alerts.Count(
                    alert =>
                        alert.Severity ==
                        TopologyAlertSeverity.Warning);

            AlertStatusText.Text =
                UiText.Format(
                    "AlertSummary",
                    criticalCount,
                    warningCount);
        }

        ShellAlertCountText.Text =
            UiText.Format(
                "ShellAlertCount",
                rows.Length);
        ShellAlertCountText.SetResourceReference(
            TextBlock.ForegroundProperty,
            rows.Length == 0
                ? "NetLoom.Brush.ShellRailTextMuted"
                : "NetLoom.Brush.Critical");

        ShellAlertsBadgeText.Text =
            rows.Length.ToString(
                System.Globalization.CultureInfo.CurrentCulture);
        ShellAlertsBadge.Visibility =
            rows.Length == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

        if (rows.Length > 0)
        {
            AlertTransitionText.Text =
                BuildShellAlertEvent(
                    rows[0]);

            if (transition ==
                TopologyAlertTransitionKind.FirstAppearance)
            {
                AnimatePulse(
                    AlertTransitionText,
                    MapMotionKind.AlertPulse);
            }

            return;
        }

        switch (transition)
        {
            case TopologyAlertTransitionKind.Resolved:
                AlertTransitionText.Text =
                    UiText.Get(
                        "AlertTransitionResolved");
                break;

            default:
                AlertTransitionText.Text =
                    UiText.Get(
                        "ShellEventIdle");
                break;
        }
    }

    private void OnAlertShowOnMapClick(
        object sender,
        RoutedEventArgs e)
    {
        var button =
            sender as Button;

        if (button == null ||
            !(button.Tag is Guid) ||
            (Guid)button.Tag == Guid.Empty)
        {
            return;
        }

        StopStartupTopologyFit();

        _highlightedDeviceId =
            null;
        _selectedDeviceId =
            null;
        _selectedInterfaceId =
            null;
        _selectedPhysicalLinkId =
            (Guid)button.Tag;
        _selectedLocationId =
            null;

        ShowShellSection(
            ShellSection.Map);
        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();

        var physicalLinkId =
            (Guid)button.Tag;

        FocusSelectedMapAtNativeZoom(
            () =>
                PulseAlertContext(
                    physicalLinkId));
    }

    private void PulseAlertContext(
        Guid physicalLinkId)
    {
        var linkVisual =
            _linkVisualsByIdentity.Values
                .FirstOrDefault(
                    item =>
                        item.Line.Tag is Guid &&
                        (Guid)item.Line.Tag ==
                            physicalLinkId);

        if (linkVisual != null)
        {
            AnimatePulse(
                linkVisual.Line,
                MapMotionKind.AlertPulse,
                1.0,
                5);

            AnimatePulse(
                linkVisual.Label,
                MapMotionKind.AlertPulse,
                1.0,
                5);
        }

        if (_lastMapSnapshot == null)
        {
            return;
        }

        var link =
            _lastMapSnapshot.Links
                .FirstOrDefault(
                    item =>
                        item.PhysicalLinkId.HasValue &&
                        item.PhysicalLinkId.Value ==
                            physicalLinkId);

        if (link == null)
        {
            return;
        }

        var endpointKeys =
            new[]
            {
                link.SourceNodeKey,
                link.TargetNodeKey
            };

        foreach (var node in
                 _lastMapSnapshot.Nodes
                     .Where(
                         item =>
                             endpointKeys.Contains(
                                 item.Key)))
        {
            if (node.DeviceId.HasValue)
            {
                AnimateDiscoveryFocus(
                    node.DeviceId.Value);
            }
        }
    }

    private sealed class AlertRow
    {
        public AlertRow(
            string severityText,
            string title,
            string scope,
            string reason,
            string timeText,
            string technicalDetails,
            Guid primaryPhysicalLinkId,
            bool isCritical,
            string showOnMapText,
            string technicalDetailsLabel)
        {
            SeverityText =
                severityText;
            Title =
                title;
            Scope =
                scope;
            Reason =
                reason;
            TimeText =
                timeText;
            TechnicalDetails =
                technicalDetails;
            PrimaryPhysicalLinkId =
                primaryPhysicalLinkId;
            IsCritical =
                isCritical;
            ShowOnMapText =
                showOnMapText;
            TechnicalDetailsLabel =
                technicalDetailsLabel;
        }

        public string SeverityText { get; }

        public string Title { get; }

        public string Scope { get; }

        public string Reason { get; }

        public string TimeText { get; }

        public string TechnicalDetails { get; }

        public Guid PrimaryPhysicalLinkId { get; }

        public bool IsCritical { get; }

        public string ShowOnMapText { get; }

        public string TechnicalDetailsLabel { get; }
    }

    private sealed class EmptyTopologyAlertSnapshotProvider :
        ITopologyAlertSnapshotProvider
    {
        public TopologyAlertSnapshot GetSnapshot(
            string instanceId)
        {
            return new TopologyAlertSnapshot(
                DateTime.UtcNow,
                instanceId,
                new TopologyAlert[0]);
        }
    }
}
