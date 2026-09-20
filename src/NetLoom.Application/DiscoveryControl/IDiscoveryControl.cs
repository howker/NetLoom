using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Application.DiscoveryControl
{
    public interface IDiscoveryControl
    {
        DiscoveryControlSnapshot Current { get; }

        event EventHandler<DiscoveryControlSnapshotChangedEventArgs>
            SnapshotChanged;

        event EventHandler<DiscoveryCandidateDiscoveredEventArgs>
            CandidateDiscovered;

        Task StartAsync(
            DiscoveryControlRequest request,
            CancellationToken cancellationToken);

        Task StopAsync(
            CancellationToken cancellationToken);
    }
}
