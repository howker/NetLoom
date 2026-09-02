using System;
using NetLoom.Topology.Lifecycle;
using NetLoom.Topology.Resolution;

namespace NetLoom.Topology.Map
{
    public sealed class MapProjectionItem
    {
        public MapProjectionItem(
            PhysicalLinkCandidate candidate,
            TopologyFreshness freshness)
        {
            Candidate = candidate ??
                throw new ArgumentNullException(
                    nameof(candidate));

            Freshness = freshness;
        }

        public PhysicalLinkCandidate Candidate { get; }

        public TopologyFreshness Freshness { get; }
    }
}
