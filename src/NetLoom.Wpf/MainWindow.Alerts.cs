using System;
using System.Collections.Generic;
using System.Linq;
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

    private static string BuildAlertSummary(
        TopologyAlert alert)
    {
        var lines =
            new List<string>
            {
                UiText.Format(
                    "AlertRowHeader",
                    AlertSeverityText(
                        alert.Severity),
                    AlertKindText(
                        alert.Kind)),
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

        lines.Add(
            UiText.Format(
                "AlertReasons",
                string.Join(
                    ", ",
                    alert.Reasons
                        .Select(
                            AlertReasonText))));

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private void ShowAlerts(
        TopologyAlertSnapshot snapshot,
        TopologyAlertTransitionKind transition)
    {
        var rows =
            snapshot.Alerts
                .Select(
                    alert =>
                        new AlertRow(
                            BuildAlertSummary(
                                alert)))
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

        switch (transition)
        {
            case TopologyAlertTransitionKind
                .FirstAppearance:
                AlertTransitionText.Text =
                    UiText.Get(
                        "AlertTransitionFirstAppearance");

                AnimatePulse(
                    AlertTransitionText,
                    MapMotionKind.AlertPulse);
                break;

            case TopologyAlertTransitionKind.Changed:
                AlertTransitionText.Text =
                    UiText.Get(
                        "AlertTransitionChanged");
                break;

            case TopologyAlertTransitionKind.Resolved:
                AlertTransitionText.Text =
                    UiText.Get(
                        "AlertTransitionResolved");
                break;

            default:
                AlertTransitionText.Text =
                    string.Empty;
                break;
        }
    }

    private sealed class AlertRow
    {
        public AlertRow(
            string summary)
        {
            Summary = summary;
        }

        public string Summary { get; }
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
