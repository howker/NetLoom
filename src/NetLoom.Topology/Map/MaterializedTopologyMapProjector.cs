using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Topology;

namespace NetLoom.Topology.Map
{
    public sealed class MaterializedTopologyMapProjector
    {
        public MapSnapshot Project(
            IEnumerable<TopologyDevice> devices,
            IEnumerable<DeviceInterface> interfaces,
            IEnumerable<PhysicalLink> links,
            IEnumerable<Location> locations,
            DateTime generatedUtc)
        {
            return Project(
                devices,
                interfaces,
                links,
                new PhysicalLinkEvidence[0],
                locations,
                generatedUtc);
        }

        public MapSnapshot Project(
            IEnumerable<TopologyDevice> devices,
            IEnumerable<DeviceInterface> interfaces,
            IEnumerable<PhysicalLink> links,
            IEnumerable<PhysicalLinkEvidence> linkEvidence,
            IEnumerable<Location> locations,
            DateTime generatedUtc)
        {
            if (devices == null)
            {
                throw new ArgumentNullException(nameof(devices));
            }

            if (interfaces == null)
            {
                throw new ArgumentNullException(nameof(interfaces));
            }

            if (links == null)
            {
                throw new ArgumentNullException(nameof(links));
            }

            if (linkEvidence == null)
            {
                throw new ArgumentNullException(nameof(linkEvidence));
            }

            if (locations == null)
            {
                throw new ArgumentNullException(nameof(locations));
            }

            if (generatedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Generated time must be UTC.",
                    nameof(generatedUtc));
            }

            var visibleDevices =
                devices
                    .Where(
                        device =>
                            !device.IsHidden &&
                            !device.IsArchived)
                    .OrderBy(
                        device =>
                            DisplayName(device),
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(device => device.Id)
                    .ToArray();

            var interfaceById =
                interfaces.ToDictionary(
                    networkInterface =>
                        networkInterface.Id);

            var currentEvidence =
                linkEvidence.ToArray();

            if (currentEvidence.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Physical link evidence cannot contain null items.",
                    nameof(linkEvidence));
            }

            var evidenceByLink =
                currentEvidence
                    .GroupBy(item => item.PhysicalLinkId)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                            group
                                .OrderBy(item => item.Kind)
                                .ThenBy(
                                    item => item.SourceAddress,
                                    StringComparer.Ordinal)
                                .ThenBy(
                                    item => item.SlotDiscriminator,
                                    StringComparer.Ordinal)
                                .ToArray());

            var nodeKeyByDevice =
                visibleDevices.ToDictionary(
                    device => device.Id,
                    device => PresentationKey(
                        "device:" +
                        device.Id.ToString("D")));

            var columns =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        Math.Sqrt(
                            visibleDevices.Length)));

            var mapNodes =
                new List<MapNode>();

            for (var index = 0;
                 index < visibleDevices.Length;
                 index++)
            {
                var device =
                    visibleDevices[index];

                var key =
                    nodeKeyByDevice[device.Id];

                mapNodes.Add(
                    new MapNode(
                        key,
                        DisplayName(device) ??
                            key,
                        BuildSecondaryText(device),
                        60.0 +
                            ((index % columns) * 240.0),
                        60.0 +
                            ((index / columns) * 170.0),
                        device.LocationId,
                        MapOrigin(
                            device.DiscoveryOrigin),
                        MapMonitoring(
                            device.MonitoringCapability),
                        MapCategory(
                            device.Category),
                        device.Id,
                        device.ManagementAddress));
            }

            var mapLinks =
                new List<MapLink>();

            foreach (var link in
                links
                    .Where(
                        link =>
                            !link.IsHidden &&
                            !link.IsArchived)
                    .OrderBy(link => link.Id))
            {
                string sourceKey;
                string targetKey;

                if (!nodeKeyByDevice.TryGetValue(
                        link.DeviceAId,
                        out sourceKey) ||
                    !nodeKeyByDevice.TryGetValue(
                        link.DeviceBId,
                        out targetKey))
                {
                    continue;
                }

                var evidence =
                    link.Strength ==
                    PhysicalLinkStrength.Manual
                        ? new[]
                        {
                            new MapEvidenceItem(
                                MapEvidenceKind.Manual,
                                MapEvidenceStrength.Strong,
                                null,
                                link.LastConfirmedUtc,
                                "User",
                                null)
                        }
                        : MapCurrentEvidence(
                            link.Id,
                            evidenceByLink);

                mapLinks.Add(
                    new MapLink(
                        PresentationKey(
                            "physical-link:" +
                            link.Id.ToString("D")),
                        sourceKey,
                        targetKey,
                        PortLabel(
                            link.InterfaceAId,
                            interfaceById),
                        PortLabel(
                            link.InterfaceBId,
                            interfaceById),
                        MapConfidenceFor(
                            link.Strength),
                        MapFreshnessFor(
                            link.Freshness),
                        evidence,
                        link.Id));
            }

            var mapLocations =
                locations
                    .OrderBy(
                        location => location.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(location => location.Id)
                    .Select(
                        location =>
                            new MapLocation(
                                location.Id,
                                location.ParentLocationId,
                                location.Name,
                                location.Description))
                    .ToArray();

            return new MapSnapshot(
                generatedUtc,
                mapNodes,
                mapLinks,
                mapLocations);
        }

        private static MapEvidenceItem[] MapCurrentEvidence(
            Guid physicalLinkId,
            IReadOnlyDictionary<Guid, PhysicalLinkEvidence[]>
                evidenceByLink)
        {
            PhysicalLinkEvidence[] evidence;

            if (!evidenceByLink.TryGetValue(
                physicalLinkId,
                out evidence))
            {
                return new MapEvidenceItem[0];
            }

            return evidence
                .Select(
                    item =>
                        new MapEvidenceItem(
                            MapEvidenceKindFor(item.Kind),
                            item.Strength ==
                            PhysicalLinkEvidenceStrength.Strong
                                ? MapEvidenceStrength.Strong
                                : MapEvidenceStrength.Weak,
                            item.ObservationId,
                            item.CapturedUtc,
                            item.SourceAddress,
                            item.Detail))
                .ToArray();
        }

        private static MapEvidenceKind MapEvidenceKindFor(
            PhysicalLinkEvidenceKind kind)
        {
            switch (kind)
            {
                case PhysicalLinkEvidenceKind.Lldp:
                    return MapEvidenceKind.Lldp;

                case PhysicalLinkEvidenceKind.Cdp:
                    return MapEvidenceKind.Cdp;

                default:
                    return MapEvidenceKind.ArpFdbCorrelation;
            }
        }

        private static string DisplayName(
            TopologyDevice device)
        {
            if (!string.IsNullOrWhiteSpace(
                device.CustomName))
            {
                return device.CustomName;
            }

            if (!string.IsNullOrWhiteSpace(
                device.DiscoveredName))
            {
                return device.DiscoveredName;
            }

            return string.IsNullOrWhiteSpace(
                device.LldpChassisId)
                ? null
                : device.LldpChassisId;
        }

        private static string BuildSecondaryText(
            TopologyDevice device)
        {
            var vendor =
                device.VendorOverride;

            var model =
                device.ModelOverride;

            if (string.IsNullOrWhiteSpace(vendor))
            {
                if (!string.IsNullOrWhiteSpace(model))
                {
                    return model;
                }

                return
                    !string.IsNullOrWhiteSpace(
                        device.LldpChassisId) &&
                    !string.Equals(
                        DisplayName(device),
                        device.LldpChassisId,
                        StringComparison.OrdinalIgnoreCase)
                        ? device.LldpChassisId
                        : null;
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                return vendor;
            }

            return vendor + " " + model;
        }

        private static string PortLabel(
            Guid? interfaceId,
            IReadOnlyDictionary<Guid, DeviceInterface>
                interfaceById)
        {
            if (!interfaceId.HasValue)
            {
                return null;
            }

            DeviceInterface networkInterface;

            if (!interfaceById.TryGetValue(
                    interfaceId.Value,
                    out networkInterface))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(
                    networkInterface.CustomName))
            {
                return networkInterface.CustomName;
            }

            if (!string.IsNullOrWhiteSpace(
                    networkInterface.IfName))
            {
                return networkInterface.IfName;
            }

            if (!string.IsNullOrWhiteSpace(
                    networkInterface.LldpPortId))
            {
                return networkInterface.LldpPortId;
            }

            if (!string.IsNullOrWhiteSpace(
                    networkInterface.LldpPortDescription))
            {
                return networkInterface.LldpPortDescription;
            }

            return networkInterface.IfIndex.HasValue
                ? "#" +
                    networkInterface.IfIndex.Value.ToString(
                        CultureInfo.InvariantCulture)
                : null;
        }

        private static MapNodeOrigin MapOrigin(
            DeviceDiscoveryOrigin origin)
        {
            switch (origin)
            {
                case DeviceDiscoveryOrigin.Manual:
                    return MapNodeOrigin.Manual;

                case DeviceDiscoveryOrigin.Imported:
                    return MapNodeOrigin.Imported;

                default:
                    return MapNodeOrigin.Automatic;
            }
        }

        private static MapMonitoringCapability MapMonitoring(
            MonitoringCapability capability)
        {
            return capability ==
                   MonitoringCapability.None
                ? MapMonitoringCapability.None
                : MapMonitoringCapability.Unknown;
        }

        private static MapNodeCategory MapCategory(
            DeviceCategory category)
        {
            switch (category)
            {
                case DeviceCategory.MediaConverter:
                    return MapNodeCategory.MediaConverter;

                case DeviceCategory.UnmanagedSwitch:
                    return MapNodeCategory.UnmanagedSwitch;

                case DeviceCategory.OpticalConverter:
                    return MapNodeCategory.OpticalConverter;

                case DeviceCategory.PassiveNetworkEquipment:
                    return MapNodeCategory.PassiveNetworkEquipment;

                default:
                    return MapNodeCategory.Unknown;
            }
        }

        private static MapConfidence MapConfidenceFor(
            PhysicalLinkStrength strength)
        {
            switch (strength)
            {
                case PhysicalLinkStrength.Confirmed:
                case PhysicalLinkStrength.Manual:
                    return MapConfidence.High;

                case PhysicalLinkStrength.Observed:
                    return MapConfidence.Medium;

                default:
                    return MapConfidence.Low;
            }
        }

        private static MapFreshness MapFreshnessFor(
            PhysicalLinkFreshness freshness)
        {
            switch (freshness)
            {
                case PhysicalLinkFreshness.Fresh:
                    return MapFreshness.Fresh;

                case PhysicalLinkFreshness.Aging:
                    return MapFreshness.Aging;

                default:
                    return MapFreshness.Stale;
            }
        }

        private static string PresentationKey(
            string value)
        {
            using (var sha = SHA256.Create())
            {
                var hash =
                    sha.ComputeHash(
                        Encoding.UTF8.GetBytes(value));

                var builder =
                    new StringBuilder("map-");

                for (var index = 0;
                     index < 12;
                     index++)
                {
                    builder.Append(
                        hash[index].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
