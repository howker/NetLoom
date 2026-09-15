using System;
using System.Collections.Generic;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceDegradationDeliveryStatusReader
    {
        IReadOnlyList<InterfaceDegradationDeliveryStatus>
            ReadStatus(
                int maxCount,
                DateTime nowUtc);
    }
}
