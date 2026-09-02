using System;

namespace NetLoom.Domain.Observations
{
    public sealed class Observation
    {
        public Observation(
            Guid id,
            ObservationKind kind,
            string sourceAddress,
            DateTime capturedUtc)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id is required.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(sourceAddress))
            {
                throw new ArgumentException(
                    "Source address is required.",
                    nameof(sourceAddress));
            }

            if (capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Captured time must be UTC.",
                    nameof(capturedUtc));
            }

            Id = id;
            Kind = kind;
            SourceAddress = sourceAddress;
            CapturedUtc = capturedUtc;
        }

        public Guid Id { get; }

        public ObservationKind Kind { get; }

        public string SourceAddress { get; }

        public DateTime CapturedUtc { get; }
    }
}
