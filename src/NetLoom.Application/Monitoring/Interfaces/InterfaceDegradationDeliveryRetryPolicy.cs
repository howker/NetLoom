using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationDeliveryRetryPolicy
    {
        public InterfaceDegradationDeliveryRetryPolicy(
            TimeSpan initialDelay,
            TimeSpan maximumDelay)
        {
            if (initialDelay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialDelay));
            }

            if (maximumDelay < initialDelay)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDelay));
            }

            InitialDelay = initialDelay;
            MaximumDelay = maximumDelay;
        }

        public TimeSpan InitialDelay { get; }

        public TimeSpan MaximumDelay { get; }

        public DateTime GetNextAttemptUtc(
            DateTime failedUtc,
            int failureCount)
        {
            if (failedUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivery failure timestamp must be UTC.",
                    nameof(failedUtc));
            }

            if (failureCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failureCount));
            }

            var delayTicks =
                InitialDelay.Ticks;

            for (var failureIndex = 1;
                 failureIndex < failureCount;
                 failureIndex++)
            {
                if (delayTicks >=
                    MaximumDelay.Ticks)
                {
                    delayTicks =
                        MaximumDelay.Ticks;

                    break;
                }

                if (delayTicks >
                    MaximumDelay.Ticks / 2)
                {
                    delayTicks =
                        MaximumDelay.Ticks;

                    break;
                }

                delayTicks *= 2;

                if (delayTicks >
                    MaximumDelay.Ticks)
                {
                    delayTicks =
                        MaximumDelay.Ticks;
                }
            }

            if (failedUtc.Ticks >
                DateTime.MaxValue.Ticks -
                    delayTicks)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failedUtc));
            }

            return new DateTime(
                failedUtc.Ticks + delayTicks,
                DateTimeKind.Utc);
        }
    }
}
