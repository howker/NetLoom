using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Domain.Access;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private readonly IMonitoringControl _monitoringControl;

        private Guid? _monitoringInputDeviceId;
        private MonitoringSessionPolicy _monitoringActivePolicy;
        private bool _monitoringStandalonePollActive;
        private bool _monitoringClosed;

        private void InitializeMonitoringPanel()
        {
            MonitoringTitleText.Text =
                UiText.Get("MonitoringTitle");
            MonitoringStateLabelText.Text =
                UiText.Get("MonitoringStateLabel");
            MonitoringActiveTargetLabelText.Text =
                UiText.Get("MonitoringActiveTargetLabel");
            MonitoringLastPollLabelText.Text =
                UiText.Get("MonitoringLastPollLabel");
            MonitoringLastRefreshLabelText.Text =
                UiText.Get("MonitoringLastRefreshLabel");
            MonitoringSelectedDeviceLabelText.Text =
                UiText.Get("MonitoringSelectedDeviceLabel");
            MonitoringTargetAddressLabelText.Text =
                UiText.Get("MonitoringTargetAddressLabel");
            MonitoringIntervalLabelText.Text =
                UiText.Get("MonitoringIntervalLabel");
            MonitoringVersionLabelText.Text =
                UiText.Get("MonitoringVersionLabel");
            MonitoringPortLabelText.Text =
                UiText.Get("MonitoringPortLabel");
            MonitoringTimeoutLabelText.Text =
                UiText.Get("MonitoringTimeoutLabel");
            MonitoringRetriesLabelText.Text =
                UiText.Get("MonitoringRetriesLabel");
            MonitoringMaxRepetitionsLabelText.Text =
                UiText.Get("MonitoringMaxRepetitionsLabel");
            MonitoringErrorThresholdLabelText.Text =
                UiText.Get("MonitoringErrorThresholdLabel");
            MonitoringDiscardThresholdLabelText.Text =
                UiText.Get("MonitoringDiscardThresholdLabel");
            MonitoringKindsLabelText.Text =
                UiText.Get("MonitoringKindsLabel");

            MonitoringStartButton.Content =
                UiText.Get("MonitoringStartAction");
            MonitoringStopButton.Content =
                UiText.Get("MonitoringStopAction");
            MonitoringPollNowButton.Content =
                UiText.Get("MonitoringPollNowAction");
            MonitoringRefreshTopologyButton.Content =
                UiText.Get("MonitoringRefreshTopologyAction");

            MonitoringKindLldpCheckBox.Content =
                UiText.Get("MonitoringKindLldp");
            MonitoringKindCdpCheckBox.Content =
                UiText.Get("MonitoringKindCdp");
            MonitoringKindFdbCheckBox.Content =
                UiText.Get("MonitoringKindFdb");
            MonitoringKindArpCheckBox.Content =
                UiText.Get("MonitoringKindArp");
            MonitoringKindHealthCheckBox.Content =
                UiText.Get("MonitoringKindHealth");
            MonitoringKindInterfaceCheckBox.Content =
                UiText.Get("MonitoringKindInterface");
            MonitoringKindStpCheckBox.Content =
                UiText.Get("MonitoringKindStp");

            MonitoringHintText.Text =
                UiText.Get("MonitoringSessionOnlyHint");
            MonitoringMessageText.Text =
                string.Empty;

            MonitoringIntervalTextBox.Text = "60";
            MonitoringVersionComboBox.ItemsSource =
                Enum.GetValues(
                    typeof(SnmpVersion));
            MonitoringVersionComboBox.SelectedItem =
                SnmpVersion.V2C;
            MonitoringPortTextBox.Text = "161";
            MonitoringTimeoutTextBox.Text = "2000";
            MonitoringRetriesTextBox.Text = "1";
            MonitoringMaxRepetitionsTextBox.Text = "25";
            MonitoringErrorThresholdTextBox.Text =
                string.Empty;
            MonitoringDiscardThresholdTextBox.Text =
                string.Empty;

            MonitoringKindLldpCheckBox.IsChecked = true;
            MonitoringKindCdpCheckBox.IsChecked = true;
            MonitoringKindFdbCheckBox.IsChecked = true;
            MonitoringKindArpCheckBox.IsChecked = true;
            MonitoringKindHealthCheckBox.IsChecked = true;
            MonitoringKindInterfaceCheckBox.IsChecked = true;
            MonitoringKindStpCheckBox.IsChecked = true;

            _monitoringControl.SnapshotChanged +=
                OnMonitoringSnapshotChanged;

            SynchronizeMonitoringSelection(
                null);
            UpdateMonitoringPresentation(
                _monitoringControl.Current);
        }

        private void CloseMonitoringPanel()
        {
            if (_monitoringClosed)
            {
                return;
            }

            _monitoringClosed = true;
            _monitoringControl.SnapshotChanged -=
                OnMonitoringSnapshotChanged;

            var disposable =
                _monitoringControl as IDisposable;

            disposable?.Dispose();
        }

        private void SynchronizeMonitoringSelection(
            DeviceDiagnostic device)
        {
            if (device == null)
            {
                if (_monitoringInputDeviceId.HasValue)
                {
                    _monitoringInputDeviceId = null;
                    MonitoringTargetAddressTextBox.Text =
                        string.Empty;
                }

                MonitoringSelectedDeviceValueText.Text =
                    UiText.Get("MonitoringNoDeviceSelected");

                UpdateMonitoringControlAvailability(
                    _monitoringControl.Current);
                return;
            }

            if (_monitoringInputDeviceId ==
                device.DeviceId)
            {
                return;
            }

            _monitoringInputDeviceId =
                device.DeviceId;

            MonitoringSelectedDeviceValueText.Text =
                UiText.Format(
                    "MonitoringSelectedDeviceValue",
                    string.IsNullOrWhiteSpace(
                        device.DisplayName)
                        ? UiText.Get(
                            "NodeUnknownLabel")
                        : device.DisplayName,
                    device.DeviceId);

            MonitoringTargetAddressTextBox.Text =
                string.IsNullOrWhiteSpace(
                    device.ManagementAddress)
                    ? string.Empty
                    : device.ManagementAddress;

            UpdateMonitoringControlAvailability(
                _monitoringControl.Current);
        }

        private async void OnMonitoringStartClick(
            object sender,
            RoutedEventArgs e)
        {
            MonitoringTarget target;
            MonitoringSessionPolicy policy;
            string validation;

            if (!TryBuildMonitoringRequest(
                    out target,
                    out policy,
                    out validation))
            {
                MonitoringMessageText.Text =
                    validation;
                return;
            }

            MonitoringMessageText.Text =
                string.Empty;
            _monitoringActivePolicy =
                policy;

            try
            {
                await _monitoringControl
                    .StartAsync(
                        target,
                        policy,
                        _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
                when (_lifetimeCancellation
                    .IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                ShowMonitoringActionFailure(
                    error);
            }
        }

        private async void OnMonitoringStopClick(
            object sender,
            RoutedEventArgs e)
        {
            MonitoringMessageText.Text =
                string.Empty;

            try
            {
                await _monitoringControl
                    .StopAsync(
                        _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
                when (_lifetimeCancellation
                    .IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                ShowMonitoringActionFailure(
                    error);
            }
        }

        private async void OnMonitoringPollNowClick(
            object sender,
            RoutedEventArgs e)
        {
            MonitoringMessageText.Text =
                string.Empty;

            var snapshot =
                _monitoringControl.Current;

            MonitoringTarget target;
            MonitoringSessionPolicy policy;
            string validation;

            var runningSchedule =
                snapshot.State ==
                    MonitoringControlState.Running ||
                (snapshot.State ==
                     MonitoringControlState.Polling &&
                 !_monitoringStandalonePollActive);

            if (runningSchedule)
            {
                target =
                    snapshot.ActiveTarget;

                policy =
                    _monitoringActivePolicy;

                if (target == null ||
                    policy == null)
                {
                    MonitoringMessageText.Text =
                        UiText.Get(
                            "MonitoringActiveSessionUnavailable");
                    return;
                }
            }
            else
            {
                if (_monitoringStandalonePollActive)
                {
                    return;
                }

                if (!TryBuildMonitoringRequest(
                        out target,
                        out policy,
                        out validation))
                {
                    MonitoringMessageText.Text =
                        validation;
                    return;
                }

                _monitoringStandalonePollActive = true;
                _monitoringActivePolicy = policy;
            }

            try
            {
                await _monitoringControl
                    .PollNowAsync(
                        target,
                        policy,
                        _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
                when (_lifetimeCancellation
                    .IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                _monitoringStandalonePollActive = false;
                UpdateMonitoringControlAvailability(
                    _monitoringControl.Current);
                ShowMonitoringActionFailure(
                    error);
            }

            if (_monitoringControl.Current.State ==
                    MonitoringControlState.Stopped ||
                _monitoringControl.Current.State ==
                    MonitoringControlState.Faulted)
            {
                _monitoringStandalonePollActive = false;
                UpdateMonitoringControlAvailability(
                    _monitoringControl.Current);
            }
        }

        private async void OnMonitoringRefreshTopologyClick(
            object sender,
            RoutedEventArgs e)
        {
            MonitoringMessageText.Text =
                string.Empty;

            await RefreshTopologyAsync();

            UpdateMonitoringPresentation(
                _monitoringControl.Current);
        }

        private void OnMonitoringSnapshotChanged(
            object sender,
            MonitoringControlSnapshotChangedEventArgs e)
        {
            if (_monitoringClosed ||
                _lifetimeCancellation
                    .IsCancellationRequested)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        () =>
                        {
                            if (!_monitoringClosed)
                            {
                                UpdateMonitoringPresentation(
                                    e.Snapshot);
                            }
                        }));
                return;
            }

            UpdateMonitoringPresentation(
                e.Snapshot);
        }

        private void UpdateMonitoringPresentation(
            MonitoringControlSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            if (snapshot.State ==
                    MonitoringControlState.Stopped ||
                snapshot.State ==
                    MonitoringControlState.Faulted)
            {
                _monitoringStandalonePollActive = false;
                _monitoringActivePolicy = null;
            }

            MonitoringStateValueText.Text =
                UiText.Get(
                    MonitoringStateResourceKey(
                        snapshot.State));

            MonitoringActiveTargetValueText.Text =
                snapshot.ActiveTarget == null
                    ? UiText.Get(
                        "DiagnosticNotAvailable")
                    : UiText.Format(
                        "MonitoringActiveTargetValue",
                        snapshot.ActiveTarget
                            .TargetAddress,
                        snapshot.ActiveTarget
                            .DeviceId);

            MonitoringLastPollValueText.Text =
                LocalMonitoringTime(
                    snapshot.LastSuccessfulPollUtc);

            MonitoringLastRefreshValueText.Text =
                LocalMonitoringTime(
                    _refreshStateTracker
                        .Current
                        .LastSuccessUtc);

            MonitoringMessageText.Text =
                string.IsNullOrWhiteSpace(
                    snapshot.FaultMessage)
                    ? string.Empty
                    : UiText.Format(
                        "MonitoringFaultMessage",
                        snapshot.FaultMessage);

            UpdateMonitoringControlAvailability(
                snapshot);
        }

        private void UpdateMonitoringControlAvailability(
            MonitoringControlSnapshot snapshot)
        {
            var canEdit =
                snapshot.State ==
                    MonitoringControlState.Stopped ||
                snapshot.State ==
                    MonitoringControlState.Faulted;

            var hasSelectedDevice =
                _monitoringInputDeviceId.HasValue;

            MonitoringTargetAddressTextBox.IsEnabled =
                canEdit &&
                hasSelectedDevice;
            MonitoringIntervalTextBox.IsEnabled = canEdit;
            MonitoringVersionComboBox.IsEnabled = canEdit;
            MonitoringPortTextBox.IsEnabled = canEdit;
            MonitoringTimeoutTextBox.IsEnabled = canEdit;
            MonitoringRetriesTextBox.IsEnabled = canEdit;
            MonitoringMaxRepetitionsTextBox.IsEnabled = canEdit;
            MonitoringErrorThresholdTextBox.IsEnabled = canEdit;
            MonitoringDiscardThresholdTextBox.IsEnabled = canEdit;
            MonitoringKindLldpCheckBox.IsEnabled = canEdit;
            MonitoringKindCdpCheckBox.IsEnabled = canEdit;
            MonitoringKindFdbCheckBox.IsEnabled = canEdit;
            MonitoringKindArpCheckBox.IsEnabled = canEdit;
            MonitoringKindHealthCheckBox.IsEnabled = canEdit;
            MonitoringKindInterfaceCheckBox.IsEnabled = canEdit;
            MonitoringKindStpCheckBox.IsEnabled = canEdit;

            MonitoringStartButton.IsEnabled =
                canEdit &&
                hasSelectedDevice;

            MonitoringStopButton.IsEnabled =
                snapshot.State ==
                    MonitoringControlState.Starting ||
                snapshot.State ==
                    MonitoringControlState.Running ||
                snapshot.State ==
                    MonitoringControlState.Polling;

            MonitoringPollNowButton.IsEnabled =
                snapshot.State ==
                    MonitoringControlState.Running ||
                (snapshot.State ==
                     MonitoringControlState.Polling &&
                 !_monitoringStandalonePollActive) ||
                (canEdit &&
                 hasSelectedDevice);

            MonitoringRefreshTopologyButton.IsEnabled =
                !_lifetimeCancellation
                    .IsCancellationRequested;
        }

        private bool TryBuildMonitoringRequest(
            out MonitoringTarget target,
            out MonitoringSessionPolicy policy,
            out string validation)
        {
            target = null;
            policy = null;
            validation = null;

            if (!_monitoringInputDeviceId.HasValue)
            {
                validation =
                    UiText.Get(
                        "MonitoringValidationSelectDevice");
                return false;
            }

            IPAddress address;

            if (!IPAddress.TryParse(
                    (MonitoringTargetAddressTextBox.Text ??
                     string.Empty)
                    .Trim(),
                    out address))
            {
                validation =
                    UiText.Get(
                        "MonitoringValidationTargetAddress");
                return false;
            }

            if (!TryBuildMonitoringPolicy(
                    out policy,
                    out validation))
            {
                return false;
            }

            target =
                new MonitoringTarget(
                    _monitoringInputDeviceId.Value,
                    address);

            return true;
        }

        private bool TryBuildMonitoringPolicy(
            out MonitoringSessionPolicy policy,
            out string validation)
        {
            policy = null;
            validation = null;

            int intervalSeconds;
            int port;
            int timeoutMilliseconds;
            int retryCount;
            int maxRepetitions;

            if (!TryParseMonitoringInteger(
                    MonitoringIntervalTextBox.Text,
                    1,
                    int.MaxValue,
                    "MonitoringIntervalLabel",
                    out intervalSeconds,
                    out validation) ||
                !TryParseMonitoringInteger(
                    MonitoringPortTextBox.Text,
                    1,
                    65535,
                    "MonitoringPortLabel",
                    out port,
                    out validation) ||
                !TryParseMonitoringInteger(
                    MonitoringTimeoutTextBox.Text,
                    1,
                    int.MaxValue,
                    "MonitoringTimeoutLabel",
                    out timeoutMilliseconds,
                    out validation) ||
                !TryParseMonitoringInteger(
                    MonitoringRetriesTextBox.Text,
                    0,
                    int.MaxValue,
                    "MonitoringRetriesLabel",
                    out retryCount,
                    out validation) ||
                !TryParseMonitoringInteger(
                    MonitoringMaxRepetitionsTextBox.Text,
                    1,
                    int.MaxValue,
                    "MonitoringMaxRepetitionsLabel",
                    out maxRepetitions,
                    out validation))
            {
                return false;
            }

            double? errorThreshold;
            double? discardThreshold;

            if (!TryParseMonitoringThreshold(
                    MonitoringErrorThresholdTextBox.Text,
                    "MonitoringErrorThresholdLabel",
                    out errorThreshold,
                    out validation) ||
                !TryParseMonitoringThreshold(
                    MonitoringDiscardThresholdTextBox.Text,
                    "MonitoringDiscardThresholdLabel",
                    out discardThreshold,
                    out validation))
            {
                return false;
            }

            var kinds =
                SelectedMonitoringKinds();

            if (kinds.Count == 0)
            {
                validation =
                    UiText.Get(
                        "MonitoringValidationPollKind");
                return false;
            }

            if (!(MonitoringVersionComboBox.SelectedItem
                    is SnmpVersion))
            {
                validation =
                    UiText.Get(
                        "MonitoringValidationVersion");
                return false;
            }

            policy =
                new MonitoringSessionPolicy(
                    TimeSpan.FromSeconds(
                        intervalSeconds),
                    (SnmpVersion)
                        MonitoringVersionComboBox.SelectedItem,
                    port,
                    timeoutMilliseconds,
                    retryCount,
                    maxRepetitions,
                    kinds,
                    errorThreshold,
                    discardThreshold);

            return true;
        }

        private List<MonitoringPollKind>
            SelectedMonitoringKinds()
        {
            var result =
                new List<MonitoringPollKind>();

            AddMonitoringKind(
                result,
                MonitoringKindLldpCheckBox.IsChecked,
                MonitoringPollKind.Lldp);
            AddMonitoringKind(
                result,
                MonitoringKindCdpCheckBox.IsChecked,
                MonitoringPollKind.Cdp);
            AddMonitoringKind(
                result,
                MonitoringKindFdbCheckBox.IsChecked,
                MonitoringPollKind.Fdb);
            AddMonitoringKind(
                result,
                MonitoringKindArpCheckBox.IsChecked,
                MonitoringPollKind.Arp);
            AddMonitoringKind(
                result,
                MonitoringKindHealthCheckBox.IsChecked,
                MonitoringPollKind.Health);
            AddMonitoringKind(
                result,
                MonitoringKindInterfaceCheckBox.IsChecked,
                MonitoringPollKind.Interface);
            AddMonitoringKind(
                result,
                MonitoringKindStpCheckBox.IsChecked,
                MonitoringPollKind.Stp);

            return result;
        }

        private static void AddMonitoringKind(
            ICollection<MonitoringPollKind> result,
            bool? selected,
            MonitoringPollKind kind)
        {
            if (selected == true)
            {
                result.Add(kind);
            }
        }

        private static bool TryParseMonitoringInteger(
            string text,
            int minimum,
            int maximum,
            string labelResourceKey,
            out int value,
            out string validation)
        {
            if (int.TryParse(
                    (text ?? string.Empty).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.CurrentCulture,
                    out value) &&
                value >= minimum &&
                value <= maximum)
            {
                validation = null;
                return true;
            }

            validation =
                UiText.Format(
                    "MonitoringValidationInteger",
                    UiText.Get(
                        labelResourceKey));
            return false;
        }

        private static bool TryParseMonitoringThreshold(
            string text,
            string labelResourceKey,
            out double? value,
            out string validation)
        {
            value = null;
            validation = null;

            var normalized =
                (text ?? string.Empty).Trim();

            if (normalized.Length == 0)
            {
                return true;
            }

            double parsed;

            var parsedOk =
                double.TryParse(
                    normalized,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out parsed) ||
                double.TryParse(
                    normalized,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed);

            if (!parsedOk ||
                double.IsNaN(parsed) ||
                double.IsInfinity(parsed) ||
                parsed <= 0.0)
            {
                validation =
                    UiText.Format(
                        "MonitoringValidationThreshold",
                        UiText.Get(
                            labelResourceKey));
                return false;
            }

            value = parsed;
            return true;
        }

        private void ShowMonitoringActionFailure(
            Exception error)
        {
            MonitoringMessageText.Text =
                UiText.Format(
                    "MonitoringActionFailed",
                    error == null
                        ? UiText.Get(
                            "DiagnosticNotAvailable")
                        : error.Message);
        }

        private static string LocalMonitoringTime(
            DateTime? utc)
        {
            return utc.HasValue
                ? utc.Value
                    .ToLocalTime()
                    .ToString(
                        "G",
                        CultureInfo.CurrentCulture)
                : UiText.Get(
                    "DiagnosticNotAvailable");
        }

        private static string MonitoringStateResourceKey(
            MonitoringControlState state)
        {
            switch (state)
            {
                case MonitoringControlState.Stopped:
                    return "MonitoringStateStopped";
                case MonitoringControlState.Starting:
                    return "MonitoringStateStarting";
                case MonitoringControlState.Running:
                    return "MonitoringStateRunning";
                case MonitoringControlState.Polling:
                    return "MonitoringStatePolling";
                case MonitoringControlState.Stopping:
                    return "MonitoringStateStopping";
                case MonitoringControlState.Faulted:
                    return "MonitoringStateFaulted";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state));
            }
        }

        private sealed class EmptyMonitoringControl :
            IMonitoringControl
        {
            private readonly MonitoringControlSnapshot
                _current =
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Stopped,
                        null,
                        null,
                        null);

            public MonitoringControlSnapshot Current =>
                _current;

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged
            {
                add { }
                remove { }
            }

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                throw new InvalidOperationException(
                    "MONITORING_CONTROL_UNAVAILABLE");
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public Task PollNowAsync(
                MonitoringTarget targetWhenStopped,
                MonitoringSessionPolicy policyWhenStopped,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                throw new InvalidOperationException(
                    "MONITORING_CONTROL_UNAVAILABLE");
            }
        }
    }
}
