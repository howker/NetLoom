using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.Rings
{
    public enum PhysicalRedundancyRegionKind
    {
        Unknown = 0,
        SimpleRing = 1,
        ParallelLinks = 2,
        Composite = 3
    }

    public sealed class PhysicalRedundancyRegion
    {
        public PhysicalRedundancyRegion(
            string regionKey,
            PhysicalRedundancyRegionKind kind,
            IEnumerable<Guid> deviceIds,
            IEnumerable<Guid> physicalLinkIds)
        {
            if (string.IsNullOrWhiteSpace(
                regionKey))
            {
                throw new ArgumentException(
                    "Region key is required.",
                    nameof(regionKey));
            }

            if (kind ==
                PhysicalRedundancyRegionKind.Unknown)
            {
                throw new ArgumentException(
                    "Region kind must be known.",
                    nameof(kind));
            }

            if (deviceIds == null)
            {
                throw new ArgumentNullException(
                    nameof(deviceIds));
            }

            if (physicalLinkIds == null)
            {
                throw new ArgumentNullException(
                    nameof(physicalLinkIds));
            }

            var devices =
                deviceIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            var links =
                physicalLinkIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            if (devices.Length < 2)
            {
                throw new ArgumentException(
                    "Physical redundancy region requires at least two devices.",
                    nameof(deviceIds));
            }

            if (links.Length < 2)
            {
                throw new ArgumentException(
                    "Physical redundancy region requires at least two links.",
                    nameof(physicalLinkIds));
            }

            if (devices.Any(
                id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    "Physical redundancy region device ids cannot be empty.",
                    nameof(deviceIds));
            }

            if (links.Any(
                id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    "Physical redundancy region link ids cannot be empty.",
                    nameof(physicalLinkIds));
            }

            RegionKey =
                regionKey.Trim();

            Kind =
                kind;

            DeviceIds =
                devices;

            PhysicalLinkIds =
                links;
        }

        public string RegionKey { get; }

        public PhysicalRedundancyRegionKind Kind { get; }

        public IReadOnlyList<Guid> DeviceIds { get; }

        public IReadOnlyList<Guid> PhysicalLinkIds { get; }

        public bool IsNamedRingCandidate =>
            Kind ==
            PhysicalRedundancyRegionKind.SimpleRing;
    }
}
