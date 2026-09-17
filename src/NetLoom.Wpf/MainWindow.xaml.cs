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
    private const int LookupCandidateLimit = 100;
    private const string CurrentStpInstanceId = "cist";

    private readonly double _nodeWidth;
    private readonly double _nodeHeight;
    private readonly double _linkLabelPlacementStep;
    private readonly double _linkLabelCollisionMargin;
    private readonly double _zoomMin;
    private readonly double _zoomMax;
    private readonly double _zoomStep;
    private readonly double _fitPadding;
    private readonly double _virtualOriginX;
    private readonly double _virtualOriginY;

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

    private readonly IMapLayoutStore
        _mapLayoutStore;

    private readonly IManualTopologyService
        _manualTopologyService;

    private readonly Guid
        _mapLayoutId =
            MapLayoutScope.PhysicalTopologyMapId;

    private readonly Dictionary<Guid, MapDeviceLayout>
        _persistedDeviceLayouts =
            new Dictionary<Guid, MapDeviceLayout>();

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

    private MapMotionMode _motionMode =
        MapMotionMode.Normal;

    private MenuItem _motionNormalMenuItem;
    private MenuItem _motionReducedMenuItem;
    private MenuItem _motionOffMenuItem;

    private double _zoom = 1.0;

    private double _pendingPanX;

    private double _pendingPanY;

    private bool _suppressLockSelectedChange;

    private MapNodeVisual _dragNodeVisual;

    private Point _dragStartPoint;

    private double _dragStartLeft;

    private double _dragStartTop;

    private bool _dragMoved;

    private bool _isPanning;

    private Point _panStartPoint;

    private double _panStartHorizontalOffset;

    private double _panStartVerticalOffset;

    private Guid? _highlightedDeviceId;

    private Guid? _selectedDeviceId;

    private Guid? _selectedPhysicalLinkId;

    private MapSnapshot _lastMapSnapshot;

    private NetworkDiagnosticSnapshot _lastDiagnosticSnapshot;

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
        : this(
            topologyRefreshSnapshotProvider,
            lookupReader,
            new EmptyMapLayoutStore())
    {
    }

    public MainWindow(
        ITopologyRefreshSnapshotProvider topologyRefreshSnapshotProvider,
        IMacIpLookupReader lookupReader,
        IMapLayoutStore mapLayoutStore)
        : this(
            topologyRefreshSnapshotProvider,
            lookupReader,
            mapLayoutStore,
            new EmptyManualTopologyService())
    {
    }

    public MainWindow(
        ITopologyRefreshSnapshotProvider topologyRefreshSnapshotProvider,
        IMacIpLookupReader lookupReader,
        IMapLayoutStore mapLayoutStore,
        IManualTopologyService manualTopologyService)
    {
        InitializeComponent();

        _nodeWidth =
            GetDoubleResource(
                "NetLoom.Map.NodeWidth");

        _nodeHeight =
            GetDoubleResource(
                "NetLoom.Map.NodeHeight");

        _linkLabelPlacementStep =
            GetDoubleResource(
                "NetLoom.Map.LinkLabelPlacementStep");

        _linkLabelCollisionMargin =
            GetDoubleResource(
                "NetLoom.Map.LinkLabelCollisionMargin");

        _zoomMin =
            GetDoubleResource(
                "NetLoom.Map.ZoomMin");

        _zoomMax =
            GetDoubleResource(
                "NetLoom.Map.ZoomMax");

        _zoomStep =
            GetDoubleResource(
                "NetLoom.Map.ZoomStep");

        _fitPadding =
            GetDoubleResource(
                "NetLoom.Map.FitPadding");

        _virtualOriginX =
            GetDoubleResource(
                "NetLoom.Map.VirtualOriginX");

        _virtualOriginY =
            GetDoubleResource(
                "NetLoom.Map.VirtualOriginY");

        _mapLayoutStore =
            mapLayoutStore ??
            throw new ArgumentNullException(
                nameof(mapLayoutStore));

        _manualTopologyService =
            manualTopologyService ??
            throw new ArgumentNullException(
                nameof(manualTopologyService));

        LoadPersistedMapLayout();

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

        MapLockSelectedCheckBox.Content =
            UiText.Get("MapLockSelected");

        MapZoomLabelText.Text =
            UiText.Get("MapZoomLabel");

        MapZoomOutButton.Content =
            UiText.Get("MapZoomOutAction");

        MapZoomInButton.Content =
            UiText.Get("MapZoomInAction");

        MapFitAllButton.Content =
            UiText.Get("MapFitAllAction");

        ManualTopologyButton.Content =
            UiText.Get("ManualTopologyAction");

        ManualTopologyButton.ToolTip =
            UiText.Get("ManualTopologyHint");

        MapHelpButton.Content =
            UiText.Get("MapHelpAction");

        MapHelpButton.ToolTip =
            UiText.Get("MapInteractionHint");

        MapSettingsButton.Content =
            UiText.Get("MapSettingsAction");

        InitializeMapSettingsMenu();

        UpdateZoomText();
        UpdateMotionModeText();
        UpdateSelectedLayoutControl();
        ApplyZoomTransform();

        DiagnosticTitleText.Text =
            UiText.Get("DiagnosticTitle");

        DiagnosticStatusText.Text =
            UiText.Get("DiagnosticNothingSelected");

        DiagnosticElementTitleText.Text =
            string.Empty;

        DiagnosticElementSubtitleText.Text =
            string.Empty;

        DiagnosticPrimaryTitleText.Text =
            string.Empty;

        DiagnosticSecondaryTitleText.Text =
            string.Empty;

        DiagnosticTertiaryTitleText.Text =
            string.Empty;

        DiagnosticFieldsList.ItemsSource =
            new DiagnosticFieldRow[0];

        DiagnosticSecondaryList.ItemsSource =
            new DiagnosticTextRow[0];

        DiagnosticTertiaryList.ItemsSource =
            new DiagnosticTextRow[0];

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

        _lastDiagnosticSnapshot =
            EmptyDiagnosticSnapshot();

        ShowMap(
            _lastMapSnapshot);

        ShowSelectedDiagnostic();
    }

    private async void OnWindowLoaded(
        object sender,
        RoutedEventArgs e)
    {
        RestoreViewportOffsets();
        _refreshTimer.Start();

        await RefreshTopologyAsync();
    }

    private void OnWindowClosed(
        object sender,
        EventArgs e)
    {
        TrySaveViewportLayout();
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

        _lastDiagnosticSnapshot =
            state.Snapshot.DiagnosticSnapshot;

        ShowMap(
            state.Snapshot.MapSnapshot);

        ShowSelectedDiagnostic();

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

            DiagnosticStatusText.Text =
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

        DiagnosticStatusText.Text =
            UiText.Format(
                "DiagnosticRefreshStale",
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

    private void LoadPersistedMapLayout()
    {
        try
        {
            var snapshot =
                _mapLayoutStore.Load(
                    _mapLayoutId);

            if (snapshot == null)
            {
                return;
            }

            _zoom =
                ClampZoom(
                    snapshot.Viewport.Zoom);

            _pendingPanX =
                snapshot.Viewport.PanX;

            _pendingPanY =
                snapshot.Viewport.PanY;

            _persistedDeviceLayouts.Clear();

            foreach (var item in snapshot.Devices)
            {
                _persistedDeviceLayouts[item.DeviceId] =
                    item;
            }
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            _zoom = 1.0;
            _pendingPanX = 0.0;
            _pendingPanY = 0.0;
            _persistedDeviceLayouts.Clear();
        }
    }

    private void RestoreViewportOffsets()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    MapScrollViewer
                        .ScrollToHorizontalOffset(
                            MapVirtualWorkspace
                                .ToScrollOffset(
                                    _pendingPanX,
                                    _virtualOriginX,
                                    _zoom));

                    MapScrollViewer
                        .ScrollToVerticalOffset(
                            MapVirtualWorkspace
                                .ToScrollOffset(
                                    _pendingPanY,
                                    _virtualOriginY,
                                    _zoom));
                }));
    }

    private void ApplyZoomTransform()
    {
        MapCanvas.LayoutTransform =
            new ScaleTransform(
                _zoom,
                _zoom);
    }

    private double ClampZoom(
        double value)
    {
        return Math.Max(
            _zoomMin,
            Math.Min(
                _zoomMax,
                value));
    }

    private void UpdateZoomText()
    {
        MapZoomValueText.Text =
            Math.Round(
                    _zoom * 100.0)
                .ToString(
                    "0",
                    CultureInfo.CurrentCulture) +
            "%";
    }

    private void ChangeZoom(
        double requestedZoom)
    {
        var next =
            ClampZoom(
                requestedZoom);

        if (Math.Abs(next - _zoom) < 0.0001)
        {
            return;
        }

        var viewportWidth =
            MapScrollViewer.ViewportWidth;

        var viewportHeight =
            MapScrollViewer.ViewportHeight;

        var logicalCenterX =
            (_zoom <= 0.0)
                ? 0.0
                : (MapScrollViewer.HorizontalOffset +
                   (viewportWidth / 2.0)) / _zoom;

        var logicalCenterY =
            (_zoom <= 0.0)
                ? 0.0
                : (MapScrollViewer.VerticalOffset +
                   (viewportHeight / 2.0)) / _zoom;

        _zoom = next;
        ApplyZoomTransform();
        UpdateZoomText();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    MapScrollViewer
                        .ScrollToHorizontalOffset(
                            Math.Max(
                                0.0,
                                (logicalCenterX * _zoom) -
                                (viewportWidth / 2.0)));

                    MapScrollViewer
                        .ScrollToVerticalOffset(
                            Math.Max(
                                0.0,
                                (logicalCenterY * _zoom) -
                                (viewportHeight / 2.0)));

                    TrySaveViewportLayout();
                }));
    }

    private void FitTopologyToViewport()
    {
        var visuals =
            _nodeVisualsByIdentity.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .ToArray();

        if (visuals.Length == 0)
        {
            return;
        }

        var viewportWidth =
            MapScrollViewer.ViewportWidth;

        var viewportHeight =
            MapScrollViewer.ViewportHeight;

        if (viewportWidth <= 0.0 ||
            viewportHeight <= 0.0)
        {
            return;
        }

        var minX =
            visuals.Min(
                NodeLeft);

        var minY =
            visuals.Min(
                NodeTop);

        var maxX =
            visuals.Max(
                visual =>
                    NodeLeft(
                        visual) +
                    _nodeWidth);

        var maxY =
            visuals.Max(
                visual =>
                    NodeTop(
                        visual) +
                    _nodeHeight);

        var contentWidth =
            Math.Max(
                1.0,
                maxX -
                minX);

        var contentHeight =
            Math.Max(
                1.0,
                maxY -
                minY);

        var availableWidth =
            Math.Max(
                1.0,
                viewportWidth -
                (_fitPadding * 2.0));

        var availableHeight =
            Math.Max(
                1.0,
                viewportHeight -
                (_fitPadding * 2.0));

        _zoom =
            ClampZoom(
                Math.Min(
                    availableWidth /
                    contentWidth,
                    availableHeight /
                    contentHeight));

        var centerX =
            (minX +
             maxX) /
            2.0;

        var centerY =
            (minY +
             maxY) /
            2.0;

        ApplyZoomTransform();
        UpdateZoomText();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    MapScrollViewer
                        .ScrollToHorizontalOffset(
                            Math.Max(
                                0.0,
                                (centerX * _zoom) -
                                (viewportWidth / 2.0)));

                    MapScrollViewer
                        .ScrollToVerticalOffset(
                            Math.Max(
                                0.0,
                                (centerY * _zoom) -
                                (viewportHeight / 2.0)));

                    TrySaveViewportLayout();
                }));
    }

    private void TrySaveViewportLayout()
    {
        try
        {
            _mapLayoutStore.SaveViewport(
                _mapLayoutId,
                new MapViewportLayout(
                    _zoom,
                    MapVirtualWorkspace
                        .ToLogicalPan(
                            MapScrollViewer
                                .HorizontalOffset,
                            _virtualOriginX,
                            _zoom),
                    MapVirtualWorkspace
                        .ToLogicalPan(
                            MapScrollViewer
                                .VerticalOffset,
                            _virtualOriginY,
                            _zoom)));
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            MapStatusText.Text =
                UiText.Get(
                    "MapLayoutSaveFailed");
        }
    }

    private void TrySaveDeviceLayout(
        MapNodeVisual visual)
    {
        if (visual == null ||
            !visual.DeviceId.HasValue)
        {
            return;
        }

        var layout =
            new MapDeviceLayout(
                visual.DeviceId.Value,
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        NodeLeft(
                            visual),
                        _virtualOriginX),
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        NodeTop(
                            visual),
                        _virtualOriginY),
                visual.IsLocked);

        try
        {
            _mapLayoutStore.SaveDevice(
                _mapLayoutId,
                layout);

            _persistedDeviceLayouts[
                layout.DeviceId] =
                layout;
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            MapStatusText.Text =
                UiText.Get(
                    "MapLayoutSaveFailed");
        }
    }

    private void InitializeMapSettingsMenu()
    {
        _motionNormalMenuItem =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionNormalAction"),
                IsCheckable = true,
                ToolTip =
                    UiText.Get(
                        "MapMotionNormalHint")
            };

        _motionNormalMenuItem.Click +=
            OnMapMotionNormalClick;

        _motionReducedMenuItem =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionReducedAction"),
                IsCheckable = true,
                ToolTip =
                    UiText.Get(
                        "MapMotionReducedHint")
            };

        _motionReducedMenuItem.Click +=
            OnMapMotionReducedClick;

        _motionOffMenuItem =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionOffAction"),
                IsCheckable = true,
                ToolTip =
                    UiText.Get(
                        "MapMotionOffHint")
            };

        _motionOffMenuItem.Click +=
            OnMapMotionOffClick;

        var motionMenu =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "MapMotionSettings")
            };

        motionMenu.Items.Add(
            _motionNormalMenuItem);

        motionMenu.Items.Add(
            _motionReducedMenuItem);

        motionMenu.Items.Add(
            _motionOffMenuItem);

        var settingsMenu =
            new ContextMenu
            {
                Placement =
                    PlacementMode.Bottom,
                PlacementTarget =
                    MapSettingsButton
            };

        settingsMenu.Items.Add(
            motionMenu);

        MapSettingsButton.ContextMenu =
            settingsMenu;

        MapSettingsButton.ToolTip =
            UiText.Get(
                "MapSettingsHint");
    }

    private void UpdateMotionModeText()
    {
        if (_motionNormalMenuItem == null ||
            _motionReducedMenuItem == null ||
            _motionOffMenuItem == null)
        {
            return;
        }

        _motionNormalMenuItem.IsChecked =
            _motionMode ==
            MapMotionMode.Normal;

        _motionReducedMenuItem.IsChecked =
            _motionMode ==
            MapMotionMode.Reduced;

        _motionOffMenuItem.IsChecked =
            _motionMode ==
            MapMotionMode.Off;
    }

    private void SetMotionMode(
        MapMotionMode mode)
    {
        _motionMode = mode;

        if (_motionMode ==
            MapMotionMode.Off)
        {
            StopAllMotion();
        }

        UpdateMotionModeText();
    }

    private void UpdateSelectedLayoutControl()
    {
        _suppressLockSelectedChange = true;

        try
        {
            MapNodeVisual visual = null;

            if (_selectedDeviceId.HasValue)
            {
                visual =
                    _nodeVisualsByIdentity.Values
                        .FirstOrDefault(
                            item =>
                                item.DeviceId.HasValue &&
                                item.DeviceId.Value ==
                                    _selectedDeviceId.Value);
            }

            MapLockSelectedCheckBox.IsEnabled =
                visual != null;

            MapLockSelectedCheckBox.IsChecked =
                visual != null &&
                visual.IsLocked;
        }
        finally
        {
            _suppressLockSelectedChange = false;
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

    private void AnimateAppearance(
        UIElement element)
    {
        if (element == null)
        {
            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                MapMotionKind.Appearance);

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 1.0;
            return;
        }

        var start =
            MapMotionPolicy.PulseOpacity(
                _motionMode);

        element.Opacity = start;

        var animation =
            new DoubleAnimation(
                start,
                1.0,
                new Duration(duration));

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity = 1.0;
            };

        element.BeginAnimation(
            UIElement.OpacityProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateRemoval(
        UIElement element,
        Action remove)
    {
        if (element == null)
        {
            remove?.Invoke();
            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                MapMotionKind.Disappearance);

        if (duration == TimeSpan.Zero)
        {
            remove?.Invoke();
            return;
        }

        var animation =
            new DoubleAnimation(
                element.Opacity,
                0.0,
                new Duration(duration));

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity = 1.0;
                remove?.Invoke();
            };

        element.BeginAnimation(
            UIElement.OpacityProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimatePulse(
        UIElement element,
        MapMotionKind kind)
    {
        if (element == null)
        {
            return;
        }

        var duration =
            MapMotionPolicy.Duration(
                _motionMode,
                kind);

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 1.0;
            return;
        }

        var halfDuration =
            TimeSpan.FromTicks(
                Math.Max(
                    1L,
                    duration.Ticks / 2L));

        var animation =
            new DoubleAnimation(
                1.0,
                MapMotionPolicy.PulseOpacity(
                    _motionMode),
                new Duration(
                    halfDuration))
            {
                AutoReverse = true
            };

        animation.Completed +=
            (sender, args) =>
            {
                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                element.Opacity = 1.0;
            };

        element.BeginAnimation(
            UIElement.OpacityProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void StopAllMotion()
    {
        foreach (var visual in
            _nodeVisualsByIdentity.Values)
        {
            StopMotion(
                visual.Border);
        }

        foreach (var visual in
            _linkVisualsByIdentity.Values)
        {
            StopMotion(
                visual.Line);

            StopMotion(
                visual.Label);
        }

        StopMotion(
            AlertTransitionText);
    }

    private static void StopMotion(
        UIElement element)
    {
        if (element == null)
        {
            return;
        }

        element.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        element.Opacity = 1.0;
    }

    private double GetDoubleResource(
        string key)
    {
        var value =
            FindResource(key);

        if (!(value is double))
        {
            throw new InvalidOperationException(
                "WPF design resource is not a double: " +
                key);
        }

        return (double)value;
    }

    private Thickness GetThicknessResource(
        string key)
    {
        var value =
            FindResource(key);

        if (!(value is Thickness))
        {
            throw new InvalidOperationException(
                "WPF design resource is not a Thickness: " +
                key);
        }

        return (Thickness)value;
    }

    private Style GetStyleResource(
        string key)
    {
        var value =
            FindResource(key);

        var style =
            value as Style;

        if (style == null)
        {
            throw new InvalidOperationException(
                "WPF design resource is not a Style: " +
                key);
        }

        return style;
    }

    private static MapSnapshot EmptySnapshot()
    {
        return new MapSnapshot(
            DateTime.UtcNow,
            new MapNode[0],
            new MapLink[0]);
    }

    private static NetworkDiagnosticSnapshot
        EmptyDiagnosticSnapshot()
    {
        return new NetworkDiagnosticSnapshot(
            DateTime.UtcNow,
            new DeviceDiagnostic[0],
            new PhysicalLinkDiagnostic[0]);
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

        UpdateSelectedLayoutControl();

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
            var created = false;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    identity,
                    out visual))
            {
                visual =
                    CreateNodeVisual();

                created = true;

                _nodeVisualsByIdentity.Add(
                    identity,
                    visual);

                var left = node.X;
                var top = node.Y;

                if (node.DeviceId.HasValue)
                {
                    MapDeviceLayout persisted;

                    if (_persistedDeviceLayouts.TryGetValue(
                            node.DeviceId.Value,
                            out persisted))
                    {
                        left = persisted.X;
                        top = persisted.Y;
                    }
                }

                Canvas.SetLeft(
                    visual.Border,
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            left,
                            _virtualOriginX));

                Canvas.SetTop(
                    visual.Border,
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            top,
                            _virtualOriginY));

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
                        MapVirtualWorkspace
                            .ToCanvasCoordinate(
                                node.X,
                                _virtualOriginX));
                }

                if (double.IsNaN(
                    Canvas.GetTop(
                        visual.Border)))
                {
                    Canvas.SetTop(
                        visual.Border,
                        MapVirtualWorkspace
                            .ToCanvasCoordinate(
                                node.Y,
                                _virtualOriginY));
                }
            }

            UpdateNodeVisual(
                visual,
                node,
                locations);

            if (created)
            {
                AnimateAppearance(
                    visual.Border);
            }
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

            _nodeVisualsByIdentity.Remove(
                identity);

            AnimateRemoval(
                visual.Border,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Border));
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
            var created = false;

            if (!_linkVisualsByIdentity.TryGetValue(
                    identity,
                    out visual))
            {
                visual =
                    CreateLinkVisual();

                created = true;

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

            if (created)
            {
                AnimateAppearance(
                    visual.Line);

                AnimateAppearance(
                    visual.Label);
            }
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

            _linkVisualsByIdentity.Remove(
                identity);

            AnimateRemoval(
                visual.Line,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Line));

            AnimateRemoval(
                visual.Label,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Label));
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

    private MapNodeVisual
        CreateNodeVisual()
    {
        var title =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeTitle")
            };

        var secondary =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeSecondary")
            };

        var topologyMetadata =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta")
            };

        var locationText =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta")
            };

        var lockBadge =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeLockBadge"),
                Text =
                    UiText.Get(
                        "MapNodeLockedBadge"),
                Visibility =
                    Visibility.Collapsed
            };

        var header =
            new DockPanel();

        DockPanel.SetDock(
            lockBadge,
            Dock.Right);

        header.Children.Add(
            lockBadge);

        header.Children.Add(
            title);

        var content =
            new StackPanel();

        content.Children.Add(header);
        content.Children.Add(secondary);
        content.Children.Add(topologyMetadata);
        content.Children.Add(locationText);

        var border =
            new Border
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeCard"),
                Child = content,
                Cursor = Cursors.Hand
            };

        border.MouseLeftButtonDown +=
            OnMapNodeMouseLeftButtonDown;

        border.MouseMove +=
            OnMapNodeMouseMove;

        border.MouseLeftButtonUp +=
            OnMapNodeMouseLeftButtonUp;

        return new MapNodeVisual(
            border,
            title,
            secondary,
            topologyMetadata,
            locationText,
            lockBadge);
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

        visual.DeviceId =
            node.DeviceId;

        visual.IsLocked = false;

        if (node.DeviceId.HasValue)
        {
            MapDeviceLayout persisted;

            if (_persistedDeviceLayouts.TryGetValue(
                    node.DeviceId.Value,
                    out persisted))
            {
                visual.IsLocked =
                    persisted.IsLocked;
            }
        }

        UpdateNodeLockPresentation(
            visual);

        visual.Border.Tag =
            node.DeviceId;

        var isHighlighted =
            node.DeviceId.HasValue &&
            ((_highlightedDeviceId.HasValue &&
              node.DeviceId.Value ==
                  _highlightedDeviceId.Value) ||
             (_selectedDeviceId.HasValue &&
              node.DeviceId.Value ==
                  _selectedDeviceId.Value));

        if (isHighlighted)
        {
            visual.Border.BorderThickness =
                GetThicknessResource(
                    "NetLoom.Thickness.BorderFocus");

            visual.Border.SetResourceReference(
                Border.BorderBrushProperty,
                "NetLoom.Brush.Selection");
        }
        else
        {
            visual.Border.ClearValue(
                Border.BorderThicknessProperty);

            visual.Border.ClearValue(
                Border.BorderBrushProperty);
        }
    }

    private MapLinkVisual
        CreateLinkVisual()
    {
        var line =
            new Line
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLink"),
                Cursor = Cursors.Hand
            };

        var label =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLinkLabel"),
                Cursor = Cursors.Hand
            };

        line.MouseLeftButtonDown +=
            OnMapLinkMouseLeftButtonDown;

        label.MouseLeftButtonDown +=
            OnMapLinkMouseLeftButtonDown;

        return new MapLinkVisual(
            line,
            label);
    }

    private void UpdateLinkVisual(
        MapLinkVisual visual,
        MapLink link,
        MapNodeVisual source,
        MapNodeVisual target)
    {
        var freshnessChanged =
            visual.LastFreshness.HasValue &&
            visual.LastFreshness.Value !=
                link.Freshness;

        var x1 =
            NodeLeft(source) +
            (_nodeWidth / 2.0);

        var y1 =
            NodeTop(source) +
            (_nodeHeight / 2.0);

        var x2 =
            NodeLeft(target) +
            (_nodeWidth / 2.0);

        var y2 =
            NodeTop(target) +
            (_nodeHeight / 2.0);

        visual.Line.X1 = x1;
        visual.Line.Y1 = y1;
        visual.Line.X2 = x2;
        visual.Line.Y2 = y2;

        visual.Label.Text =
            BuildLinkLabel(link);

        visual.Line.Tag =
            link.PhysicalLinkId;

        visual.Label.Tag =
            link.PhysicalLinkId;

        var isSelected =
            link.PhysicalLinkId.HasValue &&
            _selectedPhysicalLinkId.HasValue &&
            link.PhysicalLinkId.Value ==
                _selectedPhysicalLinkId.Value;

        if (isSelected)
        {
            visual.Line.SetResourceReference(
                Shape.StrokeProperty,
                "NetLoom.Brush.Selection");

            visual.Line.StrokeThickness =
                GetDoubleResource(
                    "NetLoom.Map.LinkSelectedStrokeThickness");

            visual.Label.SetResourceReference(
                TextBlock.ForegroundProperty,
                "NetLoom.Brush.Selection");
        }
        else
        {
            visual.Line.ClearValue(
                Shape.StrokeProperty);

            visual.Line.ClearValue(
                Shape.StrokeThicknessProperty);

            visual.Label.ClearValue(
                TextBlock.ForegroundProperty);
        }

        PlaceLinkLabel(
            visual.Label,
            x1,
            y1,
            x2,
            y2);

        if (freshnessChanged)
        {
            AnimatePulse(
                visual.Line,
                MapMotionKind.FreshnessChange);

            AnimatePulse(
                visual.Label,
                MapMotionKind.FreshnessChange);
        }

        visual.LastFreshness =
            link.Freshness;
    }

    private void PlaceLinkLabel(
        TextBlock label,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        label.Measure(
            new Size(
                double.PositiveInfinity,
                double.PositiveInfinity));

        var labelWidth =
            label.DesiredSize.Width;

        var labelHeight =
            label.DesiredSize.Height;

        var centerX =
            (x1 + x2) / 2.0;

        var centerY =
            (y1 + y2) / 2.0;

        var deltaX =
            x2 - x1;

        var deltaY =
            y2 - y1;

        var length =
            Math.Sqrt(
                (deltaX * deltaX) +
                (deltaY * deltaY));

        var normalX = 0.0;
        var normalY = -1.0;

        if (length > 0.001)
        {
            normalX =
                deltaY / length;

            normalY =
                -deltaX / length;

            if (normalY > 0.0 ||
                (Math.Abs(normalY) <= 0.001 &&
                 normalX < 0.0))
            {
                normalX = -normalX;
                normalY = -normalY;
            }
        }

        var obstacles =
            _nodeVisualsByIdentity.Values
                .Select(NodeBounds)
                .ToArray();

        const int maxPlacementSteps = 40;

        for (var step = 0;
             step <= maxPlacementSteps;
             step++)
        {
            if (step == 0)
            {
                if (TryPlaceLinkLabel(
                    label,
                    centerX,
                    centerY,
                    labelWidth,
                    labelHeight,
                    obstacles,
                    _linkLabelCollisionMargin))
                {
                    return;
                }

                continue;
            }

            var offset =
                _linkLabelPlacementStep * step;

            if (TryPlaceLinkLabel(
                label,
                centerX + (normalX * offset),
                centerY + (normalY * offset),
                labelWidth,
                labelHeight,
                obstacles,
                _linkLabelCollisionMargin))
            {
                return;
            }

            if (TryPlaceLinkLabel(
                label,
                centerX - (normalX * offset),
                centerY - (normalY * offset),
                labelWidth,
                labelHeight,
                obstacles,
                _linkLabelCollisionMargin))
            {
                return;
            }
        }

        Canvas.SetLeft(
            label,
            centerX - (labelWidth / 2.0));

        Canvas.SetTop(
            label,
            centerY - (labelHeight / 2.0));
    }

    private static bool TryPlaceLinkLabel(
        TextBlock label,
        double centerX,
        double centerY,
        double labelWidth,
        double labelHeight,
        IReadOnlyList<Rect> obstacles,
        double collisionMargin)
    {
        var left =
            centerX -
            (labelWidth / 2.0);

        var top =
            centerY -
            (labelHeight / 2.0);

        var labelBounds =
            new Rect(
                left,
                top,
                labelWidth,
                labelHeight);

        foreach (var obstacle in obstacles)
        {
            var collisionBounds =
                obstacle;

            collisionBounds.Inflate(
                collisionMargin,
                collisionMargin);

            if (collisionBounds.IntersectsWith(
                labelBounds))
            {
                return false;
            }
        }

        Canvas.SetLeft(
            label,
            left);

        Canvas.SetTop(
            label,
            top);

        return true;
    }

    private Rect NodeBounds(
        MapNodeVisual visual)
    {
        return new Rect(
            NodeLeft(visual),
            NodeTop(visual),
            _nodeWidth,
            _nodeHeight);
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

    private static void UpdateNodeLockPresentation(
        MapNodeVisual visual)
    {
        visual.Border.Cursor =
            visual.IsLocked
                ? Cursors.Hand
                : Cursors.SizeAll;

        visual.LockBadge.Visibility =
            visual.IsLocked
                ? Visibility.Visible
                : Visibility.Collapsed;

        visual.Border.ToolTip =
            visual.IsLocked
                ? UiText.Get(
                    "MapNodeLockedHint")
                : null;
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

    private void OnMapNodeMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        _highlightedDeviceId = null;

        _selectedDeviceId =
            (Guid)element.Tag;

        _selectedPhysicalLinkId =
            null;

        var visual =
            _nodeVisualsByIdentity.Values
                .FirstOrDefault(
                    item =>
                        ReferenceEquals(
                            item.Border,
                            element));

        if (visual != null &&
            !visual.IsLocked)
        {
            _dragNodeVisual =
                visual;

            _dragStartPoint =
                e.GetPosition(
                    MapCanvas);

            _dragStartLeft =
                NodeLeft(visual);

            _dragStartTop =
                NodeTop(visual);

            _dragMoved = false;

            visual.Border.CaptureMouse();
        }

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
        e.Handled = true;
    }

    private void OnMapNodeMouseMove(
        object sender,
        MouseEventArgs e)
    {
        var element =
            sender as Border;

        if (_dragNodeVisual == null ||
            element == null ||
            !ReferenceEquals(
                _dragNodeVisual.Border,
                element) ||
            e.LeftButton !=
                MouseButtonState.Pressed)
        {
            return;
        }

        var current =
            e.GetPosition(
                MapCanvas);

        var deltaX =
            current.X -
            _dragStartPoint.X;

        var deltaY =
            current.Y -
            _dragStartPoint.Y;

        if (!_dragMoved &&
            Math.Abs(deltaX) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(deltaY) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragMoved = true;

        var canvasWidth =
            MapCanvas.ActualWidth > 0.0
                ? MapCanvas.ActualWidth
                : MapCanvas.Width;

        var canvasHeight =
            MapCanvas.ActualHeight > 0.0
                ? MapCanvas.ActualHeight
                : MapCanvas.Height;

        var left =
            Math.Max(
                0.0,
                Math.Min(
                    Math.Max(
                        0.0,
                        canvasWidth -
                        _nodeWidth),
                    _dragStartLeft +
                    deltaX));

        var top =
            Math.Max(
                0.0,
                Math.Min(
                    Math.Max(
                        0.0,
                        canvasHeight -
                        _nodeHeight),
                    _dragStartTop +
                    deltaY));

        Canvas.SetLeft(
            _dragNodeVisual.Border,
            left);

        Canvas.SetTop(
            _dragNodeVisual.Border,
            top);

        UpdateLinksForCurrentNodePositions();
        e.Handled = true;
    }

    private void OnMapNodeMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_dragNodeVisual == null)
        {
            return;
        }

        var visual =
            _dragNodeVisual;

        _dragNodeVisual = null;

        if (visual.Border.IsMouseCaptured)
        {
            visual.Border.ReleaseMouseCapture();
        }

        if (_dragMoved)
        {
            TrySaveDeviceLayout(
                visual);
        }

        _dragMoved = false;
        e.Handled = true;
    }

    private void UpdateLinksForCurrentNodePositions()
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        var nodes =
            _lastMapSnapshot.Nodes
                .ToDictionary(
                    node => node.Key,
                    StringComparer.Ordinal);

        ReconcileLinks(
            _lastMapSnapshot.Links,
            nodes);
    }

    private void OnMapZoomOutClick(
        object sender,
        RoutedEventArgs e)
    {
        ChangeZoom(
            _zoom -
            _zoomStep);
    }

    private void OnMapZoomInClick(
        object sender,
        RoutedEventArgs e)
    {
        ChangeZoom(
            _zoom +
            _zoomStep);
    }

    private void OnMapFitAllClick(
        object sender,
        RoutedEventArgs e)
    {
        FitTopologyToViewport();
    }

    private async void OnManualTopologyClick(
        object sender,
        RoutedEventArgs e)
    {
        var editor =
            new ManualTopologyWindow(
                _manualTopologyService)
            {
                Owner = this
            };

        editor.ShowDialog();

        if (editor.HasChanges)
        {
            await RefreshTopologyAsync();
        }
    }

    private void OnMapHelpClick(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            UiText.Get(
                "MapHelpBody"),
            UiText.Get(
                "MapHelpTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnMapSettingsClick(
        object sender,
        RoutedEventArgs e)
    {
        if (MapSettingsButton.ContextMenu == null)
        {
            return;
        }

        MapSettingsButton.ContextMenu.PlacementTarget =
            MapSettingsButton;

        MapSettingsButton.ContextMenu.IsOpen =
            true;
    }

    private void OnMapMotionNormalClick(
        object sender,
        RoutedEventArgs e)
    {
        SetMotionMode(
            MapMotionMode.Normal);
    }

    private void OnMapMotionReducedClick(
        object sender,
        RoutedEventArgs e)
    {
        SetMotionMode(
            MapMotionMode.Reduced);
    }

    private void OnMapMotionOffClick(
        object sender,
        RoutedEventArgs e)
    {
        SetMotionMode(
            MapMotionMode.Off);
    }

    private void OnMapLockSelectedChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (_suppressLockSelectedChange ||
            !_selectedDeviceId.HasValue)
        {
            return;
        }

        var visual =
            _nodeVisualsByIdentity.Values
                .FirstOrDefault(
                    item =>
                        item.DeviceId.HasValue &&
                        item.DeviceId.Value ==
                            _selectedDeviceId.Value);

        if (visual == null)
        {
            return;
        }

        visual.IsLocked =
            MapLockSelectedCheckBox.IsChecked ==
            true;

        UpdateNodeLockPresentation(
            visual);

        TrySaveDeviceLayout(
            visual);
    }

    private void OnMapPreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        ChangeZoom(
            _zoom +
            (e.Delta > 0
                ? _zoomStep
                : -_zoomStep));

        e.Handled = true;
    }

    private void OnMapPreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Middle)
        {
            return;
        }

        _isPanning = true;
        _panStartPoint =
            e.GetPosition(
                MapScrollViewer);

        _panStartHorizontalOffset =
            MapScrollViewer.HorizontalOffset;

        _panStartVerticalOffset =
            MapScrollViewer.VerticalOffset;

        MapScrollViewer.Cursor =
            Cursors.SizeAll;

        MapScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void OnMapPreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!_isPanning ||
            e.MiddleButton !=
                MouseButtonState.Pressed)
        {
            return;
        }

        var current =
            e.GetPosition(
                MapScrollViewer);

        MapScrollViewer
            .ScrollToHorizontalOffset(
                Math.Max(
                    0.0,
                    _panStartHorizontalOffset -
                    (current.X -
                     _panStartPoint.X)));

        MapScrollViewer
            .ScrollToVerticalOffset(
                Math.Max(
                    0.0,
                    _panStartVerticalOffset -
                    (current.Y -
                     _panStartPoint.Y)));

        e.Handled = true;
    }

    private void OnMapPreviewMouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_isPanning ||
            e.ChangedButton !=
                MouseButton.Middle)
        {
            return;
        }

        _isPanning = false;

        if (MapScrollViewer.IsMouseCaptured)
        {
            MapScrollViewer.ReleaseMouseCapture();
        }

        MapScrollViewer.Cursor = null;

        TrySaveViewportLayout();
        e.Handled = true;
    }

    private void OnMapLinkMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        _highlightedDeviceId = null;

        _selectedDeviceId =
            null;

        _selectedPhysicalLinkId =
            (Guid)element.Tag;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
        e.Handled = true;
    }

    private void OnMapCanvasMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(
            e.OriginalSource,
            MapCanvas))
        {
            return;
        }

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedPhysicalLinkId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private void ShowSelectedDiagnostic()
    {
        if (_lastDiagnosticSnapshot == null)
        {
            ClearDiagnosticPanel(
                "DiagnosticNothingSelected");
            return;
        }

        if (_selectedDeviceId.HasValue)
        {
            var device =
                _lastDiagnosticSnapshot.Devices
                    .FirstOrDefault(
                        item =>
                            item.DeviceId ==
                            _selectedDeviceId.Value);

            if (device == null)
            {
                _selectedDeviceId = null;
                ClearDiagnosticPanel(
                    "DiagnosticSelectionMissing");
                return;
            }

            ShowDeviceDiagnostic(device);
            return;
        }

        if (_selectedPhysicalLinkId.HasValue)
        {
            var link =
                _lastDiagnosticSnapshot.Links
                    .FirstOrDefault(
                        item =>
                            item.PhysicalLinkId ==
                            _selectedPhysicalLinkId.Value);

            if (link == null)
            {
                _selectedPhysicalLinkId = null;
                ClearDiagnosticPanel(
                    "DiagnosticSelectionMissing");
                return;
            }

            ShowLinkDiagnostic(link);
            return;
        }

        ClearDiagnosticPanel(
            "DiagnosticNothingSelected");
    }

    private void ClearDiagnosticPanel(
        string statusKey)
    {
        DiagnosticStatusText.Text =
            UiText.Get(statusKey);

        DiagnosticElementTitleText.Text =
            string.Empty;

        DiagnosticElementSubtitleText.Text =
            string.Empty;

        DiagnosticPrimaryTitleText.Text =
            string.Empty;

        DiagnosticSecondaryTitleText.Text =
            string.Empty;

        DiagnosticTertiaryTitleText.Text =
            string.Empty;

        DiagnosticFieldsList.ItemsSource =
            new DiagnosticFieldRow[0];

        DiagnosticSecondaryList.ItemsSource =
            new DiagnosticTextRow[0];

        DiagnosticTertiaryList.ItemsSource =
            new DiagnosticTextRow[0];

        UpdateSelectedLayoutControl();
    }

    private void ShowDeviceDiagnostic(
        DeviceDiagnostic device)
    {
        DiagnosticStatusText.Text =
            UiText.Get("DiagnosticCurrent");

        DiagnosticElementTitleText.Text =
            string.IsNullOrWhiteSpace(
                device.DisplayName)
                ? UiText.Get("NodeUnknownLabel")
                : device.DisplayName;

        DiagnosticElementSubtitleText.Text =
            string.IsNullOrWhiteSpace(
                device.SecondaryText)
                ? string.Empty
                : device.SecondaryText;

        DiagnosticPrimaryTitleText.Text =
            UiText.Get("DiagnosticStateTitle");

        var degradedCount =
            device.Interfaces.Count(
                item =>
                    item.DegradationStatus ==
                    DiagnosticDegradationStatus.Degraded);

        DiagnosticFieldsList.ItemsSource =
            new[]
            {
                Field(
                    "DiagnosticFieldLocation",
                    device.LocationName),
                Field(
                    "DiagnosticFieldLastSeen",
                    LocalTimeText(
                        device.LastSeenUtc)),
                Field(
                    "DiagnosticFieldLastResolved",
                    LocalTimeText(
                        device.LastResolvedUtc)),
                Field(
                    "DiagnosticFieldInterfaces",
                    device.Interfaces.Count.ToString(
                        CultureInfo.CurrentCulture)),
                Field(
                    "DiagnosticFieldDegradedInterfaces",
                    degradedCount.ToString(
                        CultureInfo.CurrentCulture))
            };

        DiagnosticSecondaryTitleText.Text =
            UiText.Get("DiagnosticInterfacesTitle");

        DiagnosticSecondaryList.ItemsSource =
            device.Interfaces.Count == 0
                ? new[]
                {
                    Row("DiagnosticNoInterfaces")
                }
                : device.Interfaces
                    .Select(
                        item =>
                            new DiagnosticTextRow(
                                BuildInterfaceDiagnosticText(
                                    item)))
                    .ToArray();

        DiagnosticTertiaryTitleText.Text =
            UiText.Get("DiagnosticConnectionsTitle");

        var connectedLinks =
            _lastDiagnosticSnapshot.Links
                .Where(
                    item =>
                        item.DeviceAId == device.DeviceId ||
                        item.DeviceBId == device.DeviceId)
                .OrderBy(
                    item =>
                        PeerName(
                            item,
                            device.DeviceId),
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(
                    item =>
                        new DiagnosticTextRow(
                            BuildDeviceLinkDiagnosticText(
                                device.DeviceId,
                                item)))
                .ToArray();

        DiagnosticTertiaryList.ItemsSource =
            connectedLinks.Length == 0
                ? new[]
                {
                    Row("DiagnosticNoConnections")
                }
                : connectedLinks;
    }

    private void ShowLinkDiagnostic(
        PhysicalLinkDiagnostic link)
    {
        DiagnosticStatusText.Text =
            UiText.Get("DiagnosticCurrent");

        DiagnosticElementTitleText.Text =
            UiText.Format(
                "DiagnosticLinkTitle",
                DisplayDeviceName(
                    link.DeviceAName),
                DisplayDeviceName(
                    link.DeviceBName));

        DiagnosticElementSubtitleText.Text =
            UiText.Format(
                "DiagnosticLinkPorts",
                DisplayInterfaceName(
                    link.InterfaceAName),
                DisplayInterfaceName(
                    link.InterfaceBName));

        DiagnosticPrimaryTitleText.Text =
            UiText.Get("DiagnosticStateTitle");

        DiagnosticFieldsList.ItemsSource =
            new[]
            {
                Field(
                    "DiagnosticFieldStrength",
                    LinkStrengthText(
                        link.Strength)),
                Field(
                    "DiagnosticFieldFreshness",
                    FreshnessText(
                        link.Freshness)),
                Field(
                    "DiagnosticFieldLastSeen",
                    LocalTimeText(
                        link.LastSeenUtc)),
                Field(
                    "DiagnosticFieldLastConfirmed",
                    LocalTimeText(
                        link.LastConfirmedUtc)),
                Field(
                    "DiagnosticFieldMedia",
                    link.MediaType),
                Field(
                    "DiagnosticFieldSpeed",
                    SpeedText(
                        link.SpeedBps)),
                Field(
                    "DiagnosticFieldSourceSummary",
                    link.SourceSummary),
                Field(
                    "DiagnosticFieldStpSideA",
                    StpStateText(
                        link.StpStateA)),
                Field(
                    "DiagnosticFieldStpSideB",
                    StpStateText(
                        link.StpStateB))
            };

        DiagnosticSecondaryTitleText.Text =
            UiText.Get("DiagnosticImpactTitle");

        DiagnosticSecondaryList.ItemsSource =
            link.IsBridge
                ? new[]
                {
                    new DiagnosticTextRow(
                        UiText.Format(
                            "DiagnosticImpactSideA",
                            DisplayDeviceName(
                                link.DeviceAName),
                            UiText.FormatCount(
                                "DiagnosticDeviceCount",
                                link.SideADeviceCount))),
                    new DiagnosticTextRow(
                        UiText.Format(
                            "DiagnosticImpactSideB",
                            DisplayDeviceName(
                                link.DeviceBName),
                            UiText.FormatCount(
                                "DiagnosticDeviceCount",
                                link.SideBDeviceCount))),
                    new DiagnosticTextRow(
                        UiText.Format(
                            "DiagnosticImpactPairs",
                            link.SeparatedDevicePairCount))
                }
                : new[]
                {
                    Row("DiagnosticImpactAlternativePath")
                };

        DiagnosticTertiaryTitleText.Text =
            UiText.Get("DiagnosticEvidenceTitle");

        DiagnosticTertiaryList.ItemsSource =
            link.Evidence.Count == 0
                ? new[]
                {
                    Row("DiagnosticNoEvidence")
                }
                : link.Evidence
                    .Select(
                        item =>
                            new DiagnosticTextRow(
                                BuildEvidenceText(
                                    item)))
                    .ToArray();
    }

    private static DiagnosticFieldRow Field(
        string labelKey,
        string value)
    {
        return new DiagnosticFieldRow(
            UiText.Get(labelKey),
            string.IsNullOrWhiteSpace(value)
                ? UiText.Get("DiagnosticNotAvailable")
                : value);
    }

    private static DiagnosticTextRow Row(
        string textKey)
    {
        return new DiagnosticTextRow(
            UiText.Get(textKey));
    }

    private static string BuildInterfaceDiagnosticText(
        InterfaceDiagnostic item)
    {
        return UiText.Format(
            "DiagnosticInterfaceRow",
            DisplayInterfaceName(
                item.DisplayName),
            item.IfIndex.HasValue
                ? item.IfIndex.Value.ToString(
                    CultureInfo.CurrentCulture)
                : UiText.Get("DiagnosticNotAvailable"),
            ValueOrNotAvailable(
                item.AdminStatus),
            ValueOrNotAvailable(
                item.OperStatus),
            StpStateText(
                item.StpState),
            DegradationText(
                item),
            LocalTimeText(
                item.LastSeenUtc),
            ValueOrNotAvailable(
                item.MacAddress),
            ValueOrNotAvailable(
                SpeedText(
                    item.SpeedBps)));
    }

    private static string BuildDeviceLinkDiagnosticText(
        Guid selectedDeviceId,
        PhysicalLinkDiagnostic link)
    {
        var selectedIsA =
            link.DeviceAId == selectedDeviceId;

        var peer =
            selectedIsA
                ? DisplayDeviceName(
                    link.DeviceBName)
                : DisplayDeviceName(
                    link.DeviceAName);

        var localPort =
            selectedIsA
                ? DisplayInterfaceName(
                    link.InterfaceAName)
                : DisplayInterfaceName(
                    link.InterfaceBName);

        var peerPort =
            selectedIsA
                ? DisplayInterfaceName(
                    link.InterfaceBName)
                : DisplayInterfaceName(
                    link.InterfaceAName);

        if (!link.IsBridge)
        {
            return UiText.Format(
                "DiagnosticConnectionAlternate",
                peer,
                localPort,
                peerPort,
                FreshnessText(
                    link.Freshness));
        }

        var across =
            selectedIsA
                ? link.SideBDeviceCount
                : link.SideADeviceCount;

        return UiText.Format(
            "DiagnosticConnectionBridge",
            peer,
            localPort,
            peerPort,
            FreshnessText(
                link.Freshness),
            UiText.FormatCount(
                "DiagnosticDeviceCount",
                across));
    }

    private static string PeerName(
        PhysicalLinkDiagnostic link,
        Guid selectedDeviceId)
    {
        return link.DeviceAId == selectedDeviceId
            ? DisplayDeviceName(
                link.DeviceBName)
            : DisplayDeviceName(
                link.DeviceAName);
    }

    private static string BuildEvidenceText(
        DiagnosticEvidenceItem evidence)
    {
        return UiText.Format(
            "DiagnosticEvidenceRow",
            EvidenceKindText(
                evidence.Kind),
            evidence.CapturedUtc.HasValue
                ? LocalTimeText(
                    evidence.CapturedUtc.Value)
                : UiText.Get("DiagnosticNotAvailable"),
            ValueOrNotAvailable(
                evidence.SourceAddress),
            ValueOrNotAvailable(
                evidence.Detail),
            RawAvailabilityText(
                evidence.RawAvailability));
    }

    private static string RawAvailabilityText(
        DiagnosticRawAvailability availability)
    {
        switch (availability)
        {
            case DiagnosticRawAvailability.NotApplicable:
                return UiText.Get(
                    "DiagnosticRawNotApplicable");
            case DiagnosticRawAvailability.Available:
                return UiText.Get(
                    "DiagnosticRawAvailable");
            case DiagnosticRawAvailability.Expired:
                return UiText.Get(
                    "DiagnosticRawExpired");
            default:
                return UiText.Get(
                    "DiagnosticRawUnknown");
        }
    }

    private static string DegradationText(
        InterfaceDiagnostic item)
    {
        if (item.DegradationStatus ==
            DiagnosticDegradationStatus.Unknown)
        {
            return UiText.Get(
                "DiagnosticDegradationUnknown");
        }

        if (item.DegradationStatus ==
            DiagnosticDegradationStatus.Healthy)
        {
            return UiText.Format(
                "DiagnosticDegradationHealthyAt",
                LocalTimeText(
                    item.DegradationCapturedUtc));
        }

        var reasons =
            item.DegradationReasons.Count == 0
                ? UiText.Get(
                    "DiagnosticDegradationReasonUnknown")
                : string.Join(
                    ", ",
                    item.DegradationReasons
                        .Select(
                            DegradationReasonText));

        return UiText.Format(
            "DiagnosticDegradationDegradedAt",
            reasons,
            LocalTimeText(
                item.DegradationCapturedUtc));
    }

    private static string DegradationReasonText(
        DiagnosticDegradationReason reason)
    {
        switch (reason)
        {
            case DiagnosticDegradationReason.NoBaseline:
                return UiText.Get(
                    "DiagnosticDegradationReasonNoBaseline");
            case DiagnosticDegradationReason.CounterDiscontinuity:
                return UiText.Get(
                    "DiagnosticDegradationReasonDiscontinuity");
            case DiagnosticDegradationReason.IncompleteCounterData:
                return UiText.Get(
                    "DiagnosticDegradationReasonIncomplete");
            case DiagnosticDegradationReason.ErrorRateThresholdExceeded:
                return UiText.Get(
                    "DiagnosticDegradationReasonErrors");
            case DiagnosticDegradationReason.DiscardRateThresholdExceeded:
                return UiText.Get(
                    "DiagnosticDegradationReasonDiscards");
            default:
                return UiText.Get(
                    "DiagnosticDegradationReasonUnknown");
        }
    }

    private static string StpStateText(
        StpTreePortState state)
    {
        switch (state)
        {
            case StpTreePortState.Disabled:
                return UiText.Get("DiagnosticStpDisabled");
            case StpTreePortState.Blocking:
                return UiText.Get("DiagnosticStpBlocking");
            case StpTreePortState.Listening:
                return UiText.Get("DiagnosticStpListening");
            case StpTreePortState.Learning:
                return UiText.Get("DiagnosticStpLearning");
            case StpTreePortState.Forwarding:
                return UiText.Get("DiagnosticStpForwarding");
            case StpTreePortState.Broken:
                return UiText.Get("DiagnosticStpBroken");
            default:
                return UiText.Get("DiagnosticStpUnknown");
        }
    }

    private static string LinkStrengthText(
        DiagnosticLinkStrength strength)
    {
        switch (strength)
        {
            case DiagnosticLinkStrength.Confirmed:
                return UiText.Get("DiagnosticStrengthConfirmed");
            case DiagnosticLinkStrength.Observed:
                return UiText.Get("DiagnosticStrengthObserved");
            case DiagnosticLinkStrength.Inferred:
                return UiText.Get("DiagnosticStrengthInferred");
            default:
                return UiText.Get("DiagnosticStrengthManual");
        }
    }

    private static string EvidenceKindText(
        MapEvidenceKind kind)
    {
        switch (kind)
        {
            case MapEvidenceKind.Lldp:
                return UiText.Get("DiagnosticEvidenceLldp");
            case MapEvidenceKind.Cdp:
                return UiText.Get("DiagnosticEvidenceCdp");
            case MapEvidenceKind.ArpFdbCorrelation:
                return UiText.Get("DiagnosticEvidenceArpFdb");
            default:
                return UiText.Get("DiagnosticEvidenceManual");
        }
    }

    private static string SpeedText(
        long? speedBps)
    {
        if (!speedBps.HasValue)
        {
            return null;
        }

        if (speedBps.Value >= 1000000000L)
        {
            return UiText.Format(
                "DiagnosticSpeedGbps",
                speedBps.Value / 1000000000.0);
        }

        if (speedBps.Value >= 1000000L)
        {
            return UiText.Format(
                "DiagnosticSpeedMbps",
                speedBps.Value / 1000000.0);
        }

        return UiText.Format(
            "DiagnosticSpeedBps",
            speedBps.Value);
    }

    private static string LocalTimeText(
        DateTime? value)
    {
        return value.HasValue
            ? value.Value
                .ToLocalTime()
                .ToString(
                    "G",
                    CultureInfo.CurrentCulture)
            : UiText.Get("DiagnosticNotAvailable");
    }

    private static string ValueOrNotAvailable(
        string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? UiText.Get("DiagnosticNotAvailable")
            : value;
    }

    private static string DisplayDeviceName(
        string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? UiText.Get("NodeUnknownLabel")
            : value;
    }

    private static string DisplayInterfaceName(
        string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? UiText.Get("DiagnosticInterfaceUnknown")
            : value;
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

    private sealed class MapNodeVisual
    {
        public MapNodeVisual(
            Border border,
            TextBlock title,
            TextBlock secondary,
            TextBlock topologyMetadata,
            TextBlock location,
            TextBlock lockBadge)
        {
            Border = border;
            Title = title;
            Secondary = secondary;
            TopologyMetadata = topologyMetadata;
            Location = location;
            LockBadge = lockBadge;
        }

        public Border Border { get; }

        public TextBlock Title { get; }

        public TextBlock Secondary { get; }

        public TextBlock TopologyMetadata { get; }

        public TextBlock Location { get; }

        public TextBlock LockBadge { get; }

        public Guid? DeviceId { get; set; }

        public bool IsLocked { get; set; }
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

        public MapFreshness? LastFreshness { get; set; }
    }

    private sealed class DiagnosticFieldRow
    {
        public DiagnosticFieldRow(
            string label,
            string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public string Value { get; }
    }

    private sealed class DiagnosticTextRow
    {
        public DiagnosticTextRow(
            string text)
        {
            Text = text;
        }

        public string Text { get; }
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

    private sealed class EmptyManualTopologyService :
        IManualTopologyService
    {
        public ManualTopologyEditorSnapshot GetSnapshot()
        {
            return new ManualTopologyEditorSnapshot(
                new ManualTopologyDeviceItem[0],
                new ManualTopologyPortItem[0],
                new ManualTopologyLinkItem[0]);
        }

        public Guid CreateDevice(
            string name,
            ManualTopologyDeviceCategory category,
            string notes)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public void UpdateDevice(
            Guid deviceId,
            string name,
            ManualTopologyDeviceCategory category,
            string notes)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public void DeleteDevice(
            Guid deviceId)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public Guid CreatePort(
            Guid deviceId,
            string name,
            string mediaType)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public void UpdatePort(
            Guid interfaceId,
            string name,
            string mediaType)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public void DeletePort(
            Guid interfaceId)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public Guid CreateLink(
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            string mediaType,
            string notes)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public void UpdateLink(
            Guid physicalLinkId,
            string mediaType,
            string notes)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }

        public void DeleteLink(
            Guid physicalLinkId)
        {
            throw new InvalidOperationException(
                "Manual topology service is not configured.");
        }
    }

    private sealed class EmptyMapLayoutStore :
        IMapLayoutStore
    {
        public MapLayoutSnapshot Load(
            Guid mapId)
        {
            return new MapLayoutSnapshot(
                mapId,
                new MapViewportLayout(
                    1.0,
                    0.0,
                    0.0),
                new MapDeviceLayout[0]);
        }

        public void SaveViewport(
            Guid mapId,
            MapViewportLayout viewport)
        {
        }

        public void SaveDevice(
            Guid mapId,
            MapDeviceLayout deviceLayout)
        {
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
