using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NetLoom.Application.Alerts;
using NetLoom.Application.Lookup;
using NetLoom.Application.TopologyMap;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf;

public partial class MainWindow : Window
{
    private const double NodeWidth = 190.0;
    private const double NodeHeight = 92.0;
    private const int LookupCandidateLimit = 100;
    private const string CurrentStpInstanceId = "cist";

    private readonly TopologyRefreshCoordinator
        _topologyRefreshCoordinator;

    private readonly MacIpLookupSearchService
        _lookupSearchService;

    private readonly MacIpLookupRequestTracker
        _lookupRequestTracker;

    private readonly TopologyAlertTransitionTracker
        _alertTransitionTracker;

    private readonly TopologyRefreshStateTracker
        _refreshStateTracker;

    private readonly DispatcherTimer
        _refreshTimer;

    private readonly CancellationTokenSource
        _lifetimeCancellation =
            new CancellationTokenSource();

    private readonly Dictionary<Guid, Border>
        _nodeBordersByDeviceId =
            new Dictionary<Guid, Border>();

    private readonly Dictionary<string, MapNodeVisual>
        _nodeVisualsByIdentity =
            new Dictionary<string, MapNodeVisual>(
                StringComparer.Ordinal);

    private readonly Dictionary<string, MapLinkVisual>
        _linkVisualsByIdentity =
            new Dictionary<string, MapLinkVisual>(
                StringComparer.Ordinal);

    private Guid? _highlightedDeviceId;

    private MapSnapshot _lastMapSnapshot;

    public MainWindow()
        : this(
            new EmptyTopologyRefreshSnapshotProvider(),
            new EmptyMacIpLookupReader())
    {
    }

    public MainWindow(
        IMapSnapshotProvider mapSnapshotProvider)
        : this(
            new LegacyTopologyRefreshSnapshotProvider(
                mapSnapshotProvider,
                new EmptyTopologyAlertSnapshotProvider()),
            new EmptyMacIpLookupReader())
    {
    }

    public MainWindow(
        IMapSnapshotProvider mapSnapshotProvider,
        IMacIpLookupReader lookupReader)
        : this(
            new LegacyTopologyRefreshSnapshotProvider(
                mapSnapshotProvider,
                new EmptyTopologyAlertSnapshotProvider()),
            lookupReader)
    {
    }

    public MainWindow(
        IMapSnapshotProvider mapSnapshotProvider,
        IMacIpLookupReader lookupReader,
        ITopologyAlertSnapshotProvider alertSnapshotProvider)
        : this(
            new LegacyTopologyRefreshSnapshotProvider(
                mapSnapshotProvider,
                alertSnapshotProvider),
            lookupReader)
    {
    }

    public MainWindow(
        ITopologyRefreshSnapshotProvider topologyRefreshSnapshotProvider,
        IMacIpLookupReader lookupReader)
    {
        InitializeComponent();

        _topologyRefreshCoordinator =
            new TopologyRefreshCoordinator(
                topologyRefreshSnapshotProvider ??
                throw new ArgumentNullException(
                    nameof(topologyRefreshSnapshotProvider)));

        _lookupSearchService =
            new MacIpLookupSearchService(
                lookupReader ??
                throw new ArgumentNullException(
                    nameof(lookupReader)));

        _lookupRequestTracker =
            new MacIpLookupRequestTracker();

        _alertTransitionTracker =
            new TopologyAlertTransitionTracker();

        _refreshStateTracker =
            new TopologyRefreshStateTracker();

        _refreshTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromSeconds(5)
            };

        _refreshTimer.Tick +=
            OnRefreshTimerTick;

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;

        Title = UiText.Get("WindowTitle");
        MapTitleText.Text = UiText.Get("MapTitle");

        LookupTitleText.Text =
            UiText.Get("LookupTitle");

        LookupQueryLabelText.Text =
            UiText.Get("LookupQueryLabel");

        LookupSearchButton.Content =
            UiText.Get("LookupSearchAction");

        LookupResultsLabelText.Text =
            UiText.Get("LookupResultsLabel");

        LookupStatusText.Text =
            UiText.Get("LookupReady");

        LookupDetailsText.Text =
            UiText.Get("LookupSelectCandidate");

        AlertTitleText.Text =
            UiText.Get("AlertTitle");

        AlertStatusText.Text =
            UiText.Get("AlertNone");

        AlertTransitionText.Text =
            string.Empty;

        AlertList.ItemsSource =
            new AlertRow[0];

        _lastMapSnapshot =
            EmptySnapshot();

        ShowMap(
            _lastMapSnapshot);
    }

    private async void OnWindowLoaded(
        object sender,
        RoutedEventArgs e)
    {
        _refreshTimer.Start();

        await RefreshTopologyAsync();
    }

    private void OnWindowClosed(
        object sender,
        EventArgs e)
    {
        _refreshTimer.Stop();

        _topologyRefreshCoordinator.Close();
        _lookupRequestTracker.Close();
        _lifetimeCancellation.Cancel();
    }

    private async void OnRefreshTimerTick(
        object sender,
        EventArgs e)
    {
        await RefreshTopologyAsync();
    }

    private async Task RefreshTopologyAsync()
    {
        try
        {
            var cancellationToken =
                _lifetimeCancellation.Token;

            var refresh =
                await _topologyRefreshCoordinator
                    .RefreshAsync(
                        CurrentStpInstanceId,
                        cancellationToken);

            if (cancellationToken
                    .IsCancellationRequested ||
                refresh.Kind !=
                    TopologyRefreshExecutionKind.Succeeded)
            {
                return;
            }

            ApplyTopologyRefresh(
                refresh.Snapshot);
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellation
                .IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            if (_lifetimeCancellation
                .IsCancellationRequested)
            {
                return;
            }

            Trace.TraceError(
                error.ToString());

            ShowRefreshFailure(
                _refreshStateTracker
                    .ObserveFailure());
        }
    }

    private void ApplyTopologyRefresh(
        TopologyRefreshSnapshot refresh)
    {
        if (refresh == null)
        {
            throw new ArgumentNullException(
                nameof(refresh));
        }

        var state =
            _refreshStateTracker
                .ObserveSuccess(
                    refresh,
                    DateTime.UtcNow);

        _lastMapSnapshot =
            state.Snapshot.MapSnapshot;

        ShowMap(
            state.Snapshot.MapSnapshot);

        var transition =
            _alertTransitionTracker.Observe(
                state.Snapshot.AlertSnapshot);

        ShowAlerts(
            state.Snapshot.AlertSnapshot,
            transition);
    }

    private void ShowRefreshFailure(
        TopologyRefreshState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(
                nameof(state));
        }

        AlertTransitionText.Text =
            string.Empty;

        if (state.Kind ==
            TopologyRefreshStateKind.InitialFailure)
        {
            MapStatusText.Text =
                UiText.Get(
                    "TopologyRefreshInitialFailed");

            AlertStatusText.Text =
                UiText.Get(
                    "TopologyRefreshInitialFailed");

            return;
        }

        if (state.Kind !=
                TopologyRefreshStateKind.Stale ||
            !state.LastSuccessUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Refresh failure must be initial or stale.");
        }

        var lastSuccessLocal =
            state.LastSuccessUtc.Value
                .ToLocalTime();

        MapStatusText.Text =
            UiText.Format(
                "MapRefreshStale",
                lastSuccessLocal);

        AlertStatusText.Text =
            UiText.Format(
                "AlertRefreshStale",
                lastSuccessLocal);
    }

    private void RedrawCurrentMap()
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        ShowMap(
            _lastMapSnapshot);

        var state =
            _refreshStateTracker.Current;

        if (state.Kind ==
                TopologyRefreshStateKind.Stale &&
            state.LastSuccessUtc.HasValue)
        {
            MapStatusText.Text =
                UiText.Format(
                    "MapRefreshStale",
                    state.LastSuccessUtc.Value
                        .ToLocalTime());
        }
    }

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

    private static MapSnapshot EmptySnapshot()
    {
        return new MapSnapshot(
            DateTime.UtcNow,
            new MapNode[0],
            new MapLink[0]);
    }

    public void ShowMap(MapSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var nodes =
            snapshot.Nodes.ToDictionary(
                node => node.Key,
                StringComparer.Ordinal);

        var locations =
            snapshot.Locations.ToDictionary(
                location => location.Id);

        ReconcileNodes(
            snapshot.Nodes,
            locations);

        ReconcileLinks(
            snapshot.Links,
            nodes);

        if (snapshot.Nodes.Count == 0)
        {
            MapStatusText.Text =
                UiText.Get("MapNotLoaded");

            return;
        }

        MapStatusText.Text =
            UiText.Format(
                "MapSummary",
                UiText.FormatCount(
                    "MapNodeCount",
                    snapshot.Nodes.Count),
                UiText.FormatCount(
                    "MapLinkCount",
                    snapshot.Links.Count),
                UiText.FormatCount(
                    "MapLocationCount",
                    snapshot.Locations.Count));
    }

    private void ReconcileNodes(
        IReadOnlyList<MapNode> nodes,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var desiredIdentities =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            var identity =
                NodeIdentity(node);

            if (!desiredIdentities.Add(identity))
            {
                throw new InvalidOperationException(
                    "Map snapshot contains duplicate stable node identity.");
            }

            MapNodeVisual visual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    identity,
                    out visual))
            {
                visual =
                    CreateNodeVisual();

                _nodeVisualsByIdentity.Add(
                    identity,
                    visual);

                Canvas.SetLeft(
                    visual.Border,
                    node.X);

                Canvas.SetTop(
                    visual.Border,
                    node.Y);

                Panel.SetZIndex(
                    visual.Border,
                    2);

                MapCanvas.Children.Add(
                    visual.Border);
            }
            else
            {
                if (double.IsNaN(
                    Canvas.GetLeft(
                        visual.Border)))
                {
                    Canvas.SetLeft(
                        visual.Border,
                        node.X);
                }

                if (double.IsNaN(
                    Canvas.GetTop(
                        visual.Border)))
                {
                    Canvas.SetTop(
                        visual.Border,
                        node.Y);
                }
            }

            UpdateNodeVisual(
                visual,
                node,
                locations);
        }

        foreach (var identity in
            _nodeVisualsByIdentity.Keys
                .Where(
                    key =>
                        !desiredIdentities.Contains(
                            key))
                .ToArray())
        {
            var visual =
                _nodeVisualsByIdentity[
                    identity];

            MapCanvas.Children.Remove(
                visual.Border);

            _nodeVisualsByIdentity.Remove(
                identity);
        }

        _nodeBordersByDeviceId.Clear();

        foreach (var node in nodes)
        {
            if (!node.DeviceId.HasValue)
            {
                continue;
            }

            MapNodeVisual visual;

            if (_nodeVisualsByIdentity.TryGetValue(
                NodeIdentity(node),
                out visual))
            {
                _nodeBordersByDeviceId[
                    node.DeviceId.Value] =
                    visual.Border;
            }
        }
    }

    private void ReconcileLinks(
        IReadOnlyList<MapLink> links,
        IReadOnlyDictionary<string, MapNode> nodes)
    {
        var desiredIdentities =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var link in links)
        {
            MapNode source;
            MapNode target;

            if (!nodes.TryGetValue(
                    link.SourceNodeKey,
                    out source) ||
                !nodes.TryGetValue(
                    link.TargetNodeKey,
                    out target))
            {
                continue;
            }

            MapNodeVisual sourceVisual;
            MapNodeVisual targetVisual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(source),
                    out sourceVisual) ||
                !_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(target),
                    out targetVisual))
            {
                continue;
            }

            var identity =
                LinkIdentity(link);

            if (!desiredIdentities.Add(identity))
            {
                throw new InvalidOperationException(
                    "Map snapshot contains duplicate stable link identity.");
            }

            MapLinkVisual visual;

            if (!_linkVisualsByIdentity.TryGetValue(
                    identity,
                    out visual))
            {
                visual =
                    CreateLinkVisual();

                _linkVisualsByIdentity.Add(
                    identity,
                    visual);

                Panel.SetZIndex(
                    visual.Line,
                    0);

                Panel.SetZIndex(
                    visual.Label,
                    1);

                MapCanvas.Children.Add(
                    visual.Line);

                MapCanvas.Children.Add(
                    visual.Label);
            }

            UpdateLinkVisual(
                visual,
                link,
                sourceVisual,
                targetVisual);
        }

        foreach (var identity in
            _linkVisualsByIdentity.Keys
                .Where(
                    key =>
                        !desiredIdentities.Contains(
                            key))
                .ToArray())
        {
            var visual =
                _linkVisualsByIdentity[
                    identity];

            MapCanvas.Children.Remove(
                visual.Line);

            MapCanvas.Children.Remove(
                visual.Label);

            _linkVisualsByIdentity.Remove(
                identity);
        }
    }

    private static string NodeIdentity(
        MapNode node)
    {
        if (node.DeviceId.HasValue)
        {
            return
                "device:" +
                node.DeviceId.Value.ToString("D");
        }

        return
            "key:" +
            node.Key;
    }

    private static string LinkIdentity(
        MapLink link)
    {
        if (link.PhysicalLinkId.HasValue)
        {
            return
                "physical-link:" +
                link.PhysicalLinkId.Value.ToString("D");
        }

        return
            "key:" +
            link.Key;
    }

    private static MapNodeVisual
        CreateNodeVisual()
    {
        var title =
            new TextBlock
            {
                FontWeight =
                    FontWeights.SemiBold,
                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        var secondary =
            new TextBlock
            {
                Margin =
                    new Thickness(0, 5, 0, 0),
                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        var topologyMetadata =
            new TextBlock
            {
                Margin =
                    new Thickness(0, 4, 0, 0),
                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        var locationText =
            new TextBlock
            {
                Margin =
                    new Thickness(0, 4, 0, 0),
                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        var content =
            new StackPanel();

        content.Children.Add(title);
        content.Children.Add(secondary);
        content.Children.Add(topologyMetadata);
        content.Children.Add(locationText);

        var border =
            new Border
            {
                Width = NodeWidth,
                Height = NodeHeight,
                Padding = new Thickness(10),
                Background =
                    SystemColors.WindowBrush,
                Child = content
            };

        return new MapNodeVisual(
            border,
            title,
            secondary,
            topologyMetadata,
            locationText);
    }

    private void UpdateNodeVisual(
        MapNodeVisual visual,
        MapNode node,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        visual.Title.Text =
            DisplayNodeLabel(
                node);

        visual.Secondary.Text =
            string.IsNullOrWhiteSpace(
                node.SecondaryText)
                ? string.Empty
                : node.SecondaryText;

        visual.TopologyMetadata.Text =
            BuildTopologyMetadata(node);

        visual.Location.Text =
            BuildLocationText(
                node,
                locations);

        var isHighlighted =
            node.DeviceId.HasValue &&
            _highlightedDeviceId.HasValue &&
            node.DeviceId.Value ==
            _highlightedDeviceId.Value;

        visual.Border.BorderThickness =
            isHighlighted
                ? new Thickness(3)
                : new Thickness(1);

        visual.Border.BorderBrush =
            isHighlighted
                ? SystemColors.HighlightBrush
                : SystemColors.ControlDarkBrush;
    }

    private static MapLinkVisual
        CreateLinkVisual()
    {
        return new MapLinkVisual(
            new Line
            {
                Stroke =
                    SystemColors.ControlDarkBrush,
                StrokeThickness = 2.0
            },
            new TextBlock
            {
                Background =
                    SystemColors.WindowBrush,
                Padding =
                    new Thickness(4, 2, 4, 2)
            });
    }

    private static void UpdateLinkVisual(
        MapLinkVisual visual,
        MapLink link,
        MapNodeVisual source,
        MapNodeVisual target)
    {
        var x1 =
            NodeLeft(source) +
            (NodeWidth / 2.0);

        var y1 =
            NodeTop(source) +
            (NodeHeight / 2.0);

        var x2 =
            NodeLeft(target) +
            (NodeWidth / 2.0);

        var y2 =
            NodeTop(target) +
            (NodeHeight / 2.0);

        visual.Line.X1 = x1;
        visual.Line.Y1 = y1;
        visual.Line.X2 = x2;
        visual.Line.Y2 = y2;

        visual.Label.Text =
            BuildLinkLabel(link);

        Canvas.SetLeft(
            visual.Label,
            ((x1 + x2) / 2.0) - 45.0);

        Canvas.SetTop(
            visual.Label,
            ((y1 + y2) / 2.0) - 12.0);
    }

    private static double NodeLeft(
        MapNodeVisual visual)
    {
        var value =
            Canvas.GetLeft(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private static double NodeTop(
        MapNodeVisual visual)
    {
        var value =
            Canvas.GetTop(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private static string BuildLinkLabel(
        MapLink link)
    {
        var status =
            ConfidenceText(link.Confidence) +
            " • " +
            FreshnessText(link.Freshness) +
            " • " +
            UiText.FormatCount(
                "EvidenceCount",
                link.Evidence.Count);

        if (string.IsNullOrWhiteSpace(
                link.SourcePortLabel) &&
            string.IsNullOrWhiteSpace(
                link.TargetPortLabel))
        {
            return status;
        }

        return
            (link.SourcePortLabel ?? "?") +
            " ↔ " +
            (link.TargetPortLabel ?? "?") +
            " • " +
            status;
    }

    private static string DisplayNodeLabel(
        MapNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(
                nameof(node));
        }

        return string.Equals(
                node.Label,
                node.Key,
                StringComparison.Ordinal)
            ? UiText.Get(
                "NodeUnknownLabel")
            : node.Label;
    }

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

            RedrawCurrentMap();
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

    private sealed class MapNodeVisual
    {
        public MapNodeVisual(
            Border border,
            TextBlock title,
            TextBlock secondary,
            TextBlock topologyMetadata,
            TextBlock location)
        {
            Border = border;
            Title = title;
            Secondary = secondary;
            TopologyMetadata = topologyMetadata;
            Location = location;
        }

        public Border Border { get; }

        public TextBlock Title { get; }

        public TextBlock Secondary { get; }

        public TextBlock TopologyMetadata { get; }

        public TextBlock Location { get; }
    }

    private sealed class MapLinkVisual
    {
        public MapLinkVisual(
            Line line,
            TextBlock label)
        {
            Line = line;
            Label = label;
        }

        public Line Line { get; }

        public TextBlock Label { get; }
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

    private sealed class AlertRow
    {
        public AlertRow(
            string summary)
        {
            Summary = summary;
        }

        public string Summary { get; }
    }

    private sealed class LegacyTopologyRefreshSnapshotProvider :
        ITopologyRefreshSnapshotProvider
    {
        private readonly IMapSnapshotProvider
            _mapSnapshotProvider;

        private readonly ITopologyAlertSnapshotProvider
            _alertSnapshotProvider;

        public LegacyTopologyRefreshSnapshotProvider(
            IMapSnapshotProvider mapSnapshotProvider,
            ITopologyAlertSnapshotProvider alertSnapshotProvider)
        {
            _mapSnapshotProvider =
                mapSnapshotProvider ??
                throw new ArgumentNullException(
                    nameof(mapSnapshotProvider));

            _alertSnapshotProvider =
                alertSnapshotProvider ??
                throw new ArgumentNullException(
                    nameof(alertSnapshotProvider));
        }

        public TopologyRefreshSnapshot GetSnapshot(
            string stpInstanceId)
        {
            return new TopologyRefreshSnapshot(
                _mapSnapshotProvider.GetSnapshot(),
                _alertSnapshotProvider.GetSnapshot(
                    stpInstanceId));
        }
    }

    private sealed class EmptyTopologyRefreshSnapshotProvider :
        ITopologyRefreshSnapshotProvider
    {
        public TopologyRefreshSnapshot GetSnapshot(
            string stpInstanceId)
        {
            return new TopologyRefreshSnapshot(
                EmptySnapshot(),
                new TopologyAlertSnapshot(
                    DateTime.UtcNow,
                    stpInstanceId,
                    new TopologyAlert[0]));
        }
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

    private sealed class EmptyMapSnapshotProvider :
        IMapSnapshotProvider
    {
        public MapSnapshot GetSnapshot()
        {
            return EmptySnapshot();
        }
    }

    private sealed class EmptyMacIpLookupReader :
        IMacIpLookupReader
    {
        public MacIpLookupResult FindByMac(
            string macAddress,
            int maxCandidates)
        {
            return new MacIpLookupResult(
                MacIpLookupKind.Mac,
                macAddress,
                new MacIpLookupCandidate[0]);
        }

        public MacIpLookupResult FindByIp(
            string ipAddress,
            int maxCandidates)
        {
            return new MacIpLookupResult(
                MacIpLookupKind.Ip,
                ipAddress,
                new MacIpLookupCandidate[0]);
        }
    }
}
