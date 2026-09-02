using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Topology.Resolution
{
    public sealed class PhysicalLinkCandidate
    {
        public PhysicalLinkCandidate(
            LinkEndpointClaim localEndpoint,
            LinkEndpointClaim remoteEndpoint,
            TopologyConfidence confidence,
            IEnumerable<TopologyEvidence> evidence)
        {
            LocalEndpoint = localEndpoint ??
                throw new ArgumentNullException(
                    nameof(localEndpoint));

            RemoteEndpoint = remoteEndpoint ??
                throw new ArgumentNullException(
                    nameof(remoteEndpoint));

            if (evidence == null)
            {
                throw new ArgumentNullException(
                    nameof(evidence));
            }

            Confidence = confidence;
            Evidence = evidence.ToArray();
        }

        public LinkEndpointClaim LocalEndpoint { get; }

        public LinkEndpointClaim RemoteEndpoint { get; }

        public TopologyConfidence Confidence { get; }

        public IReadOnlyList<TopologyEvidence> Evidence { get; }
    }
}
