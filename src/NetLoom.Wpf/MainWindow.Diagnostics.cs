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
    private void ShowSelectedDiagnostic()
    {
        if (_selectedLocationId.HasValue)
        {
            if (ShowLocationDiagnostic(
                    _selectedLocationId.Value))
            {
                return;
            }

            _selectedLocationId = null;
        }

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

        DiagnosticStatusText.Text =
            UiText.Get(
                "MapLocationSelectedStatus");

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

        var stateFields =
            new List<DiagnosticFieldRow>();

        stateFields.Add(
            Field(
                "DiagnosticFieldManagementAddress",
                device.ManagementAddress));

        if (!string.IsNullOrWhiteSpace(
            device.LocationName))
        {
            stateFields.Add(
                Field(
                    "DiagnosticFieldLocation",
                    device.LocationName));
        }

        if (device.LastSeenUtc.HasValue)
        {
            stateFields.Add(
                Field(
                    "DiagnosticFieldLastSeen",
                    LocalTimeText(
                        device.LastSeenUtc)));
        }

        if (device.LastResolvedUtc.HasValue)
        {
            stateFields.Add(
                Field(
                    "DiagnosticFieldLastResolved",
                    LocalTimeText(
                        device.LastResolvedUtc)));
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
            UiText.Get("DiagnosticConnectionsTitle");

        DiagnosticSecondaryList.ItemsSource =
            connectedLinks.Length == 0
                ? new[]
                {
                    Row("DiagnosticNoConnections")
                }
                : connectedLinks
                    .Select(
                        item =>
                            new DiagnosticTextRow(
                                BuildDeviceLinkDiagnosticText(
                                    device.DeviceId,
                                    item)))
                    .ToArray();

        DiagnosticTertiaryTitleText.Text =
            UiText.Get("DiagnosticInterfacesTitle");

        DiagnosticTertiaryList.ItemsSource =
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
                DisplayLinkEndpointInterface(
                    link.InterfaceAId,
                    link.InterfaceAName),
                DisplayLinkEndpointInterface(
                    link.InterfaceBId,
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
                    LocalTimeText(
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

    private static string IfTypeText(
        int ifType)
    {
        string name;

        switch (ifType)
        {
            case 6:
                name = "ethernetCsmacd";
                break;
            case 24:
                name = "softwareLoopback";
                break;
            case 53:
                name = "propVirtual";
                break;
            case 62:
                name = "fastEther";
                break;
            case 69:
                name = "fastEtherFX";
                break;
            case 71:
                name = "ieee80211";
                break;
            case 117:
                name = "gigabitEthernet";
                break;
            case 131:
                name = "tunnel";
                break;
            case 135:
                name = "l2vlan";
                break;
            case 161:
                name = "ieee8023adLag";
                break;
            default:
                name = null;
                break;
        }

        return name == null
            ? ifType.ToString(
                CultureInfo.InvariantCulture)
            : ifType.ToString(
                CultureInfo.InvariantCulture) +
              " (" +
              name +
              ")";
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

}
