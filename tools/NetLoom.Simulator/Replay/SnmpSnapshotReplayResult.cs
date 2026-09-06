using System;
using NetLoom.Application.Observations;
using NetLoom.Domain.Observations;

namespace NetLoom.Simulator.Replay
{
    public sealed class SnmpSnapshotReplayResult
    {
        public SnmpSnapshotReplayResult(
            SnmpObservation rawObservation,
            object parsedObservation,
            int parsedItemCount)
        {
            RawObservation =
                rawObservation ??
                throw new ArgumentNullException(
                    nameof(rawObservation));

            ParsedObservation =
                parsedObservation ??
                throw new ArgumentNullException(
                    nameof(parsedObservation));

            if (parsedItemCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parsedItemCount));
            }

            ParsedItemCount =
                parsedItemCount;
        }

        public SnmpObservation RawObservation { get; }

        public object ParsedObservation { get; }

        public int ParsedItemCount { get; }

        public ObservationKind Kind
        {
            get
            {
                return RawObservation.Observation.Kind;
            }
        }
    }
}
