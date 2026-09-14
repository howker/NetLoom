using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceDegradationTransitionProcessor
    {
        InterfaceDegradationTransition Observe(
            InterfaceDegradationClassification classification);
    }
}
