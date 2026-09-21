using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Application.MonitoringControl
{
    public interface IMultiTargetMonitoringControl :
        IMonitoringControl
    {
        Task StartSetAsync(
            IReadOnlyList<MonitoringTarget> targets,
            MonitoringSessionPolicy policy,
            MonitoringTargetSetPolicy targetSetPolicy,
            CancellationToken cancellationToken);
    }
}
