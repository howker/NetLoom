using System;

namespace NetLoom.Contracts.TopologyMap
{
    public sealed class MapEvidenceItem
    {
        public MapEvidenceItem(
            MapEvidenceKind kind,
            MapEvidenceStrength strength,
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

        public MapEvidenceKind Kind { get; }

        public MapEvidenceStrength Strength { get; }

        public Guid? ObservationId { get; }

        public DateTime? CapturedUtc { get; }

        public string SourceAddress { get; }

        public string Detail { get; }
    }
}
