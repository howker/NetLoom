using System;
using System.Net;
using NetLoom.Application.Discovery;

namespace NetLoom.Application.DiscoveryControl
{
    public sealed class DiscoveryControlSnapshot
    {
        public DiscoveryControlSnapshot(
            DiscoveryControlState state,
            string cidr,
            Guid? accessProfileId,
            int processedAddresses,
            int totalAddresses,
            int foundCandidates,
            IPAddress currentAddress,
            string faultMessage,
            DiscoveryPhase? currentPhase = null,
            int phaseStep = 0,
            int phaseCount = 0)
        {
            if (!Enum.IsDefined(
                typeof(DiscoveryControlState),
                state))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state));
            }

            if (processedAddresses < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(processedAddresses));
            }

            if (totalAddresses < processedAddresses)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalAddresses));
            }

            if (foundCandidates < 0 ||
                foundCandidates > processedAddresses)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(foundCandidates));
            }

            if (accessProfileId.HasValue &&
                accessProfileId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "DISCOVERY_ACCESS_PROFILE_ID_REQUIRED",
                    nameof(accessProfileId));
            }

            if (!currentPhase.HasValue)
            {
                if (phaseStep != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(phaseStep));
                }

                if (phaseCount != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(phaseCount));
                }
            }
            else
            {
                if (phaseStep < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(phaseStep));
                }

                if (phaseCount < phaseStep ||
                    phaseCount > 3)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(phaseCount));
                }
            }

            CurrentPhase = currentPhase;
            PhaseStep = phaseStep;
            PhaseCount = phaseCount;
            State = state;
            Cidr = Normalize(cidr);
            AccessProfileId = accessProfileId;
            ProcessedAddresses = processedAddresses;
            TotalAddresses = totalAddresses;
            FoundCandidates = foundCandidates;
            CurrentAddress = currentAddress;
            FaultMessage = Normalize(faultMessage);
        }

        public DiscoveryControlState State { get; }

        public string Cidr { get; }

        public Guid? AccessProfileId { get; }

        public int ProcessedAddresses { get; }

        public int TotalAddresses { get; }

        public int FoundCandidates { get; }

        public IPAddress CurrentAddress { get; }

        public string FaultMessage { get; }

        public DiscoveryPhase? CurrentPhase { get; }

        public int PhaseStep { get; }

        public int PhaseCount { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
