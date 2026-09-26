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
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private readonly IMonitoringControl _monitoringControl;

        private const int InitialMonitoringMaxConcurrentPolls = 4;

        private static readonly TimeSpan
            InitialMonitoringStartupJitter =
                TimeSpan.FromSeconds(15);

        private Guid? _monitoringInputDeviceId;
        private MonitoringSessionPolicy _monitoringActivePolicy;
        private int _monitoringActiveTargetCount;
        private bool _monitoringTargetSetSessionActive;
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
                MonitoringSelectedAddressText(
                    device);

            UpdateMonitoringControlAvailability(
                _monitoringControl.Current);
        }

        private async void OnMonitoringStartClick(
            object sender,
            RoutedEventArgs e)
        {
            var multiTargetControl =
                _monitoringControl as
                    IMultiTargetMonitoringControl;

            if (multiTargetControl != null)
            {
                IReadOnlyList<MonitoringTarget> targets;
                MonitoringSessionPolicy policy;
                string validation;

                if (!TryBuildMonitoringTargetSetRequest(
                        out targets,
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
                _monitoringActiveTargetCount =
                    targets.Count;
                _monitoringTargetSetSessionActive =
                    true;

                try
                {
                    await multiTargetControl
                        .StartSetAsync(
                            targets,
                            policy,
                            InitialMonitoringTargetSetPolicy(),
                            _lifetimeCancellation.Token);
                }
                catch (OperationCanceledException)
                    when (_lifetimeCancellation
                        .IsCancellationRequested)
                {
                }
                catch (Exception error)
                {
                    if (_monitoringControl.Current.State ==
                            MonitoringControlState.Stopped ||
                        _monitoringControl.Current.State ==
                            MonitoringControlState.Faulted)
                    {
                        _monitoringTargetSetSessionActive =
                            false;
                        _monitoringActiveTargetCount =
                            0;
                    }

                    UpdateMonitoringPresentation(
                        _monitoringControl.Current);

                    ShowMonitoringActionFailure(
                        error);
                }

                return;
            }

            MonitoringTarget target;
            MonitoringSessionPolicy legacyPolicy;
            string legacyValidation;

            if (!TryBuildMonitoringRequest(
                    out target,
                    out legacyPolicy,
                    out legacyValidation))
            {
                MonitoringMessageText.Text =
                    legacyValidation;
                return;
            }

            MonitoringMessageText.Text =
                string.Empty;
            _monitoringActivePolicy =
                legacyPolicy;

            try
            {
                await _monitoringControl
                    .StartAsync(
                        target,
                        legacyPolicy,
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
                if (_monitoringTargetSetSessionActive)
                {
                    target = null;
                    policy = null;
                }
                else
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
                _monitoringTargetSetSessionActive =
                    false;
                _monitoringActiveTargetCount =
                    0;
            }

            MonitoringStateValueText.Text =
                UiText.Get(
                    MonitoringStateResourceKey(
                        snapshot.State));

            if (_monitoringControl is
                    IMultiTargetMonitoringControl)
            {
                MonitoringActiveTargetLabelText.Text =
                    MonitoringAvailableScopeText(
                        snapshot);
                MonitoringActiveTargetValueText.Text =
                    MonitoringActiveScopeText();
            }
            else
            {
                MonitoringActiveTargetLabelText.Text =
                    UiText.Get(
                        "MonitoringActiveTargetLabel");
                MonitoringActiveTargetValueText.Text =
                    MonitoringScopeText(
                        snapshot);
            }

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

            var multiTargetControl =
                _monitoringControl as
                    IMultiTargetMonitoringControl;

            MonitoringStartButton.IsEnabled =
                canEdit &&
                (multiTargetControl != null
                    ? CurrentMonitoringTargetSetCandidateCount() > 0
                    : hasSelectedDevice);

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

        private bool TryBuildMonitoringTargetSetRequest(
            out IReadOnlyList<MonitoringTarget> targets,
            out MonitoringSessionPolicy policy,
            out string validation)
        {
            targets = null;
            policy = null;
            validation = null;

            if (!TryBuildMonitoringPolicy(
                    out policy,
                    out validation))
            {
                return false;
            }

            var selectedOverrideText =
                (MonitoringTargetAddressTextBox.Text ??
                 string.Empty)
                .Trim();

            var result =
                new List<MonitoringTarget>();

            var seenDeviceIds =
                new HashSet<Guid>();

            if (_lastMapSnapshot != null)
            {
                foreach (var node in
                    _lastMapSnapshot.Nodes)
                {
                    if (!node.DeviceId.HasValue ||
                        node.DeviceId.Value ==
                            Guid.Empty ||
                        node.MonitoringCapability ==
                            MapMonitoringCapability.None)
                    {
                        continue;
                    }

                    IPAddress address;

                    var selectedNode =
                        _monitoringInputDeviceId ==
                            node.DeviceId;

                    var addressText =
                        selectedNode &&
                        selectedOverrideText.Length > 0
                            ? selectedOverrideText
                            : node.ManagementAddress;

                    if (!IPAddress.TryParse(
                            addressText,
                            out address))
                    {
                        if (selectedNode &&
                            selectedOverrideText.Length > 0)
                        {
                            validation =
                                UiText.Get(
                                    "MonitoringValidationTargetAddress");
                            return false;
                        }

                        continue;
                    }

                    if (!seenDeviceIds.Add(
                        node.DeviceId.Value))
                    {
                        validation =
                            UiText.Get(
                                "MonitoringValidationDuplicateDevice");
                        return false;
                    }

                    result.Add(
                        new MonitoringTarget(
                            node.DeviceId.Value,
                            address));
                }
            }

            if (result.Count == 0)
            {
                validation =
                    UiText.Get(
                        "MonitoringValidationNoPollableTargets");
                return false;
            }

            result.Sort(
                (left, right) =>
                    left.DeviceId.CompareTo(
                        right.DeviceId));

            targets =
                result;

            return true;
        }

        private int CurrentMonitoringTargetSetCandidateCount()
        {
            if (_lastMapSnapshot == null)
            {
                return 0;
            }

            var deviceIds =
                new HashSet<Guid>();

            foreach (var node in
                _lastMapSnapshot.Nodes)
            {
                if (!node.DeviceId.HasValue ||
                    node.DeviceId.Value ==
                        Guid.Empty ||
                    node.MonitoringCapability ==
                        MapMonitoringCapability.None)
                {
                    continue;
                }

                IPAddress address;

                if (IPAddress.TryParse(
                    node.ManagementAddress,
                    out address))
                {
                    deviceIds.Add(
                        node.DeviceId.Value);
                }
            }

            return deviceIds.Count;
        }

        private string MonitoringSelectedAddressText(
            DeviceDiagnostic device)
        {
            IPAddress address;

            if (IPAddress.TryParse(
                    (device.ManagementAddress ??
                     string.Empty)
                    .Trim(),
                    out address))
            {
                return address.ToString();
            }

            return TryGetMapManagementAddress(
                    device.DeviceId,
                    out address)
                ? address.ToString()
                : string.Empty;
        }

        private bool TryGetMapManagementAddress(
            Guid deviceId,
            out IPAddress address)
        {
            address = null;

            if (_lastMapSnapshot == null)
            {
                return false;
            }

            foreach (var node in
                _lastMapSnapshot.Nodes)
            {
                if (!node.DeviceId.HasValue ||
                    node.DeviceId.Value !=
                        deviceId)
                {
                    continue;
                }

                return IPAddress.TryParse(
                    node.ManagementAddress,
                    out address);
            }

            return false;
        }

        private string MonitoringAvailableScopeText(
            MonitoringControlSnapshot snapshot)
        {
            if (!(_monitoringControl is
                    IMultiTargetMonitoringControl))
            {
                return MonitoringScopeText(
                    snapshot);
            }

            var count =
                CurrentMonitoringTargetSetCandidateCount();

            return count == 0
                ? UiText.Get(
                    "MonitoringScopeNoTargets")
                : UiText.Format(
                    "MonitoringScopeReadyValue",
                    count);
        }

        private string MonitoringActiveScopeText()
        {
            if (!(_monitoringControl is
                    IMultiTargetMonitoringControl))
            {
                return string.Empty;
            }

            var count =
                _monitoringTargetSetSessionActive
                    ? _monitoringActiveTargetCount
                    : 0;

            return UiText.Format(
                "MonitoringScopeActiveValue",
                count);
        }

        private string MonitoringScopeText(
            MonitoringControlSnapshot snapshot)
        {
            if (_monitoringTargetSetSessionActive &&
                _monitoringActiveTargetCount > 0)
            {
                return UiText.Format(
                    "MonitoringScopeActiveValue",
                    _monitoringActiveTargetCount);
            }

            if (snapshot.ActiveTarget != null)
            {
                return UiText.Format(
                    "MonitoringActiveTargetValue",
                    snapshot.ActiveTarget
                        .TargetAddress,
                    snapshot.ActiveTarget
                        .DeviceId);
            }

            if (_monitoringControl is
                IMultiTargetMonitoringControl)
            {
                var count =
                    CurrentMonitoringTargetSetCandidateCount();

                return count == 0
                    ? UiText.Get(
                        "MonitoringScopeNoTargets")
                    : UiText.Format(
                        "MonitoringScopeReadyValue",
                        count);
            }

            return UiText.Get(
                "DiagnosticNotAvailable");
        }

        private static MonitoringTargetSetPolicy
            InitialMonitoringTargetSetPolicy()
        {
            // По результатам приёмочного стенда Sprint 43 используем
            // 4 параллельных опроса и startup jitter 15 с: 15/15 целей
            // Завершают первую волну без backpressure skips.
            return new MonitoringTargetSetPolicy(
                InitialMonitoringMaxConcurrentPolls,
                InitialMonitoringStartupJitter);
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

            var addressText =
                (MonitoringTargetAddressTextBox.Text ??
                 string.Empty)
                .Trim();

            if (addressText.Length == 0)
            {
                if (!TryGetMapManagementAddress(
                        _monitoringInputDeviceId.Value,
                        out address))
                {
                    validation =
                        UiText.Get(
                            "MonitoringValidationTargetAddress");
                    return false;
                }
            }
            else if (!IPAddress.TryParse(
                         addressText,
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

            var selectedProfile =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;

            if (selectedProfile != null)
            {
                MonitoringVersionComboBox.SelectedItem =
                    selectedProfile.Profile.SnmpVersion;
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
                    selectedProfile == null
                        ? (SnmpVersion)
                            MonitoringVersionComboBox.SelectedItem
                        : selectedProfile.Profile.SnmpVersion,
                    port,
                    timeoutMilliseconds,
                    retryCount,
                    maxRepetitions,
                    kinds,
                    errorThreshold,
                    discardThreshold,
                    selectedProfile == null
                        ? (Guid?)null
                        : selectedProfile.Profile.Id);

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
