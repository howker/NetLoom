using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Application.MonitoringControl
{
    public interface IMonitoringControl
    {
        MonitoringControlSnapshot Current { get; }

        event EventHandler<MonitoringControlSnapshotChangedEventArgs>
            SnapshotChanged;

        Task StartAsync(
            MonitoringTarget target,
            MonitoringSessionPolicy policy,
            CancellationToken cancellationToken);

        Task StopAsync(
            CancellationToken cancellationToken);

        Task PollNowAsync(
            MonitoringTarget targetWhenStopped,
            MonitoringSessionPolicy policyWhenStopped,
            CancellationToken cancellationToken);
    }
}
