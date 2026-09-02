using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Domain.Observations.Arp
{
    public sealed class ArpObservation
    {
        public ArpObservation(
            Observation observation,
            IEnumerable<ArpEntry> entries)
        {
            Observation = observation ??
                throw new ArgumentNullException(nameof(observation));

            if (observation.Kind != ObservationKind.Arp)
            {
                throw new ArgumentException(
                    "Observation kind must be ARP.",
                    nameof(observation));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            Entries = entries.ToArray();
        }

        public Observation Observation { get; }

        public IReadOnlyList<ArpEntry> Entries { get; }
    }
}
