using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Topology.Correlation;

namespace NetLoom.Topology.Resolution
{
    public sealed class TopologyResolver
    {
        public IReadOnlyList<PhysicalLinkCandidate> Resolve(
            TopologyResolutionInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var result =
                new List<PhysicalLinkCandidate>();

            foreach (var observation in
                input.LldpObservations)
            {
                foreach (var neighbor in
                    observation.Neighbors)
                {
                    result.Add(
                        BuildLldpCandidate(
                            observation,
                            neighbor,
                            input.MacCorrelations));
                }
            }

            foreach (var observation in
                input.CdpObservations)
            {
                foreach (var neighbor in
                    observation.Neighbors)
                {
                    result.Add(
                        BuildCdpCandidate(
                            observation,
                            neighbor,
                            input.MacCorrelations));
                }
            }

            // ????:
            // MAC/FDB correlation ???? ?? ????
            // ?? ??????? ????????? ??????????? ?????.

            return result;
        }

        private static PhysicalLinkCandidate
            BuildLldpCandidate(
                LldpObservation observation,
                LldpRemoteNeighbor neighbor,
                IEnumerable<MacCorrelation> correlations)
        {
            var local =
                new LinkEndpointClaim(
                    observation.Observation.SourceAddress,
                    null,
                    null,
                    null,
                    LinkPortReferenceKind
                        .LldpLocalPortNumber,
                    neighbor.LocalPortNumber,
                    neighbor.LocalPort == null
                        ? null
                        : neighbor.LocalPort.PortId,
                    neighbor.LocalPort == null
                        ? null
                        : neighbor.LocalPort.PortDescription);

            var remote =
                new LinkEndpointClaim(
                    null,
                    neighbor.SystemName,
                    neighbor.ChassisId,
                    null,
                    LinkPortReferenceKind.ProtocolPortId,
                    null,
                    neighbor.PortId,
                    neighbor.PortDescription);

            var evidence =
                new List<TopologyEvidence>
                {
                    new TopologyEvidence(
                        TopologyEvidenceKind.Lldp,
                        TopologyEvidenceStrength.Strong,
                        observation.Observation.Id,
                        observation.Observation.CapturedUtc,
                        observation.Observation.SourceAddress,
                        "LLDP adjacency")
                };

            AttachSupportingCorrelations(
                local,
                remote,
                correlations,
                evidence);

            return new PhysicalLinkCandidate(
                local,
                remote,
                CalculateConfidence(
                    local,
                    remote),
                evidence);
        }

        private static PhysicalLinkCandidate
            BuildCdpCandidate(
                CdpObservation observation,
                CdpRemoteNeighbor neighbor,
                IEnumerable<MacCorrelation> correlations)
        {
            var local =
                new LinkEndpointClaim(
                    observation.Observation.SourceAddress,
                    null,
                    null,
                    null,
                    LinkPortReferenceKind.CdpCacheIfIndex,
                    neighbor.CacheIfIndex > 0
                        ? (int?)neighbor.CacheIfIndex
                        : null,
                    null,
                    null);

            var remoteAddress =
                !string.IsNullOrWhiteSpace(
                    neighbor.PrimaryManagementAddress)
                    ? neighbor.PrimaryManagementAddress
                    : neighbor.Address;

            var remote =
                new LinkEndpointClaim(
                    remoteAddress,
                    neighbor.SystemName,
                    null,
                    neighbor.DeviceId,
                    LinkPortReferenceKind.ProtocolPortId,
                    null,
                    neighbor.DevicePort,
                    null);

            var evidence =
                new List<TopologyEvidence>
                {
                    new TopologyEvidence(
                        TopologyEvidenceKind.Cdp,
                        TopologyEvidenceStrength.Strong,
                        observation.Observation.Id,
                        observation.Observation.CapturedUtc,
                        observation.Observation.SourceAddress,
                        "CDP adjacency")
                };

            AttachSupportingCorrelations(
                local,
                remote,
                correlations,
                evidence);

            return new PhysicalLinkCandidate(
                local,
                remote,
                CalculateConfidence(
                    local,
                    remote),
                evidence);
        }

        private static TopologyConfidence
            CalculateConfidence(
                LinkEndpointClaim local,
                LinkEndpointClaim remote)
        {
            var hasRemoteIdentity =
                !string.IsNullOrWhiteSpace(
                    remote.ChassisId) ||
                !string.IsNullOrWhiteSpace(
                    remote.DeviceIdClaim) ||
                !string.IsNullOrWhiteSpace(
                    remote.SystemName) ||
                !string.IsNullOrWhiteSpace(
                    remote.ManagementAddress);

            var hasRemotePort =
                !string.IsNullOrWhiteSpace(
                    remote.PortId);

            var hasLocalPort =
                local.PortIndex.HasValue ||
                !string.IsNullOrWhiteSpace(
                    local.PortId);

            return
                hasRemoteIdentity &&
                hasRemotePort &&
                hasLocalPort
                    ? TopologyConfidence.High
                    : TopologyConfidence.Medium;
        }

        private static void AttachSupportingCorrelations(
            LinkEndpointClaim local,
            LinkEndpointClaim remote,
            IEnumerable<MacCorrelation> correlations,
            ICollection<TopologyEvidence> evidence)
        {
            foreach (var correlation in correlations)
            {
                if (!Same(
                    local.ManagementAddress,
                    correlation.FdbSourceAddress))
                {
                    continue;
                }

                var addressMatch =
                    Same(
                        remote.ManagementAddress,
                        correlation.IpAddress);

                var chassisMacMatch =
                    SameMac(
                        remote.ChassisId,
                        correlation.MacAddress);

                var deviceMacMatch =
                    SameMac(
                        remote.DeviceIdClaim,
                        correlation.MacAddress);

                if (!addressMatch &&
                    !chassisMacMatch &&
                    !deviceMacMatch)
                {
                    continue;
                }

                evidence.Add(
                    new TopologyEvidence(
                        TopologyEvidenceKind
                            .ArpFdbCorrelation,
                        TopologyEvidenceStrength.Weak,
                        null,
                        null,
                        correlation.FdbSourceAddress,
                        "ARP/FDB MAC correlation"));
            }
        }

        private static bool Same(
            string left,
            string right)
        {
            return
                !string.IsNullOrWhiteSpace(left) &&
                !string.IsNullOrWhiteSpace(right) &&
                string.Equals(
                    left.Trim(),
                    right.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameMac(
            string left,
            string right)
        {
            var normalizedLeft =
                NormalizeMac(left);

            var normalizedRight =
                NormalizeMac(right);

            return
                normalizedLeft != null &&
                normalizedRight != null &&
                string.Equals(
                    normalizedLeft,
                    normalizedRight,
                    StringComparison.Ordinal);
        }

        private static string NormalizeMac(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var chars =
                value
                    .Where(
                        character =>
                            (character >= '0' &&
                             character <= '9') ||
                            (character >= 'A' &&
                             character <= 'F') ||
                            (character >= 'a' &&
                             character <= 'f'))
                    .Select(char.ToUpperInvariant)
                    .ToArray();

            // MAC-48 only.
            return chars.Length == 12
                ? new string(chars)
                : null;
        }
    }
}
