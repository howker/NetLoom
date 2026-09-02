using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Domain.Observations.Cdp;
using NetLoom.Domain.Observations.Lldp;
using NetLoom.Topology.Correlation;

namespace NetLoom.Topology.Resolution
{
    public sealed class TopologyResolutionInput
    {
        public TopologyResolutionInput(
            IEnumerable<LldpObservation> lldpObservations,
            IEnumerable<CdpObservation> cdpObservations,
            IEnumerable<MacCorrelation> macCorrelations)
        {
            LldpObservations =
                (lldpObservations ??
                    throw new ArgumentNullException(
                        nameof(lldpObservations)))
                .ToArray();

            CdpObservations =
                (cdpObservations ??
                    throw new ArgumentNullException(
                        nameof(cdpObservations)))
                .ToArray();

            MacCorrelations =
                (macCorrelations ??
                    throw new ArgumentNullException(
                        nameof(macCorrelations)))
                .ToArray();
        }

        public IReadOnlyList<LldpObservation>
            LldpObservations { get; }

        public IReadOnlyList<CdpObservation>
            CdpObservations { get; }

        public IReadOnlyList<MacCorrelation>
            MacCorrelations { get; }
    }
}
