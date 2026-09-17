using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Topology;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Safety;
using NetLoom.Topology.Stp;

namespace NetLoom.Topology.Diagnostics
{
    public sealed class MaterializedTopologyDiagnosticSnapshotProjector
    {
        public NetworkDiagnosticSnapshot Project(
            MaterializedTopologyReadSet readSet,
            MapSnapshot mapSnapshot,
            string stpInstanceId)
        {
            if (readSet == null)
            {
                throw new ArgumentNullException(
                    nameof(readSet));
            }

            if (mapSnapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(mapSnapshot));
            }

            if (string.IsNullOrWhiteSpace(stpInstanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(stpInstanceId));
            }

            var normalizedInstanceId =
                stpInstanceId.Trim();

            var domainDeviceById =
                readSet.Devices.ToDictionary(
                    item => item.Id);

            var interfaceById =
                readSet.Interfaces.ToDictionary(
                    item => item.Id);

            var interfacesByDevice =
                readSet.Interfaces
                    .Where(item => !item.IsHidden)
                    .GroupBy(item => item.DeviceId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToArray());

            var locationById =
                readSet.Locations.ToDictionary(
                    item => item.Id);

            var degradationByInterface =
                readSet.InterfaceDegradationStates
                    .ToDictionary(
                        item =>
                            InterfaceKey(
                                item.DeviceId,
                                item.IfIndex));

            var rawAvailabilityByObservationId =
                BuildRawAvailabilityByObservationId(
                    readSet.PhysicalLinkEvidenceExplanations);

            var stpByDevice =
                BuildStpSnapshots(
                    readSet,
                    normalizedInstanceId);

            var mapNodeByDeviceId =
                mapSnapshot.Nodes
                    .Where(item => item.DeviceId.HasValue)
                    .ToDictionary(
                        item => item.DeviceId.Value);

            var devices =
                mapNodeByDeviceId
                    .OrderBy(pair => pair.Value.Label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(pair => pair.Key)
                    .Select(
                        pair =>
                            ProjectDevice(
                                pair.Key,
                                pair.Value,
                                domainDeviceById,
                                interfacesByDevice,
                                locationById,
                                degradationByInterface,
                                stpByDevice))
                    .Where(item => item != null)
                    .ToArray();

            var impactByLink =
                new PhysicalGraphSafetyAnalyzer()
                    .AnalyzePhysicalFailures(
                        readSet.PhysicalLinks)
                    .ToDictionary(
                        item => item.PhysicalLinkId);

            var domainLinkById =
                readSet.PhysicalLinks.ToDictionary(
                    item => item.Id);

            var links =
                mapSnapshot.Links
                    .Where(item => item.PhysicalLinkId.HasValue)
                    .OrderBy(item => item.PhysicalLinkId.Value)
                    .Select(
                        mapLink =>
                            ProjectLink(
                                mapLink,
                                domainLinkById,
                                interfaceById,
                                mapNodeByDeviceId,
                                stpByDevice,
                                impactByLink,
                                rawAvailabilityByObservationId))
                    .Where(item => item != null)
                    .ToArray();

            return new NetworkDiagnosticSnapshot(
                mapSnapshot.GeneratedUtc,
                devices,
                links);
        }

        private static DeviceDiagnostic ProjectDevice(
            Guid deviceId,
            MapNode mapNode,
            IReadOnlyDictionary<Guid, TopologyDevice> domainDeviceById,
            IReadOnlyDictionary<Guid, DeviceInterface[]> interfacesByDevice,
            IReadOnlyDictionary<Guid, NetLoom.Domain.Locations.Location> locationById,
            IReadOnlyDictionary<string, InterfaceDegradationState> degradationByInterface,
            IReadOnlyDictionary<Guid, StpTreeSnapshot> stpByDevice)
        {
            TopologyDevice device;

            if (!domainDeviceById.TryGetValue(
                deviceId,
                out device))
            {
                return null;
            }

            DeviceInterface[] deviceInterfaces;

            if (!interfacesByDevice.TryGetValue(
                deviceId,
                out deviceInterfaces))
            {
                deviceInterfaces =
                    new DeviceInterface[0];
            }

            StpTreeSnapshot stp;
            stpByDevice.TryGetValue(
                deviceId,
                out stp);

            var diagnostics =
                deviceInterfaces
                    .OrderBy(
                        InterfaceDisplayName,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.IfIndex ?? int.MaxValue)
                    .ThenBy(item => item.Id)
                    .Select(
                        item =>
                            ProjectInterface(
                                item,
                                degradationByInterface,
                                stp))
                    .ToArray();

            string locationName = null;

            if (device.LocationId.HasValue)
            {
                NetLoom.Domain.Locations.Location location;

                if (locationById.TryGetValue(
                    device.LocationId.Value,
                    out location))
                {
                    locationName =
                        location.Name;
                }
            }

            var displayName =
                string.Equals(
                    mapNode.Label,
                    mapNode.Key,
                    StringComparison.Ordinal)
                    ? null
                    : mapNode.Label;

            return new DeviceDiagnostic(
                device.Id,
                displayName,
                mapNode.SecondaryText,
                locationName,
                device.LastSeenUtc,
                device.LastResolvedUtc,
                diagnostics,
                device.ManagementAddress);
        }

        private static InterfaceDiagnostic ProjectInterface(
            DeviceInterface networkInterface,
            IReadOnlyDictionary<string, InterfaceDegradationState> degradationByInterface,
            StpTreeSnapshot stp)
        {
            InterfaceDegradationState degradation = null;

            if (networkInterface.IfIndex.HasValue)
            {
                degradationByInterface.TryGetValue(
                    InterfaceKey(
                        networkInterface.DeviceId,
                        networkInterface.IfIndex.Value),
                    out degradation);
            }

            var stpState =
                StpTreePortState.Unknown;

            if (stp != null)
            {
                var port =
                    stp.Ports.FirstOrDefault(
                        item =>
                            item.InterfaceId.HasValue &&
                            item.InterfaceId.Value ==
                                networkInterface.Id);

                if (port != null)
                {
                    stpState =
                        port.State;
                }
            }

            return new InterfaceDiagnostic(
                networkInterface.Id,
                networkInterface.DeviceId,
                networkInterface.IfIndex,
                InterfaceDisplayName(networkInterface),
                networkInterface.MacAddress,
                networkInterface.AdminStatus,
                networkInterface.OperStatus,
                networkInterface.SpeedBps,
                networkInterface.LastSeenUtc,
                stpState,
                DiagnosticDegradationStatusFor(
                    degradation),
                degradation == null
                    ? (DateTime?)null
                    : degradation.CapturedUtc,
                DiagnosticDegradationReasonsFor(
                    degradation),
                networkInterface.IfName,
                networkInterface.IfAlias,
                networkInterface.IfType,
                networkInterface.IfDescription);
        }

        private static PhysicalLinkDiagnostic ProjectLink(
            MapLink mapLink,
            IReadOnlyDictionary<Guid, PhysicalLink> domainLinkById,
            IReadOnlyDictionary<Guid, DeviceInterface> interfaceById,
            IReadOnlyDictionary<Guid, MapNode> mapNodeByDeviceId,
            IReadOnlyDictionary<Guid, StpTreeSnapshot> stpByDevice,
            IReadOnlyDictionary<Guid, NetLoom.Contracts.GraphSafety.PhysicalLinkFailureImpact> impactByLink,
            IReadOnlyDictionary<Guid, DiagnosticRawAvailability> rawAvailabilityByObservationId)
        {
            var physicalLinkId =
                mapLink.PhysicalLinkId.Value;

            PhysicalLink link;

            if (!domainLinkById.TryGetValue(
                physicalLinkId,
                out link))
            {
                return null;
            }

            NetLoom.Contracts.GraphSafety.PhysicalLinkFailureImpact impact;
            impactByLink.TryGetValue(
                physicalLinkId,
                out impact);

            return new PhysicalLinkDiagnostic(
                link.Id,
                link.DeviceAId,
                link.DeviceBId,
                link.InterfaceAId,
                link.InterfaceBId,
                DeviceDisplayName(
                    link.DeviceAId,
                    mapNodeByDeviceId),
                DeviceDisplayName(
                    link.DeviceBId,
                    mapNodeByDeviceId),
                InterfaceDisplayName(
                    link.InterfaceAId,
                    interfaceById),
                InterfaceDisplayName(
                    link.InterfaceBId,
                    interfaceById),
                LinkStrength(link.Strength),
                mapLink.Freshness,
                link.MediaTypeResolved,
                link.SpeedBpsResolved,
                link.SourceSummary,
                link.LastSeenUtc,
                link.LastConfirmedUtc,
                EndpointStpState(
                    link.DeviceAId,
                    link.InterfaceAId,
                    stpByDevice),
                EndpointStpState(
                    link.DeviceBId,
                    link.InterfaceBId,
                    stpByDevice),
                mapLink.Evidence
                    .Select(
                        item =>
                            new DiagnosticEvidenceItem(
                                item.Kind,
                                item.Strength,
                                item.ObservationId,
                                item.CapturedUtc,
                                item.SourceAddress,
                                item.Detail,
                                RawAvailabilityFor(
                                    item.ObservationId,
                                    rawAvailabilityByObservationId)))
                    .ToArray(),
                impact != null && impact.IsBridge,
                impact != null && impact.IsBridge
                    ? impact.SideADeviceIds.Count
                    : 0,
                impact != null && impact.IsBridge
                    ? impact.SideBDeviceIds.Count
                    : 0,
                impact != null && impact.IsBridge
                    ? impact.SeparatedDevicePairCount
                    : 0L);
        }

        private static IReadOnlyDictionary<Guid, DiagnosticRawAvailability>
            BuildRawAvailabilityByObservationId(
                IEnumerable<PhysicalLinkEvidenceExplanation> explanations)
        {
            return explanations
                .Where(
                    item =>
                        item.Evidence.ObservationId.HasValue)
                .GroupBy(
                    item =>
                        item.Evidence.ObservationId.Value)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var states =
                            group
                                .Select(
                                    item =>
                                        DiagnosticRawAvailabilityFor(
                                            item.RawAvailability))
                                .Distinct()
                                .ToArray();

                        return states.Length == 1
                            ? states[0]
                            : DiagnosticRawAvailability.Unknown;
                    });
        }

        private static DiagnosticRawAvailability RawAvailabilityFor(
            Guid? observationId,
            IReadOnlyDictionary<Guid, DiagnosticRawAvailability> rawAvailabilityByObservationId)
        {
            if (!observationId.HasValue)
            {
                return DiagnosticRawAvailability.NotApplicable;
            }

            DiagnosticRawAvailability availability;

            return rawAvailabilityByObservationId.TryGetValue(
                    observationId.Value,
                    out availability)
                ? availability
                : DiagnosticRawAvailability.Unknown;
        }

        private static DiagnosticRawAvailability
            DiagnosticRawAvailabilityFor(
                ObservationRawAvailability availability)
        {
            switch (availability)
            {
                case ObservationRawAvailability.NotApplicable:
                    return DiagnosticRawAvailability.NotApplicable;
                case ObservationRawAvailability.Available:
                    return DiagnosticRawAvailability.Available;
                case ObservationRawAvailability.Expired:
                    return DiagnosticRawAvailability.Expired;
                default:
                    return DiagnosticRawAvailability.Unknown;
            }
        }

        private static IReadOnlyDictionary<Guid, StpTreeSnapshot>
            BuildStpSnapshots(
                MaterializedTopologyReadSet readSet,
                string instanceId)
        {
            var projector =
                new StpTreeProjector();

            return readSet.LatestStp
                .Select(
                    item =>
                        projector.Project(
                            item.DeviceId,
                            readSet.Interfaces,
                            item.Observation))
                .Where(
                    item =>
                        string.Equals(
                            item.InstanceId,
                            instanceId,
                            StringComparison.Ordinal))
                .ToDictionary(
                    item => item.DeviceId);
        }

        private static StpTreePortState EndpointStpState(
            Guid deviceId,
            Guid? interfaceId,
            IReadOnlyDictionary<Guid, StpTreeSnapshot> stpByDevice)
        {
            if (!interfaceId.HasValue)
            {
                return StpTreePortState.Unknown;
            }

            StpTreeSnapshot stp;

            if (!stpByDevice.TryGetValue(
                deviceId,
                out stp))
            {
                return StpTreePortState.Unknown;
            }

            var port =
                stp.Ports.FirstOrDefault(
                    item =>
                        item.InterfaceId.HasValue &&
                        item.InterfaceId.Value ==
                            interfaceId.Value);

            return port == null
                ? StpTreePortState.Unknown
                : port.State;
        }

        private static string DeviceDisplayName(
            Guid deviceId,
            IReadOnlyDictionary<Guid, MapNode> mapNodeByDeviceId)
        {
            MapNode node;

            if (!mapNodeByDeviceId.TryGetValue(
                deviceId,
                out node) ||
                string.Equals(
                    node.Label,
                    node.Key,
                    StringComparison.Ordinal))
            {
                return null;
            }

            return node.Label;
        }

        private static string InterfaceDisplayName(
            Guid? interfaceId,
            IReadOnlyDictionary<Guid, DeviceInterface> interfaceById)
        {
            if (!interfaceId.HasValue)
            {
                return null;
            }

            DeviceInterface networkInterface;

            return interfaceById.TryGetValue(
                    interfaceId.Value,
                    out networkInterface)
                ? InterfaceDisplayName(networkInterface)
                : null;
        }

        private static string InterfaceDisplayName(
            DeviceInterface networkInterface)
        {
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
                networkInterface.IfAlias))
            {
                return networkInterface.IfAlias;
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

            if (!string.IsNullOrWhiteSpace(
                networkInterface.IfDescription))
            {
                return networkInterface.IfDescription;
            }

            return networkInterface.IfIndex.HasValue
                ? "#" +
                    networkInterface.IfIndex.Value.ToString(
                        CultureInfo.InvariantCulture)
                : null;
        }

        private static DiagnosticDegradationStatus
            DiagnosticDegradationStatusFor(
                InterfaceDegradationState state)
        {
            if (state == null)
            {
                return DiagnosticDegradationStatus.Unknown;
            }

            return state.Status == InterfaceDegradationStatus.Degraded
                ? DiagnosticDegradationStatus.Degraded
                : DiagnosticDegradationStatus.Healthy;
        }

        private static IReadOnlyList<DiagnosticDegradationReason>
            DiagnosticDegradationReasonsFor(
                InterfaceDegradationState state)
        {
            if (state == null ||
                state.Status != InterfaceDegradationStatus.Degraded)
            {
                return new DiagnosticDegradationReason[0];
            }

            return state.EvidenceFingerprint
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseDegradationReason)
                .Distinct()
                .OrderBy(item => (int)item)
                .ToArray();
        }

        private static DiagnosticDegradationReason ParseDegradationReason(
            string value)
        {
            int numeric;

            if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out numeric))
            {
                return DiagnosticDegradationReason.Unknown;
            }

            switch ((InterfaceDegradationReason)numeric)
            {
                case InterfaceDegradationReason.NoBaseline:
                    return DiagnosticDegradationReason.NoBaseline;
                case InterfaceDegradationReason.CounterDiscontinuity:
                    return DiagnosticDegradationReason.CounterDiscontinuity;
                case InterfaceDegradationReason.IncompleteCounterData:
                    return DiagnosticDegradationReason.IncompleteCounterData;
                case InterfaceDegradationReason.ErrorRateThresholdExceeded:
                    return DiagnosticDegradationReason.ErrorRateThresholdExceeded;
                case InterfaceDegradationReason.DiscardRateThresholdExceeded:
                    return DiagnosticDegradationReason.DiscardRateThresholdExceeded;
                default:
                    return DiagnosticDegradationReason.Unknown;
            }
        }

        private static DiagnosticLinkStrength LinkStrength(
            PhysicalLinkStrength strength)
        {
            switch (strength)
            {
                case PhysicalLinkStrength.Confirmed:
                    return DiagnosticLinkStrength.Confirmed;
                case PhysicalLinkStrength.Observed:
                    return DiagnosticLinkStrength.Observed;
                case PhysicalLinkStrength.Inferred:
                    return DiagnosticLinkStrength.Inferred;
                default:
                    return DiagnosticLinkStrength.Manual;
            }
        }

        private static string InterfaceKey(
            Guid deviceId,
            int ifIndex)
        {
            return deviceId.ToString("D") +
                   ":" +
                   ifIndex.ToString(CultureInfo.InvariantCulture);
        }
    }
}
