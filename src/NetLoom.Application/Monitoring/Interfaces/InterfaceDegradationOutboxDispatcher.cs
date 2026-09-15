using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationOutboxDispatcher
    {
        private readonly IInterfaceDegradationEventOutbox
            _outbox;

        private readonly IInterfaceDegradationDeliveryAdapter
            _adapter;

        private readonly InterfaceDegradationDeliveryRetryPolicy
            _retryPolicy;

        public InterfaceDegradationOutboxDispatcher(
            IInterfaceDegradationEventOutbox outbox,
            IInterfaceDegradationDeliveryAdapter adapter)
            : this(
                outbox,
                adapter,
                new InterfaceDegradationDeliveryRetryPolicy(
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromHours(1)))
        {
        }

        public InterfaceDegradationOutboxDispatcher(
            IInterfaceDegradationEventOutbox outbox,
            IInterfaceDegradationDeliveryAdapter adapter,
            InterfaceDegradationDeliveryRetryPolicy retryPolicy)
        {
            _outbox =
                outbox ??
                throw new ArgumentNullException(
                    nameof(outbox));

            _adapter =
                adapter ??
                throw new ArgumentNullException(
                    nameof(adapter));

            _retryPolicy =
                retryPolicy ??
                throw new ArgumentNullException(
                    nameof(retryPolicy));
        }

        public int DispatchPending(
            int maxCount,
            Func<DateTime> utcNow)
        {
            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCount));
            }

            if (utcNow == null)
            {
                throw new ArgumentNullException(
                    nameof(utcNow));
            }

            var eligibleUtc =
                ReadUtc(
                    utcNow,
                    "Interface degradation delivery eligibility time must be UTC.");

            var pending =
                _outbox.ReadReady(
                    maxCount,
                    eligibleUtc);

            var delivered = 0;

            foreach (var delivery in pending)
            {
                try
                {
                    _adapter.Deliver(
                        delivery.Event);
                }
                catch
                {
                    var failedUtc =
                        ReadUtc(
                            utcNow,
                            "Interface degradation delivery failure time must be UTC.");

                    var nextAttemptUtc =
                        _retryPolicy.GetNextAttemptUtc(
                            failedUtc,
                            checked(
                                delivery.FailureCount +
                                1));

                    _outbox.MarkDeliveryFailed(
                        delivery.Event.EventKey,
                        delivery.FailureCount,
                        failedUtc,
                        nextAttemptUtc);

                    throw;
                }

                var deliveredUtc =
                    ReadUtc(
                        utcNow,
                        "Interface degradation delivery acknowledgement time must be UTC.");

                _outbox.MarkDelivered(
                    delivery.Event.EventKey,
                    deliveredUtc);

                delivered++;
            }

            return delivered;
        }

        private static DateTime ReadUtc(
            Func<DateTime> utcNow,
            string errorMessage)
        {
            var value =
                utcNow();

            if (value.Kind !=
                DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    errorMessage);
            }

            return value;
        }
    }
}
