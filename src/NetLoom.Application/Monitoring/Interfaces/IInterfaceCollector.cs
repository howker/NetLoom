using System.Collections.Generic;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceCollector
    {
        IReadOnlyList<InterfaceMonitoringSnapshot> Collect(
            InterfaceCollectionRequest request);
    }
}
