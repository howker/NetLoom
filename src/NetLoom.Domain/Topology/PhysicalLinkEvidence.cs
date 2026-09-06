using System;

namespace NetLoom.Domain.Topology
{
    public sealed class PhysicalLinkEvidence
    {
        public PhysicalLinkEvidence(
            Guid physicalLinkId,
            PhysicalLinkEvidenceKind kind,
            PhysicalLinkEvidenceStrength strength,
            string sourceAddress,
            string slotDiscriminator,
            Guid? observationId,
            DateTime? capturedUtc,
            string detail)
        {
            if (physicalLinkId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(physicalLinkId));
            }

            if (string.IsNullOrWhiteSpace(sourceAddress))
            {
                throw new ArgumentException(
                    "Evidence source address is required.",
                    nameof(sourceAddress));
            }

            if (string.IsNullOrWhiteSpace(slotDiscriminator))
            {
                throw new ArgumentException(
                    "Evidence slot discriminator is required.",
                    nameof(slotDiscriminator));
            }

            if (observationId.HasValue &&
                observationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id cannot be empty.",
                    nameof(observationId));
            }

            if (capturedUtc.HasValue &&
                capturedUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Captured time must be UTC.",
                    nameof(capturedUtc));
            }

            PhysicalLinkId = physicalLinkId;
            Kind = kind;
            Strength = strength;
            SourceAddress = sourceAddress.Trim();
            SlotDiscriminator = slotDiscriminator.Trim();
            ObservationId = observationId;
            CapturedUtc = capturedUtc;
            Detail = Normalize(detail);
        }

        public Guid PhysicalLinkId { get; }

        public PhysicalLinkEvidenceKind Kind { get; }

        public PhysicalLinkEvidenceStrength Strength { get; }

        public string SourceAddress { get; }

        public string SlotDiscriminator { get; }

        public Guid? ObservationId { get; }

        public DateTime? CapturedUtc { get; }

        public string Detail { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }

    public enum PhysicalLinkEvidenceKind
    {
        Lldp,
        Cdp,
        ArpFdbCorrelation
    }

    public enum PhysicalLinkEvidenceStrength
    {
        Weak,
        Strong
    }
}
