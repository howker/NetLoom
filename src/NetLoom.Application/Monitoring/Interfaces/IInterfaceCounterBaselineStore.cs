using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceCounterBaselineStore
    {
        InterfaceMonitoringSnapshot ReplaceAndGetPrevious(
            InterfaceMonitoringSnapshot current);
    }
}
