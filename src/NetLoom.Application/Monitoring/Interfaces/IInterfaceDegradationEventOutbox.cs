using System.Collections.Generic;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceDegradationEventOutbox
    {
        IReadOnlyList<InterfaceDegradationOutboxEvent>
            ReadPending(
                int maxCount);
    }
}
