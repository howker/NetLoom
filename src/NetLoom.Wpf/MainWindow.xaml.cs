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
    private readonly double _locationDefaultWidth;
    private readonly double _locationDefaultHeight;
    private readonly double _locationMinWidth;
    private readonly double _locationMinHeight;
    private readonly double _locationHeaderHeight;
    private readonly double _locationContentPadding;
    private readonly double _locationResizeThumbSize;

    private readonly TopologyRefreshCoordinator
        _topologyRefreshCoordinator;

    private readonly MacIpLookupSearchService
        _lookupSearchService;

    private readonly MacIpLookupRequestTracker
        _lookupRequestTracker;

    private readonly TopologyRefreshStateTracker
        _refreshStateTracker;

    private readonly DispatcherTimer
        _refreshTimer;

    private readonly IMapLayoutStore
        _mapLayoutStore;

    private readonly IManualTopologyService
        _manualTopologyService;

    private readonly ILocationTopologyService
        _locationTopologyService;

    private readonly IMapLocationLayoutStore
        _mapLocationLayoutStore;

    private readonly Guid
        _mapLayoutId =
            MapLayoutScope.PhysicalTopologyMapId;

    private readonly Dictionary<Guid, MapDeviceLayout>
        _persistedDeviceLayouts =
            new Dictionary<Guid, MapDeviceLayout>();

    private readonly Dictionary<Guid, MapLocationLayout>
        _persistedLocationLayouts =
            new Dictionary<Guid, MapLocationLayout>();

    private readonly Dictionary<Guid, MapLocationVisual>
        _locationVisualsById =
            new Dictionary<Guid, MapLocationVisual>();

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

    private MapLocationVisual _dragLocationVisual;

    private Point _locationDragStartPoint;

    private double _locationDragStartLeft;

    private double _locationDragStartTop;

    private bool _locationDragMoved;

    private readonly Dictionary<MapLocationVisual, Point>
        _locationDragLocationStarts =
            new Dictionary<MapLocationVisual, Point>();

    private readonly Dictionary<MapNodeVisual, Point>
        _locationDragDeviceStarts =
            new Dictionary<MapNodeVisual, Point>();

    private bool _isPanning;

    private Point _panStartPoint;

    private double _panStartHorizontalOffset;

    private double _panStartVerticalOffset;

    private Guid? _highlightedDeviceId;

    private Guid? _selectedDeviceId;

    private Guid? _selectedPhysicalLinkId;

    private Guid? _selectedLocationId;

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
        : this(
            topologyRefreshSnapshotProvider,
            lookupReader,
            mapLayoutStore,
            manualTopologyService,
            new EmptyLocationTopologyService(),
            mapLayoutStore
                as IMapLocationLayoutStore ??
                new EmptyMapLocationLayoutStore())
    {
    }

    public MainWindow(
        ITopologyRefreshSnapshotProvider topologyRefreshSnapshotProvider,
        IMacIpLookupReader lookupReader,
        IMapLayoutStore mapLayoutStore,
        IManualTopologyService manualTopologyService,
        ILocationTopologyService locationTopologyService,
        IMapLocationLayoutStore mapLocationLayoutStore)
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

        _locationDefaultWidth =
            GetDoubleResource(
                "NetLoom.Map.LocationDefaultWidth");

        _locationDefaultHeight =
            GetDoubleResource(
                "NetLoom.Map.LocationDefaultHeight");

        _locationMinWidth =
            GetDoubleResource(
                "NetLoom.Map.LocationMinWidth");

        _locationMinHeight =
            GetDoubleResource(
                "NetLoom.Map.LocationMinHeight");

        _locationHeaderHeight =
            GetDoubleResource(
                "NetLoom.Map.LocationHeaderHeight");

        _locationContentPadding =
            GetDoubleResource(
                "NetLoom.Map.LocationContentPadding");

        _locationResizeThumbSize =
            GetDoubleResource(
                "NetLoom.Map.LocationResizeThumbSize");

        _mapLayoutStore =
            mapLayoutStore ??
            throw new ArgumentNullException(
                nameof(mapLayoutStore));

        _manualTopologyService =
            manualTopologyService ??
            throw new ArgumentNullException(
                nameof(manualTopologyService));

        _locationTopologyService =
            locationTopologyService ??
            throw new ArgumentNullException(
                nameof(locationTopologyService));

        _mapLocationLayoutStore =
            mapLocationLayoutStore ??
            throw new ArgumentNullException(
                nameof(mapLocationLayoutStore));

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
        PreviewKeyDown += OnMainWindowPreviewKeyDown;

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

        LocationsButton.Content =
            UiText.Get("LocationTopologyAction");

        LocationsButton.ToolTip =
            UiText.Get("LocationTopologyMapHint");

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
        _refreshTimer.Start();

        await RefreshTopologyAsync();
        FitTopologyToViewport();
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

            _persistedLocationLayouts.Clear();

            foreach (var item in snapshot.Locations)
            {
                _persistedLocationLayouts[item.LocationId] =
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
            _persistedLocationLayouts.Clear();
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
        var bounds =
            new List<Rect>();

        bounds.AddRange(
            _nodeVisualsByIdentity.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    NodeBounds));

        bounds.AddRange(
            _locationVisualsById.Values
                .Where(
                    visual =>
                        visual != null &&
                        visual.Border.Visibility ==
                            Visibility.Visible)
                .Select(
                    LocationVisibleBounds));

        if (bounds.Count == 0)
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
            bounds.Min(
                item => item.Left);

        var minY =
            bounds.Min(
                item => item.Top);

        var maxX =
            bounds.Max(
                item => item.Right);

        var maxY =
            bounds.Max(
                item => item.Bottom);

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

    private void TrySaveLocationLayout(
        MapLocationVisual visual)
    {
        if (visual == null ||
            visual.LocationId == Guid.Empty)
        {
            return;
        }

        var layout =
            new MapLocationLayout(
                visual.LocationId,
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        LocationLeft(
                            visual),
                        _virtualOriginX),
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        LocationTop(
                            visual),
                        _virtualOriginY),
                Math.Max(
                    _locationMinWidth,
                    visual.ExpandedWidth),
                Math.Max(
                    _locationMinHeight,
                    visual.ExpandedHeight),
                visual.IsCollapsed,
                visual.IsLocked);

        try
        {
            _mapLocationLayoutStore.SaveLocation(
                _mapLayoutId,
                layout);

            _persistedLocationLayouts[
                layout.LocationId] =
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
            if (_selectedLocationId.HasValue)
            {
                var locationVisual =
                    LocationVisual(
                        _selectedLocationId.Value);

                MapLockSelectedCheckBox.IsEnabled =
                    locationVisual != null;

                MapLockSelectedCheckBox.IsChecked =
                    locationVisual != null &&
                    locationVisual.IsLocked;

                return;
            }

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

        _lastMapSnapshot =
            snapshot;

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

        ReconcileLocations(
            snapshot.Locations,
            snapshot.Nodes);

        NormalizeLocationHierarchy(
            snapshot.Locations,
            snapshot.Nodes);

        UpdateLocationHierarchyVisibility();

        ReconcileLinks(
            snapshot.Links,
            nodes);

        UpdateSelectedLayoutControl();

        if (snapshot.Nodes.Count == 0 &&
            snapshot.Locations.Count == 0)
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

    private void ReconcileLocations(
        IReadOnlyList<MapLocation> locations,
        IReadOnlyList<MapNode> nodes)
    {
        var desiredIds =
            new HashSet<Guid>(
                locations.Select(
                    item => item.Id));

        var byId =
            locations.ToDictionary(
                item => item.Id);

        var ordered =
            locations
                .OrderBy(
                    item =>
                        LocationDepth(
                            item,
                            byId))
                .ThenBy(
                    item => item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Id)
                .ToArray();

        for (var index = 0;
             index < ordered.Length;
             index++)
        {
            var location =
                ordered[index];

            MapLocationVisual visual;
            var created = false;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                visual =
                    CreateLocationVisual();

                created = true;

                _locationVisualsById.Add(
                    location.Id,
                    visual);

                MapCanvas.Children.Add(
                    visual.Border);
            }

            MapLocationLayout layout = null;

            if (_persistedLocationLayouts.TryGetValue(
                    location.Id,
                    out layout))
            {
                ApplyLocationLayout(
                    visual,
                    layout);
            }
            else if (created)
            {
                layout =
                    CreateDefaultLocationLayout(
                        location,
                        index,
                        nodes,
                        locations);

                ApplyLocationLayout(
                    visual,
                    layout);
            }

            Panel.SetZIndex(
                visual.Border,
                -100 +
                LocationDepth(
                    location,
                    byId));

            UpdateLocationVisual(
                visual,
                location,
                byId);

            if (created)
            {
                AnimateAppearance(
                    visual.Border);
            }
        }

        foreach (var locationId in
            _locationVisualsById.Keys
                .Where(
                    id =>
                        !desiredIds.Contains(
                            id))
                .ToArray())
        {
            var visual =
                _locationVisualsById[
                    locationId];

            _locationVisualsById.Remove(
                locationId);

            AnimateRemoval(
                visual.Border,
                () =>
                    MapCanvas.Children.Remove(
                        visual.Border));
        }

        if (_selectedLocationId.HasValue &&
            !desiredIds.Contains(
                _selectedLocationId.Value))
        {
            _selectedLocationId = null;
        }

        UpdateLocationSelectionPresentation();
    }

    private void NormalizeLocationHierarchy(
        IReadOnlyList<MapLocation> locations,
        IReadOnlyList<MapNode> nodes)
    {
        if (locations == null ||
            locations.Count == 0)
        {
            return;
        }

        var byId =
            locations.ToDictionary(
                item => item.Id);

        // Сначала увеличиваем только размер контейнеров снизу вверх.
        // Позиция родителя остаётся стабильной.
        // Иерархия не должна тянуть его к ошибочно сохранённому ребёнку.
        foreach (var location in
            locations
                .OrderByDescending(
                    item =>
                        LocationDepth(
                            item,
                            byId)))
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                continue;
            }

            var requiredWidth =
                _locationMinWidth;

            var requiredHeight =
                _locationMinHeight;

            foreach (var child in
                locations.Where(
                    item =>
                        item.ParentLocationId ==
                        location.Id))
            {
                MapLocationVisual childVisual;

                if (!_locationVisualsById.TryGetValue(
                        child.Id,
                        out childVisual))
                {
                    continue;
                }

                requiredWidth =
                    Math.Max(
                        requiredWidth,
                        Math.Max(
                            _locationMinWidth,
                            childVisual.ExpandedWidth) +
                        (2.0 *
                         _locationContentPadding));

                requiredHeight =
                    Math.Max(
                        requiredHeight,
                        Math.Max(
                            _locationMinHeight,
                            childVisual.ExpandedHeight) +
                        _locationHeaderHeight +
                        (2.0 *
                         _locationContentPadding));
            }

            foreach (var node in
                nodes.Where(
                    item =>
                        item.LocationId ==
                        location.Id))
            {
                MapNodeVisual nodeVisual;

                if (!_nodeVisualsByIdentity.TryGetValue(
                        NodeIdentity(node),
                        out nodeVisual))
                {
                    continue;
                }

                requiredWidth =
                    Math.Max(
                        requiredWidth,
                        _nodeWidth +
                        (2.0 *
                         _locationContentPadding));

                requiredHeight =
                    Math.Max(
                        requiredHeight,
                        NodeVisualHeight(
                            nodeVisual) +
                        _locationHeaderHeight +
                        (2.0 *
                         _locationContentPadding));
            }

            var widthChanged =
                visual.ExpandedWidth + 0.001 <
                requiredWidth;

            var heightChanged =
                visual.ExpandedHeight + 0.001 <
                requiredHeight;

            if (!widthChanged &&
                !heightChanged)
            {
                continue;
            }

            visual.ExpandedWidth =
                Math.Max(
                    visual.ExpandedWidth,
                    requiredWidth);

            visual.ExpandedHeight =
                Math.Max(
                    visual.ExpandedHeight,
                    requiredHeight);

            UpdateLocationVisualState(
                visual);

            if (_persistedLocationLayouts.ContainsKey(
                    location.Id))
            {
                TrySaveLocationLayout(
                    visual);
            }
        }

        // Затем сверху вниз возвращаем каждый дочерний контейнер и устройство внутрь физического родителя.
        // При сдвиге Location перемещается всё его поддерево, а не только рамка.
        foreach (var location in
            locations
                .OrderBy(
                    item =>
                        LocationDepth(
                            item,
                            byId)))
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                continue;
            }

            if (visual.ParentLocationId.HasValue)
            {
                var left =
                    LocationLeft(
                        visual);

                var top =
                    LocationTop(
                        visual);

                var constrained =
                    ConstrainLocationPositionToParent(
                        visual,
                        left,
                        top);

                var deltaX =
                    constrained.X - left;

                var deltaY =
                    constrained.Y - top;

                if (Math.Abs(deltaX) > 0.001 ||
                    Math.Abs(deltaY) > 0.001)
                {
                    MoveLocationSubtreeByDelta(
                        location.Id,
                        deltaX,
                        deltaY);
                }
            }

            foreach (var node in
                nodes.Where(
                    item =>
                        item.LocationId ==
                        location.Id))
            {
                MapNodeVisual nodeVisual;

                if (!_nodeVisualsByIdentity.TryGetValue(
                        NodeIdentity(node),
                        out nodeVisual))
                {
                    continue;
                }

                var left =
                    NodeLeft(
                        nodeVisual);

                var top =
                    NodeTop(
                        nodeVisual);

                var constrained =
                    ConstrainNodePositionToLocation(
                        nodeVisual,
                        left,
                        top);

                if (Math.Abs(
                        constrained.X - left) <= 0.001 &&
                    Math.Abs(
                        constrained.Y - top) <= 0.001)
                {
                    continue;
                }

                Canvas.SetLeft(
                    nodeVisual.Border,
                    constrained.X);

                Canvas.SetTop(
                    nodeVisual.Border,
                    constrained.Y);

                if (node.DeviceId.HasValue &&
                    _persistedDeviceLayouts.ContainsKey(
                        node.DeviceId.Value))
                {
                    TrySaveDeviceLayout(
                        nodeVisual);
                }
            }
        }
    }

    private void MoveLocationSubtreeByDelta(
        Guid locationId,
        double deltaX,
        double deltaY)
    {
        if (Math.Abs(deltaX) <= 0.001 &&
            Math.Abs(deltaY) <= 0.001)
        {
            return;
        }

        var includedLocations =
            DescendantLocationIds(
                locationId);

        includedLocations.Add(
            locationId);

        foreach (var currentLocationId in
            includedLocations)
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    currentLocationId,
                    out visual))
            {
                continue;
            }

            Canvas.SetLeft(
                visual.Border,
                LocationLeft(
                    visual) +
                deltaX);

            Canvas.SetTop(
                visual.Border,
                LocationTop(
                    visual) +
                deltaY);

            if (_persistedLocationLayouts.ContainsKey(
                    currentLocationId))
            {
                TrySaveLocationLayout(
                    visual);
            }
        }

        if (_lastMapSnapshot == null)
        {
            return;
        }

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            if (!node.LocationId.HasValue ||
                !includedLocations.Contains(
                    node.LocationId.Value))
            {
                continue;
            }

            MapNodeVisual visual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out visual))
            {
                continue;
            }

            Canvas.SetLeft(
                visual.Border,
                NodeLeft(
                    visual) +
                deltaX);

            Canvas.SetTop(
                visual.Border,
                NodeTop(
                    visual) +
                deltaY);

            if (node.DeviceId.HasValue &&
                _persistedDeviceLayouts.ContainsKey(
                    node.DeviceId.Value))
            {
                TrySaveDeviceLayout(
                    visual);
            }
        }
    }

    private void UpdateLocationHierarchyVisibility()
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        var byId =
            _lastMapSnapshot.Locations
                .ToDictionary(
                    item => item.Id);

        foreach (var location in
            _lastMapSnapshot.Locations)
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    location.Id,
                    out visual))
            {
                continue;
            }

            visual.Border.Visibility =
                HasCollapsedLocationAncestor(
                    location,
                    byId,
                    includeSelf: false)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            MapNodeVisual visual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out visual))
            {
                continue;
            }

            var hidden = false;

            if (node.LocationId.HasValue)
            {
                MapLocation location;

                if (byId.TryGetValue(
                        node.LocationId.Value,
                        out location))
                {
                    hidden =
                        HasCollapsedLocationAncestor(
                            location,
                            byId,
                            includeSelf: true);
                }
            }

            visual.Border.Visibility =
                hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }
    }

    private bool HasCollapsedLocationAncestor(
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations,
        bool includeSelf)
    {
        var current =
            includeSelf
                ? location
                : null;

        var parentId =
            includeSelf
                ? (Guid?)location.Id
                : location.ParentLocationId;

        var visited =
            new HashSet<Guid>();

        while (parentId.HasValue &&
               visited.Add(
                   parentId.Value))
        {
            MapLocation candidate;

            if (!locations.TryGetValue(
                    parentId.Value,
                    out candidate))
            {
                break;
            }

            MapLocationVisual visual;

            if (_locationVisualsById.TryGetValue(
                    candidate.Id,
                    out visual) &&
                visual.IsCollapsed)
            {
                return true;
            }

            current = candidate;
            parentId =
                current.ParentLocationId;
        }

        return false;
    }

    private MapLocationVisual CreateLocationVisual()
    {
        var title =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationTitle"),
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var lockBadge =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationLockBadge"),
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var collapseButton =
            new Button
            {
                MinWidth = 28.0,
                MinHeight = 24.0,
                Padding = new Thickness(
                    4.0,
                    0.0,
                    4.0,
                    0.0),
                Focusable = false,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        collapseButton.Click +=
            OnMapLocationCollapseClick;

        var headerGrid =
            new Grid
            {
                Height = _locationHeaderHeight,
                Cursor = Cursors.SizeAll
            };

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1.0,
                        GridUnitType.Star)
            });

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid.SetColumn(
            title,
            0);

        Grid.SetColumn(
            lockBadge,
            1);

        Grid.SetColumn(
            collapseButton,
            2);

        headerGrid.Children.Add(
            title);

        headerGrid.Children.Add(
            lockBadge);

        headerGrid.Children.Add(
            collapseButton);

        var header =
            new Border
            {
                Background =
                    FindResource(
                        "NetLoom.Brush.AccentSoft")
                        as Brush,
                Padding =
                    GetThicknessResource(
                        "NetLoom.Thickness.MapLocationHeader"),
                Child = headerGrid
            };

        var description =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationDescription"),
                Margin =
                    GetThicknessResource(
                        "NetLoom.Thickness.MapLocationDescription"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var resizeThumb =
            new Thumb
            {
                Width =
                    _locationResizeThumbSize,
                Height =
                    _locationResizeThumbSize,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                Cursor =
                    Cursors.SizeNWSE,
                Focusable = false
            };

        resizeThumb.DragDelta +=
            OnMapLocationResizeDragDelta;

        resizeThumb.DragCompleted +=
            OnMapLocationResizeDragCompleted;

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1.0,
                        GridUnitType.Star)
            });

        Grid.SetRow(
            header,
            0);

        Grid.SetRow(
            description,
            1);

        Grid.SetRowSpan(
            resizeThumb,
            2);

        root.Children.Add(
            header);

        root.Children.Add(
            description);

        root.Children.Add(
            resizeThumb);

        var border =
            new Border
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLocationContainer"),
                Child = root,
                Focusable = false
            };

        border.ContextMenu =
            CreateLocationContextMenu(
                border);

        header.MouseLeftButtonDown +=
            OnMapLocationMouseLeftButtonDown;

        header.MouseMove +=
            OnMapLocationMouseMove;

        header.MouseLeftButtonUp +=
            OnMapLocationMouseLeftButtonUp;

        header.MouseRightButtonDown +=
            OnMapLocationMouseRightButtonDown;

        border.MouseLeftButtonDown +=
            OnMapLocationBodyMouseLeftButtonDown;

        return new MapLocationVisual(
            border,
            header,
            title,
            description,
            collapseButton,
            lockBadge,
            resizeThumb);
    }

    private ContextMenu CreateLocationContextMenu(
        Border target)
    {
        var menu =
            new ContextMenu
            {
                Tag = target
            };

        var edit =
            new MenuItem
            {
                Tag = target
            };

        edit.Click +=
            OnMapLocationContextEditClick;

        var collapse =
            new MenuItem
            {
                Tag = target
            };

        collapse.Click +=
            OnMapLocationContextCollapseClick;

        var lockItem =
            new MenuItem
            {
                Tag = target
            };

        lockItem.Click +=
            OnMapLocationContextLockClick;

        var delete =
            new MenuItem
            {
                Tag = target
            };

        delete.Click +=
            OnMapLocationContextDeleteClick;

        menu.Items.Add(
            edit);

        menu.Items.Add(
            new Separator());

        menu.Items.Add(
            collapse);

        menu.Items.Add(
            lockItem);

        menu.Items.Add(
            new Separator());

        menu.Items.Add(
            delete);

        menu.Opened +=
            OnMapLocationContextMenuOpened;

        return menu;
    }

    private void UpdateLocationVisual(
        MapLocationVisual visual,
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        visual.LocationId =
            location.Id;

        visual.ParentLocationId =
            location.ParentLocationId;

        visual.Title.Text =
            location.Name;

        visual.Description.Text =
            string.IsNullOrWhiteSpace(
                location.Description)
                ? UiText.Get(
                    "MapLocationNoDescription")
                : location.Description;

        visual.Border.Tag =
            location.Id;

        visual.Header.Tag =
            location.Id;

        visual.CollapseButton.Tag =
            location.Id;

        visual.ResizeThumb.Tag =
            location.Id;

        visual.Border.ToolTip =
            UiText.Format(
                "MapLocationToolTip",
                BuildLocationPath(
                    location,
                    locations),
                string.IsNullOrWhiteSpace(
                    location.Description)
                    ? UiText.Get(
                        "MapLocationNoDescription")
                    : location.Description);

        UpdateLocationVisualState(
            visual);
    }

    private bool TryGetExpandedLocationBounds(
        Guid locationId,
        out Rect bounds)
    {
        MapLocationVisual visual;

        if (_locationVisualsById.TryGetValue(
                locationId,
                out visual))
        {
            bounds =
                ExpandedLocationBounds(
                    visual);

            return true;
        }

        MapLocationLayout layout;

        if (_persistedLocationLayouts.TryGetValue(
                locationId,
                out layout))
        {
            bounds =
                new Rect(
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            layout.X,
                            _virtualOriginX),
                    MapVirtualWorkspace
                        .ToCanvasCoordinate(
                            layout.Y,
                            _virtualOriginY),
                    Math.Max(
                        _locationMinWidth,
                        layout.Width),
                    Math.Max(
                        _locationMinHeight,
                        layout.Height));

            return true;
        }

        bounds = Rect.Empty;
        return false;
    }

    private MapLocationLayout CreateDefaultLocationLayout(
        MapLocation location,
        int order,
        IReadOnlyList<MapNode> nodes,
        IReadOnlyList<MapLocation> locations)
    {
        var bounds =
            new List<Rect>();

        foreach (var node in nodes)
        {
            if (node.LocationId !=
                location.Id)
            {
                continue;
            }

            MapNodeVisual nodeVisual;

            if (_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out nodeVisual))
            {
                bounds.Add(
                    NodeBounds(
                        nodeVisual));
            }
        }

        foreach (var child in locations)
        {
            if (child.ParentLocationId !=
                location.Id)
            {
                continue;
            }

            MapLocationVisual childVisual;

            if (_locationVisualsById.TryGetValue(
                    child.Id,
                    out childVisual))
            {
                bounds.Add(
                    ExpandedLocationBounds(
                        childVisual));
            }
        }

        if (bounds.Count > 0)
        {
            var left =
                bounds.Min(
                    item => item.Left) -
                _locationContentPadding;

            var top =
                bounds.Min(
                    item => item.Top) -
                _locationContentPadding -
                _locationHeaderHeight;

            var right =
                bounds.Max(
                    item => item.Right) +
                _locationContentPadding;

            var bottom =
                bounds.Max(
                    item => item.Bottom) +
                _locationContentPadding;

            var width =
                Math.Max(
                    _locationMinWidth,
                    right - left);

            var height =
                Math.Max(
                    _locationMinHeight,
                    bottom - top);

            return new MapLocationLayout(
                location.Id,
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        left,
                        _virtualOriginX),
                MapVirtualWorkspace
                    .ToLogicalCoordinate(
                        top,
                        _virtualOriginY),
                width,
                height,
                false,
                false);
        }

        if (location.ParentLocationId.HasValue)
        {
            Rect parentBounds;

            if (TryGetExpandedLocationBounds(
                    location.ParentLocationId.Value,
                    out parentBounds))
            {
                var availableWidth =
                    Math.Max(
                        _locationMinWidth,
                        parentBounds.Width -
                        (2.0 *
                         _locationContentPadding));

                var availableHeight =
                    Math.Max(
                        _locationMinHeight,
                        parentBounds.Height -
                        _locationHeaderHeight -
                        (2.0 *
                         _locationContentPadding));

                var width =
                    Math.Min(
                        _locationDefaultWidth,
                        availableWidth);

                var height =
                    Math.Min(
                        _locationDefaultHeight,
                        availableHeight);

                var left =
                    parentBounds.Left +
                    _locationContentPadding;

                var top =
                    parentBounds.Top +
                    _locationHeaderHeight +
                    _locationContentPadding;

                return new MapLocationLayout(
                    location.Id,
                    MapVirtualWorkspace
                        .ToLogicalCoordinate(
                            left,
                            _virtualOriginX),
                    MapVirtualWorkspace
                        .ToLogicalCoordinate(
                            top,
                            _virtualOriginY),
                    width,
                    height,
                    false,
                    false);
            }
        }

        var column =
            order % 3;

        var row =
            order / 3;

        return new MapLocationLayout(
            location.Id,
            (column *
                (_locationDefaultWidth + 56.0)) -
                (_locationDefaultWidth / 2.0),
            (row *
                (_locationDefaultHeight + 56.0)) -
                (_locationDefaultHeight / 2.0),
            _locationDefaultWidth,
            _locationDefaultHeight,
            false,
            false);
    }

    private static int LocationDepth(
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var depth = 0;
        var parentId =
            location.ParentLocationId;

        var visited =
            new HashSet<Guid>();

        while (parentId.HasValue &&
               visited.Add(
                   parentId.Value))
        {
            MapLocation parent;

            if (!locations.TryGetValue(
                    parentId.Value,
                    out parent))
            {
                break;
            }

            depth++;
            parentId =
                parent.ParentLocationId;
        }

        return depth;
    }

    private static string BuildLocationPath(
        MapLocation location,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var names =
            new List<string>();

        var current =
            location;

        var visited =
            new HashSet<Guid>();

        while (current != null &&
               visited.Add(
                   current.Id))
        {
            names.Add(
                current.Name);

            if (!current.ParentLocationId.HasValue)
            {
                break;
            }

            MapLocation parent;

            if (!locations.TryGetValue(
                    current.ParentLocationId.Value,
                    out parent))
            {
                break;
            }

            current = parent;
        }

        names.Reverse();

        return string.Join(
            " / ",
            names);
    }

    private void ApplyLocationLayout(
        MapLocationVisual visual,
        MapLocationLayout layout)
    {
        visual.IsCollapsed =
            layout.IsCollapsed;

        visual.IsLocked =
            layout.IsLocked;

        visual.ExpandedWidth =
            Math.Max(
                _locationMinWidth,
                layout.Width);

        visual.ExpandedHeight =
            Math.Max(
                _locationMinHeight,
                layout.Height);

        Canvas.SetLeft(
            visual.Border,
            MapVirtualWorkspace
                .ToCanvasCoordinate(
                    layout.X,
                    _virtualOriginX));

        Canvas.SetTop(
            visual.Border,
            MapVirtualWorkspace
                .ToCanvasCoordinate(
                    layout.Y,
                    _virtualOriginY));

        visual.Border.Width =
            visual.ExpandedWidth;

        visual.Border.Height =
            visual.IsCollapsed
                ? _locationHeaderHeight
                : visual.ExpandedHeight;
    }

    private Rect ExpandedLocationBounds(
        MapLocationVisual visual)
    {
        return new Rect(
            LocationLeft(
                visual),
            LocationTop(
                visual),
            Math.Max(
                _locationMinWidth,
                visual.ExpandedWidth),
            Math.Max(
                _locationMinHeight,
                visual.ExpandedHeight));
    }

    private Rect LocationVisibleBounds(
        MapLocationVisual visual)
    {
        return new Rect(
            LocationLeft(
                visual),
            LocationTop(
                visual),
            Math.Max(
                _locationMinWidth,
                visual.Border.Width),
            Math.Max(
                _locationHeaderHeight,
                visual.Border.Height));
    }

    private static double LocationLeft(
        MapLocationVisual visual)
    {
        var value =
            Canvas.GetLeft(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private static double LocationTop(
        MapLocationVisual visual)
    {
        var value =
            Canvas.GetTop(
                visual.Border);

        return double.IsNaN(value)
            ? 0.0
            : value;
    }

    private void UpdateLocationVisualState(
        MapLocationVisual visual)
    {
        visual.Border.Width =
            Math.Max(
                _locationMinWidth,
                visual.ExpandedWidth);

        visual.Border.Height =
            visual.IsCollapsed
                ? _locationHeaderHeight
                : Math.Max(
                    _locationMinHeight,
                    visual.ExpandedHeight);

        visual.Description.Visibility =
            visual.IsCollapsed
                ? Visibility.Collapsed
                : Visibility.Visible;

        visual.ResizeThumb.Visibility =
            !visual.IsCollapsed &&
            !visual.IsLocked
                ? Visibility.Visible
                : Visibility.Collapsed;

        visual.Header.Cursor =
            visual.IsLocked
                ? Cursors.Hand
                : Cursors.SizeAll;

        visual.CollapseButton.Content =
            visual.IsCollapsed
                ? "+"
                : "−";

        visual.CollapseButton.ToolTip =
            UiText.Get(
                visual.IsCollapsed
                    ? "MapLocationExpand"
                    : "MapLocationCollapse");

        visual.LockBadge.Text =
            visual.IsLocked
                ? UiText.Get(
                    "MapLocationLockedBadge")
                : string.Empty;

        UpdateLocationSelectionPresentation();
    }

    private void UpdateLocationSelectionPresentation()
    {
        var selectionBrush =
            FindResource(
                "NetLoom.Brush.Selection")
                as Brush;

        var normalBrush =
            FindResource(
                "NetLoom.Brush.BorderStrong")
                as Brush;

        foreach (var visual in
            _locationVisualsById.Values)
        {
            var selected =
                _selectedLocationId.HasValue &&
                visual.LocationId ==
                    _selectedLocationId.Value;

            visual.Border.BorderBrush =
                selected
                    ? selectionBrush
                    : normalBrush;

            visual.Border.BorderThickness =
                selected
                    ? new Thickness(3.0)
                    : new Thickness(1.0);
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

            var linkVisible =
                sourceVisual.Border.Visibility ==
                    Visibility.Visible &&
                targetVisual.Border.Visibility ==
                    Visibility.Visible;

            visual.Line.Visibility =
                linkVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            visual.Label.Visibility =
                linkVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

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

    private ContextMenu CreateMapElementContextMenu(
        FrameworkElement target,
        RoutedEventHandler editHandler,
        RoutedEventHandler deleteHandler,
        RoutedEventHandler openedHandler)
    {
        var edit =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "ManualTopologyEdit"),
                Tag = target
            };

        edit.Click +=
            editHandler;

        var delete =
            new MenuItem
            {
                Header =
                    UiText.Get(
                        "ManualTopologyDelete"),
                Tag = target
            };

        delete.Click +=
            deleteHandler;

        var menu =
            new ContextMenu
            {
                Tag = target
            };

        menu.Items.Add(edit);
        menu.Items.Add(new Separator());
        menu.Items.Add(delete);

        menu.Opened +=
            openedHandler;

        return menu;
    }

    private static void UpdateMapElementContextMenuState(
        ContextMenu menu,
        bool canChange)
    {
        if (menu == null ||
            menu.Items.Count < 3)
        {
            return;
        }

        var edit =
            menu.Items[0]
                as MenuItem;

        var delete =
            menu.Items[2]
                as MenuItem;

        if (edit != null)
        {
            edit.IsEnabled =
                canChange;
        }

        if (delete != null)
        {
            delete.IsEnabled =
                canChange;
        }
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
                        "NetLoom.Style.MapNodeSecondary"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var topologyMetadata =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var managementAddressText =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta"),
                TextWrapping =
                    TextWrapping.Wrap
            };

        var locationText =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeMeta"),
                TextWrapping =
                    TextWrapping.Wrap
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
        content.Children.Add(managementAddressText);
        content.Children.Add(locationText);

        var border =
            new Border
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapNodeCard"),
                Child = content,
                Cursor = Cursors.Hand,
                Focusable = false
            };

        border.ContextMenu =
            CreateMapElementContextMenu(
                border,
                OnMapNodeContextEditClick,
                OnMapNodeContextDeleteClick,
                OnMapNodeContextMenuOpened);

        border.MouseLeftButtonDown +=
            OnMapNodeMouseLeftButtonDown;

        border.MouseRightButtonDown +=
            OnMapNodeMouseRightButtonDown;

        border.MouseMove +=
            OnMapNodeMouseMove;

        border.MouseLeftButtonUp +=
            OnMapNodeMouseLeftButtonUp;

        return new MapNodeVisual(
            border,
            title,
            secondary,
            topologyMetadata,
            managementAddressText,
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

        visual.ManagementAddress.Text =
            UiText.Format(
                "MapNodeIpAddress",
                string.IsNullOrWhiteSpace(
                    node.ManagementAddress)
                    ? UiText.Get(
                        "DiagnosticNotAvailable")
                    : node.ManagementAddress);

        visual.Location.Text =
            BuildLocationText(
                node,
                locations);

        visual.DeviceId =
            node.DeviceId;

        visual.LocationId =
            node.LocationId;

        visual.IsManual =
            node.Origin ==
            MapNodeOrigin.Manual;

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
                Cursor = Cursors.Hand,
                Focusable = false
            };

        var label =
            new TextBlock
            {
                Style =
                    GetStyleResource(
                        "NetLoom.Style.MapLinkLabel"),
                Cursor = Cursors.Hand,
                Focusable = false
            };

        line.ContextMenu =
            CreateMapElementContextMenu(
                line,
                OnMapLinkContextEditClick,
                OnMapLinkContextDeleteClick,
                OnMapLinkContextMenuOpened);

        label.ContextMenu =
            CreateMapElementContextMenu(
                label,
                OnMapLinkContextEditClick,
                OnMapLinkContextDeleteClick,
                OnMapLinkContextMenuOpened);

        line.MouseLeftButtonDown +=
            OnMapLinkMouseLeftButtonDown;

        label.MouseLeftButtonDown +=
            OnMapLinkMouseLeftButtonDown;

        line.MouseRightButtonDown +=
            OnMapLinkMouseRightButtonDown;

        label.MouseRightButtonDown +=
            OnMapLinkMouseRightButtonDown;

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
            (NodeVisualHeight(source) / 2.0);

        var x2 =
            NodeLeft(target) +
            (_nodeWidth / 2.0);

        var y2 =
            NodeTop(target) +
            (NodeVisualHeight(target) / 2.0);

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
            NodeVisualHeight(visual));
    }

    private double NodeVisualHeight(
        MapNodeVisual visual)
    {
        if (visual == null ||
            visual.Border == null)
        {
            return _nodeHeight;
        }

        visual.Border.Measure(
            new Size(
                _nodeWidth,
                double.PositiveInfinity));

        var measured =
            Math.Max(
                visual.Border.ActualHeight,
                visual.Border.DesiredSize.Height);

        return measured > 0.0
            ? Math.Max(
                _nodeHeight,
                measured)
            : _nodeHeight;
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
                : visual.IsManual
                    ? UiText.Get(
                        "ManualTopologyMapNodeHint")
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

    private async void OnMapNodeMouseLeftButtonDown(
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

        var deviceId =
            (Guid)element.Tag;

        _highlightedDeviceId = null;

        _selectedDeviceId =
            deviceId;

        _selectedPhysicalLinkId =
            null;

        _selectedLocationId =
            null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();

        if (e.ClickCount >= 2)
        {
            _dragNodeVisual = null;
            _dragMoved = false;

            if (element.IsMouseCaptured)
            {
                element.ReleaseMouseCapture();
            }

            if (IsManualDevice(deviceId))
            {
                e.Handled = true;

                await OpenManualTopologyEditorAsync(
                    deviceId,
                    null);
            }

            return;
        }

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

        e.Handled = true;
    }

    private void OnMapNodeMouseRightButtonDown(
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
        _selectedDeviceId = (Guid)element.Tag;
        _selectedPhysicalLinkId = null;


        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private void OnMapNodeContextMenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        var menu =
            sender as ContextMenu;

        var target =
            menu == null
                ? null
                : menu.Tag as FrameworkElement;

        var canChange =
            target != null &&
            target.Tag is Guid &&
            IsManualDevice(
                (Guid)target.Tag);

        UpdateMapElementContextMenuState(
            menu,
            canChange);
    }

    private async void OnMapNodeContextEditClick(
        object sender,
        RoutedEventArgs e)
    {
        var deviceId =
            MapElementIdFromMenuItem(sender);

        if (!deviceId.HasValue ||
            !IsManualDevice(
                deviceId.Value))
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualDevice");
            return;
        }

        await OpenManualTopologyEditorAsync(
            deviceId.Value,
            null);
    }

    private async void OnMapNodeContextDeleteClick(
        object sender,
        RoutedEventArgs e)
    {
        var deviceId =
            MapElementIdFromMenuItem(sender);

        if (!deviceId.HasValue)
        {
            return;
        }

        await DeleteManualDeviceFromMapAsync(
            deviceId.Value);
    }

    private async void OnMainWindowPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Delete ||
            e.OriginalSource is TextBoxBase ||
            e.OriginalSource is PasswordBox)
        {
            return;
        }

        if (_selectedLocationId.HasValue)
        {
            e.Handled = true;

            await DeleteLocationFromMapAsync(
                _selectedLocationId.Value);

            return;
        }

        if (_selectedPhysicalLinkId.HasValue)
        {
            e.Handled = true;

            await DeleteManualLinkFromMapAsync(
                _selectedPhysicalLinkId.Value);

            return;
        }

        if (_selectedDeviceId.HasValue)
        {
            e.Handled = true;

            await DeleteManualDeviceFromMapAsync(
                _selectedDeviceId.Value);
        }
    }

    private async void OnMapNodeKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
        {
            return;
        }

        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        e.Handled = true;

        await DeleteManualDeviceFromMapAsync(
            (Guid)element.Tag);
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
                        NodeVisualHeight(
                            _dragNodeVisual)),
                    _dragStartTop +
                    deltaY));

        var constrained =
            ConstrainNodePositionToLocation(
                _dragNodeVisual,
                left,
                top);

        Canvas.SetLeft(
            _dragNodeVisual.Border,
            constrained.X);

        Canvas.SetTop(
            _dragNodeVisual.Border,
            constrained.Y);

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
        await OpenManualTopologyEditorAsync(
            null,
            null);
    }

    private async Task OpenManualTopologyEditorAsync(
        Guid? initialDeviceId,
        Guid? initialLinkId)
    {
        var editor =
            new ManualTopologyWindow(
                _manualTopologyService,
                initialDeviceId,
                initialLinkId)
            {
                Owner = this
            };

        editor.ShowDialog();

        if (editor.HasChanges)
        {
            await RefreshTopologyAsync();
        }
    }

    private async void OnLocationsClick(
        object sender,
        RoutedEventArgs e)
    {
        await OpenLocationTopologyEditorAsync(
            null);
    }

    private async Task OpenLocationTopologyEditorAsync(
        Guid? initialLocationId)
    {
        var editor =
            new LocationTopologyWindow(
                _locationTopologyService,
                initialLocationId)
            {
                Owner = this
            };

        editor.ShowDialog();

        if (editor.HasChanges)
        {
            await RefreshTopologyAsync();
        }
    }

    private MapLocationVisual LocationVisual(
        Guid locationId)
    {
        MapLocationVisual visual;

        return _locationVisualsById.TryGetValue(
            locationId,
            out visual)
            ? visual
            : null;
    }

    private Guid? LocationIdFromElement(
        object sender)
    {
        var element =
            sender as FrameworkElement;

        return element != null &&
               element.Tag is Guid
            ? (Guid?)((Guid)element.Tag)
            : null;
    }

    private Guid? LocationIdFromMenuItem(
        object sender)
    {
        var menuItem =
            sender as MenuItem;

        var target =
            menuItem == null
                ? null
                : menuItem.Tag
                    as FrameworkElement;

        return target != null &&
               target.Tag is Guid
            ? (Guid?)((Guid)target.Tag)
            : null;
    }

    private void SelectLocation(
        Guid locationId)
    {
        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedPhysicalLinkId = null;
        _selectedLocationId =
            locationId;

        UpdateLocationSelectionPresentation();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private async void OnMapLocationMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        var visual =
            LocationVisual(
                locationId.Value);

        if (visual == null)
        {
            return;
        }

        SelectLocation(
            locationId.Value);

        if (e.ClickCount >= 2)
        {
            _dragLocationVisual = null;
            _locationDragMoved = false;

            if (visual.Header.IsMouseCaptured)
            {
                visual.Header.ReleaseMouseCapture();
            }

            e.Handled = true;

            await OpenLocationTopologyEditorAsync(
                locationId.Value);

            return;
        }

        if (visual.IsLocked)
        {
            e.Handled = true;
            return;
        }

        _dragLocationVisual =
            visual;

        _locationDragStartPoint =
            e.GetPosition(
                MapCanvas);

        _locationDragStartLeft =
            LocationLeft(
                visual);

        _locationDragStartTop =
            LocationTop(
                visual);

        _locationDragMoved = false;

        _locationDragLocationStarts.Clear();
        _locationDragDeviceStarts.Clear();

        CaptureLocationSubtreeStarts(
            locationId.Value);

        CaptureLocationDeviceStarts(
            locationId.Value);

        visual.Header.CaptureMouse();
        e.Handled = true;
    }

    private async void OnMapLocationBodyMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        SelectLocation(
            locationId.Value);

        if (e.ClickCount >= 2)
        {
            await OpenLocationTopologyEditorAsync(
                locationId.Value);
        }

        e.Handled = true;
    }

    private void OnMapLocationMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        SelectLocation(
            locationId.Value);
    }

    private void OnMapLocationMouseMove(
        object sender,
        MouseEventArgs e)
    {
        var visual =
            _dragLocationVisual;

        if (visual == null ||
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
            _locationDragStartPoint.X;

        var deltaY =
            current.Y -
            _locationDragStartPoint.Y;

        if (!_locationDragMoved &&
            Math.Abs(deltaX) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(deltaY) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _locationDragMoved = true;

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
                        visual.Border.Width),
                    _locationDragStartLeft +
                    deltaX));

        var top =
            Math.Max(
                0.0,
                Math.Min(
                    Math.Max(
                        0.0,
                        canvasHeight -
                        visual.Border.Height),
                    _locationDragStartTop +
                    deltaY));

        ApplyLocationDrag(
            visual,
            left,
            top);

        e.Handled = true;
    }

    private void OnMapLocationMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        var visual =
            _dragLocationVisual;

        if (visual == null)
        {
            return;
        }

        _dragLocationVisual = null;

        if (visual.Header.IsMouseCaptured)
        {
            visual.Header.ReleaseMouseCapture();
        }

        if (_locationDragMoved)
        {
            TrySaveLocationLayout(
                visual);

            foreach (var child in
                _locationDragLocationStarts.Keys)
            {
                TrySaveLocationLayout(
                    child);
            }

            foreach (var node in
                _locationDragDeviceStarts.Keys)
            {
                TrySaveDeviceLayout(
                    node);
            }
        }

        _locationDragMoved = false;
        _locationDragLocationStarts.Clear();
        _locationDragDeviceStarts.Clear();

        e.Handled = true;
    }

    private void CaptureLocationDeviceStarts(
        Guid locationId)
    {
        if (_lastMapSnapshot == null)
        {
            return;
        }

        var includedLocations =
            DescendantLocationIds(
                locationId);

        includedLocations.Add(
            locationId);

        foreach (var node in
            _lastMapSnapshot.Nodes)
        {
            if (!node.DeviceId.HasValue ||
                !node.LocationId.HasValue ||
                !includedLocations.Contains(
                    node.LocationId.Value))
            {
                continue;
            }

            MapNodeVisual visual;

            if (!_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(node),
                    out visual))
            {
                continue;
            }

            _locationDragDeviceStarts[
                visual] =
                new Point(
                    NodeLeft(
                        visual),
                    NodeTop(
                        visual));
        }
    }

    private HashSet<Guid> DescendantLocationIds(
        Guid locationId)
    {
        var result =
            new HashSet<Guid>();

        if (_lastMapSnapshot == null)
        {
            return result;
        }

        var pending =
            new Queue<Guid>();

        pending.Enqueue(
            locationId);

        while (pending.Count > 0)
        {
            var parentId =
                pending.Dequeue();

            foreach (var child in
                _lastMapSnapshot.Locations
                    .Where(
                        item =>
                            item.ParentLocationId ==
                            parentId))
            {
                if (result.Add(
                        child.Id))
                {
                    pending.Enqueue(
                        child.Id);
                }
            }
        }

        return result;
    }

    private void ApplyLocationDrag(
        MapLocationVisual visual,
        double desiredLeft,
        double desiredTop)
    {
        if (visual == null)
        {
            return;
        }

        var constrained =
            ConstrainLocationPositionToParent(
                visual,
                desiredLeft,
                desiredTop);

        var actualDeltaX =
            constrained.X -
            _locationDragStartLeft;

        var actualDeltaY =
            constrained.Y -
            _locationDragStartTop;

        Canvas.SetLeft(
            visual.Border,
            constrained.X);

        Canvas.SetTop(
            visual.Border,
            constrained.Y);

        foreach (var pair in
            _locationDragLocationStarts)
        {
            Canvas.SetLeft(
                pair.Key.Border,
                pair.Value.X +
                actualDeltaX);

            Canvas.SetTop(
                pair.Key.Border,
                pair.Value.Y +
                actualDeltaY);
        }

        foreach (var pair in
            _locationDragDeviceStarts)
        {
            Canvas.SetLeft(
                pair.Key.Border,
                pair.Value.X +
                actualDeltaX);

            Canvas.SetTop(
                pair.Key.Border,
                pair.Value.Y +
                actualDeltaY);
        }

        UpdateLinksForCurrentNodePositions();
    }

    private void CaptureLocationSubtreeStarts(
        Guid locationId)
    {
        foreach (var descendantId in
            DescendantLocationIds(
                locationId))
        {
            MapLocationVisual visual;

            if (!_locationVisualsById.TryGetValue(
                    descendantId,
                    out visual))
            {
                continue;
            }

            _locationDragLocationStarts[
                visual] =
                new Point(
                    LocationLeft(
                        visual),
                    LocationTop(
                        visual));
        }
    }

    private Point ConstrainLocationPositionToParent(
        MapLocationVisual visual,
        double desiredLeft,
        double desiredTop)
    {
        if (visual == null ||
            !visual.ParentLocationId.HasValue)
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        MapLocationVisual parent;

        if (!_locationVisualsById.TryGetValue(
                visual.ParentLocationId.Value,
                out parent))
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        var parentBounds =
            ExpandedLocationBounds(
                parent);

        var width =
            Math.Max(
                _locationMinWidth,
                visual.ExpandedWidth);

        var height =
            Math.Max(
                _locationMinHeight,
                visual.ExpandedHeight);

        var minimumLeft =
            parentBounds.Left +
            _locationContentPadding;

        var minimumTop =
            parentBounds.Top +
            _locationHeaderHeight +
            _locationContentPadding;

        var maximumLeft =
            Math.Max(
                minimumLeft,
                parentBounds.Right -
                _locationContentPadding -
                width);

        var maximumTop =
            Math.Max(
                minimumTop,
                parentBounds.Bottom -
                _locationContentPadding -
                height);

        return new Point(
            Math.Max(
                minimumLeft,
                Math.Min(
                    maximumLeft,
                    desiredLeft)),
            Math.Max(
                minimumTop,
                Math.Min(
                    maximumTop,
                    desiredTop)));
    }

    private Point ConstrainNodePositionToLocation(
        MapNodeVisual visual,
        double desiredLeft,
        double desiredTop)
    {
        if (visual == null ||
            !visual.LocationId.HasValue)
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        MapLocationVisual location;

        if (!_locationVisualsById.TryGetValue(
                visual.LocationId.Value,
                out location))
        {
            return new Point(
                desiredLeft,
                desiredTop);
        }

        var bounds =
            ExpandedLocationBounds(
                location);

        var nodeHeight =
            NodeVisualHeight(
                visual);

        var minimumLeft =
            bounds.Left +
            _locationContentPadding;

        var minimumTop =
            bounds.Top +
            _locationHeaderHeight +
            _locationContentPadding;

        var maximumLeft =
            Math.Max(
                minimumLeft,
                bounds.Right -
                _locationContentPadding -
                _nodeWidth);

        var maximumTop =
            Math.Max(
                minimumTop,
                bounds.Bottom -
                _locationContentPadding -
                nodeHeight);

        return new Point(
            Math.Max(
                minimumLeft,
                Math.Min(
                    maximumLeft,
                    desiredLeft)),
            Math.Max(
                minimumTop,
                Math.Min(
                    maximumTop,
                    desiredTop)));
    }

    private Size MinimumLocationExpandedSize(
        Guid locationId)
    {
        var visual =
            LocationVisual(
                locationId);

        if (visual == null)
        {
            return new Size(
                _locationMinWidth,
                _locationMinHeight);
        }

        var left =
            LocationLeft(
                visual);

        var top =
            LocationTop(
                visual);

        var requiredRight =
            left +
            _locationMinWidth;

        var requiredBottom =
            top +
            _locationMinHeight;

        foreach (var child in
            _locationVisualsById.Values
                .Where(
                    item =>
                        item.ParentLocationId ==
                        locationId))
        {
            var childBounds =
                ExpandedLocationBounds(
                    child);

            requiredRight =
                Math.Max(
                    requiredRight,
                    childBounds.Right +
                    _locationContentPadding);

            requiredBottom =
                Math.Max(
                    requiredBottom,
                    childBounds.Bottom +
                    _locationContentPadding);
        }

        if (_lastMapSnapshot != null)
        {
            foreach (var node in
                _lastMapSnapshot.Nodes
                    .Where(
                        item =>
                            item.LocationId ==
                            locationId))
            {
                MapNodeVisual nodeVisual;

                if (!_nodeVisualsByIdentity.TryGetValue(
                        NodeIdentity(node),
                        out nodeVisual))
                {
                    continue;
                }

                var nodeBounds =
                    NodeBounds(
                        nodeVisual);

                requiredRight =
                    Math.Max(
                        requiredRight,
                        nodeBounds.Right +
                        _locationContentPadding);

                requiredBottom =
                    Math.Max(
                        requiredBottom,
                        nodeBounds.Bottom +
                        _locationContentPadding);
            }
        }

        return new Size(
            Math.Max(
                _locationMinWidth,
                requiredRight - left),
            Math.Max(
                _locationMinHeight,
                requiredBottom - top));
    }

    private Size MaximumLocationExpandedSizeInParent(
        MapLocationVisual visual)
    {
        if (visual == null ||
            !visual.ParentLocationId.HasValue)
        {
            return new Size(
                double.PositiveInfinity,
                double.PositiveInfinity);
        }

        MapLocationVisual parent;

        if (!_locationVisualsById.TryGetValue(
                visual.ParentLocationId.Value,
                out parent))
        {
            return new Size(
                double.PositiveInfinity,
                double.PositiveInfinity);
        }

        var parentBounds =
            ExpandedLocationBounds(
                parent);

        return new Size(
            Math.Max(
                _locationMinWidth,
                parentBounds.Right -
                _locationContentPadding -
                LocationLeft(
                    visual)),
            Math.Max(
                _locationMinHeight,
                parentBounds.Bottom -
                _locationContentPadding -
                LocationTop(
                    visual)));
    }

    private void OnMapLocationResizeDragDelta(
        object sender,
        DragDeltaEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        var visual =
            LocationVisual(
                locationId.Value);

        if (visual == null ||
            visual.IsLocked ||
            visual.IsCollapsed)
        {
            return;
        }

        var minimum =
            MinimumLocationExpandedSize(
                locationId.Value);

        var maximum =
            MaximumLocationExpandedSizeInParent(
                visual);

        visual.ExpandedWidth =
            Math.Max(
                minimum.Width,
                Math.Min(
                    maximum.Width,
                    visual.ExpandedWidth +
                    e.HorizontalChange));

        visual.ExpandedHeight =
            Math.Max(
                minimum.Height,
                Math.Min(
                    maximum.Height,
                    visual.ExpandedHeight +
                    e.VerticalChange));

        UpdateLocationVisualState(
            visual);

        e.Handled = true;
    }

    private void OnMapLocationResizeDragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        var visual =
            LocationVisual(
                locationId.Value);

        if (visual != null &&
            !visual.IsLocked &&
            !visual.IsCollapsed)
        {
            TrySaveLocationLayout(
                visual);
        }

        e.Handled = true;
    }

    private void OnMapLocationCollapseClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromElement(
                sender);

        if (!locationId.HasValue)
        {
            return;
        }

        ToggleLocationCollapsed(
            locationId.Value);

        e.Handled = true;
    }

    private void ToggleLocationCollapsed(
        Guid locationId)
    {
        var visual =
            LocationVisual(
                locationId);

        if (visual == null)
        {
            return;
        }

        visual.IsCollapsed =
            !visual.IsCollapsed;

        UpdateLocationVisualState(
            visual);

        UpdateLocationHierarchyVisibility();
        UpdateLinksForCurrentNodePositions();

        TrySaveLocationLayout(
            visual);
    }

    private void ToggleLocationLocked(
        Guid locationId,
        bool? locked = null)
    {
        var visual =
            LocationVisual(
                locationId);

        if (visual == null)
        {
            return;
        }

        visual.IsLocked =
            locked ??
            !visual.IsLocked;

        UpdateLocationVisualState(
            visual);

        TrySaveLocationLayout(
            visual);

        UpdateSelectedLayoutControl();
    }

    private void OnMapLocationContextMenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        var menu =
            sender as ContextMenu;

        var target =
            menu == null
                ? null
                : menu.Tag
                    as FrameworkElement;

        if (target == null ||
            !(target.Tag is Guid) ||
            menu.Items.Count < 6)
        {
            return;
        }

        var visual =
            LocationVisual(
                (Guid)target.Tag);

        if (visual == null)
        {
            return;
        }

        var edit =
            menu.Items[0]
                as MenuItem;

        var collapse =
            menu.Items[2]
                as MenuItem;

        var lockItem =
            menu.Items[3]
                as MenuItem;

        var delete =
            menu.Items[5]
                as MenuItem;

        if (edit != null)
        {
            edit.Header =
                UiText.Get(
                    "MapLocationEdit");
        }

        if (collapse != null)
        {
            collapse.Header =
                UiText.Get(
                    visual.IsCollapsed
                        ? "MapLocationExpand"
                        : "MapLocationCollapse");
        }

        if (lockItem != null)
        {
            lockItem.Header =
                UiText.Get(
                    visual.IsLocked
                        ? "MapLocationUnlock"
                        : "MapLocationLock");
        }

        if (delete != null)
        {
            delete.Header =
                UiText.Get(
                    "LocationTopologyDelete");
        }
    }

    private async void OnMapLocationContextEditClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            await OpenLocationTopologyEditorAsync(
                locationId.Value);
        }
    }

    private void OnMapLocationContextCollapseClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            ToggleLocationCollapsed(
                locationId.Value);
        }
    }

    private void OnMapLocationContextLockClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            ToggleLocationLocked(
                locationId.Value);
        }
    }

    private async void OnMapLocationContextDeleteClick(
        object sender,
        RoutedEventArgs e)
    {
        var locationId =
            LocationIdFromMenuItem(
                sender);

        if (locationId.HasValue)
        {
            await DeleteLocationFromMapAsync(
                locationId.Value);
        }
    }

    private async Task DeleteLocationFromMapAsync(
        Guid locationId)
    {
        var location =
            _lastMapSnapshot == null
                ? null
                : _lastMapSnapshot.Locations
                    .FirstOrDefault(
                        item =>
                            item.Id ==
                            locationId);

        if (location == null)
        {
            return;
        }

        var answer =
            MessageBox.Show(
                this,
                UiText.Format(
                    "LocationTopologyDeleteConfirm",
                    location.Name),
                UiText.Get(
                    "LocationTopologyWindowTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

        if (answer !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _locationTopologyService.DeleteLocation(
                locationId);

            _selectedLocationId = null;
            _persistedLocationLayouts.Remove(
                locationId);

            await RefreshTopologyAsync();
        }
        catch (Exception error)
        {
            Trace.TraceError(
                "LOCATION_MAP_DELETE_FAILED " +
                error);

            MessageBox.Show(
                this,
                UiText.Get(
                    "LocationTopologyDeleteBlocked"),
                UiText.Get(
                    "LocationTopologyWindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static Guid? MapElementIdFromMenuItem(
        object sender)
    {
        var menuItem =
            sender as MenuItem;

        var target =
            menuItem == null
                ? null
                : menuItem.Tag as FrameworkElement;

        return target != null &&
               target.Tag is Guid
            ? (Guid?)((Guid)target.Tag)
            : null;
    }

    private bool IsManualDevice(
        Guid deviceId)
    {
        try
        {
            return _manualTopologyService
                .GetSnapshot()
                .Devices
                .Any(
                    item =>
                        item.DeviceId == deviceId &&
                        item.IsManual);
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "MANUAL_TOPOLOGY_MAP_DEVICE_LOOKUP_FAILED " +
                exception);

            return false;
        }
    }

    private bool IsManualLink(
        Guid physicalLinkId)
    {
        try
        {
            return _manualTopologyService
                .GetSnapshot()
                .Links
                .Any(
                    item =>
                        item.PhysicalLinkId ==
                            physicalLinkId &&
                        item.IsManual);
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "MANUAL_TOPOLOGY_MAP_LINK_LOOKUP_FAILED " +
                exception);

            return false;
        }
    }

    private async Task DeleteManualDeviceFromMapAsync(
        Guid deviceId)
    {
        ManualTopologyEditorSnapshot snapshot;

        try
        {
            snapshot =
                _manualTopologyService
                    .GetSnapshot();
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
            return;
        }

        var device =
            snapshot.Devices
                .FirstOrDefault(
                    item =>
                        item.DeviceId == deviceId);

        if (device == null ||
            !device.IsManual)
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualDevice");
            return;
        }

        if (snapshot.Links.Any(
                link =>
                    link.DeviceAId == deviceId ||
                    link.DeviceBId == deviceId))
        {
            ShowManualTopologyWarning(
                "ManualTopologyDeleteConnectedDevice");
            return;
        }

        if (!ConfirmManualTopologyDelete(
                "ManualTopologyConfirmDeleteDevice",
                device.DisplayName))
        {
            return;
        }

        try
        {
            _manualTopologyService.DeleteDevice(
                deviceId);

            if (_selectedDeviceId == deviceId)
            {
                _selectedDeviceId = null;
            }

            await RefreshTopologyAsync();
        }
        catch (InvalidOperationException exception)
        {
            Trace.TraceError(
                "MANUAL_TOPOLOGY_MAP_DELETE_DEVICE_BLOCKED " +
                exception);

            ShowManualTopologyWarning(
                "ManualTopologyDeleteConnectedDevice");
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
        }
    }

    private async Task DeleteManualLinkFromMapAsync(
        Guid physicalLinkId)
    {
        ManualTopologyEditorSnapshot snapshot;

        try
        {
            snapshot =
                _manualTopologyService
                    .GetSnapshot();
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
            return;
        }

        var link =
            snapshot.Links
                .FirstOrDefault(
                    item =>
                        item.PhysicalLinkId ==
                            physicalLinkId);

        if (link == null ||
            !link.IsManual)
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualLink");
            return;
        }

        if (!ConfirmManualTopologyDelete(
                "ManualTopologyConfirmDeleteLink",
                ManualTopologyLinkDisplayName(
                    snapshot,
                    link)))
        {
            return;
        }

        try
        {
            _manualTopologyService.DeleteLink(
                physicalLinkId);

            if (_selectedPhysicalLinkId ==
                physicalLinkId)
            {
                _selectedPhysicalLinkId = null;
            }

            await RefreshTopologyAsync();
        }
        catch (Exception exception)
        {
            ShowManualTopologyOperationFailure(
                exception);
        }
    }

    private static string ManualTopologyLinkDisplayName(
        ManualTopologyEditorSnapshot snapshot,
        ManualTopologyLinkItem link)
    {
        var sideA =
            snapshot.Devices
                .FirstOrDefault(
                    item =>
                        item.DeviceId ==
                            link.DeviceAId);

        var sideB =
            snapshot.Devices
                .FirstOrDefault(
                    item =>
                        item.DeviceId ==
                            link.DeviceBId);

        return
            (sideA == null
                ? link.DeviceAId.ToString("D")
                : sideA.DisplayName) +
            " ↔ " +
            (sideB == null
                ? link.DeviceBId.ToString("D")
                : sideB.DisplayName);
    }

    private bool ConfirmManualTopologyDelete(
        string messageKey,
        string displayName)
    {
        return MessageBox.Show(
            this,
            UiText.Format(
                messageKey,
                displayName),
            UiText.Get(
                "ManualTopologyConfirmDeleteTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) ==
            MessageBoxResult.Yes;
    }

    private void ShowManualTopologyWarning(
        string messageKey)
    {
        MessageBox.Show(
            this,
            UiText.Get(
                messageKey),
            UiText.Get(
                "ManualTopologyWarningTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void ShowManualTopologyOperationFailure(
        Exception exception)
    {
        Trace.TraceError(
            "MANUAL_TOPOLOGY_MAP_OPERATION_FAILED " +
            exception);

        MessageBox.Show(
            this,
            UiText.Get(
                "ManualTopologyOperationFailed"),
            UiText.Get(
                "ManualTopologyErrorTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
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
        if (_suppressLockSelectedChange)
        {
            return;
        }

        if (_selectedLocationId.HasValue)
        {
            ToggleLocationLocked(
                _selectedLocationId.Value,
                MapLockSelectedCheckBox.IsChecked ==
                    true);

            return;
        }

        if (!_selectedDeviceId.HasValue)
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

    private async void OnMapLinkMouseLeftButtonDown(
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

        var physicalLinkId =
            (Guid)element.Tag;

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedPhysicalLinkId =
            physicalLinkId;

        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();

        if (e.ClickCount >= 2 &&
            IsManualLink(
                physicalLinkId))
        {
            e.Handled = true;

            await OpenManualTopologyEditorAsync(
                null,
                physicalLinkId);

            return;
        }

        e.Handled = true;
    }

    private void OnMapLinkMouseRightButtonDown(
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
        _selectedDeviceId = null;
        _selectedPhysicalLinkId =
            (Guid)element.Tag;

        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private void OnMapLinkContextMenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        var menu =
            sender as ContextMenu;

        var target =
            menu == null
                ? null
                : menu.Tag as FrameworkElement;

        var canChange =
            target != null &&
            target.Tag is Guid &&
            IsManualLink(
                (Guid)target.Tag);

        UpdateMapElementContextMenuState(
            menu,
            canChange);
    }

    private async void OnMapLinkContextEditClick(
        object sender,
        RoutedEventArgs e)
    {
        var physicalLinkId =
            MapElementIdFromMenuItem(sender);

        if (!physicalLinkId.HasValue ||
            !IsManualLink(
                physicalLinkId.Value))
        {
            ShowManualTopologyWarning(
                "ManualTopologyValidationManualLink");
            return;
        }

        await OpenManualTopologyEditorAsync(
            null,
            physicalLinkId.Value);
    }

    private async void OnMapLinkContextDeleteClick(
        object sender,
        RoutedEventArgs e)
    {
        var physicalLinkId =
            MapElementIdFromMenuItem(sender);

        if (!physicalLinkId.HasValue)
        {
            return;
        }

        await DeleteManualLinkFromMapAsync(
            physicalLinkId.Value);
    }

    private async void OnMapLinkKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
        {
            return;
        }

        var element =
            sender as FrameworkElement;

        if (element == null ||
            !(element.Tag is Guid))
        {
            return;
        }

        e.Handled = true;

        await DeleteManualLinkFromMapAsync(
            (Guid)element.Tag);
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
        _selectedLocationId = null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private sealed class MapLocationVisual
    {
        public MapLocationVisual(
            Border border,
            FrameworkElement header,
            TextBlock title,
            TextBlock description,
            Button collapseButton,
            TextBlock lockBadge,
            Thumb resizeThumb)
        {
            Border = border;
            Header = header;
            Title = title;
            Description = description;
            CollapseButton = collapseButton;
            LockBadge = lockBadge;
            ResizeThumb = resizeThumb;
        }

        public Border Border { get; }

        public FrameworkElement Header { get; }

        public TextBlock Title { get; }

        public TextBlock Description { get; }

        public Button CollapseButton { get; }

        public TextBlock LockBadge { get; }

        public Thumb ResizeThumb { get; }

        public Guid LocationId { get; set; }

        public Guid? ParentLocationId { get; set; }

        public bool IsCollapsed { get; set; }

        public bool IsLocked { get; set; }

        public double ExpandedWidth { get; set; }

        public double ExpandedHeight { get; set; }
    }

    private sealed class MapNodeVisual
    {
        public MapNodeVisual(
            Border border,
            TextBlock title,
            TextBlock secondary,
            TextBlock topologyMetadata,
            TextBlock managementAddress,
            TextBlock location,
            TextBlock lockBadge)
        {
            Border = border;
            Title = title;
            Secondary = secondary;
            TopologyMetadata = topologyMetadata;
            ManagementAddress = managementAddress;
            Location = location;
            LockBadge = lockBadge;
        }

        public Border Border { get; }

        public TextBlock Title { get; }

        public TextBlock Secondary { get; }

        public TextBlock TopologyMetadata { get; }

        public TextBlock ManagementAddress { get; }

        public TextBlock Location { get; }

        public TextBlock LockBadge { get; }

        public Guid? DeviceId { get; set; }

        public Guid? LocationId { get; set; }

        public bool IsManual { get; set; }

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

    private sealed class EmptyLocationTopologyService :
        ILocationTopologyService
    {
        public LocationTopologySnapshot GetSnapshot()
        {
            return new LocationTopologySnapshot(
                new LocationTopologyLocation[0],
                new LocationTopologyDevice[0]);
        }

        public Guid CreateLocation(
            Guid? parentLocationId,
            string name,
            string description)
        {
            throw new InvalidOperationException(
                "Location topology service is not configured.");
        }

        public void UpdateLocation(
            Guid locationId,
            Guid? parentLocationId,
            string name,
            string description)
        {
            throw new InvalidOperationException(
                "Location topology service is not configured.");
        }

        public void DeleteLocation(
            Guid locationId)
        {
            throw new InvalidOperationException(
                "Location topology service is not configured.");
        }

        public void AssignDevice(
            Guid deviceId,
            Guid? locationId)
        {
            throw new InvalidOperationException(
                "Location topology service is not configured.");
        }
    }

    private sealed class EmptyMapLocationLayoutStore :
        IMapLocationLayoutStore
    {
        public void SaveLocation(
            Guid mapId,
            MapLocationLayout locationLayout)
        {
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
