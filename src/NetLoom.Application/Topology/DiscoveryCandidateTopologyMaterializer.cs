using System;
using System.Linq;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.Topology
{
    public sealed class DiscoveryCandidateTopologyMaterializer :
        IDiscoveryCandidateMaterializer
    {
        private readonly IMaterializedTopologyRepository _repository;

        public DiscoveryCandidateTopologyMaterializer(
            IMaterializedTopologyRepository repository)
        {
            _repository = repository ??
                throw new ArgumentNullException(
                    nameof(repository));
        }

        public Guid Materialize(
            DiscoveryCandidateSnapshot candidate,
            DateTime observedUtc)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(
                    nameof(candidate));
            }

            if (observedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Observed time must be UTC.",
                    nameof(observedUtc));
            }

            var managementAddress =
                candidate.Address.ToString();

            var matches =
                _repository
                    .GetDevices()
                    .Where(
                        item =>
                            string.Equals(
                                item.ManagementAddress,
                                managementAddress,
                                StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_MANAGEMENT_ADDRESS_AMBIGUOUS");
            }

            if (matches.Length == 1)
            {
                var existing =
                    matches[0];

                if (existing.DiscoveryOrigin !=
                    DeviceDiscoveryOrigin.Automatic)
                {
                    return existing.Id;
                }

                _repository.SaveDevice(
                    UpdatedAutomaticDevice(
                        existing,
                        candidate,
                        observedUtc,
                        managementAddress));

                return existing.Id;
            }

            var deviceId =
                Guid.NewGuid();

            _repository.SaveDevice(
                new TopologyDevice(
                    deviceId,
                    null,
                    null,
                    DeviceCategory.Unknown,
                    DeviceDiscoveryOrigin.Automatic,
                    MonitoringCapability.Unknown,
                    null,
                    null,
                    null,
                    false,
                    false,
                    observedUtc,
                    observedUtc,
                    candidate.SnmpResponded
                        ? observedUtc
                        : (DateTime?)null,
                    FirstNonEmpty(
                        candidate.SysName,
                        managementAddress),
                    null,
                    managementAddress));

            return deviceId;
        }

        private static TopologyDevice UpdatedAutomaticDevice(
            TopologyDevice existing,
            DiscoveryCandidateSnapshot candidate,
            DateTime observedUtc,
            string managementAddress)
        {
            var firstSeen =
                existing.FirstSeenUtc.HasValue &&
                existing.FirstSeenUtc.Value <= observedUtc
                    ? existing.FirstSeenUtc
                    : observedUtc;

            var lastSeen =
                existing.LastSeenUtc.HasValue &&
                existing.LastSeenUtc.Value > observedUtc
                    ? existing.LastSeenUtc.Value
                    : observedUtc;

            var lastResolved =
                candidate.SnmpResponded
                    ? Latest(
                        existing.LastResolvedUtc,
                        observedUtc)
                    : existing.LastResolvedUtc;

            return new TopologyDevice(
                existing.Id,
                existing.LocationId,
                existing.CustomName,
                existing.Category,
                existing.DiscoveryOrigin,
                existing.MonitoringCapability,
                existing.VendorOverride,
                existing.ModelOverride,
                existing.Notes,
                existing.IsHidden,
                existing.IsArchived,
                firstSeen,
                lastSeen,
                lastResolved,
                FirstNonEmpty(
                    candidate.SysName,
                    existing.DiscoveredName,
                    managementAddress),
                existing.LldpChassisId,
                managementAddress);
        }

        private static DateTime? Latest(
            DateTime? existing,
            DateTime candidate)
        {
            return existing.HasValue &&
                existing.Value > candidate
                    ? existing
                    : candidate;
        }

        private static string FirstNonEmpty(
            params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }
    }
}
