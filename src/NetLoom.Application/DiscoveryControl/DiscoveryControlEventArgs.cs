using System;

namespace NetLoom.Application.DiscoveryControl
{
    public sealed class DiscoveryControlSnapshotChangedEventArgs :
        EventArgs
    {
        public DiscoveryControlSnapshotChangedEventArgs(
            DiscoveryControlSnapshot snapshot)
        {
            Snapshot = snapshot ??
                throw new ArgumentNullException(
                    nameof(snapshot));
        }

        public DiscoveryControlSnapshot Snapshot { get; }
    }

    public sealed class DiscoveryCandidateDiscoveredEventArgs :
        EventArgs
    {
        public DiscoveryCandidateDiscoveredEventArgs(
            DiscoveryCandidateSnapshot candidate)
        {
            Candidate = candidate ??
                throw new ArgumentNullException(
                    nameof(candidate));
        }

        public DiscoveryCandidateSnapshot Candidate { get; }
    }
}
