using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationOutboxDispatcher
    {
        private readonly IInterfaceDegradationEventOutbox
            _outbox;

        private readonly IInterfaceDegradationDeliveryAdapter
            _adapter;

        public InterfaceDegradationOutboxDispatcher(
            IInterfaceDegradationEventOutbox outbox,
            IInterfaceDegradationDeliveryAdapter adapter)
        {
            _outbox =
                outbox ??
                throw new ArgumentNullException(
                    nameof(outbox));

            _adapter =
                adapter ??
                throw new ArgumentNullException(
                    nameof(adapter));
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

            var pending =
                _outbox.ReadPending(
                    maxCount);

            var delivered = 0;

            foreach (var item in pending)
            {
                _adapter.Deliver(
                    item);

                var deliveredUtc =
                    utcNow();

                if (deliveredUtc.Kind !=
                    DateTimeKind.Utc)
                {
                    throw new InvalidOperationException(
                        "Interface degradation delivery acknowledgement time must be UTC.");
                }

                _outbox.MarkDelivered(
                    item.EventKey,
                    deliveredUtc);

                delivered++;
            }

            return delivered;
        }
    }
}
