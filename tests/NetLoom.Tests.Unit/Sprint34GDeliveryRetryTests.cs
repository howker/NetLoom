using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint34GDeliveryRetryTests
    {
        private static readonly DateTime T0 =
            new DateTime(
                2026, 1, 15, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void RetryPolicyUsesDeterministicExponentialBackoffWithCap()
        {
            var policy =
                Policy();

            Assert.AreEqual(
                T0.AddMinutes(1),
                policy.GetNextAttemptUtc(
                    T0,
                    1));

            Assert.AreEqual(
                T0.AddMinutes(2),
                policy.GetNextAttemptUtc(
                    T0,
                    2));

            Assert.AreEqual(
                T0.AddMinutes(32),
                policy.GetNextAttemptUtc(
                    T0,
                    6));

            Assert.AreEqual(
                T0.AddHours(1),
                policy.GetNextAttemptUtc(
                    T0,
                    7));

            Assert.AreEqual(
                T0.AddHours(1),
                policy.GetNextAttemptUtc(
                    T0,
                    100));
        }

        [TestMethod]
        public void FailedDeliveryPersistsRetryWindow()
        {
            var outbox =
                new FakeOutbox(
                    CreateEvent());

            try
            {
                new InterfaceDegradationOutboxDispatcher(
                    outbox,
                    new AlwaysFailAdapter(),
                    Policy())
                    .DispatchPending(
                        10,
                        () => T0);

                Assert.Fail(
                    "Synthetic delivery failure must propagate.");
            }
            catch (InvalidOperationException exception)
            {
                Assert.AreEqual(
                    "synthetic delivery failure",
                    exception.Message);
            }

            Assert.AreEqual(
                1,
                outbox.FailureCount);

            Assert.AreEqual(
                T0,
                outbox.LastFailureUtc);

            Assert.AreEqual(
                T0.AddMinutes(1),
                outbox.NextAttemptUtc);

            Assert.AreEqual(
                0,
                outbox.ReadReady(
                    10,
                    T0.AddSeconds(59))
                    .Count);

            Assert.AreEqual(
                1,
                outbox.ReadReady(
                    10,
                    T0.AddMinutes(1))
                    .Count);
        }

        [TestMethod]
        public void SecondFailureUsesDurableFailureCount()
        {
            var outbox =
                new FakeOutbox(
                    CreateEvent());

            FailAt(
                outbox,
                T0);

            FailAt(
                outbox,
                T0.AddMinutes(1));

            Assert.AreEqual(
                2,
                outbox.FailureCount);

            Assert.AreEqual(
                T0.AddMinutes(1),
                outbox.LastFailureUtc);

            Assert.AreEqual(
                T0.AddMinutes(3),
                outbox.NextAttemptUtc);
        }

        [TestMethod]
        public void SuccessfulRetryAcknowledgesEvent()
        {
            var outbox =
                new FakeOutbox(
                    CreateEvent());

            FailAt(
                outbox,
                T0);

            var delivered =
                new InterfaceDegradationOutboxDispatcher(
                    outbox,
                    new RecordingAdapter(),
                    Policy())
                    .DispatchPending(
                        10,
                        () => T0.AddMinutes(1));

            Assert.AreEqual(
                1,
                delivered);

            Assert.AreEqual(
                0,
                outbox.ReadPending(
                    10)
                    .Count);
        }

        private static void FailAt(
            FakeOutbox outbox,
            DateTime nowUtc)
        {
            try
            {
                new InterfaceDegradationOutboxDispatcher(
                    outbox,
                    new AlwaysFailAdapter(),
                    Policy())
                    .DispatchPending(
                        10,
                        () => nowUtc);

                Assert.Fail(
                    "Synthetic delivery failure must propagate.");
            }
            catch (InvalidOperationException exception)
            {
                Assert.AreEqual(
                    "synthetic delivery failure",
                    exception.Message);
            }
        }

        private static InterfaceDegradationDeliveryRetryPolicy
            Policy()
        {
            return new InterfaceDegradationDeliveryRetryPolicy(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromHours(1));
        }

        private static InterfaceDegradationOutboxEvent
            CreateEvent()
        {
            return new InterfaceDegradationOutboxEvent(
                new Guid(
                    "bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
                9,
                T0,
                InterfaceDegradationTransitionKind.FirstAppearance,
                null,
                string.Empty,
                InterfaceDegradationStatus.Degraded,
                "4",
                8.0,
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
            public void Deliver(
                InterfaceDegradationOutboxEvent item)
            {
            }
        }

        private sealed class AlwaysFailAdapter :
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
            private readonly InterfaceDegradationOutboxEvent
                _item;

            private bool _delivered;

            public FakeOutbox(
                InterfaceDegradationOutboxEvent item)
            {
                _item = item;
            }

            public int FailureCount { get; private set; }

            public DateTime? LastFailureUtc { get; private set; }

            public DateTime? NextAttemptUtc { get; private set; }

            public IReadOnlyList<InterfaceDegradationOutboxEvent>
                ReadPending(
                    int maxCount)
            {
                if (_delivered ||
                    maxCount < 1)
                {
                    return Array.Empty<InterfaceDegradationOutboxEvent>();
                }

                return new[]
                {
                    _item
                };
            }

            public IReadOnlyList<InterfaceDegradationPendingDelivery>
                ReadReady(
                    int maxCount,
                    DateTime eligibleUtc)
            {
                if (_delivered ||
                    maxCount < 1 ||
                    (NextAttemptUtc.HasValue &&
                     NextAttemptUtc.Value >
                        eligibleUtc))
                {
                    return Array.Empty<InterfaceDegradationPendingDelivery>();
                }

                return new[]
                {
                    new InterfaceDegradationPendingDelivery(
                        _item,
                        FailureCount,
                        LastFailureUtc,
                        NextAttemptUtc)
                };
            }

            public bool MarkDeliveryFailed(
                string eventKey,
                int expectedFailureCount,
                DateTime failedUtc,
                DateTime nextAttemptUtc)
            {
                if (_delivered ||
                    !string.Equals(
                        eventKey,
                        _item.EventKey,
                        StringComparison.Ordinal) ||
                    expectedFailureCount !=
                        FailureCount)
                {
                    return false;
                }

                FailureCount++;
                LastFailureUtc = failedUtc;
                NextAttemptUtc = nextAttemptUtc;

                return true;
            }

            public bool MarkDelivered(
                string eventKey,
                DateTime deliveredUtc)
            {
                if (_delivered ||
                    !string.Equals(
                        eventKey,
                        _item.EventKey,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                _delivered = true;
                return true;
            }
        }
    }
}
