using System;
using System.Net;

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
            string faultMessage)
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

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
