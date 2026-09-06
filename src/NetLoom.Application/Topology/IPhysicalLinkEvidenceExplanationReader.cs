using System;
using System.Collections.Generic;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.Topology
{
    public interface IPhysicalLinkEvidenceExplanationReader
    {
        IReadOnlyList<PhysicalLinkEvidenceExplanation> Get(
            Guid physicalLinkId);
    }

    public sealed class PhysicalLinkEvidenceExplanation
    {
        public PhysicalLinkEvidenceExplanation(
            PhysicalLinkEvidence evidence,
            ObservationRawAvailability rawAvailability)
        {
            Evidence =
                evidence ??
                throw new ArgumentNullException(nameof(evidence));

            if (!evidence.ObservationId.HasValue &&
                rawAvailability !=
                ObservationRawAvailability.NotApplicable)
            {
                throw new ArgumentException(
                    "Evidence without observation id must be NotApplicable.",
                    nameof(rawAvailability));
            }

            if (evidence.ObservationId.HasValue &&
                rawAvailability ==
                ObservationRawAvailability.NotApplicable)
            {
                throw new ArgumentException(
                    "Evidence with observation id must have raw availability state.",
                    nameof(rawAvailability));
            }

            RawAvailability =
                rawAvailability;
        }

        public PhysicalLinkEvidence Evidence { get; }

        public ObservationRawAvailability RawAvailability { get; }
    }

    public enum ObservationRawAvailability
    {
        NotApplicable = 0,
        Available = 1,
        Expired = 2
    }
}
