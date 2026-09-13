using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Topology;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Correlation;
using NetLoom.Topology.Resolution;

namespace NetLoom.Topology.Materialization
{
    public sealed class MonitoringTopologyMaterializer :
        IMonitoringTopologyMaterializer
    {
        private const string ResolverVersion =
            "monitoring-lldp-v1";

        private readonly IMaterializedTopologyRepository
            _repository;

        private readonly TopologyResolver
            _resolver;

        private readonly PhysicalLinkEvidenceMaterializer
            _evidenceMaterializer;

        public MonitoringTopologyMaterializer(
            IMaterializedTopologyRepository repository)
        {
            _repository =
                repository ??
                throw new ArgumentNullException(
                    nameof(repository));

            _resolver = new TopologyResolver();
            _evidenceMaterializer =
                new PhysicalLinkEvidenceMaterializer();
        }

        public void MaterializeDevice(
            Guid deviceId,
            DateTime observedUtc)
        {
            RequireDeviceId(deviceId);
            RequireUtc(observedUtc);

            var existing =
                _repository.GetDevice(
                    deviceId);

            if (existing != null &&
                existing.DiscoveryOrigin ==
                    DeviceDiscoveryOrigin.Manual)
            {
                return;
            }

            SaveDevice(
                deviceId,
                observedUtc,
                existing,
                null,
                null);
        }

        public void MaterializeInterface(
            Guid deviceId,
            int ifIndex,
            DateTime observedUtc)
        {
            RequireDeviceId(deviceId);
            RequireUtc(observedUtc);

            if (ifIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (_repository.GetDevice(deviceId) == null)
            {
                MaterializeDevice(
                    deviceId,
                    observedUtc);
            }

            var matches =
                _repository
                    .GetInterfaces()
                    .Where(
                        item =>
                            item.DeviceId == deviceId &&
                            item.IfIndex == ifIndex)
                    .ToArray();

            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    "Multiple materialized interfaces share DeviceId and ifIndex.");
            }

            var existing =
                matches.Length == 0
                    ? null
                    : matches[0];

            if (existing != null &&
                existing.IsManual)
            {
                return;
            }

            _repository.SaveInterface(
                new DeviceInterface(
                    existing == null
                        ? Guid.NewGuid()
                        : existing.Id,
                    deviceId,
                    ifIndex,
                    existing == null
                        ? null
                        : existing.IfName,
                    existing == null
                        ? null
                        : existing.IfDescription,
                    existing == null
                        ? null
                        : existing.IfAlias,
                    existing == null
                        ? null
                        : existing.CustomName,
                    existing == null
                        ? null
                        : existing.MacAddress,
                    existing == null
                        ? null
                        : existing.AdminStatus,
                    existing == null
                        ? null
                        : existing.OperStatus,
                    existing == null
                        ? (long?)null
                        : existing.SpeedBps,
                    existing == null
                        ? null
                        : existing.MediaTypeAuto,
                    existing == null
                        ? null
                        : existing.MediaTypeOverride,
                    false,
                    existing != null &&
                        existing.IsHidden,
                    existing == null
                        ? observedUtc
                        : existing.FirstSeenUtc,
                    observedUtc));
        }

        public void MaterializeLldp(
            Guid deviceId,
            LldpObservation observation)
        {
            RequireDeviceId(deviceId);

            if (observation == null)
            {
                throw new ArgumentNullException(
                    nameof(observation));
            }

            var observedUtc =
                observation.Observation.CapturedUtc;

            RequireUtc(observedUtc);

            var localSystem =
                observation.LocalSystem;

            var existing =
                _repository.GetDevice(deviceId);

            SaveDevice(
                deviceId,
                observedUtc,
                existing,
                localSystem == null
                    ? null
                    : localSystem.SystemName,
                localSystem == null
                    ? null
                    : localSystem.ChassisId);

            var candidates =
                _resolver.Resolve(
                    new TopologyResolutionInput(
                        new[] { observation },
                        new CdpObservation[0],
                        new MacCorrelation[0]));

            foreach (var candidate in candidates)
            {
                MaterializeCandidate(
                    deviceId,
                    observedUtc,
                    candidate);
            }
        }

        private void MaterializeCandidate(
            Guid localDeviceId,
            DateTime observedUtc,
            PhysicalLinkCandidate candidate)
        {
            var remoteDevice =
                ResolveRemoteDevice(
                    localDeviceId,
                    candidate.RemoteEndpoint.ChassisId);

            if (remoteDevice == null)
            {
                return;
            }

            var localInterfaceId =
                MaterializeLldpInterface(
                    localDeviceId,
                    candidate.LocalEndpoint.PortId,
                    candidate.LocalEndpoint.PortDescription,
                    candidate.LocalEndpoint.PortIndex,
                    observedUtc);

            var remoteInterfaceId =
                MaterializeLldpInterface(
                    remoteDevice.Id,
                    candidate.RemoteEndpoint.PortId,
                    candidate.RemoteEndpoint.PortDescription,
                    null,
                    observedUtc);

            if (HasCompatibleManualLink(
                localDeviceId,
                localInterfaceId,
                remoteDevice.Id,
                remoteInterfaceId))
            {
                return;
            }

            var linkKey =
                PhysicalLinkIdentity.BuildLinkKey(
                    localDeviceId,
                    localInterfaceId,
                    remoteDevice.Id,
                    remoteInterfaceId);

            var link =
                _repository.SavePhysicalLink(
                    new PhysicalLink(
                        StableGuid(
                            "physical-link|" +
                            linkKey),
                        localDeviceId,
                        localInterfaceId,
                        remoteDevice.Id,
                        remoteInterfaceId,
                        PhysicalLinkStrength.Observed,
                        PhysicalLinkFreshness.Fresh,
                        null,
                        null,
                        "LLDP",
                        observedUtc,
                        observedUtc,
                        null,
                        ResolverVersion,
                        false,
                        false,
                        null));

            var directionPrefix =
                DirectionPrefix(localDeviceId);

            var existingEvidence =
                _repository
                    .GetPhysicalLinkEvidence(
                        link.Id)
                    .Where(
                        item =>
                            !item.SlotDiscriminator.StartsWith(
                                directionPrefix,
                                StringComparison.Ordinal))
                    .ToArray();

            var newEvidence =
                _evidenceMaterializer
                    .Materialize(
                        link.Id,
                        candidate.Evidence)
                    .Select(
                        item =>
                            WithDirection(
                                item,
                                localDeviceId))
                    .ToArray();

            var currentEvidence =
                existingEvidence
                    .Concat(newEvidence)
                    .ToArray();

            _repository.ReplacePhysicalLinkEvidence(
                link.Id,
                currentEvidence);

            if (HasReciprocalLldpEvidence(
                link,
                currentEvidence))
            {
                _repository.SavePhysicalLink(
                    new PhysicalLink(
                        link.Id,
                        link.DeviceAId,
                        link.InterfaceAId,
                        link.DeviceBId,
                        link.InterfaceBId,
                        PhysicalLinkStrength.Confirmed,
                        PhysicalLinkFreshness.Fresh,
                        link.MediaTypeResolved,
                        link.SpeedBpsResolved,
                        link.SourceSummary,
                        link.FirstSeenUtc,
                        observedUtc,
                        observedUtc,
                        ResolverVersion,
                        link.IsHidden,
                        link.IsArchived,
                        link.Notes));
            }
        }

        private static PhysicalLinkEvidence WithDirection(
            PhysicalLinkEvidence evidence,
            Guid localDeviceId)
        {
            return new PhysicalLinkEvidence(
                evidence.PhysicalLinkId,
                evidence.Kind,
                evidence.Strength,
                evidence.SourceAddress,
                DirectionPrefix(localDeviceId) +
                    evidence.SlotDiscriminator,
                evidence.ObservationId,
                evidence.CapturedUtc,
                evidence.Detail);
        }

        private static bool HasReciprocalLldpEvidence(
            PhysicalLink link,
            IEnumerable<PhysicalLinkEvidence> evidence)
        {
            var directions =
                new HashSet<string>(
                    evidence
                        .Where(
                            item =>
                                item.Kind ==
                                PhysicalLinkEvidenceKind.Lldp)
                        .Select(
                            item =>
                                DirectionFrom(
                                    item.SlotDiscriminator))
                        .Where(item => item != null),
                    StringComparer.Ordinal);

            return
                directions.Contains(
                    link.DeviceAId.ToString("N")) &&
                directions.Contains(
                    link.DeviceBId.ToString("N"));
        }

        private static string DirectionFrom(
            string slotDiscriminator)
        {
            const string prefix = "device:";

            if (string.IsNullOrWhiteSpace(
                    slotDiscriminator) ||
                !slotDiscriminator.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var separator =
                slotDiscriminator.IndexOf('|');

            if (separator <= prefix.Length)
            {
                return null;
            }

            return slotDiscriminator.Substring(
                prefix.Length,
                separator - prefix.Length);
        }

        private static string DirectionPrefix(
            Guid deviceId)
        {
            return
                "device:" +
                deviceId.ToString("N") +
                "|";
        }

        private bool HasCompatibleManualLink(
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId)
        {
            foreach (var link in
                _repository.GetPhysicalLinks())
            {
                if (link.Strength !=
                    PhysicalLinkStrength.Manual)
                {
                    continue;
                }

                Guid? existingA;
                Guid? existingB;

                if (link.DeviceAId == deviceAId &&
                    link.DeviceBId == deviceBId)
                {
                    existingA = link.InterfaceAId;
                    existingB = link.InterfaceBId;
                }
                else if (link.DeviceAId == deviceBId &&
                    link.DeviceBId == deviceAId)
                {
                    existingA = link.InterfaceBId;
                    existingB = link.InterfaceAId;
                }
                else
                {
                    continue;
                }

                if (InterfaceCompatible(
                        existingA,
                        interfaceAId) &&
                    InterfaceCompatible(
                        existingB,
                        interfaceBId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InterfaceCompatible(
            Guid? left,
            Guid? right)
        {
            return
                !left.HasValue ||
                !right.HasValue ||
                left.Value == right.Value;
        }

        private TopologyDevice ResolveRemoteDevice(
            Guid localDeviceId,
            string chassisId)
        {
            var normalized =
                NormalizeIdentity(chassisId);

            if (normalized == null)
            {
                return null;
            }

            var matches =
                _repository
                    .GetDevices()
                    .Where(
                        device =>
                            device.Id != localDeviceId &&
                            string.Equals(
                                NormalizeIdentity(
                                    device.LldpChassisId),
                                normalized,
                                StringComparison.Ordinal))
                    .ToArray();

            return matches.Length == 1
                ? matches[0]
                : null;
        }

        private Guid? MaterializeLldpInterface(
            Guid deviceId,
            string portId,
            string portDescription,
            int? localPortNumber,
            DateTime observedUtc)
        {
            var normalizedPortId =
                NormalizePortId(portId);

            var interfaces =
                _repository
                    .GetInterfaces()
                    .Where(
                        item =>
                            item.DeviceId == deviceId)
                    .ToArray();

            if (normalizedPortId != null)
            {
                var byName =
                    interfaces
                        .Where(
                            item =>
                                string.Equals(
                                    NormalizePortId(
                                        FirstNonEmpty(
                                            item.LldpPortId,
                                            item.IfName)),
                                    normalizedPortId,
                                    StringComparison.Ordinal))
                        .ToArray();

                if (byName.Length == 1)
                {
                    TouchLldpInterface(
                        byName[0],
                        portId,
                        portDescription,
                        observedUtc);

                    return byName[0].Id;
                }

                if (byName.Length > 1)
                {
                    return null;
                }
            }

            string identityToken;

            if (normalizedPortId != null)
            {
                identityToken =
                    "port-id|" + normalizedPortId;
            }
            else if (localPortNumber.HasValue)
            {
                identityToken =
                    "lldp-local-port-number|" +
                    localPortNumber.Value.ToString(
                        CultureInfo.InvariantCulture);
            }
            else
            {
                return null;
            }

            var id =
                StableGuid(
                    "lldp-interface|" +
                    deviceId.ToString("N") +
                    "|" +
                    identityToken);

            var existingById =
                interfaces
                    .SingleOrDefault(
                        item => item.Id == id);

            if (existingById != null)
            {
                TouchLldpInterface(
                    existingById,
                    portId,
                    portDescription,
                    observedUtc);

                return existingById.Id;
            }

            _repository.SaveInterface(
                new DeviceInterface(
                    id,
                    deviceId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    false,
                    observedUtc,
                    observedUtc,
                    portId,
                    portDescription));

            return id;
        }

        private void TouchLldpInterface(
            DeviceInterface existing,
            string portId,
            string portDescription,
            DateTime observedUtc)
        {
            if (existing.IsManual)
            {
                return;
            }

            _repository.SaveInterface(
                new DeviceInterface(
                    existing.Id,
                    existing.DeviceId,
                    existing.IfIndex,
                    existing.IfName,
                    existing.IfDescription,
                    existing.IfAlias,
                    existing.CustomName,
                    existing.MacAddress,
                    existing.AdminStatus,
                    existing.OperStatus,
                    existing.SpeedBps,
                    existing.MediaTypeAuto,
                    existing.MediaTypeOverride,
                    false,
                    existing.IsHidden,
                    FirstSeen(
                        existing.FirstSeenUtc,
                        observedUtc),
                    observedUtc,
                    FirstNonEmpty(
                        portId,
                        existing.LldpPortId),
                    FirstNonEmpty(
                        portDescription,
                        existing.LldpPortDescription)));
        }

        private void SaveDevice(
            Guid deviceId,
            DateTime observedUtc,
            TopologyDevice existing,
            string discoveredName,
            string lldpChassisId)
        {
            var origin =
                existing == null
                    ? DeviceDiscoveryOrigin.Automatic
                    : existing.DiscoveryOrigin;

            _repository.SaveDevice(
                new TopologyDevice(
                    deviceId,
                    existing == null
                        ? (Guid?)null
                        : existing.LocationId,
                    existing == null
                        ? null
                        : existing.CustomName,
                    existing == null
                        ? DeviceCategory.Unknown
                        : existing.Category,
                    origin,
                    existing == null
                        ? MonitoringCapability.Unknown
                        : existing.MonitoringCapability,
                    existing == null
                        ? null
                        : existing.VendorOverride,
                    existing == null
                        ? null
                        : existing.ModelOverride,
                    existing == null
                        ? null
                        : existing.Notes,
                    existing != null &&
                        existing.IsHidden,
                    existing != null &&
                        existing.IsArchived,
                    existing == null
                        ? observedUtc
                        : existing.FirstSeenUtc,
                    observedUtc,
                    observedUtc,
                    FirstNonEmpty(
                        discoveredName,
                        existing == null
                            ? null
                            : existing.DiscoveredName),
                    FirstNonEmpty(
                        lldpChassisId,
                        existing == null
                            ? null
                            : existing.LldpChassisId)));
        }

        private static DateTime FirstSeen(
            DateTime? existing,
            DateTime observedUtc)
        {
            return existing.HasValue &&
                existing.Value <= observedUtc
                    ? existing.Value
                    : observedUtc;
        }

        private static string FirstNonEmpty(
            string preferred,
            string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred)
                ? preferred.Trim()
                : fallback;
        }

        private static string NormalizePortId(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().ToUpperInvariant();
        }

        private static string NormalizeIdentity(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            var compact =
                new string(
                    trimmed
                        .Where(
                            valueChar =>
                                valueChar != ':' &&
                                valueChar != '-' &&
                                valueChar != '.' &&
                                !char.IsWhiteSpace(valueChar))
                        .ToArray());

            if (compact.Length == 12 &&
                compact.All(IsHexDigit))
            {
                return compact.ToUpperInvariant();
            }

            return trimmed.ToUpperInvariant();
        }

        private static bool IsHexDigit(char value)
        {
            return
                (value >= '0' && value <= '9') ||
                (value >= 'a' && value <= 'f') ||
                (value >= 'A' && value <= 'F');
        }

        private static Guid StableGuid(string value)
        {
            using (var sha = SHA256.Create())
            {
                var hash =
                    sha.ComputeHash(
                        Encoding.UTF8.GetBytes(value));

                var bytes = new byte[16];
                Array.Copy(hash, bytes, bytes.Length);
                return new Guid(bytes);
            }
        }

        private static void RequireDeviceId(
            Guid deviceId)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }
        }

        private static void RequireUtc(
            DateTime observedUtc)
        {
            if (observedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Observed time must be UTC.",
                    nameof(observedUtc));
            }
        }
    }
}
