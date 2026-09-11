using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        MacIpLookupRequestTrackerTests
    {
        [TestMethod]
        public void
            RunningWorkerPreventsConcurrentStart()
        {
            var tracker =
                new MacIpLookupRequestTracker();

            var first =
                tracker.Queue(
                    "first");

            MacIpLookupRequest started;

            Assert.IsTrue(
                tracker.TryStartWorker(
                    out started));

            Assert.AreSame(
                first,
                started);

            tracker.Queue(
                "second");

            MacIpLookupRequest duplicate;

            Assert.IsFalse(
                tracker.TryStartWorker(
                    out duplicate));

            Assert.IsNull(
                duplicate);

            tracker.CompleteWorker();
        }

        [TestMethod]
        public void
            LatestPendingRequestSupersedesOlderPendingRequest()
        {
            var tracker =
                new MacIpLookupRequestTracker();

            var first =
                tracker.Queue(
                    "first");

            MacIpLookupRequest started;

            Assert.IsTrue(
                tracker.TryStartWorker(
                    out started));

            var second =
                tracker.Queue(
                    "second");

            var third =
                tracker.Queue(
                    "third");

            Assert.IsFalse(
                tracker.IsCurrent(
                    first));

            Assert.IsFalse(
                tracker.IsCurrent(
                    second));

            Assert.IsTrue(
                tracker.IsCurrent(
                    third));

            MacIpLookupRequest pending;

            Assert.IsTrue(
                tracker.TryTakePending(
                    out pending));

            Assert.AreSame(
                third,
                pending);

            Assert.AreEqual(
                "third",
                pending.Query);

            tracker.CompleteWorker();
        }

        [TestMethod]
        public void
            CloseInvalidatesCurrentAndClearsPendingRequest()
        {
            var tracker =
                new MacIpLookupRequestTracker();

            var first =
                tracker.Queue(
                    "first");

            MacIpLookupRequest started;

            Assert.IsTrue(
                tracker.TryStartWorker(
                    out started));

            var second =
                tracker.Queue(
                    "second");

            tracker.Close();

            Assert.IsFalse(
                tracker.IsCurrent(
                    first));

            Assert.IsFalse(
                tracker.IsCurrent(
                    second));

            MacIpLookupRequest pending;

            Assert.IsFalse(
                tracker.TryTakePending(
                    out pending));

            Assert.IsNull(
                pending);

            tracker.CompleteWorker();
        }

        [TestMethod]
        public void
            ClosedTrackerRejectsNewRequests()
        {
            var tracker =
                new MacIpLookupRequestTracker();

            tracker.Close();

            try
            {
                tracker.Queue(
                    "after-close");

                Assert.Fail(
                    "Expected closed tracker to reject a new request.");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
