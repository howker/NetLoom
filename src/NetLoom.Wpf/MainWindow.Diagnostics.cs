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
    private Guid? _inspectorEntityId;

    private TopologyAlert _inspectorPrimaryAlert;

    private enum InspectorProblemSubject
    {
        Generic = 0,
        Device = 1,
        Link = 2
    }

    private void ShowSelectedDiagnostic()
    {
        UpdateShellBreadcrumb();

        if (_selectedLocationId.HasValue)
        {
            if (ShowLocationDiagnostic(
                    _selectedLocationId.Value))
            {
                SynchronizeMonitoringSelection(
                    null);
                return;
            }

            _selectedLocationId = null;
        }

        if (_lastDiagnosticSnapshot == null)
        {
            SynchronizeMonitoringSelection(
                null);
            ClearDiagnosticPanel(
                "DiagnosticNothingSelected");
            return;
        }

        if (_selectedInterfaceId.HasValue &&
            _selectedDeviceId.HasValue)
        {
            var interfaceDevice =
                _lastDiagnosticSnapshot.Devices
                    .FirstOrDefault(
                        item =>
                            item.DeviceId ==
                            _selectedDeviceId.Value);

            var selectedInterface =
                interfaceDevice == null
                    ? null
                    : interfaceDevice.Interfaces
                        .FirstOrDefault(
                            item =>
                                item.InterfaceId ==
                                _selectedInterfaceId.Value);

            if (selectedInterface != null)
            {
                SynchronizeMonitoringSelection(
                    interfaceDevice);
                ShowInterfaceDiagnostic(
                    interfaceDevice,
                    selectedInterface);
                return;
            }

            _selectedInterfaceId = null;
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
                SynchronizeMonitoringSelection(
                    null);
                ClearDiagnosticPanel(
                    "DiagnosticSelectionMissing");
                return;
            }

            SynchronizeMonitoringSelection(
                device);
            ShowDeviceDiagnostic(device);
            return;
        }

        if (_selectedPhysicalLinkId.HasValue)
        {
            SynchronizeMonitoringSelection(
                null);

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

        SynchronizeMonitoringSelection(
            null);
        ClearDiagnosticPanel(
            "DiagnosticNothingSelected");
    }

    private void UpdateShellBreadcrumb()
    {
        var context =
            SelectedPhysicalContextPath();

        // G2 (sprint46-mockup-gap, ADR-083 п. 2): хлебные крошки — путь размещений «АГПЗ › ГПП-1 › Серверная».
        // Выбор не сбрасывается при смене раздела, поэтому путь виден во всех разделах, как на макетах.
        // Без выбранного размещённого объекта — название открытого раздела (раньше всегда «Карта»).
        // Если путь не помещается в MaxWidth, скрывается начало пути («… › »), полный путь — в подсказке.
        if (string.IsNullOrWhiteSpace(
                context))
        {
            SetShellBreadcrumb(
                string.Empty,
                UiText.Get(
                    ShellSectionTitleKey(
                        _shellSection)));
            ShellBreadcrumbText.ToolTip =
                null;
            return;
        }

        var segments =
            context
                .Split(
                    new[] { " / " },
                    StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        var full =
            string.Join(
                BreadcrumbSeparator,
                segments);

        var text =
            full;

        while (segments.Count > 1 &&
               BreadcrumbWidth(
                   text) >
               BreadcrumbAvailableWidth())
        {
            segments.RemoveAt(0);
            text =
                BreadcrumbEllipsis +
                BreadcrumbSeparator +
                string.Join(
                    BreadcrumbSeparator,
                    segments);
        }

        var currentStart =
            text.LastIndexOf(
                BreadcrumbSeparator,
                StringComparison.Ordinal);

        SetShellBreadcrumb(
            currentStart < 0
                ? string.Empty
                : text.Substring(
                    0,
                    currentStart +
                    BreadcrumbSeparator.Length),
            currentStart < 0
                ? text
                : text.Substring(
                    currentStart +
                    BreadcrumbSeparator.Length));
        ShellBreadcrumbText.ToolTip =
            full;
    }

    // Как на макетах ADR-083: начало пути приглушённым цветом, текущее место — основным цветом и полужирным.
    private void SetShellBreadcrumb(
        string prefix,
        string current)
    {
        ShellBreadcrumbText.Inlines.Clear();

        if (!string.IsNullOrEmpty(
                prefix))
        {
            ShellBreadcrumbText.Inlines.Add(
                new System.Windows.Documents.Run(
                    prefix));
        }

        var currentRun =
            new System.Windows.Documents.Run(
                current)
            {
                FontWeight =
                    FontWeights.SemiBold
            };

        currentRun.SetResourceReference(
            System.Windows.Documents.TextElement.ForegroundProperty,
            "NetLoom.Brush.TextPrimary");

        ShellBreadcrumbText.Inlines.Add(
            currentRun);
    }

    private static string ShellSectionTitleKey(
        ShellSection section)
    {
        switch (section)
        {
            case ShellSection.Equipment:
                return "ShellEquipmentSection";
            case ShellSection.Monitoring:
                return "ShellMonitoringLabel";
            case ShellSection.Alerts:
                return "ShellAlertsSection";
            case ShellSection.Discovery:
                return "ShellDiscoverySection";
            case ShellSection.Search:
                return "ShellSearchSection";
            case ShellSection.Settings:
                return "ShellSettingsSection";
            default:
                return "ShellMapSection";
        }
    }

    private const string BreadcrumbSeparator = " › ";

    private const string BreadcrumbEllipsis = "…";

    private double BreadcrumbAvailableWidth()
    {
        var maxWidth =
            ShellBreadcrumbText.MaxWidth;

        return double.IsInfinity(maxWidth) ||
               double.IsNaN(maxWidth)
            ? double.MaxValue
            : maxWidth -
              ShellBreadcrumbText.Padding.Left -
              ShellBreadcrumbText.Padding.Right;
    }

    private double BreadcrumbWidth(
        string text)
    {
        var formatted =
            new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                ShellBreadcrumbText.FlowDirection,
                new Typeface(
                    ShellBreadcrumbText.FontFamily,
                    ShellBreadcrumbText.FontStyle,
                    // Мерим полужирным: текущее место полужирное, так оценка не меньше фактической ширины.
                    FontWeights.SemiBold,
                    ShellBreadcrumbText.FontStretch),
                ShellBreadcrumbText.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(
                    ShellBreadcrumbText).PixelsPerDip);

        return formatted.WidthIncludingTrailingWhitespace;
    }

    private string SelectedPhysicalContextPath()
    {
        if (_lastMapSnapshot == null)
        {
            return null;
        }

        if (_selectedLocationId.HasValue)
        {
            return LocationPath(
                _selectedLocationId.Value);
        }

        if (_selectedDeviceId.HasValue)
        {
            return LocationPathForDevice(
                _selectedDeviceId.Value);
        }

        if (_selectedPhysicalLinkId.HasValue &&
            _lastDiagnosticSnapshot != null)
        {
            var link =
                _lastDiagnosticSnapshot.Links
                    .FirstOrDefault(
                        item =>
                            item.PhysicalLinkId ==
                            _selectedPhysicalLinkId.Value);

            if (link == null)
            {
                return null;
            }

            var sideA =
                LocationPathForDevice(
                    link.DeviceAId);

            var sideB =
                LocationPathForDevice(
                    link.DeviceBId);

            if (string.Equals(
                    sideA,
                    sideB,
                    StringComparison.CurrentCulture))
            {
                return sideA;
            }

            if (string.IsNullOrWhiteSpace(
                sideA))
            {
                return sideB;
            }

            if (string.IsNullOrWhiteSpace(
                sideB))
            {
                return sideA;
            }

            return sideA +
                   " ↔ " +
                   sideB;
        }

        return null;
    }

    private string LocationPathForDevice(
        Guid deviceId)
    {
        if (_lastMapSnapshot == null)
        {
            return null;
        }

        var node =
            _lastMapSnapshot.Nodes
                .FirstOrDefault(
                    item =>
                        item.DeviceId.HasValue &&
                        item.DeviceId.Value ==
                        deviceId);

        return node == null ||
               !node.LocationId.HasValue
            ? null
            : LocationPath(
                node.LocationId.Value);
    }

    private string LocationPath(
        Guid locationId)
    {
        if (_lastMapSnapshot == null)
        {
            return null;
        }

        var location =
            _lastMapSnapshot.Locations
                .FirstOrDefault(
                    item =>
                        item.Id ==
                        locationId);

        if (location == null)
        {
            return null;
        }

        var byId =
            _lastMapSnapshot.Locations
                .ToDictionary(
                    item => item.Id);

        return BuildLocationPath(
            location,
            byId);
    }

    private void SetInspectorEntity(
        string typeKey,
        Guid entityId)
    {
        var preserveExpanded =
            _inspectorEntityId.HasValue &&
            _inspectorEntityId.Value ==
                entityId &&
            InspectorTechnicalDetailsExpander.IsExpanded;

        _inspectorEntityId =
            entityId;

        InspectorEntityTypeText.Text =
            UiText.Get(
                typeKey);

        InspectorEntityIdText.Text =
            UiText.Format(
                "InspectorEntityId",
                entityId.ToString("D"));

        InspectorTechnicalDetailsExpander.Visibility =
            Visibility.Visible;
        InspectorTechnicalDetailsExpander.IsExpanded =
            preserveExpanded;
    }

    private void ClearInspectorEntity()
    {
        _inspectorEntityId =
            null;
        _inspectorPrimaryAlert =
            null;

        InspectorProblemText.Text =
            string.Empty;
        InspectorProblemText.Visibility =
            Visibility.Collapsed;
        InspectorProblemExplanationText.Text =
            string.Empty;
        InspectorProblemExplanationText.Visibility =
            Visibility.Collapsed;
        InspectorPrimaryActionButton.Visibility =
            Visibility.Collapsed;

        InspectorEntityTypeText.Text =
            string.Empty;

        ClearInspectorOperatorStatus();

        InspectorEntityIdText.Text =
            string.Empty;

        InspectorTechnicalDetailsExpander.IsExpanded =
            false;
        InspectorTechnicalDetailsExpander.Visibility =
            Visibility.Collapsed;
    }

    private void ConfigureInspectorTabs(
        bool interfacesVisible,
        bool linksVisible,
        bool evidenceVisible)
    {
        InspectorTabControl.Visibility =
            Visibility.Visible;

        InspectorOverviewTab.Visibility =
            Visibility.Visible;

        InspectorInterfacesTab.Visibility =
            interfacesVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

        InspectorLinksTab.Visibility =
            linksVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

        InspectorEvidenceTab.Visibility =
            evidenceVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

        var selected =
            InspectorTabControl.SelectedItem
                as TabItem;

        if (selected == null ||
            selected.Visibility !=
                Visibility.Visible)
        {
            InspectorTabControl.SelectedItem =
                InspectorOverviewTab;
        }
    }

    private void OnDiagnosticInterfaceClick(
        object sender,
        RoutedEventArgs e)
    {
        var button =
            sender as Button;

        if (button == null ||
            !(button.Tag is Guid) ||
            (Guid)button.Tag ==
                Guid.Empty ||
            _lastDiagnosticSnapshot == null)
        {
            return;
        }

        var interfaceId =
            (Guid)button.Tag;

        var diagnostic =
            _lastDiagnosticSnapshot.Devices
                .SelectMany(
                    device =>
                        device.Interfaces)
                .FirstOrDefault(
                    item =>
                        item.InterfaceId ==
                        interfaceId);

        if (diagnostic == null)
        {
            return;
        }

        StopStartupTopologyFit();

        _selectedDeviceId =
            diagnostic.DeviceId;
        _selectedInterfaceId =
            interfaceId;
        _selectedPhysicalLinkId =
            null;
        _selectedLocationId =
            null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private void OnDiagnosticLinkClick(
        object sender,
        RoutedEventArgs e)
    {
        var button =
            sender as Button;

        if (button == null ||
            !(button.Tag is Guid) ||
            (Guid)button.Tag ==
                Guid.Empty)
        {
            return;
        }

        StopStartupTopologyFit();

        _selectedDeviceId =
            null;
        _selectedInterfaceId =
            null;
        _selectedPhysicalLinkId =
            (Guid)button.Tag;
        _selectedLocationId =
            null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private bool ShowLocationDiagnostic(
        Guid locationId)
    {
        if (_lastMapSnapshot == null)
        {
            return false;
        }

        var location =
            _lastMapSnapshot.Locations
                .FirstOrDefault(
                    item =>
                        item.Id ==
                        locationId);

        if (location == null)
        {
            return false;
        }

        SetInspectorEntity(
            "InspectorEntityLocation",
            location.Id);

        ConfigureInspectorTabs(
            false,
            false,
            false);

        DiagnosticInterfaceList.ItemsSource =
            new DiagnosticEntityRow[0];

        DiagnosticLinkList.ItemsSource =
            new DiagnosticEntityRow[0];

        var byId =
            _lastMapSnapshot.Locations
                .ToDictionary(
                    item => item.Id);

        var visual =
            LocationVisual(
                locationId);

        var directDevices =
            _lastMapSnapshot.Nodes.Count(
                item =>
                    item.LocationId ==
                    locationId);

        var childLocations =
            _lastMapSnapshot.Locations.Count(
                item =>
                    item.ParentLocationId ==
                    locationId);

        DiagnosticStatusText.Visibility =
            Visibility.Collapsed;
        DiagnosticStatusText.Text =
            string.Empty;

        SetInspectorLocationStatus(
            locationId);

        ConfigureInspectorProblem(
            InspectorAlertForLocation(
                locationId),
            InspectorProblemSubject.Generic);

        DiagnosticElementTitleText.Text =
            location.Name;

        DiagnosticElementSubtitleText.Text =
            BuildLocationPath(
                location,
                byId);

        DiagnosticPrimaryTitleText.Text =
            UiText.Get(
                "MapLocationDetailsTitle");

        var fields =
            new List<DiagnosticFieldRow>
            {
                new DiagnosticFieldRow(
                    UiText.Get(
                        "MapLocationDeviceCount"),
                    directDevices.ToString(
                        CultureInfo.CurrentCulture)),
                new DiagnosticFieldRow(
                    UiText.Get(
                        "MapLocationChildCount"),
                    childLocations.ToString(
                        CultureInfo.CurrentCulture)),
                new DiagnosticFieldRow(
                    UiText.Get(
                        "InspectorLocationProblemCount"),
                    CountLocationAlerts(
                        locationId)
                        .ToString(
                            CultureInfo.CurrentCulture))
            };

        if (visual != null)
        {
            fields.Add(
                new DiagnosticFieldRow(
                    UiText.Get(
                        "MapLocationState"),
                    UiText.Get(
                        visual.IsCollapsed
                            ? "MapLocationStateCollapsed"
                            : "MapLocationStateExpanded")));

            fields.Add(
                new DiagnosticFieldRow(
                    UiText.Get(
                        "MapLocationLockState"),
                    UiText.Get(
                        visual.IsLocked
                            ? "MapLocationStateLocked"
                            : "MapLocationStateUnlocked")));
        }

        DiagnosticFieldsList.ItemsSource =
            fields;

        DiagnosticSecondaryTitleText.Text =
            UiText.Get(
                "MapLocationDescriptionTitle");

        DiagnosticSecondaryList.ItemsSource =
            new[]
            {
                new DiagnosticTextRow(
                    string.IsNullOrWhiteSpace(
                        location.Description)
                        ? UiText.Get(
                            "MapLocationNoDescription")
                        : location.Description)
            };

        DiagnosticTertiaryTitleText.Text =
            string.Empty;

        DiagnosticTertiaryList.ItemsSource =
            new DiagnosticTextRow[0];

        return true;
    }

    private void ClearDiagnosticPanel(
        string statusKey)
    {
        UpdateShellBreadcrumb();
        ClearInspectorEntity();

        ConfigureInspectorTabs(
            false,
            false,
            false);

        InspectorTabControl.Visibility =
            Visibility.Collapsed;

        DiagnosticStatusText.Visibility =
            Visibility.Visible;
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

        DiagnosticInterfaceList.ItemsSource =
            new DiagnosticEntityRow[0];

        DiagnosticLinkList.ItemsSource =
            new DiagnosticEntityRow[0];

        DiagnosticSecondaryList.ItemsSource =
            new DiagnosticTextRow[0];

        DiagnosticTertiaryList.ItemsSource =
            new DiagnosticTextRow[0];

        UpdateSelectedLayoutControl();
    }

    private void ShowDeviceDiagnostic(
        DeviceDiagnostic device)
    {
        SetInspectorEntity(
            "InspectorEntityDevice",
            device.DeviceId);

        ConfigureInspectorTabs(
            true,
            true,
            true);

        SetInspectorPortHeaders();

        DiagnosticStatusText.Text =
            string.Empty;
        DiagnosticStatusText.Visibility =
            Visibility.Collapsed;

        SetInspectorDeviceAvailability(
            device);

        var inspectorAlert =
            InspectorAlertForDevice(
                device.DeviceId);

        ConfigureInspectorProblem(
            inspectorAlert,
            InspectorProblemSubject.Device);

        DiagnosticElementTitleText.Text =
            string.IsNullOrWhiteSpace(
                device.DisplayName)
                ? UiText.Get("NodeUnknownLabel")
                : device.DisplayName;

        DiagnosticElementSubtitleText.Text =
            string.Empty;

        DiagnosticPrimaryTitleText.Text =
            UiText.Get("DiagnosticStateTitle");

        var degradedCount =
            device.Interfaces.Count(
                item =>
                    item.DegradationStatus ==
                    DiagnosticDegradationStatus.Degraded);

        var stateFields =
            new List<DiagnosticFieldRow>();

        stateFields.Add(
            Field(
                "DiagnosticFieldManagementAddress",
                device.ManagementAddress));

        stateFields.Add(
            Field(
                "DiagnosticFieldDescription",
                device.SecondaryText));

        var deviceLocationPath =
            LocationPathForDevice(
                device.DeviceId);

        if (string.IsNullOrWhiteSpace(
            deviceLocationPath))
        {
            deviceLocationPath =
                string.IsNullOrWhiteSpace(
                    device.LocationName)
                    ? UiText.Get(
                        "DiagnosticLocationNotAssigned")
                    : device.LocationName;
        }

        stateFields.Add(
            Field(
                "DiagnosticFieldLocation",
                deviceLocationPath));

        if (device.LastSeenUtc.HasValue)
        {
            stateFields.Add(
                Field(
                    "DiagnosticFieldLastSeen",
                    RelativeTimeText(
                        device.LastSeenUtc)));
        }

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
                .ToArray();

        stateFields.Add(
            Field(
                "DiagnosticFieldConnections",
                connectedLinks.Length.ToString(
                    CultureInfo.CurrentCulture)));

        stateFields.Add(
            Field(
                "DiagnosticFieldInterfaces",
                device.Interfaces.Count.ToString(
                    CultureInfo.CurrentCulture)));

        if (degradedCount > 0)
        {
            stateFields.Add(
                Field(
                    "DiagnosticFieldDegradedInterfaces",
                    degradedCount.ToString(
                        CultureInfo.CurrentCulture)));
        }

        DiagnosticFieldsList.ItemsSource =
            stateFields;

        DiagnosticSecondaryTitleText.Text =
            string.Empty;

        DiagnosticSecondaryList.ItemsSource =
            new DiagnosticTextRow[0];

        DiagnosticInterfaceList.ItemsSource =
            device.Interfaces.Count == 0
                ? new[]
                {
                    DiagnosticEntityRow.DisabledPort(
                        UiText.Get(
                            "DiagnosticNoInterfaces"))
                }
                : device.Interfaces
                    .Select(
                        item =>
                            BuildDiagnosticPortRow(
                                device.DeviceId,
                                item,
                                connectedLinks,
                                inspectorAlert))
                    .ToArray();

        DiagnosticLinkList.ItemsSource =
            connectedLinks.Length == 0
                ? new[]
                {
                    DiagnosticEntityRow.Disabled(
                        UiText.Get(
                            "DiagnosticNoConnections"))
                }
                : connectedLinks
                    .Select(
                        item =>
                            new DiagnosticEntityRow(
                                item.PhysicalLinkId,
                                BuildDeviceLinkDiagnosticText(
                                    device.DeviceId,
                                    item)))
                    .ToArray();

        DiagnosticTertiaryTitleText.Text =
            UiText.Get(
                "DiagnosticEvidenceTitle");

        var evidence =
            connectedLinks
                .SelectMany(
                    link =>
                        link.Evidence.Select(
                            item =>
                                new DiagnosticTextRow(
                                    PeerName(
                                        link,
                                        device.DeviceId) +
                                    " — " +
                                    BuildEvidenceText(
                                        item))))
                .ToArray();

        DiagnosticTertiaryList.ItemsSource =
            evidence.Length == 0
                ? new[]
                {
                    Row("DiagnosticNoEvidenceDevice")
                }
                : evidence;
    }

    private void ShowInterfaceDiagnostic(
        DeviceDiagnostic device,
        InterfaceDiagnostic item)
    {
        SetInspectorEntity(
            "InspectorEntityInterface",
            item.InterfaceId);

        ConfigureInspectorTabs(
            true,
            true,
            true);

        SetInspectorPortHeaders();

        DiagnosticStatusText.Text =
            string.Empty;
        DiagnosticStatusText.Visibility =
            Visibility.Collapsed;

        SetInspectorOperatorStatus(
            InterfaceStatusSemantic(
                item.DegradationStatus));

        ConfigureInspectorProblem(
            InspectorAlertForDevice(
                device.DeviceId),
            InspectorProblemSubject.Device);

        DiagnosticElementTitleText.Text =
            InterfaceIdentity(
                item);

        DiagnosticElementSubtitleText.Text =
            string.IsNullOrWhiteSpace(
                device.LocationName)
                ? DisplayDeviceName(
                    device.DisplayName)
                : DisplayDeviceName(
                    device.DisplayName) +
                  " • " +
                  device.LocationName;

        DiagnosticPrimaryTitleText.Text =
            UiText.Get(
                "DiagnosticStateTitle");

        DiagnosticFieldsList.ItemsSource =
            new[]
            {
                Field(
                    "InspectorFieldIfIndex",
                    item.IfIndex.HasValue
                        ? item.IfIndex.Value.ToString(
                            CultureInfo.CurrentCulture)
                        : null),
                Field(
                    "InspectorFieldIfAlias",
                    item.IfAlias),
                Field(
                    "InspectorFieldIfDescription",
                    item.IfDescription),
                Field(
                    "InspectorFieldIfType",
                    item.IfType.HasValue
                        ? IfTypeText(
                            item.IfType.Value)
                        : null),
                Field(
                    "InspectorFieldMac",
                    item.MacAddress),
                Field(
                    "InspectorFieldAdmin",
                    item.AdminStatus),
                Field(
                    "InspectorFieldOper",
                    item.OperStatus),
                Field(
                    "DiagnosticFieldSpeed",
                    SpeedText(
                        item.SpeedBps)),
                Field(
                    "DiagnosticFieldLastSeen",
                    RelativeTimeText(
                        item.LastSeenUtc)),
                Field(
                    "InspectorFieldStp",
                    StpStateText(
                        item.StpState)),
                Field(
                    "InspectorFieldDegradation",
                    DegradationText(
                        item))
            };

        DiagnosticSecondaryTitleText.Text =
            string.Empty;

        DiagnosticSecondaryList.ItemsSource =
            new DiagnosticTextRow[0];

        var deviceConnectedLinks =
            _lastDiagnosticSnapshot.Links
                .Where(
                    link =>
                        link.DeviceAId == device.DeviceId ||
                        link.DeviceBId == device.DeviceId)
                .OrderBy(
                    link =>
                        PeerName(
                            link,
                            device.DeviceId),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        var interfaceAlert =
            InspectorAlertForDevice(
                device.DeviceId);

        DiagnosticInterfaceList.ItemsSource =
            device.Interfaces.Count == 0
                ? new[]
                {
                    DiagnosticEntityRow.DisabledPort(
                        UiText.Get(
                            "DiagnosticNoInterfaces"))
                }
                : device.Interfaces
                    .Select(
                        candidate =>
                            BuildDiagnosticPortRow(
                                device.DeviceId,
                                candidate,
                                deviceConnectedLinks,
                                interfaceAlert))
                    .ToArray();

        var connectedLinks =
            deviceConnectedLinks
                .Where(
                    link =>
                        link.InterfaceAId ==
                            item.InterfaceId ||
                        link.InterfaceBId ==
                            item.InterfaceId)
                .ToArray();

        DiagnosticLinkList.ItemsSource =
            connectedLinks.Length == 0
                ? new[]
                {
                    DiagnosticEntityRow.Disabled(
                        UiText.Get(
                            "DiagnosticNoConnections"))
                }
                : connectedLinks
                    .Select(
                        link =>
                            new DiagnosticEntityRow(
                                link.PhysicalLinkId,
                                BuildDeviceLinkDiagnosticText(
                                    device.DeviceId,
                                    link)))
                    .ToArray();

        DiagnosticTertiaryTitleText.Text =
            UiText.Get(
                "DiagnosticEvidenceTitle");

        var evidence =
            connectedLinks
                .SelectMany(
                    link =>
                        link.Evidence.Select(
                            evidenceItem =>
                                new DiagnosticTextRow(
                                    PeerName(
                                        link,
                                        device.DeviceId) +
                                    " — " +
                                    BuildEvidenceText(
                                        evidenceItem))))
                .ToArray();

        DiagnosticTertiaryList.ItemsSource =
            evidence.Length == 0
                ? new[]
                {
                    Row("DiagnosticNoEvidenceInterface")
                }
                : evidence;
    }

    private void ShowLinkDiagnostic(
        PhysicalLinkDiagnostic link)
    {
        SetInspectorEntity(
            "InspectorEntityLink",
            link.PhysicalLinkId);

        ConfigureInspectorTabs(
            false,
            false,
            true);

        DiagnosticInterfaceList.ItemsSource =
            new DiagnosticEntityRow[0];

        DiagnosticLinkList.ItemsSource =
            new DiagnosticEntityRow[0];

        DiagnosticStatusText.Text =
            string.Empty;
        DiagnosticStatusText.Visibility =
            Visibility.Collapsed;

        SetInspectorLinkAvailability(
            link);

        ConfigureInspectorProblem(
            InspectorAlertForPhysicalLink(
                link.PhysicalLinkId),
            InspectorProblemSubject.Link);

        DiagnosticElementTitleText.Text =
            UiText.Format(
                "DiagnosticLinkTitle",
                DisplayDeviceName(
                    link.DeviceAName),
                DisplayDeviceName(
                    link.DeviceBName));

        DiagnosticElementSubtitleText.Text =
            string.Empty;

        DiagnosticPrimaryTitleText.Text =
            UiText.Get("DiagnosticStateTitle");

        var sideALocation =
            DisplayLocationForDevice(
                link.DeviceAId);
        var sideBLocation =
            DisplayLocationForDevice(
                link.DeviceBId);

        DiagnosticFieldsList.ItemsSource =
            new[]
            {
                Section(
                    DisplayDeviceName(
                        link.DeviceAName)),
                Field(
                    "DiagnosticFieldPort",
                    DisplayLinkEndpointInterfaceName(
                        link.InterfaceAId,
                        link.InterfaceAName)),
                Field(
                    "InspectorFieldStp",
                    StpStateText(
                        link.StpStateA)),
                Field(
                    "DiagnosticFieldLocation",
                    sideALocation),
                Section(
                    DisplayDeviceName(
                        link.DeviceBName)),
                Field(
                    "DiagnosticFieldPort",
                    DisplayLinkEndpointInterfaceName(
                        link.InterfaceBId,
                        link.InterfaceBName)),
                Field(
                    "InspectorFieldStp",
                    StpStateText(
                        link.StpStateB)),
                Field(
                    "DiagnosticFieldLocation",
                    sideBLocation),
                Field(
                    "DiagnosticFieldStrength",
                    LinkStrengthText(
                        link.Strength)),
                Field(
                    "DiagnosticFieldFreshness",
                    CurrentFreshnessText(
                        link)),
                Field(
                    "DiagnosticFieldLastSeen",
                    RelativeTimeText(
                        link.LastSeenUtc)),
                Field(
                    "DiagnosticFieldLastConfirmed",
                    RelativeTimeText(
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
                    link.SourceSummary)
            };

        DiagnosticSecondaryTitleText.Text =
            UiText.Get("DiagnosticImpactTitle");

        DiagnosticSecondaryList.ItemsSource =
            link.IsBridge
                ? new[]
                {
                    Row(
                        "DiagnosticImpactSinglePath"),
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

    private void SetInspectorLinkAvailability(
        PhysicalLinkDiagnostic link)
    {
        var age =
            RelativeTimeText(
                link.LastSeenUtc);

        string text;
        OperatorStatusSemantic semantic;

        if (link.StpStateA ==
                StpTreePortState.Broken ||
            link.StpStateB ==
                StpTreePortState.Broken)
        {
            text =
                UiText.Format(
                    "InspectorLinkAvailabilityStpBroken",
                    LinkEndpointNameForState(
                        link,
                        StpTreePortState.Broken),
                    age);
            semantic =
                OperatorStatusSemantic.Critical;
        }
        else if (link.StpStateA ==
                     StpTreePortState.Disabled ||
                 link.StpStateB ==
                     StpTreePortState.Disabled)
        {
            text =
                UiText.Format(
                    "InspectorLinkAvailabilityStpDisabled",
                    LinkEndpointNameForState(
                        link,
                        StpTreePortState.Disabled),
                    age);
            semantic =
                OperatorStatusSemantic.Stopped;
        }
        else if (link.StpStateA ==
                     StpTreePortState.Blocking ||
                 link.StpStateB ==
                     StpTreePortState.Blocking)
        {
            text =
                UiText.Format(
                    "InspectorLinkAvailabilityStpBlocking",
                    LinkEndpointNameForState(
                        link,
                        StpTreePortState.Blocking),
                    age);
            semantic =
                OperatorStatusSemantic.Blocked;
        }
        else if (IsTransitionalStpState(
                     link.StpStateA) ||
                 IsTransitionalStpState(
                     link.StpStateB))
        {
            text =
                UiText.Format(
                    "InspectorLinkAvailabilityStpTransition",
                    IsTransitionalStpState(
                        link.StpStateA)
                        ? DisplayDeviceName(
                            link.DeviceAName)
                        : DisplayDeviceName(
                            link.DeviceBName),
                    age);
            semantic =
                OperatorStatusSemantic.Transition;
        }
        else if (link.StpStateA ==
                     StpTreePortState.Forwarding ||
                 link.StpStateB ==
                     StpTreePortState.Forwarding)
        {
            text =
                UiText.Format(
                    "InspectorLinkAvailabilityForwarding",
                    age);
            semantic =
                OperatorStatusSemantic.Normal;
        }
        else
        {
            text =
                UiText.Format(
                    "InspectorLinkAvailabilityUnknown",
                    age);
            semantic =
                OperatorStatusSemantic.Unknown;
        }

        InspectorOperationalStatusText.Text =
            text;
        InspectorOperationalStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            OperatorStatusBrushKey(
                semantic));
        InspectorOperationalStatusText.Visibility =
            Visibility.Visible;
    }

    private static bool IsTransitionalStpState(
        StpTreePortState state)
    {
        return state ==
                   StpTreePortState.Listening ||
               state ==
                   StpTreePortState.Learning;
    }

    private static string LinkEndpointNameForState(
        PhysicalLinkDiagnostic link,
        StpTreePortState state)
    {
        return DisplayDeviceName(
            link.StpStateA == state
                ? link.DeviceAName
                : link.DeviceBName);
    }

    private void SetInspectorDeviceAvailability(
        DeviceDiagnostic device)
    {
        string text;
        string brushKey;

        if (_monitoringControl.Current.State ==
            NetLoom.Application.MonitoringControl.MonitoringControlState.Stopped)
        {
            text =
                device.LastSeenUtc.HasValue
                    ? UiText.Format(
                        "InspectorAvailabilityMonitoringStopped",
                        RelativeTimeText(
                            device.LastSeenUtc))
                    : UiText.Get(
                        "InspectorAvailabilityMonitoringStoppedNoData");
            brushKey =
                "NetLoom.Brush.ShellRailTextMuted";
        }
        else if (!device.LastSeenUtc.HasValue)
        {
            text =
                UiText.Get(
                    "InspectorAvailabilityNoData");
            brushKey =
                "NetLoom.Brush.ShellRailTextMuted";
        }
        else
        {
            text =
                UiText.Format(
                    "InspectorAvailabilityIndirect",
                    RelativeTimeText(
                        device.LastSeenUtc));
            brushKey =
                "NetLoom.Brush.Warning";
        }

        InspectorOperationalStatusText.Text =
            text;
        InspectorOperationalStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            brushKey);
        InspectorOperationalStatusText.Visibility =
            Visibility.Visible;
    }

    private void SetInspectorLocationStatus(
        Guid locationId)
    {
        var problemCount =
            CountLocationAlerts(
                locationId);

        InspectorOperationalStatusText.Text =
            problemCount == 0
                ? UiText.Get(
                    "InspectorLocationNoProblems")
                : UiText.Format(
                    "InspectorLocationProblems",
                    problemCount);

        InspectorOperationalStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            problemCount == 0
                ? "NetLoom.Brush.TextSecondary" // норма нейтральна (ADR-083 п. 6); ключа Positive в темах нет
                : "NetLoom.Brush.Warning");
        InspectorOperationalStatusText.Visibility =
            Visibility.Visible;
    }

    private void ConfigureInspectorProblem(
        TopologyAlert alert,
        InspectorProblemSubject subject)
    {
        _inspectorPrimaryAlert =
            alert;

        if (alert == null)
        {
            InspectorProblemText.Text =
                UiText.Get(
                    "InspectorProblemNone");
            InspectorProblemText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "NetLoom.Brush.TextSecondary");
            InspectorProblemText.Visibility =
                Visibility.Visible;
            InspectorProblemExplanationText.Text =
                string.Empty;
            InspectorProblemExplanationText.Visibility =
                Visibility.Collapsed;
            InspectorPrimaryActionButton.Visibility =
                Visibility.Collapsed;
            UpdateInspectorShowOnMapAction();
            return;
        }

        InspectorProblemText.Text =
            AlertSeverityText(
                alert.Severity) +
            " — " +
            BuildInspectorProblemTitle(
                alert,
                subject);
        InspectorProblemText.SetResourceReference(
            TextBlock.ForegroundProperty,
            alert.Severity ==
                TopologyAlertSeverity.Critical
                ? "NetLoom.Brush.Critical"
                : "NetLoom.Brush.Warning");
        InspectorProblemText.Visibility =
            Visibility.Visible;

        var explanation =
            BuildInspectorProblemExplanation(
                alert);

        InspectorProblemExplanationText.Text =
            explanation;
        InspectorProblemExplanationText.Visibility =
            string.IsNullOrWhiteSpace(
                explanation)
                ? Visibility.Collapsed
                : Visibility.Visible;

        InspectorPrimaryActionButton.Content =
            UiText.Get(
                alert.Kind ==
                    TopologyAlertKind.ForwardingCycle
                    ? "InspectorShowLoopAction"
                    : "AlertShowOnMapAction");
        InspectorPrimaryActionButton.Visibility =
            Visibility.Visible;
    }

    private string BuildInspectorProblemTitle(
        TopologyAlert alert,
        InspectorProblemSubject subject)
    {
        if (alert.Kind ==
            TopologyAlertKind.ForwardingCycle)
        {
            var resourceKey =
                subject == InspectorProblemSubject.Device
                    ? "InspectorProblemForwardingCycleDeviceTitle"
                    : subject == InspectorProblemSubject.Link
                        ? "InspectorProblemForwardingCycleLinkTitle"
                        : "InspectorProblemForwardingCycleGenericTitle";

            return UiText.Get(
                resourceKey);
        }

        return AlertKindText(
            alert.Kind);
    }

    private string BuildInspectorProblemExplanation(
        TopologyAlert alert)
    {
        if (alert.Kind ==
            TopologyAlertKind.ForwardingCycle)
        {
            return UiText.Format(
                       "InspectorProblemForwardingCycleScope",
                       BuildAlertScope(
                           alert)) +
                   Environment.NewLine +
                   UiText.Get(
                       "InspectorProblemForwardingCycleExplanation");
        }

        return BuildAlertReasonSummary(
            alert);
    }

    private TopologyAlert InspectorAlertForPhysicalLink(
        Guid physicalLinkId)
    {
        return _lastAlertSnapshot == null
            ? null
            : _lastAlertSnapshot.Alerts
                .FirstOrDefault(
                    alert =>
                        alert.PhysicalLinkIds.Contains(
                            physicalLinkId));
    }

    private TopologyAlert InspectorAlertForDevice(
        Guid deviceId)
    {
        if (_lastAlertSnapshot == null ||
            _lastMapSnapshot == null)
        {
            return null;
        }

        var node =
            _lastMapSnapshot.Nodes
                .FirstOrDefault(
                    item =>
                        item.DeviceId.HasValue &&
                        item.DeviceId.Value ==
                            deviceId);

        if (node == null)
        {
            return null;
        }

        var linkIds =
            new HashSet<Guid>(
                _lastMapSnapshot.Links
                    .Where(
                        link =>
                            link.PhysicalLinkId.HasValue &&
                            (
                                string.Equals(
                                    link.SourceNodeKey,
                                    node.Key,
                                    StringComparison.Ordinal) ||
                                string.Equals(
                                    link.TargetNodeKey,
                                    node.Key,
                                    StringComparison.Ordinal)
                            ))
                    .Select(
                        link =>
                            link.PhysicalLinkId.Value));

        return _lastAlertSnapshot.Alerts
            .FirstOrDefault(
                alert =>
                    alert.PhysicalLinkIds.Any(
                        linkIds.Contains));
    }

    private TopologyAlert InspectorAlertForLocation(
        Guid locationId)
    {
        if (_lastAlertSnapshot == null ||
            _lastMapSnapshot == null)
        {
            return null;
        }

        var locationIds =
            DescendantLocationIds(
                locationId);
        locationIds.Add(
            locationId);

        var nodeKeys =
            new HashSet<string>(
                _lastMapSnapshot.Nodes
                    .Where(
                        node =>
                            node.LocationId.HasValue &&
                            locationIds.Contains(
                                node.LocationId.Value))
                    .Select(
                        node => node.Key),
                StringComparer.Ordinal);

        var linkIds =
            new HashSet<Guid>(
                _lastMapSnapshot.Links
                    .Where(
                        link =>
                            link.PhysicalLinkId.HasValue &&
                            (
                                nodeKeys.Contains(
                                    link.SourceNodeKey) ||
                                nodeKeys.Contains(
                                    link.TargetNodeKey)
                            ))
                    .Select(
                        link =>
                            link.PhysicalLinkId.Value));

        return _lastAlertSnapshot.Alerts
            .FirstOrDefault(
                alert =>
                    alert.PhysicalLinkIds.Any(
                        linkIds.Contains));
    }

    private int CountLocationAlerts(
        Guid locationId)
    {
        if (_lastAlertSnapshot == null ||
            _lastMapSnapshot == null)
        {
            return 0;
        }

        var locationIds =
            DescendantLocationIds(
                locationId);
        locationIds.Add(
            locationId);

        var nodeKeys =
            new HashSet<string>(
                _lastMapSnapshot.Nodes
                    .Where(
                        node =>
                            node.LocationId.HasValue &&
                            locationIds.Contains(
                                node.LocationId.Value))
                    .Select(
                        node => node.Key),
                StringComparer.Ordinal);

        var linkIds =
            new HashSet<Guid>(
                _lastMapSnapshot.Links
                    .Where(
                        link =>
                            link.PhysicalLinkId.HasValue &&
                            (
                                nodeKeys.Contains(
                                    link.SourceNodeKey) ||
                                nodeKeys.Contains(
                                    link.TargetNodeKey)
                            ))
                    .Select(
                        link =>
                            link.PhysicalLinkId.Value));

        return _lastAlertSnapshot.Alerts.Count(
            alert =>
                alert.PhysicalLinkIds.Any(
                    linkIds.Contains));
    }

    private void OnInspectorPrimaryActionClick(
        object sender,
        RoutedEventArgs e)
    {
        // Действие инспектора всегда ведёт на карту: из раздела, где карта скрыта,
        // сначала переключаемся на «Карту», затем показываем объект.
        if (ShellMapSurface.Visibility != Visibility.Visible)
        {
            ShowShellSection(
                ShellSection.Map);
        }

        if (_inspectorPrimaryAlert == null ||
            _inspectorPrimaryAlert.PhysicalLinkIds.Count == 0)
        {
            if (_selectedDeviceId.HasValue)
            {
                var deviceId =
                    _selectedDeviceId.Value;

                FocusSelectedMapAtNativeZoom(
                    () =>
                        AnimateDiscoveryFocus(
                            deviceId));
            }

            return;
        }

        var linkIds =
            _inspectorPrimaryAlert.PhysicalLinkIds
                .ToArray();

        SelectAlertPhysicalContext(
            linkIds[0]);

        FocusAlertContextToViewport(
            linkIds,
            () =>
                PulseAlertContext(
                    linkIds));
    }

    private static string RelativeTimeText(
        DateTime? valueUtc)
    {
        if (!valueUtc.HasValue)
        {
            return UiText.Get(
                "DiagnosticNotAvailable");
        }

        var age =
            DateTime.UtcNow -
            valueUtc.Value;

        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return UiText.Get(
                "InspectorRelativeNow");
        }

        if (age < TimeSpan.FromHours(1))
        {
            return UiText.FormatCount(
                "InspectorRelativeMinutes",
                Math.Max(
                    1,
                    (int)Math.Floor(
                        age.TotalMinutes)));
        }

        if (age < TimeSpan.FromDays(1))
        {
            return UiText.FormatCount(
                "InspectorRelativeHours",
                Math.Max(
                    1,
                    (int)Math.Floor(
                        age.TotalHours)));
        }

        return UiText.FormatCount(
            "InspectorRelativeDays",
            Math.Max(
                1,
                (int)Math.Floor(
                    age.TotalDays)));
    }

    private string DisplayLocationForDevice(
        Guid deviceId)
    {
        var path =
            LocationPathForDevice(
                deviceId);

        if (!string.IsNullOrWhiteSpace(
            path))
        {
            return path;
        }

        var device =
            _lastDiagnosticSnapshot == null
                ? null
                : _lastDiagnosticSnapshot.Devices
                    .FirstOrDefault(
                        item =>
                            item.DeviceId == deviceId);

        return device != null &&
               !string.IsNullOrWhiteSpace(
                   device.LocationName)
            ? device.LocationName
            : UiText.Get(
                "DiagnosticLocationNotAssigned");
    }

    private static string CurrentFreshnessText(
        PhysicalLinkDiagnostic link)
    {
        if (link == null)
        {
            return UiText.Get(
                "FreshnessStale");
        }

        if (link.Strength ==
            DiagnosticLinkStrength.Manual)
        {
            return FreshnessText(
                link.Freshness);
        }

        var age =
            DateTime.UtcNow -
            link.LastSeenUtc;

        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age >=
            TimeSpan.FromMinutes(15))
        {
            return FreshnessText(
                MapFreshness.Stale);
        }

        if (age >=
                TimeSpan.FromMinutes(5) &&
            link.Freshness ==
                MapFreshness.Fresh)
        {
            return FreshnessText(
                MapFreshness.Aging);
        }

        return FreshnessText(
            link.Freshness);
    }

    private void SetInspectorPortHeaders()
    {
    }

    private DiagnosticEntityRow BuildDiagnosticPortRow(
        Guid selectedDeviceId,
        InterfaceDiagnostic item,
        IReadOnlyList<PhysicalLinkDiagnostic> connectedLinks,
        TopologyAlert alert)
    {
        var interfaceLinks =
            connectedLinks
                .Where(
                    link =>
                        link.InterfaceAId == item.InterfaceId ||
                        link.InterfaceBId == item.InterfaceId)
                .ToArray();

        var neighborText =
            interfaceLinks.Length == 0
                ? UiText.Get(
                    "DiagnosticNotAvailable")
                : string.Join(
                    "; ",
                    interfaceLinks
                        .Select(
                            link =>
                                BuildPortNeighborText(
                                    selectedDeviceId,
                                    link))
                        .Distinct(
                            StringComparer.CurrentCultureIgnoreCase));

        var meta =
            new List<string>();

        if (item.IfIndex.HasValue)
        {
            meta.Add(
                UiText.Format(
                    "InspectorPortIfIndex",
                    item.IfIndex.Value));
        }

        if (!string.IsNullOrWhiteSpace(
            item.MacAddress))
        {
            meta.Add(
                UiText.Format(
                    "InspectorPortMac",
                    item.MacAddress));
        }

        if (item.LastSeenUtc.HasValue)
        {
            meta.Add(
                UiText.Format(
                    "InspectorPortLastSeen",
                    RelativeTimeText(
                        item.LastSeenUtc)));
        }

        var alertLinkIds =
            alert == null
                ? null
                : new HashSet<Guid>(
                    alert.PhysicalLinkIds);

        var isRiskyStp =
            item.StpState ==
                StpTreePortState.Forwarding &&
            alertLinkIds != null &&
            interfaceLinks.Any(
                link =>
                    alertLinkIds.Contains(
                        link.PhysicalLinkId));

        return DiagnosticEntityRow.Port(
            item.InterfaceId,
            DisplayInterfaceName(
                item.DisplayName),
            string.Join(
                " · ",
                meta),
            PortStatusText(
                item.OperStatus),
            SpeedText(
                item.SpeedBps),
            StpStateText(
                item.StpState),
            neighborText,
            isRiskyStp,
            PortStatusIsDown(
                item.OperStatus),
            item.DegradationStatus ==
                DiagnosticDegradationStatus.Degraded);
    }

    private string BuildPortNeighborText(
        Guid selectedDeviceId,
        PhysicalLinkDiagnostic link)
    {
        var selectedIsA =
            link.DeviceAId ==
                selectedDeviceId;

        return UiText.Format(
            "InspectorPortNeighbor",
            DisplayDeviceName(
                selectedIsA
                    ? link.DeviceBName
                    : link.DeviceAName),
            DisplayLinkEndpointInterface(
                selectedIsA
                    ? link.InterfaceBId
                    : link.InterfaceAId,
                selectedIsA
                    ? link.InterfaceBName
                    : link.InterfaceAName));
    }

    private static string PortStatusText(
        string operStatus)
    {
        if (string.Equals(
            operStatus,
            "up",
            StringComparison.OrdinalIgnoreCase))
        {
            return OperatorStatusGlyph(
                       OperatorStatusSemantic.Normal) +
                   " " +
                   UiText.Get(
                       "InspectorPortStatusUp");
        }

        if (PortStatusIsDown(
            operStatus))
        {
            return OperatorStatusGlyph(
                       OperatorStatusSemantic.Critical) +
                   " " +
                   UiText.Get(
                       "InspectorPortStatusDown");
        }

        return OperatorStatusGlyph(
                   OperatorStatusSemantic.Unknown) +
               " " +
               (string.IsNullOrWhiteSpace(
                    operStatus)
                    ? UiText.Get(
                        "InspectorPortStatusUnknown")
                    : operStatus);
    }

    private static bool PortStatusIsDown(
        string operStatus)
    {
        return string.Equals(
                   operStatus,
                   "down",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   operStatus,
                   "lowerLayerDown",
                   StringComparison.OrdinalIgnoreCase);
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

    private static DiagnosticFieldRow Section(
        string title)
    {
        return new DiagnosticFieldRow(
            title,
            string.Empty,
            true);
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
        var identity =
            InterfaceIdentity(item);

        var details =
            new List<string>();

        details.Add(
            UiText.Format(
                "DiagnosticInterfaceType",
                item.IfType.HasValue
                    ? IfTypeText(
                        item.IfType.Value)
                    : UiText.Get(
                        "DiagnosticNotAvailable")));

        if (!string.IsNullOrWhiteSpace(
            item.IfName))
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceIfName",
                    item.IfName));
        }

        if (!string.IsNullOrWhiteSpace(
            item.IfAlias))
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceIfAlias",
                    item.IfAlias));
        }

        if (!string.IsNullOrWhiteSpace(
            item.IfDescription) &&
            !string.Equals(
                item.IfDescription,
                item.IfName,
                StringComparison.OrdinalIgnoreCase))
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceIfDescription",
                    item.IfDescription));
        }

        if (!string.IsNullOrWhiteSpace(
            item.AdminStatus))
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceAdmin",
                    item.AdminStatus));
        }

        if (!string.IsNullOrWhiteSpace(
            item.OperStatus))
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceOper",
                    item.OperStatus));
        }

        var stp =
            StpStateText(
                item.StpState);

        if (!string.Equals(
            stp,
            UiText.Get(
                "DiagnosticStpUnknown"),
            StringComparison.CurrentCulture))
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceStp",
                    stp));
        }

        if (item.DegradationStatus !=
            DiagnosticDegradationStatus.Unknown)
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceDegradation",
                    DegradationText(
                        item)));
        }

        if (item.LastSeenUtc.HasValue)
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceLastSeen",
                    RelativeTimeText(
                        item.LastSeenUtc)));
        }

        if (!string.IsNullOrWhiteSpace(
            item.MacAddress))
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceMac",
                    item.MacAddress));
        }

        if (item.SpeedBps.HasValue)
        {
            details.Add(
                UiText.Format(
                    "DiagnosticInterfaceSpeed",
                    SpeedText(
                        item.SpeedBps)));
        }

        return details.Count == 0
            ? UiText.Format(
                "DiagnosticInterfaceNoTelemetry",
                identity)
            : identity +
              Environment.NewLine +
              string.Join(
                  " • ",
                  details);
    }

    private static string InterfaceIdentity(
        InterfaceDiagnostic item)
    {
        var name =
            DisplayInterfaceName(
                item.DisplayName);

        if (item.IfIndex.HasValue)
        {
            return UiText.Format(
                "DiagnosticInterfaceIdentityIfIndex",
                name,
                item.IfIndex.Value.ToString(
                    CultureInfo.CurrentCulture));
        }

        return UiText.Format(
            "DiagnosticInterfaceIdentityId",
            name,
            ShortIdentifier(
                item.InterfaceId));
    }

    private string DisplayLinkEndpointInterface(
        Guid? interfaceId,
        string interfaceName)
    {
        if (!interfaceId.HasValue)
        {
            return UiText.Get(
                "DiagnosticDeviceLevelEndpoint");
        }

        var diagnostic =
            _lastDiagnosticSnapshot == null
                ? null
                : _lastDiagnosticSnapshot.Devices
                    .SelectMany(
                        device =>
                            device.Interfaces)
                    .FirstOrDefault(
                        item =>
                            item.InterfaceId ==
                            interfaceId.Value);

        if (diagnostic != null)
        {
            return InterfaceIdentity(
                diagnostic);
        }

        return UiText.Format(
            "DiagnosticInterfaceIdentityId",
            DisplayInterfaceName(
                interfaceName),
            ShortIdentifier(
                interfaceId.Value));
    }

    private string DisplayLinkEndpointInterfaceName(
        Guid? interfaceId,
        string interfaceName)
    {
        if (interfaceId.HasValue &&
            _lastDiagnosticSnapshot != null)
        {
            var diagnostic =
                _lastDiagnosticSnapshot.Devices
                    .SelectMany(
                        device =>
                            device.Interfaces)
                    .FirstOrDefault(
                        item =>
                            item.InterfaceId ==
                                interfaceId.Value);

            if (diagnostic != null)
            {
                return DisplayInterfaceName(
                    diagnostic.DisplayName);
            }
        }

        return DisplayInterfaceName(
            interfaceName);
    }

    private static string IfTypeText(
        int ifType)
    {
        switch (ifType)
        {
            case 6:
                return UiText.Get(
                    "InspectorIfTypeEthernet");
            case 24:
                return UiText.Get(
                    "InspectorIfTypeLoopback");
            case 53:
                return UiText.Get(
                    "InspectorIfTypeVirtual");
            case 62:
            case 69:
                return UiText.Get(
                    "InspectorIfTypeFastEthernet");
            case 71:
                return UiText.Get(
                    "InspectorIfTypeWifi");
            case 117:
                return UiText.Get(
                    "InspectorIfTypeGigabitEthernet");
            case 131:
                return UiText.Get(
                    "InspectorIfTypeTunnel");
            case 135:
                return UiText.Get(
                    "InspectorIfTypeVlan");
            case 161:
                return UiText.Get(
                    "InspectorIfTypeLag");
            default:
                return UiText.Format(
                    "InspectorIfTypeOther",
                    ifType);
        }
    }

    private static string ShortIdentifier(
        Guid value)
    {
        return value
            .ToString("N")
            .Substring(0, 8)
            .ToUpperInvariant();
    }

    private string BuildDeviceLinkDiagnosticText(
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
                ? DisplayLinkEndpointInterface(
                    link.InterfaceAId,
                    link.InterfaceAName)
                : DisplayLinkEndpointInterface(
                    link.InterfaceBId,
                    link.InterfaceBName);

        var peerPort =
            selectedIsA
                ? DisplayLinkEndpointInterface(
                    link.InterfaceBId,
                    link.InterfaceBName)
                : DisplayLinkEndpointInterface(
                    link.InterfaceAId,
                    link.InterfaceAName);

        if (!link.IsBridge)
        {
            return UiText.Format(
                "DiagnosticConnectionAlternate",
                peer,
                localPort,
                peerPort,
                CurrentFreshnessText(
                    link));
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
            CurrentFreshnessText(
                link),
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

    private sealed class DiagnosticFieldRow
    {
        public DiagnosticFieldRow(
            string label,
            string value)
            : this(
                label,
                value,
                false)
        {
        }

        public DiagnosticFieldRow(
            string label,
            string value,
            bool isSectionHeader)
        {
            Label = label;
            Value = value;
            IsSectionHeader = isSectionHeader;
        }

        public string Label { get; }

        public string Value { get; }

        public bool IsSectionHeader { get; }
    }

    private sealed class DiagnosticEntityRow
    {
        public DiagnosticEntityRow(
            Guid entityId,
            string text)
            : this(
                entityId,
                text,
                true,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                false)
        {
        }

        private DiagnosticEntityRow(
            Guid entityId,
            string text,
            bool isEnabled,
            string portName,
            string portMeta,
            string portStatus,
            string portSpeed,
            string portStp,
            string portNeighbor,
            bool isRiskyStp,
            bool isPortDown,
            bool isPortDegraded)
        {
            EntityId = entityId;
            Text = text;
            IsEnabled = isEnabled;
            PortName = portName;
            PortMeta = portMeta;
            PortStatus = portStatus;
            PortSpeed = portSpeed;
            PortStp = portStp;
            PortNeighbor = portNeighbor;
            IsRiskyStp = isRiskyStp;
            IsPortDown = isPortDown;
            IsPortDegraded = isPortDegraded;
        }

        public Guid EntityId { get; }

        public string Text { get; }

        public bool IsEnabled { get; }

        public string PortName { get; }

        public string PortMeta { get; }

        public string PortStatus { get; }

        public string PortSpeed { get; }

        public string PortStp { get; }

        public string PortNeighbor { get; }

        public string PortStatusGlyph
        {
            get
            {
                return string.IsNullOrWhiteSpace(
                           PortStatus)
                    ? string.Empty
                    : PortStatus.Substring(
                        0,
                        1);
            }
        }

        public string PortSummary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(
                    PortSpeed))
                {
                    return PortStp;
                }

                if (string.IsNullOrWhiteSpace(
                    PortStp))
                {
                    return PortSpeed;
                }

                return UiText.Format(
                    "InspectorPortSummary",
                    PortSpeed,
                    PortStp);
            }
        }

        public string PortNeighborLine
        {
            get
            {
                return UiText.Format(
                    "InspectorPortNeighborLine",
                    PortNeighbor);
            }
        }

        public bool IsRiskyStp { get; }

        public bool IsPortDown { get; }

        public bool IsPortDegraded { get; }

        public static DiagnosticEntityRow Port(
            Guid entityId,
            string name,
            string meta,
            string status,
            string speed,
            string stp,
            string neighbor,
            bool isRiskyStp,
            bool isPortDown,
            bool isPortDegraded)
        {
            return new DiagnosticEntityRow(
                entityId,
                null,
                true,
                name,
                meta,
                status,
                speed,
                stp,
                neighbor,
                isRiskyStp,
                isPortDown,
                isPortDegraded);
        }

        public static DiagnosticEntityRow Disabled(
            string text)
        {
            return new DiagnosticEntityRow(
                Guid.Empty,
                text,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                false);
        }

        public static DiagnosticEntityRow DisabledPort(
            string text)
        {
            return new DiagnosticEntityRow(
                Guid.Empty,
                null,
                false,
                text,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                false);
        }
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

}
