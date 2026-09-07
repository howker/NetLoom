using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.GraphSafety
{
    public sealed class ForwardingCycleAnalysis
    {
        public ForwardingCycleAnalysis(
            string instanceId,
            IEnumerable<Guid> confirmedCyclePhysicalLinkIds,
            IEnumerable<Guid> unresolvedPhysicalLinkIds)
        {
            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            if (confirmedCyclePhysicalLinkIds == null)
            {
                throw new ArgumentNullException(
                    nameof(confirmedCyclePhysicalLinkIds));
            }

            if (unresolvedPhysicalLinkIds == null)
            {
                throw new ArgumentNullException(
                    nameof(unresolvedPhysicalLinkIds));
            }

            var cycle =
                confirmedCyclePhysicalLinkIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            var unresolved =
                unresolvedPhysicalLinkIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            if (cycle.Any(id => id == Guid.Empty) ||
                unresolved.Any(id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    "Physical link ids cannot be empty.");
            }

            var overlap =
                new HashSet<Guid>(
                    cycle);

            overlap.IntersectWith(
                unresolved);

            if (overlap.Count > 0)
            {
                throw new ArgumentException(
                    "A physical link cannot be both confirmed-cycle and unresolved.");
            }

            InstanceId =
                instanceId.Trim();

            ConfirmedCyclePhysicalLinkIds =
                cycle;

            UnresolvedPhysicalLinkIds =
                unresolved;
        }

        public string InstanceId { get; }

        public IReadOnlyList<Guid>
            ConfirmedCyclePhysicalLinkIds { get; }

        public IReadOnlyList<Guid>
            UnresolvedPhysicalLinkIds { get; }

        public bool HasConfirmedForwardingCycle =>
            ConfirmedCyclePhysicalLinkIds.Count > 0;

        public bool IsComplete =>
            UnresolvedPhysicalLinkIds.Count == 0;
    }
}
