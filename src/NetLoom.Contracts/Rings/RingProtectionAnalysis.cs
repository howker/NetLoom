using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.Rings
{
    public enum RingProtectionStatus
    {
        Unknown = 0,
        NotApplicable = 1,
        Protected = 2,
        Unprotected = 3,
        Degraded = 4,
        Unresolved = 5
    }

    public sealed class RingProtectionAnalysis
    {
        public RingProtectionAnalysis(
            string regionKey,
            string instanceId,
            RingProtectionStatus status,
            IEnumerable<Guid> forwardingPhysicalLinkIds,
            IEnumerable<Guid> blockingPhysicalLinkIds,
            IEnumerable<Guid> disabledPhysicalLinkIds,
            IEnumerable<Guid> unresolvedPhysicalLinkIds)
        {
            if (string.IsNullOrWhiteSpace(
                regionKey))
            {
                throw new ArgumentException(
                    "Region key is required.",
                    nameof(regionKey));
            }

            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            if (status ==
                RingProtectionStatus.Unknown)
            {
                throw new ArgumentException(
                    "Ring protection status must be known.",
                    nameof(status));
            }

            RegionKey =
                regionKey.Trim();

            InstanceId =
                instanceId.Trim();

            Status =
                status;

            ForwardingPhysicalLinkIds =
                NormalizeIds(
                    forwardingPhysicalLinkIds,
                    nameof(forwardingPhysicalLinkIds));

            BlockingPhysicalLinkIds =
                NormalizeIds(
                    blockingPhysicalLinkIds,
                    nameof(blockingPhysicalLinkIds));

            DisabledPhysicalLinkIds =
                NormalizeIds(
                    disabledPhysicalLinkIds,
                    nameof(disabledPhysicalLinkIds));

            UnresolvedPhysicalLinkIds =
                NormalizeIds(
                    unresolvedPhysicalLinkIds,
                    nameof(unresolvedPhysicalLinkIds));

            EnsureNoOverlap();
        }

        public string RegionKey { get; }

        public string InstanceId { get; }

        public RingProtectionStatus Status { get; }

        public IReadOnlyList<Guid>
            ForwardingPhysicalLinkIds { get; }

        public IReadOnlyList<Guid>
            BlockingPhysicalLinkIds { get; }

        public IReadOnlyList<Guid>
            DisabledPhysicalLinkIds { get; }

        public IReadOnlyList<Guid>
            UnresolvedPhysicalLinkIds { get; }

        public bool IsComplete =>
            UnresolvedPhysicalLinkIds.Count == 0;

        private static Guid[] NormalizeIds(
            IEnumerable<Guid> ids,
            string parameterName)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(
                    parameterName);
            }

            var result =
                ids
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            if (result.Any(
                id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    "Physical link ids cannot be empty.",
                    parameterName);
            }

            return result;
        }

        private void EnsureNoOverlap()
        {
            var seen =
                new HashSet<Guid>();

            foreach (var id in
                ForwardingPhysicalLinkIds
                    .Concat(
                        BlockingPhysicalLinkIds)
                    .Concat(
                        DisabledPhysicalLinkIds)
                    .Concat(
                        UnresolvedPhysicalLinkIds))
            {
                if (!seen.Add(id))
                {
                    throw new ArgumentException(
                        "A physical link can have only one protection classification.");
                }
            }
        }
    }
}
