using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.Rings;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Stp;

namespace NetLoom.Topology.Rings
{
    public sealed class RingProtectionAnalyzer
    {
        public RingProtectionAnalysis Analyze(
            PhysicalRedundancyRegion region,
            IEnumerable<PhysicalLink> links,
            IEnumerable<StpTreeSnapshot> stpSnapshots,
            string instanceId)
        {
            if (region == null)
            {
                throw new ArgumentNullException(
                    nameof(region));
            }

            if (links == null)
            {
                throw new ArgumentNullException(
                    nameof(links));
            }

            if (stpSnapshots == null)
            {
                throw new ArgumentNullException(
                    nameof(stpSnapshots));
            }

            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            var normalizedInstanceId =
                instanceId.Trim();

            if (region.Kind !=
                PhysicalRedundancyRegionKind.SimpleRing)
            {
                return new RingProtectionAnalysis(
                    region.RegionKey,
                    normalizedInstanceId,
                    RingProtectionStatus.NotApplicable,
                    new Guid[0],
                    new Guid[0],
                    new Guid[0],
                    new Guid[0]);
            }

            var linkInput =
                links.ToArray();

            if (linkInput.Any(
                link => link == null))
            {
                throw new ArgumentException(
                    "Physical links cannot contain null.",
                    nameof(links));
            }

            var endpointResolver =
                new StpEndpointStateResolver(
                    stpSnapshots,
                    normalizedInstanceId);

            var linksById =
                linkInput
                    .GroupBy(
                        link => link.Id)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToArray());

            var memberDevices =
                new HashSet<Guid>(
                    region.DeviceIds);

            var forwarding =
                new List<Guid>();

            var blocking =
                new List<Guid>();

            var disabled =
                new List<Guid>();

            var unresolved =
                new List<Guid>();

            foreach (var physicalLinkId in
                region.PhysicalLinkIds)
            {
                PhysicalLink[] candidates;

                if (!linksById.TryGetValue(
                        physicalLinkId,
                        out candidates) ||
                    candidates.Length != 1)
                {
                    unresolved.Add(
                        physicalLinkId);

                    continue;
                }

                var link =
                    candidates[0];

                if (link.IsArchived ||
                    !memberDevices.Contains(
                        link.DeviceAId) ||
                    !memberDevices.Contains(
                        link.DeviceBId))
                {
                    unresolved.Add(
                        physicalLinkId);

                    continue;
                }

                var endpointA =
                    endpointResolver.Resolve(
                        link.DeviceAId,
                        link.InterfaceAId);

                var endpointB =
                    endpointResolver.Resolve(
                        link.DeviceBId,
                        link.InterfaceBId);

                var linkState =
                    ClassifyLink(
                        endpointA,
                        endpointB);

                switch (linkState)
                {
                    case StpEndpointState.Forwarding:
                        forwarding.Add(
                            physicalLinkId);
                        break;

                    case StpEndpointState.Blocking:
                        blocking.Add(
                            physicalLinkId);
                        break;

                    case StpEndpointState.Disabled:
                        disabled.Add(
                            physicalLinkId);
                        break;

                    default:
                        unresolved.Add(
                            physicalLinkId);
                        break;
                }
            }

            var status =
                DetermineStatus(
                    region.PhysicalLinkIds.Count,
                    forwarding.Count,
                    blocking.Count,
                    disabled.Count,
                    unresolved.Count);

            return new RingProtectionAnalysis(
                region.RegionKey,
                normalizedInstanceId,
                status,
                forwarding,
                blocking,
                disabled,
                unresolved);
        }

        private static StpEndpointState ClassifyLink(
            StpEndpointState endpointA,
            StpEndpointState endpointB)
        {
            if (endpointA ==
                    StpEndpointState.Unresolved ||
                endpointB ==
                    StpEndpointState.Unresolved)
            {
                return
                    StpEndpointState.Unresolved;
            }

            if (endpointA ==
                    StpEndpointState.Disabled ||
                endpointB ==
                    StpEndpointState.Disabled)
            {
                return
                    StpEndpointState.Disabled;
            }

            if (endpointA ==
                    StpEndpointState.Blocking ||
                endpointB ==
                    StpEndpointState.Blocking)
            {
                return
                    StpEndpointState.Blocking;
            }

            if (endpointA ==
                    StpEndpointState.Forwarding &&
                endpointB ==
                    StpEndpointState.Forwarding)
            {
                return
                    StpEndpointState.Forwarding;
            }

            return
                StpEndpointState.Unresolved;
        }

        private static RingProtectionStatus DetermineStatus(
            int memberLinkCount,
            int forwardingCount,
            int blockingCount,
            int disabledCount,
            int unresolvedCount)
        {
            if (unresolvedCount > 0)
            {
                return
                    RingProtectionStatus.Unresolved;
            }

            if (disabledCount > 0)
            {
                return
                    RingProtectionStatus.Degraded;
            }

            if (blockingCount == 1 &&
                forwardingCount ==
                    memberLinkCount - 1)
            {
                return
                    RingProtectionStatus.Protected;
            }

            if (blockingCount == 0 &&
                forwardingCount ==
                    memberLinkCount)
            {
                return
                    RingProtectionStatus.Unprotected;
            }

            return
                RingProtectionStatus.Degraded;
        }
    }
}
