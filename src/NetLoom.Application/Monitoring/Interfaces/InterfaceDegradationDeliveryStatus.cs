using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationDeliveryStatus
    {
        public InterfaceDegradationDeliveryStatus(
            InterfaceDegradationOutboxEvent item,
            int failureCount,
            DateTime? lastFailureUtc,
            DateTime? nextAttemptUtc,
            DateTime? deliveredUtc,
            DateTime nowUtc)
        {
            if (item == null)
            {
                throw new ArgumentNullException(
                    nameof(item));
            }

            if (nowUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivery status time must be UTC.",
                    nameof(nowUtc));
            }

            var retryState =
                new InterfaceDegradationPendingDelivery(
                    item,
                    failureCount,
                    lastFailureUtc,
                    nextAttemptUtc);

            if (deliveredUtc.HasValue &&
                deliveredUtc.Value.Kind !=
                    DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivered timestamp must be UTC.",
                    nameof(deliveredUtc));
            }

            Event = retryState.Event;
            FailureCount = retryState.FailureCount;
            LastFailureUtc = retryState.LastFailureUtc;
            NextAttemptUtc = retryState.NextAttemptUtc;
            DeliveredUtc = deliveredUtc;

            if (deliveredUtc.HasValue)
            {
                Kind =
                    InterfaceDegradationDeliveryStatusKind
                        .Delivered;
            }
            else if (nextAttemptUtc.HasValue &&
                nextAttemptUtc.Value > nowUtc)
            {
                Kind =
                    InterfaceDegradationDeliveryStatusKind
                        .Deferred;
            }
            else
            {
                Kind =
                    InterfaceDegradationDeliveryStatusKind
                        .Ready;
            }
        }

        public InterfaceDegradationOutboxEvent Event { get; }

        public int FailureCount { get; }

        public DateTime? LastFailureUtc { get; }

        public DateTime? NextAttemptUtc { get; }

        public DateTime? DeliveredUtc { get; }

        public InterfaceDegradationDeliveryStatusKind Kind
        {
            get;
        }
    }
}
