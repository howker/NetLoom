using System;
using NetLoom.Application.DiscoveryControl;

namespace NetLoom.Application.Topology
{
    public interface IDiscoveryCandidateMaterializer
    {
        Guid Materialize(
            DiscoveryCandidateSnapshot candidate,
            DateTime observedUtc);
    }
}
