using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Domain.Observations.Fdb
{
    public sealed class FdbObservation
    {
        public FdbObservation(
            Observation observation,
            IEnumerable<BridgePortMapping> bridgePortMappings,
            IEnumerable<FdbEntry> entries)
        {
            Observation = observation ??
                throw new ArgumentNullException(nameof(observation));

            if (observation.Kind != ObservationKind.Fdb)
            {
                throw new ArgumentException(
                    "Observation kind must be FDB.",
                    nameof(observation));
            }

            if (bridgePortMappings == null)
            {
                throw new ArgumentNullException(
                    nameof(bridgePortMappings));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            BridgePortMappings =
                bridgePortMappings.ToArray();

            Entries = entries.ToArray();
        }

        public Observation Observation { get; }

        public IReadOnlyList<BridgePortMapping>
            BridgePortMappings { get; }

        public IReadOnlyList<FdbEntry> Entries { get; }
    }
}
