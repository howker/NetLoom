using System;
using System.Net;

namespace NetLoom.Application.Discovery
{
    public sealed class DiscoveryPhaseUpdate
    {
        public DiscoveryPhaseUpdate(
            IPAddress address,
            int addressIndex,
            int totalAddresses,
            DiscoveryPhase phase,
            int step,
            int stepCount)
        {
            Address = address ??
                throw new ArgumentNullException(nameof(address));

            if (addressIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(addressIndex));
            }

            if (totalAddresses < addressIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalAddresses));
            }

            if (!Enum.IsDefined(
                typeof(DiscoveryPhase),
                phase))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(phase));
            }

            if (step < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(step));
            }

            if (stepCount < step ||
                stepCount > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stepCount));
            }

            AddressIndex = addressIndex;
            TotalAddresses = totalAddresses;
            Phase = phase;
            Step = step;
            StepCount = stepCount;
        }

        public IPAddress Address { get; }

        public int AddressIndex { get; }

        public int TotalAddresses { get; }

        public DiscoveryPhase Phase { get; }

        public int Step { get; }

        public int StepCount { get; }
    }
}
