using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint34FInterfaceDegradationDeliveryTests
    {
        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 14, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void DispatcherAcknowledgesOnlyAfterAdapterSuccess()
        {
            var outbox =
                new FakeOutbox(
                    CreateEvent());

            var adapter =
                new RecordingAdapter();

            var delivered =
                new InterfaceDegradationOutboxDispatcher(
                    outbox,
                    adapter)
                    .DispatchPending(
                        10,
                        () => T0.AddMinutes(1));

            Assert.AreEqual(
                1,
                delivered);

            Assert.AreEqual(
                1,
                adapter.Delivered.Count);

            Assert.AreEqual(
                1,
                outbox.Acknowledged.Count);
        }

        [TestMethod]
        public void AdapterFailureLeavesEventPendingAndUnacknowledged()
        {
            var item =
                CreateEvent();

            var outbox =
                new FakeOutbox(
                    item);

            try
            {
                new InterfaceDegradationOutboxDispatcher(
                    outbox,
                    new ThrowingAdapter())
                    .DispatchPending(
                        10,
                        () => T0.AddMinutes(1));

                Assert.Fail(
                    "Delivery failure must propagate.");
            }
            catch (InvalidOperationException exception)
            {
                Assert.AreEqual(
                    "synthetic delivery failure",
                    exception.Message);
            }

            Assert.AreEqual(
                0,
                outbox.Acknowledged.Count);

            Assert.AreEqual(
                1,
                outbox.ReadPending(10).Count);
        }

        [TestMethod]
        public void InvalidAcknowledgementClockCannotMarkDelivered()
        {
            var outbox =
                new FakeOutbox(
                    CreateEvent());

            try
            {
                new InterfaceDegradationOutboxDispatcher(
                    outbox,
                    new RecordingAdapter())
                    .DispatchPending(
                        10,
                        () => DateTime.Now);

                Assert.Fail(
                    "Non-UTC acknowledgement clock must fail.");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.AreEqual(
                0,
                outbox.Acknowledged.Count);
        }

        [TestMethod]
        public void EmptyOutboxDoesNotInvokeAdapter()
        {
            var outbox =
                new FakeOutbox();

            var adapter =
                new RecordingAdapter();

            var delivered =
                new InterfaceDegradationOutboxDispatcher(
                    outbox,
                    adapter)
                    .DispatchPending(
                        10,
                        () => T0);

            Assert.AreEqual(
                0,
                delivered);

            Assert.AreEqual(
                0,
                adapter.Delivered.Count);
        }

        private static InterfaceDegradationOutboxEvent
            CreateEvent()
        {
            return new InterfaceDegradationOutboxEvent(
                new Guid(
                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                7,
                T0,
                InterfaceDegradationTransitionKind.FirstAppearance,
                null,
                string.Empty,
                InterfaceDegradationStatus.Degraded,
                "1",
                10.0,
                null,
                new[]
                {
                    InterfaceDegradationReason
                        .ErrorRateThresholdExceeded
                });
        }

        private sealed class RecordingAdapter :
            IInterfaceDegradationDeliveryAdapter
        {
            public List<InterfaceDegradationOutboxEvent>
                Delivered { get; } =
                    new List<InterfaceDegradationOutboxEvent>();

            public void Deliver(
                InterfaceDegradationOutboxEvent item)
            {
                Delivered.Add(
                    item);
            }
        }

        private sealed class ThrowingAdapter :
            IInterfaceDegradationDeliveryAdapter
        {
            public void Deliver(
                InterfaceDegradationOutboxEvent item)
            {
                throw new InvalidOperationException(
                    "synthetic delivery failure");
            }
        }

        private sealed class FakeOutbox :
            IInterfaceDegradationEventOutbox
        {
            private readonly List<InterfaceDegradationOutboxEvent>
                _pending;

            public FakeOutbox(
                params InterfaceDegradationOutboxEvent[] items)
            {
                _pending =
                    new List<InterfaceDegradationOutboxEvent>(
                        items);
            }

            public List<string> Acknowledged { get; } =
                new List<string>();

            public IReadOnlyList<InterfaceDegradationOutboxEvent>
                ReadPending(
                    int maxCount)
            {
                return _pending.GetRange(
                    0,
                    Math.Min(
                        maxCount,
                        _pending.Count));
            }

            public IReadOnlyList<InterfaceDegradationPendingDelivery>
                ReadReady(
                    int maxCount,
                    DateTime eligibleUtc)
            {
                var count =
                    Math.Min(
                        maxCount,
                        _pending.Count);

                var result =
                    new List<InterfaceDegradationPendingDelivery>();

                for (var index = 0;
                     index < count;
                     index++)
                {
                    result.Add(
                        new InterfaceDegradationPendingDelivery(
                            _pending[index],
                            0,
                            null,
                            null));
                }

                return result;
            }

            public bool MarkDeliveryFailed(
                string eventKey,
                int expectedFailureCount,
                DateTime failedUtc,
                DateTime nextAttemptUtc)
            {
                return true;
            }

            public bool MarkDelivered(
                string eventKey,
                DateTime deliveredUtc)
            {
                for (var index = 0;
                     index < _pending.Count;
                     index++)
                {
                    if (string.Equals(
                        _pending[index].EventKey,
                        eventKey,
                        StringComparison.Ordinal))
                    {
                        _pending.RemoveAt(
                            index);

                        Acknowledged.Add(
                            eventKey);

                        return true;
                    }
                }

                return false;
            }
        }
    }
}
