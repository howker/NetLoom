using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Domain.Observations.Cdp
{
    public sealed class CdpObservation
    {
        public CdpObservation(
            Observation observation,
            IEnumerable<CdpRemoteNeighbor> neighbors)
        {
            Observation = observation ??
                throw new ArgumentNullException(nameof(observation));

            if (observation.Kind != ObservationKind.Cdp)
            {
                throw new ArgumentException(
                    "Observation kind must be CDP.",
                    nameof(observation));
            }

            if (neighbors == null)
            {
                throw new ArgumentNullException(nameof(neighbors));
            }

            Neighbors = neighbors.ToArray();
        }

        public Observation Observation { get; }

        public IReadOnlyList<CdpRemoteNeighbor> Neighbors { get; }
    }
}
