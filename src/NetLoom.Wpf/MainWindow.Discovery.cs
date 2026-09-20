using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NetLoom.Application.Discovery;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private const int DiscoveryMaxAddresses = 4096;

        private readonly IDiscoveryControl _discoveryControl;
        private readonly IReadOnlyList<AccessProfile> _discoveryProfiles;
        private readonly IDiscoveryCandidateMaterializer
            _discoveryCandidateMaterializer;

        private readonly List<DiscoveryCandidateRow>
            _discoveryCandidateRows =
                new List<DiscoveryCandidateRow>();

        private bool _discoveryClosed;

        private void InitializeDiscoveryPanel()
        {
            DiscoveryTitleText.Text =
                UiText.Get("DiscoveryTitle");
            DiscoveryStateLabelText.Text =
                UiText.Get("DiscoveryStateLabel");
            DiscoveryProgressLabelText.Text =
                UiText.Get("DiscoveryProgressLabel");
            DiscoveryCurrentAddressLabelText.Text =
                UiText.Get("DiscoveryCurrentAddressLabel");
            DiscoveryStartAddressLabelText.Text =
                UiText.Get("DiscoveryStartAddressLabel");
            DiscoveryEndAddressLabelText.Text =
                UiText.Get("DiscoveryEndAddressLabel");
            DiscoverySubnetMaskLabelText.Text =
                UiText.Get("DiscoverySubnetMaskLabel");
            DiscoveryProfileLabelText.Text =
                UiText.Get("DiscoveryProfileLabel");
            DiscoveryStartButton.Content =
                UiText.Get("DiscoveryStartAction");
            DiscoveryStopButton.Content =
                UiText.Get("DiscoveryStopAction");
            DiscoveryWarningText.Text =
                UiText.Get("DiscoveryWarning");
            DiscoveryCandidatesLabelText.Text =
                UiText.Get("DiscoveryCandidatesLabel");

            DiscoveryStartAddressTextBox.Text =
                string.Empty;
            DiscoveryEndAddressTextBox.Text =
                string.Empty;
            DiscoverySubnetMaskTextBox.Text =
                "255.255.255.0";

            DiscoveryProfileComboBox.ItemsSource =
                _discoveryProfiles
                    .Select(
                        profile =>
                            new DiscoveryProfileOption(
                                profile,
                                UiText.Format(
                                    "DiscoveryProfileDisplay",
                                    profile.Name,
                                    SnmpVersionText(
                                        profile.SnmpVersion))))
                    .ToArray();

            DiscoveryProfileComboBox.SelectedIndex =
                -1;

            DiscoveryCandidatesList.ItemsSource =
                new DiscoveryCandidateRow[0];

            _discoveryControl.SnapshotChanged +=
                OnDiscoverySnapshotChanged;
            _discoveryControl.CandidateDiscovered +=
                OnDiscoveryCandidateDiscovered;

            DiscoveryMessageText.Text =
                _discoveryProfiles.Count == 0
                    ? UiText.Get(
                        "DiscoveryNoProfiles")
                    : string.Empty;

            UpdateDiscoveryPresentation(
                _discoveryControl.Current);
        }

        private void CloseDiscoveryPanel()
        {
            if (_discoveryClosed)
            {
                return;
            }

            _discoveryClosed = true;

            _discoveryControl.SnapshotChanged -=
                OnDiscoverySnapshotChanged;
            _discoveryControl.CandidateDiscovered -=
                OnDiscoveryCandidateDiscovered;

            var disposable =
                _discoveryControl as IDisposable;

            disposable?.Dispose();
        }

        private async void OnDiscoveryStartClick(
            object sender,
            RoutedEventArgs e)
        {
            DiscoveryControlRequest request;
            string validation;

            if (!TryBuildDiscoveryRequest(
                    out request,
                    out validation))
            {
                DiscoveryMessageText.Text =
                    validation;
                return;
            }

            _discoveryCandidateRows.Clear();
            RefreshDiscoveryCandidateRows();
            DiscoveryMessageText.Text =
                string.Empty;

            try
            {
                await _discoveryControl
                    .StartAsync(
                        request,
                        _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
                when (_lifetimeCancellation
                    .IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                ShowDiscoveryActionFailure(
                    error);
            }
        }

        private async void OnDiscoveryStopClick(
            object sender,
            RoutedEventArgs e)
        {
            DiscoveryMessageText.Text =
                string.Empty;

            try
            {
                await _discoveryControl
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
                ShowDiscoveryActionFailure(
                    error);
            }
        }

        private void OnDiscoverySnapshotChanged(
            object sender,
            DiscoveryControlSnapshotChangedEventArgs e)
        {
            if (_discoveryClosed ||
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
                            if (!_discoveryClosed)
                            {
                                UpdateDiscoveryPresentation(
                                    e.Snapshot);
                            }
                        }));
                return;
            }

            UpdateDiscoveryPresentation(
                e.Snapshot);
        }

        private void FocusDiscoveredDevice(
            string managementAddress)
        {
            if (string.IsNullOrWhiteSpace(
                    managementAddress) ||
                _lastMapSnapshot == null)
            {
                return;
            }

            foreach (var node in
                _lastMapSnapshot.Nodes)
            {
                if (!node.DeviceId.HasValue ||
                    !string.Equals(
                        node.ManagementAddress,
                        managementAddress,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var deviceId =
                    node.DeviceId.Value;

                _highlightedDeviceId = null;
                _selectedDeviceId = deviceId;
                _selectedPhysicalLinkId = null;
                _selectedLocationId = null;

                RedrawCurrentMap();
                ShowSelectedDiagnostic();
                UpdateSelectedLayoutControl();

                FocusSelectedMapAtNativeZoom(
                    () =>
                    {
                        if (!_selectedDeviceId.HasValue ||
                            _selectedDeviceId.Value != deviceId)
                        {
                            return;
                        }

                        AnimateDiscoveryFocus(
                            deviceId);
                    });

                return;
            }
        }

        private void OnDiscoveryCandidateDiscovered(
            object sender,
            DiscoveryCandidateDiscoveredEventArgs e)
        {
            if (_discoveryClosed ||
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
                            PresentDiscoveryCandidate(
                                e.Candidate)));
                return;
            }

            PresentDiscoveryCandidate(
                e.Candidate);
        }

        private async void PresentDiscoveryCandidate(
            DiscoveryCandidateSnapshot candidate)
        {
            if (_discoveryClosed ||
                candidate == null)
            {
                return;
            }

            var address =
                candidate.Address.ToString();

            var existingIndex =
                _discoveryCandidateRows.FindIndex(
                    row =>
                        string.Equals(
                            row.Address,
                            address,
                            StringComparison.OrdinalIgnoreCase));

            var row =
                new DiscoveryCandidateRow(
                    address,
                    UiText.Format(
                        "DiscoveryCandidateSummary",
                        address,
                        string.IsNullOrWhiteSpace(
                            candidate.SysName)
                            ? UiText.Get(
                                "DiscoveryUnnamedCandidate")
                            : candidate.SysName,
                        UiText.Get(
                            candidate.SnmpResponded
                                ? "DiscoverySnmpResponded"
                                : "DiscoverySnmpUnavailable")));

            if (existingIndex >= 0)
            {
                _discoveryCandidateRows[existingIndex] =
                    row;
            }
            else
            {
                _discoveryCandidateRows.Add(
                    row);
            }

            RefreshDiscoveryCandidateRows();

            try
            {
                _discoveryCandidateMaterializer
                    .Materialize(
                        candidate,
                        DateTime.UtcNow);

                await RefreshTopologyAsync();

                FocusDiscoveredDevice(
                    address);
            }
            catch (Exception error)
            {
                DiscoveryMessageText.Text =
                    UiText.Format(
                        "DiscoveryMaterializationFailed",
                        address,
                        error.Message);
            }
        }

        private bool TryBuildDiscoveryRequest(
            out DiscoveryControlRequest request,
            out string validation)
        {
            request = null;
            validation = null;

            var startAddress =
                (DiscoveryStartAddressTextBox.Text ??
                    string.Empty)
                    .Trim();

            var endAddress =
                (DiscoveryEndAddressTextBox.Text ??
                    string.Empty)
                    .Trim();

            var subnetMask =
                (DiscoverySubnetMaskTextBox.Text ??
                    string.Empty)
                    .Trim();

            if (startAddress.Length == 0 ||
                endAddress.Length == 0 ||
                subnetMask.Length == 0)
            {
                validation =
                    UiText.Get(
                        "DiscoveryValidationRangeRequired");
                return false;
            }

            try
            {
                Ipv4RangeExpander.Expand(
                    startAddress,
                    endAddress,
                    subnetMask,
                    DiscoveryMaxAddresses);
            }
            catch (InvalidOperationException)
            {
                validation =
                    UiText.Format(
                        "DiscoveryValidationRangeTooLarge",
                        DiscoveryMaxAddresses);
                return false;
            }
            catch (FormatException)
            {
                validation =
                    UiText.Get(
                        "DiscoveryValidationRangeInvalid");
                return false;
            }
            catch (ArgumentException error)
            {
                validation =
                    string.Equals(
                        error.Message,
                        "DISCOVERY_RANGE_CROSSES_SUBNET",
                        StringComparison.Ordinal)
                        ? UiText.Get(
                            "DiscoveryValidationRangeCrossesSubnet")
                        : UiText.Get(
                            "DiscoveryValidationRangeInvalid");
                return false;
            }

            var selected =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;

            if (selected == null)
            {
                validation =
                    _discoveryProfiles.Count == 0
                        ? UiText.Get(
                            "DiscoveryNoProfiles")
                        : UiText.Get(
                            "DiscoveryValidationProfileRequired");
                return false;
            }

            request =
                new DiscoveryControlRequest(
                    startAddress,
                    endAddress,
                    subnetMask,
                    selected.Profile.Id,
                    selected.Profile.SnmpVersion);

            return true;
        }

        private void UpdateDiscoveryPresentation(
            DiscoveryControlSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            DiscoveryStateValueText.Text =
                UiText.Get(
                    DiscoveryStateResourceKey(
                        snapshot.State));

            DiscoveryProgressValueText.Text =
                snapshot.TotalAddresses > 0
                    ? UiText.Format(
                        "DiscoveryProgressValue",
                        snapshot.ProcessedAddresses,
                        snapshot.TotalAddresses,
                        snapshot.FoundCandidates)
                    : UiText.Get(
                        "DiscoveryProgressPending");

            DiscoveryCurrentAddressValueText.Text =
                snapshot.CurrentAddress == null
                    ? UiText.Get(
                        "DiagnosticNotAvailable")
                    : snapshot.CurrentAddress
                        .ToString();

            var canStart =
                snapshot.State ==
                    DiscoveryControlState.Idle ||
                snapshot.State ==
                    DiscoveryControlState.Completed ||
                snapshot.State ==
                    DiscoveryControlState.Stopped ||
                snapshot.State ==
                    DiscoveryControlState.Faulted;

            DiscoveryStartAddressTextBox.IsEnabled =
                canStart;
            DiscoveryEndAddressTextBox.IsEnabled =
                canStart;
            DiscoverySubnetMaskTextBox.IsEnabled =
                canStart;
            DiscoveryProfileComboBox.IsEnabled =
                canStart;
            DiscoveryStartButton.IsEnabled =
                canStart &&
                _discoveryProfiles.Count > 0;
            DiscoveryStopButton.IsEnabled =
                snapshot.State ==
                    DiscoveryControlState.Starting ||
                snapshot.State ==
                    DiscoveryControlState.Running;

            if (snapshot.State ==
                    DiscoveryControlState.Faulted &&
                !string.IsNullOrWhiteSpace(
                    snapshot.FaultMessage))
            {
                DiscoveryMessageText.Text =
                    UiText.Format(
                        "DiscoveryFaultMessage",
                        snapshot.FaultMessage);
            }
        }

        private void RefreshDiscoveryCandidateRows()
        {
            DiscoveryCandidatesList.ItemsSource =
                _discoveryCandidateRows
                    .OrderBy(
                        row =>
                            row.Address,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
        }

        private void ShowDiscoveryActionFailure(
            Exception error)
        {
            var message =
                error == null
                    ? null
                    : error.Message;

            switch (message)
            {
                case "DISCOVERY_SNMP_COMMUNITY_REQUIRED":
                    DiscoveryMessageText.Text =
                        UiText.Get(
                            "DiscoveryProfileCredentialsMissing");
                    return;

                case "DISCOVERY_SNMP_V3_SECURITY_PROTOCOLS_NOT_STORED":
                    DiscoveryMessageText.Text =
                        UiText.Get(
                            "DiscoveryV3SecurityUnsupported");
                    return;

                default:
                    DiscoveryMessageText.Text =
                        UiText.Format(
                            "DiscoveryActionFailed",
                            string.IsNullOrWhiteSpace(
                                message)
                                ? UiText.Get(
                                    "DiagnosticNotAvailable")
                                : message);
                    return;
            }
        }

        private static string DiscoveryStateResourceKey(
            DiscoveryControlState state)
        {
            switch (state)
            {
                case DiscoveryControlState.Idle:
                    return "DiscoveryStateIdle";
                case DiscoveryControlState.Starting:
                    return "DiscoveryStateStarting";
                case DiscoveryControlState.Running:
                    return "DiscoveryStateRunning";
                case DiscoveryControlState.Stopping:
                    return "DiscoveryStateStopping";
                case DiscoveryControlState.Completed:
                    return "DiscoveryStateCompleted";
                case DiscoveryControlState.Stopped:
                    return "DiscoveryStateStopped";
                case DiscoveryControlState.Faulted:
                    return "DiscoveryStateFaulted";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state));
            }
        }

        private static string SnmpVersionText(
            SnmpVersion version)
        {
            switch (version)
            {
                case SnmpVersion.V1:
                    return "v1";
                case SnmpVersion.V2C:
                    return "v2c";
                case SnmpVersion.V3:
                    return "v3";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(version));
            }
        }

        private sealed class DiscoveryProfileOption
        {
            public DiscoveryProfileOption(
                AccessProfile profile,
                string displayName)
            {
                Profile = profile ??
                    throw new ArgumentNullException(
                        nameof(profile));
                DisplayName = displayName ??
                    throw new ArgumentNullException(
                        nameof(displayName));
            }

            public AccessProfile Profile { get; }

            public string DisplayName { get; }
        }

        private sealed class DiscoveryCandidateRow
        {
            public DiscoveryCandidateRow(
                string address,
                string summary)
            {
                Address = address;
                Summary = summary;
            }

            public string Address { get; }

            public string Summary { get; }
        }

        private sealed class EmptyDiscoveryControl :
            IDiscoveryControl
        {
            private readonly DiscoveryControlSnapshot
                _current =
                    new DiscoveryControlSnapshot(
                        DiscoveryControlState.Idle,
                        null,
                        null,
                        0,
                        0,
                        0,
                        null,
                        null);

            public DiscoveryControlSnapshot Current =>
                _current;

            public event EventHandler<DiscoveryControlSnapshotChangedEventArgs>
                SnapshotChanged
            {
                add { }
                remove { }
            }

            public event EventHandler<DiscoveryCandidateDiscoveredEventArgs>
                CandidateDiscovered
            {
                add { }
                remove { }
            }

            public Task StartAsync(
                DiscoveryControlRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                throw new InvalidOperationException(
                    "DISCOVERY_CONTROL_UNAVAILABLE");
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class EmptyDiscoveryCandidateMaterializer :
            IDiscoveryCandidateMaterializer
        {
            public Guid Materialize(
                DiscoveryCandidateSnapshot candidate,
                DateTime observedUtc)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_MATERIALIZER_UNAVAILABLE");
            }
        }
    }
}
