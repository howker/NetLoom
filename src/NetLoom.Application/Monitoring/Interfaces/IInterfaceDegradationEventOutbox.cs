using System;
using System.Collections.Generic;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public interface IInterfaceDegradationEventOutbox
    {
        IReadOnlyList<InterfaceDegradationOutboxEvent>
            ReadPending(
                int maxCount);

        IReadOnlyList<InterfaceDegradationPendingDelivery>
            ReadReady(
                int maxCount,
                DateTime eligibleUtc);

        bool MarkDeliveryFailed(
            string eventKey,
            int expectedFailureCount,
            DateTime failedUtc,
            DateTime nextAttemptUtc);

        bool MarkDelivered(
            string eventKey,
            DateTime deliveredUtc);
    }
}
