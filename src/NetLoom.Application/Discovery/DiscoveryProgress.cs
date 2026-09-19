using System;
using System.Net;

namespace NetLoom.Application.Discovery
{
    public sealed class DiscoveryProgress
    {
        public DiscoveryProgress(
            IPAddress address,
            int processedAddresses,
            int totalAddresses,
            DiscoveryCandidate candidate)
        {
            Address = address ??
                throw new ArgumentNullException(nameof(address));

            if (processedAddresses < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(processedAddresses));
            }

            if (totalAddresses < processedAddresses)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalAddresses));
            }

            ProcessedAddresses = processedAddresses;
            TotalAddresses = totalAddresses;
            Candidate = candidate;
        }

        public IPAddress Address { get; }

        public int ProcessedAddresses { get; }

        public int TotalAddresses { get; }

        public DiscoveryCandidate Candidate { get; }

        public bool CandidateFound
        {
            get { return Candidate != null; }
        }
    }
}
