using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationPendingDelivery
    {
        public InterfaceDegradationPendingDelivery(
            InterfaceDegradationOutboxEvent item,
            int failureCount,
            DateTime? lastFailureUtc,
            DateTime? nextAttemptUtc)
        {
            if (item == null)
            {
                throw new ArgumentNullException(
                    nameof(item));
            }

            if (failureCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failureCount));
            }

            if (failureCount == 0)
            {
                if (lastFailureUtc.HasValue ||
                    nextAttemptUtc.HasValue)
                {
                    throw new ArgumentException(
                        "Delivery without failures cannot contain retry timestamps.");
                }
            }
            else
            {
                if (!lastFailureUtc.HasValue ||
                    !nextAttemptUtc.HasValue)
                {
                    throw new ArgumentException(
                        "Failed delivery requires durable failure and retry timestamps.");
                }

                ValidateUtc(
                    lastFailureUtc.Value,
                    nameof(lastFailureUtc));

                ValidateUtc(
                    nextAttemptUtc.Value,
                    nameof(nextAttemptUtc));

                if (nextAttemptUtc.Value <=
                    lastFailureUtc.Value)
                {
                    throw new ArgumentException(
                        "Next delivery attempt must be later than the last delivery failure.",
                        nameof(nextAttemptUtc));
                }
            }

            Event = item;
            FailureCount = failureCount;
            LastFailureUtc = lastFailureUtc;
            NextAttemptUtc = nextAttemptUtc;
        }

        public InterfaceDegradationOutboxEvent Event { get; }

        public int FailureCount { get; }

        public DateTime? LastFailureUtc { get; }

        public DateTime? NextAttemptUtc { get; }

        private static void ValidateUtc(
            DateTime value,
            string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivery retry timestamp must be UTC.",
                    parameterName);
            }
        }
    }
}
