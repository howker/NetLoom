using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NetLoom.Application.Alerts;
using NetLoom.Application.Locations;
using NetLoom.Application.Lookup;
using NetLoom.Application.MapLayout;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyMap;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow : Window
{
    private async void OnLookupSearchClick(
        object sender,
        RoutedEventArgs e)
    {
        await QueueLookupAsync();
    }

    private async void OnLookupQueryKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await QueueLookupAsync();
            e.Handled = true;
        }
    }

    private async Task QueueLookupAsync()
    {
        if (_lifetimeCancellation
            .IsCancellationRequested)
        {
            return;
        }

        _lookupRequestTracker.Queue(
            LookupQueryTextBox.Text);

        LookupResultsList.ItemsSource = null;

        LookupDetailsText.Text =
            UiText.Get(
                "LookupSelectCandidate");

        _highlightedDeviceId = null;

        RedrawCurrentMap();

        MacIpLookupRequest request;

        if (!_lookupRequestTracker
            .TryStartWorker(
                out request))
        {
            return;
        }

        await RunLookupWorkerAsync(
            request);
    }

    private async Task RunLookupWorkerAsync(
        MacIpLookupRequest request)
    {
        try
        {
            while (request != null &&
                   !_lifetimeCancellation
                       .IsCancellationRequested)
            {
                MacIpLookupResult result =
                    null;

                Exception error =
                    null;

                try
                {
                    var cancellationToken =
                        _lifetimeCancellation.Token;

                    result =
                        await Task.Run(
                            () =>
                                _lookupSearchService
                                    .Search(
                                        request.Query,
                                        LookupCandidateLimit),
                            cancellationToken);
                }
                catch (Exception lookupError)
                {
                    error =
                        lookupError;
                }

                if (_lifetimeCancellation
                    .IsCancellationRequested)
                {
                    return;
                }

                if (_lookupRequestTracker
                    .IsCurrent(
                        request))
                {
                    if (error is ArgumentException)
                    {
                        LookupStatusText.Text =
                            UiText.Get(
                                "LookupInvalidQuery");
                    }
                    else if (error != null)
                    {
                        Trace.TraceError(
                            error.ToString());

                        LookupStatusText.Text =
                            UiText.Get(
                                "LookupSearchFailed");
                    }
                    else
                    {
                        ApplyLookupResult(
                            result);
                    }
                }

                MacIpLookupRequest nextRequest;

                if (!_lookupRequestTracker
                    .TryTakePending(
                        out nextRequest))
                {
                    return;
                }

                request =
                    nextRequest;
            }
        }
        finally
        {
            _lookupRequestTracker
                .CompleteWorker();
        }
    }

    private void ApplyLookupResult(
        MacIpLookupResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(
                nameof(result));
        }

        var rows =
            result.Candidates
                .Select(
                    candidate =>
                        new LookupCandidateRow(
                            candidate,
                            BuildCandidateSummary(
                                candidate),
                            BuildCandidateDetails(
                                candidate)))
                .ToArray();

        LookupResultsList.ItemsSource =
            rows;

        LookupStatusText.Text =
            rows.Length == 0
                ? UiText.Get(
                    "LookupNoResults")
                : UiText.FormatCount(
                    "LookupResultCount",
                    rows.Length,
                    result.NormalizedQuery);
    }

    private void OnLookupSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var row =
            LookupResultsList.SelectedItem
            as LookupCandidateRow;

        if (row == null)
        {
            return;
        }

        LookupDetailsText.Text =
            row.Details;

        var candidate =
            row.Candidate;

        if (candidate.Status ==
                MacIpLookupCandidateStatus
                    .ResolvedInterface &&
            candidate.DeviceId.HasValue)
        {
            _highlightedDeviceId =
                candidate.DeviceId.Value;

            _selectedDeviceId =
                candidate.DeviceId.Value;

            _selectedPhysicalLinkId =
                null;

            RedrawCurrentMap();
            ShowSelectedDiagnostic();
            UpdateSelectedLayoutControl();
            BringHighlightedDeviceIntoView();
        }
        else
        {
            _highlightedDeviceId = null;
            RedrawCurrentMap();
        }
    }

    private void BringHighlightedDeviceIntoView()
    {
        if (!_highlightedDeviceId.HasValue)
        {
            return;
        }

        Border border;

        if (_nodeBordersByDeviceId.TryGetValue(
            _highlightedDeviceId.Value,
            out border))
        {
            border.BringIntoView();

            AnimatePulse(
                border,
                MapMotionKind.SearchFocus);
        }
    }

    private static string BuildCandidateSummary(
        MacIpLookupCandidate candidate)
    {
        var interfaceText =
            candidate.IfIndex.HasValue
                ? UiText.Format(
                    "LookupInterfaceShort",
                    candidate.IfIndex.Value)
                : UiText.Get(
                    "LookupInterfaceUnavailable");

        return UiText.Format(
            "LookupCandidateSummary",
            StatusText(candidate.Status),
            candidate.MacAddress,
            interfaceText);
    }

    private static string BuildCandidateDetails(
        MacIpLookupCandidate candidate)
    {
        var values =
            new List<string>();

        if (!string.IsNullOrWhiteSpace(
            candidate.IpAddress))
        {
            values.Add(
                UiText.Format(
                    "LookupDetailIp",
                    candidate.IpAddress));
        }

        values.Add(
            UiText.Format(
                "LookupDetailMac",
                candidate.MacAddress));

        values.Add(
            UiText.Format(
                "LookupDetailStatus",
                StatusText(
                    candidate.Status)));

        if (candidate.DeviceId.HasValue)
        {
            values.Add(
                UiText.Format(
                    "LookupDetailDeviceId",
                    candidate.DeviceId.Value));
        }

        if (candidate.InterfaceId.HasValue)
        {
            values.Add(
                UiText.Format(
                    "LookupDetailInterfaceId",
                    candidate.InterfaceId.Value));
        }

        if (candidate.IfIndex.HasValue)
        {
            values.Add(
                UiText.Format(
                    "LookupDetailIfIndex",
                    candidate.IfIndex.Value));
        }

        if (candidate.BridgePortIndex.HasValue)
        {
            values.Add(
                UiText.Format(
                    "LookupDetailBridgePort",
                    candidate.BridgePortIndex.Value));
        }

        if (candidate.FdbCapturedUtc.HasValue)
        {
            values.Add(
                UiText.Format(
                    "LookupDetailFdbObserved",
                    candidate.FdbCapturedUtc.Value
                        .ToLocalTime()));
        }

        if (!string.IsNullOrWhiteSpace(
            candidate.FdbSourceAddress))
        {
            values.Add(
                UiText.Format(
                    "LookupDetailFdbSource",
                    candidate.FdbSourceAddress));
        }

        if (candidate.ArpCapturedUtc.HasValue)
        {
            values.Add(
                UiText.Format(
                    "LookupDetailArpObserved",
                    candidate.ArpCapturedUtc.Value
                        .ToLocalTime()));
        }

        if (!string.IsNullOrWhiteSpace(
            candidate.ArpSourceAddress))
        {
            values.Add(
                UiText.Format(
                    "LookupDetailArpSource",
                    candidate.ArpSourceAddress));
        }

        if (candidate.Status ==
                MacIpLookupCandidateStatus
                    .ResolvedInterface)
        {
            values.Add(
                UiText.Get(
                    "LookupResolvedInterfaceCaveat"));
        }

        return string.Join(
            Environment.NewLine,
            values);
    }

    private static string StatusText(
        MacIpLookupCandidateStatus status)
    {
        switch (status)
        {
            case MacIpLookupCandidateStatus
                .FdbNotObserved:
                return UiText.Get(
                    "LookupStatusFdbNotObserved");

            case MacIpLookupCandidateStatus
                .ObservationUnbound:
                return UiText.Get(
                    "LookupStatusObservationUnbound");

            case MacIpLookupCandidateStatus
                .BridgePortUnresolved:
                return UiText.Get(
                    "LookupStatusBridgePortUnresolved");

            case MacIpLookupCandidateStatus
                .BridgePortAmbiguous:
                return UiText.Get(
                    "LookupStatusBridgePortAmbiguous");

            case MacIpLookupCandidateStatus
                .InterfaceNotMaterialized:
                return UiText.Get(
                    "LookupStatusInterfaceNotMaterialized");

            default:
                return UiText.Get(
                    "LookupStatusResolvedInterface");
        }
    }

    private static string BuildTopologyMetadata(
        MapNode node)
    {
        var values =
            new List<string>();

        var origin =
            OriginText(node.Origin);

        if (!string.IsNullOrWhiteSpace(origin))
        {
            values.Add(origin);
        }

        var category =
            CategoryText(node.Category);

        if (!string.IsNullOrWhiteSpace(category))
        {
            values.Add(category);
        }

        var monitoring =
            MonitoringText(
                node.MonitoringCapability);

        if (!string.IsNullOrWhiteSpace(monitoring))
        {
            values.Add(monitoring);
        }

        return string.Join(
            " • ",
            values);
    }

    private static string OriginText(
        MapNodeOrigin origin)
    {
        switch (origin)
        {
            case MapNodeOrigin.Manual:
                return UiText.Get(
                    "NodeOriginManual");

            case MapNodeOrigin.Imported:
                return UiText.Get(
                    "NodeOriginImported");

            case MapNodeOrigin.Automatic:
                return UiText.Get(
                    "NodeOriginAutomatic");

            default:
                return null;
        }
    }

    private static string MonitoringText(
        MapMonitoringCapability capability)
    {
        return capability ==
               MapMonitoringCapability.None
            ? UiText.Get("MonitoringNone")
            : null;
    }

    private static string CategoryText(
        MapNodeCategory category)
    {
        switch (category)
        {
            case MapNodeCategory.MediaConverter:
                return UiText.Get(
                    "CategoryMediaConverter");

            case MapNodeCategory.UnmanagedSwitch:
                return UiText.Get(
                    "CategoryUnmanagedSwitch");

            case MapNodeCategory.OpticalConverter:
                return UiText.Get(
                    "CategoryOpticalConverter");

            case MapNodeCategory.PassiveNetworkEquipment:
                return UiText.Get(
                    "CategoryPassiveNetworkEquipment");

            default:
                return null;
        }
    }

    private static string BuildLocationText(
        MapNode node,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        if (!node.LocationId.HasValue)
        {
            return UiText.Get(
                "LocationUnassigned");
        }

        MapLocation location;

        if (!locations.TryGetValue(
            node.LocationId.Value,
            out location))
        {
            return UiText.Get(
                "LocationUnknown");
        }

        return UiText.Format(
            "LocationNamed",
            location.Name);
    }

    private static string ConfidenceText(
        MapConfidence confidence)
    {
        switch (confidence)
        {
            case MapConfidence.High:
                return UiText.Get(
                    "ConfidenceHigh");

            case MapConfidence.Medium:
                return UiText.Get(
                    "ConfidenceMedium");

            default:
                return UiText.Get(
                    "ConfidenceLow");
        }
    }

    private static string FreshnessText(
        MapFreshness freshness)
    {
        switch (freshness)
        {
            case MapFreshness.Fresh:
                return UiText.Get(
                    "FreshnessFresh");

            case MapFreshness.Aging:
                return UiText.Get(
                    "FreshnessAging");

            default:
                return UiText.Get(
                    "FreshnessStale");
        }
    }

    private sealed class LookupCandidateRow
    {
        public LookupCandidateRow(
            MacIpLookupCandidate candidate,
            string summary,
            string details)
        {
            Candidate = candidate;
            Summary = summary;
            Details = details;
        }

        public MacIpLookupCandidate Candidate { get; }

        public string Summary { get; }

        public string Details { get; }
    }

}
