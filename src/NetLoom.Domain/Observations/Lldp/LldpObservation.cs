using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Domain.Observations.Lldp
{
    public sealed class LldpObservation
    {
        public LldpObservation(
            Observation observation,
            IEnumerable<LldpRemoteNeighbor> neighbors)
        {
            Observation = observation ??
                throw new ArgumentNullException(nameof(observation));

            if (observation.Kind != ObservationKind.Lldp)
            {
                throw new ArgumentException(
                    "Observation kind must be LLDP.",
                    nameof(observation));
            }

            if (neighbors == null)
            {
                throw new ArgumentNullException(nameof(neighbors));
            }

            Neighbors = neighbors.ToArray();
        }

        public Observation Observation { get; }

        public IReadOnlyList<LldpRemoteNeighbor> Neighbors { get; }
    }
}
