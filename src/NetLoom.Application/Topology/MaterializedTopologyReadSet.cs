using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations.Stp;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.Topology
{
    public sealed class MaterializedTopologyReadSet
    {
        public MaterializedTopologyReadSet(
            IEnumerable<TopologyDevice> devices,
            IEnumerable<DeviceInterface> interfaces,
            IEnumerable<PhysicalLink> physicalLinks,
            IEnumerable<PhysicalLinkEvidence> physicalLinkEvidence,
            IEnumerable<Location> locations,
            IEnumerable<BoundStpObservation> latestStp,
            IEnumerable<InterfaceDegradationState> interfaceDegradationStates = null,
            IEnumerable<PhysicalLinkEvidenceExplanation> physicalLinkEvidenceExplanations = null)
        {
            Devices = Copy(
                devices,
                nameof(devices));

            Interfaces = Copy(
                interfaces,
                nameof(interfaces));

            PhysicalLinks = Copy(
                physicalLinks,
                nameof(physicalLinks));

            PhysicalLinkEvidence = Copy(
                physicalLinkEvidence,
                nameof(physicalLinkEvidence));

            Locations = Copy(
                locations,
                nameof(locations));

            LatestStp = Copy(
                latestStp,
                nameof(latestStp));

            InterfaceDegradationStates =
                interfaceDegradationStates == null
                    ? new InterfaceDegradationState[0]
                    : Copy(
                        interfaceDegradationStates,
                        nameof(interfaceDegradationStates));

            PhysicalLinkEvidenceExplanations =
                physicalLinkEvidenceExplanations == null
                    ? new PhysicalLinkEvidenceExplanation[0]
                    : Copy(
                        physicalLinkEvidenceExplanations,
                        nameof(physicalLinkEvidenceExplanations));
        }

        public IReadOnlyList<TopologyDevice> Devices { get; }

        public IReadOnlyList<DeviceInterface> Interfaces { get; }

        public IReadOnlyList<PhysicalLink> PhysicalLinks { get; }

        public IReadOnlyList<PhysicalLinkEvidence>
            PhysicalLinkEvidence { get; }

        public IReadOnlyList<Location> Locations { get; }

        public IReadOnlyList<BoundStpObservation> LatestStp { get; }

        public IReadOnlyList<InterfaceDegradationState>
            InterfaceDegradationStates { get; }

        public IReadOnlyList<PhysicalLinkEvidenceExplanation>
            PhysicalLinkEvidenceExplanations { get; }

        private static IReadOnlyList<T> Copy<T>(
            IEnumerable<T> items,
            string parameterName)
        {
            if (items == null)
            {
                throw new ArgumentNullException(
                    parameterName);
            }

            return items.ToArray();
        }
    }
}
