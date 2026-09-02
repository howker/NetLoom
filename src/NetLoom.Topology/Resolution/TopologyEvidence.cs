using System;

namespace NetLoom.Topology.Resolution
{
    public sealed class TopologyEvidence
    {
        public TopologyEvidence(
            TopologyEvidenceKind kind,
            TopologyEvidenceStrength strength,
            Guid? observationId,
            DateTime? capturedUtc,
            string sourceAddress,
            string detail)
        {
            Kind = kind;
            Strength = strength;
            ObservationId = observationId;
            CapturedUtc = capturedUtc;
            SourceAddress = sourceAddress;
            Detail = detail;
        }

        public TopologyEvidenceKind Kind { get; }

        public TopologyEvidenceStrength Strength { get; }

        public Guid? ObservationId { get; }

        public DateTime? CapturedUtc { get; }

        public string SourceAddress { get; }

        public string Detail { get; }
    }
}
