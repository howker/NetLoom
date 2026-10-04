using System;
using System.Windows;
using System.Windows.Controls;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.MonitoringControl;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private enum OperatorStatusSemantic
        {
            Unknown = 0,
            Normal = 1,
            Active = 2,
            Warning = 3,
            Critical = 4,
            Stopped = 5,
            Transition = 6,
            Blocked = 7,
            Idle = 8
        }

        private static OperatorStatusSemantic MonitoringStatusSemantic(
            MonitoringControlState state)
        {
            switch (state)
            {
                case MonitoringControlState.Faulted:
                    return OperatorStatusSemantic.Critical;

                case MonitoringControlState.Starting:
                case MonitoringControlState.Running:
                case MonitoringControlState.Polling:
                case MonitoringControlState.Stopping:
                    return OperatorStatusSemantic.Active;

                case MonitoringControlState.Stopped:
                default:
                    return OperatorStatusSemantic.Stopped;
            }
        }

        private static OperatorStatusSemantic DiscoveryStatusSemantic(
            DiscoveryControlState state)
        {
            switch (state)
            {
                case DiscoveryControlState.Faulted:
                    return OperatorStatusSemantic.Critical;

                case DiscoveryControlState.Starting:
                case DiscoveryControlState.Running:
                case DiscoveryControlState.Stopping:
                    return OperatorStatusSemantic.Active;

                case DiscoveryControlState.Stopped:
                    return OperatorStatusSemantic.Stopped;

                case DiscoveryControlState.Idle:
                    return OperatorStatusSemantic.Idle;

                case DiscoveryControlState.Completed:
                default:
                    return OperatorStatusSemantic.Normal;
            }
        }

        private static OperatorStatusSemantic AlertStatusSemantic(
            TopologyAlertSeverity severity)
        {
            return severity == TopologyAlertSeverity.Critical
                ? OperatorStatusSemantic.Critical
                : OperatorStatusSemantic.Warning;
        }

        private static OperatorStatusSemantic NodeStatusSemantic(
            MapNodeDegradationState state)
        {
            switch (state)
            {
                case MapNodeDegradationState.Critical:
                    return OperatorStatusSemantic.Critical;
                case MapNodeDegradationState.Degraded:
                    return OperatorStatusSemantic.Warning;
                case MapNodeDegradationState.Normal:
                    return OperatorStatusSemantic.Normal;
                case MapNodeDegradationState.Unknown:
                default:
                    return OperatorStatusSemantic.Unknown;
            }
        }

        private static OperatorStatusSemantic LinkStatusSemantic(
            MapLinkOperationalState state)
        {
            switch (state)
            {
                case MapLinkOperationalState.Critical:
                    return OperatorStatusSemantic.Critical;
                case MapLinkOperationalState.Degraded:
                    return OperatorStatusSemantic.Warning;
                case MapLinkOperationalState.Blocked:
                    return OperatorStatusSemantic.Blocked;
                case MapLinkOperationalState.Transition:
                    return OperatorStatusSemantic.Transition;
                case MapLinkOperationalState.Forwarding:
                case MapLinkOperationalState.Normal:
                default:
                    return OperatorStatusSemantic.Normal;
            }
        }

        private static OperatorStatusSemantic InterfaceStatusSemantic(
            DiagnosticDegradationStatus state)
        {
            switch (state)
            {
                case DiagnosticDegradationStatus.Degraded:
                    return OperatorStatusSemantic.Warning;
                case DiagnosticDegradationStatus.Healthy:
                    return OperatorStatusSemantic.Normal;
                case DiagnosticDegradationStatus.Unknown:
                default:
                    return OperatorStatusSemantic.Unknown;
            }
        }

        private static string OperatorStatusBrushKey(
            OperatorStatusSemantic status)
        {
            switch (status)
            {
                case OperatorStatusSemantic.Normal:
                case OperatorStatusSemantic.Active:
                    return "NetLoom.Brush.TextSecondary";
                case OperatorStatusSemantic.Warning:
                    return "NetLoom.Brush.Warning";
                case OperatorStatusSemantic.Critical:
                    return "NetLoom.Brush.Critical";
                case OperatorStatusSemantic.Transition:
                    return "NetLoom.Brush.AccentHover";
                case OperatorStatusSemantic.Blocked:
                    return "NetLoom.Brush.AccentPressed";
                case OperatorStatusSemantic.Stopped:
                case OperatorStatusSemantic.Idle:
                case OperatorStatusSemantic.Unknown:
                default:
                    return "NetLoom.Brush.TextDisabled";
            }
        }

        private static string OperatorStatusLabelResourceKey(
            OperatorStatusSemantic status)
        {
            switch (status)
            {
                case OperatorStatusSemantic.Normal:
                    return "OperatorStatusNormal";
                case OperatorStatusSemantic.Active:
                    return "OperatorStatusActive";
                case OperatorStatusSemantic.Warning:
                    return "OperatorStatusWarning";
                case OperatorStatusSemantic.Critical:
                    return "OperatorStatusCritical";
                case OperatorStatusSemantic.Stopped:
                    return "OperatorStatusStopped";
                case OperatorStatusSemantic.Transition:
                    return "OperatorStatusTransition";
                case OperatorStatusSemantic.Blocked:
                    return "OperatorStatusBlocked";
                case OperatorStatusSemantic.Idle:
                    return "OperatorStatusIdle";
                case OperatorStatusSemantic.Unknown:
                default:
                    return "OperatorStatusUnknown";
            }
        }

        private static string OperatorStatusIconGeometryKey(
            OperatorStatusSemantic status)
        {
            switch (status)
            {
                case OperatorStatusSemantic.Normal:
                    return "NetLoom.Icon.StatusNormal";
                case OperatorStatusSemantic.Active:
                    return "NetLoom.Icon.StatusActive";
                case OperatorStatusSemantic.Warning:
                    return "NetLoom.Icon.StatusWarning";
                case OperatorStatusSemantic.Critical:
                    return "NetLoom.Icon.StatusCritical";
                case OperatorStatusSemantic.Stopped:
                    return "NetLoom.Icon.StatusStopped";
                case OperatorStatusSemantic.Transition:
                    return "NetLoom.Icon.StatusTransition";
                case OperatorStatusSemantic.Blocked:
                    return "NetLoom.Icon.StatusBlocked";
                case OperatorStatusSemantic.Unknown:
                default:
                    return "NetLoom.Icon.StatusUnknown";
            }
        }

        private static string OperatorStatusGlyphResourceKey(
            OperatorStatusSemantic status)
        {
            switch (status)
            {
                case OperatorStatusSemantic.Normal:
                    return "OperatorStatusGlyphNormal";
                case OperatorStatusSemantic.Active:
                    return "OperatorStatusGlyphActive";
                case OperatorStatusSemantic.Warning:
                    return "OperatorStatusGlyphWarning";
                case OperatorStatusSemantic.Critical:
                    return "OperatorStatusGlyphCritical";
                case OperatorStatusSemantic.Stopped:
                    return "OperatorStatusGlyphStopped";
                case OperatorStatusSemantic.Transition:
                    return "OperatorStatusGlyphTransition";
                case OperatorStatusSemantic.Blocked:
                    return "OperatorStatusGlyphBlocked";
                case OperatorStatusSemantic.Idle:
                    return "OperatorStatusGlyphIdle";
                case OperatorStatusSemantic.Unknown:
                default:
                    return "OperatorStatusGlyphUnknown";
            }
        }

        private static string OperatorStatusGlyph(
            OperatorStatusSemantic status)
        {
            return UiText.Get(
                OperatorStatusGlyphResourceKey(
                    status));
        }

        private static string OperatorStatusLabel(
            OperatorStatusSemantic status)
        {
            return UiText.Get(
                OperatorStatusLabelResourceKey(
                    status));
        }

        private static string OperatorStatusDecoratedLabel(
            OperatorStatusSemantic status)
        {
            return OperatorStatusGlyph(status) +
                   " " +
                   OperatorStatusLabel(status);
        }

        private static string OperatorStatusDecoratedDetail(
            OperatorStatusSemantic status,
            string detail)
        {
            return string.IsNullOrWhiteSpace(detail)
                ? OperatorStatusDecoratedLabel(status)
                : OperatorStatusGlyph(status) +
                  " " +
                  detail;
        }

        private static void ApplyOperatorStatus(
            TextBlock glyphText,
            TextBlock detailText,
            OperatorStatusSemantic status,
            string detail)
        {
            if (glyphText == null)
            {
                throw new ArgumentNullException(
                    nameof(glyphText));
            }

            if (detailText == null)
            {
                throw new ArgumentNullException(
                    nameof(detailText));
            }

            var brushKey =
                OperatorStatusBrushKey(status);
            var label =
                OperatorStatusLabel(status);

            glyphText.Text =
                OperatorStatusGlyph(status);
            glyphText.ToolTip =
                label;
            glyphText.SetResourceReference(
                TextBlock.ForegroundProperty,
                brushKey);

            detailText.Text =
                detail ?? string.Empty;
            detailText.ToolTip =
                label;
            detailText.SetResourceReference(
                TextBlock.ForegroundProperty,
                brushKey);
        }

        private void SetInspectorOperatorStatus(
            OperatorStatusSemantic status)
        {
            InspectorOperationalStatusText.Text =
                OperatorStatusDecoratedLabel(status);

            InspectorOperationalStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                OperatorStatusBrushKey(status));

            InspectorOperationalStatusText.Visibility =
                Visibility.Visible;
        }

        private void ClearInspectorOperatorStatus()
        {
            InspectorOperationalStatusText.Text =
                string.Empty;
            InspectorOperationalStatusText.Visibility =
                Visibility.Collapsed;
        }
    }
}
