using System;
using System.Collections.Generic;
using NetLoom.Domain.Topology;

namespace NetLoom.Topology.Resolution
{
    public sealed class PhysicalLinkEvidenceMaterializer
    {
        public IReadOnlyList<PhysicalLinkEvidence> Materialize(
            Guid physicalLinkId,
            IEnumerable<TopologyEvidence> evidence)
        {
            if (physicalLinkId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(physicalLinkId));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var result =
                new List<PhysicalLinkEvidence>();

            foreach (var item in evidence)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "Evidence item cannot be null.",
                        nameof(evidence));
                }

                result.Add(
                    new PhysicalLinkEvidence(
                        physicalLinkId,
                        MapKind(item.Kind),
                        MapStrength(item.Strength),
                        item.SourceAddress,
                        item.SlotDiscriminator,
                        item.ObservationId,
                        item.CapturedUtc,
                        item.Detail));
            }

            return result;
        }

        private static PhysicalLinkEvidenceKind MapKind(
            TopologyEvidenceKind kind)
        {
            switch (kind)
            {
                case TopologyEvidenceKind.Lldp:
                    return PhysicalLinkEvidenceKind.Lldp;

                case TopologyEvidenceKind.Cdp:
                    return PhysicalLinkEvidenceKind.Cdp;

                default:
                    return PhysicalLinkEvidenceKind.ArpFdbCorrelation;
            }
        }

        private static PhysicalLinkEvidenceStrength MapStrength(
            TopologyEvidenceStrength strength)
        {
            return strength == TopologyEvidenceStrength.Strong
                ? PhysicalLinkEvidenceStrength.Strong
                : PhysicalLinkEvidenceStrength.Weak;
        }
    }
}
