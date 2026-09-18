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
