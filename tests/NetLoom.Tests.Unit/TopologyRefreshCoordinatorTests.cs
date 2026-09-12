using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        TopologyRefreshCoordinatorTests
    {
        [TestMethod]
        public void
            BlockedRefreshRunsOffCallerAndSecondRefreshIsSkipped()
        {
            using (var provider =
                new BlockingProvider(false))
            {
                var coordinator =
                    new TopologyRefreshCoordinator(
                        provider);

                var callerThreadId =
                    Thread.CurrentThread.ManagedThreadId;

                var first =
                    coordinator.RefreshAsync(
                        "cist",
                        CancellationToken.None);

                Assert.IsTrue(
                    provider.Entered.Wait(
                        TimeSpan.FromSeconds(2)),
                    "Background refresh did not start.");

                Assert.AreNotEqual(
                    callerThreadId,
                    provider.WorkerThreadId);

                Assert.IsFalse(
                    first.IsCompleted);

                var second =
                    coordinator.RefreshAsync(
                            "cist",
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                Assert.AreEqual(
                    TopologyRefreshExecutionKind.Skipped,
                    second.Kind);

                Assert.AreEqual(
                    1,
                    provider.CallCount);

                provider.Release.Set();

                var completed =
                    first.GetAwaiter()
                        .GetResult();

                Assert.AreEqual(
                    TopologyRefreshExecutionKind.Succeeded,
                    completed.Kind);

                Assert.IsNotNull(
                    completed.Snapshot);
            }
        }

        [TestMethod]
        public void
            CloseDuringInFlightDiscardsSuccessAndRejectsFutureRefresh()
        {
            using (var provider =
                new BlockingProvider(false))
            {
                var coordinator =
                    new TopologyRefreshCoordinator(
                        provider);

                var first =
                    coordinator.RefreshAsync(
                        "cist",
                        CancellationToken.None);

                Assert.IsTrue(
                    provider.Entered.Wait(
                        TimeSpan.FromSeconds(2)),
                    "Background refresh did not start.");

                coordinator.Close();
                provider.Release.Set();

                var discarded =
                    first.GetAwaiter()
                        .GetResult();

                Assert.AreEqual(
                    TopologyRefreshExecutionKind.Discarded,
                    discarded.Kind);

                Assert.IsNull(
                    discarded.Snapshot);

                var afterClose =
                    coordinator.RefreshAsync(
                            "cist",
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                Assert.AreEqual(
                    TopologyRefreshExecutionKind.Skipped,
                    afterClose.Kind);

                Assert.AreEqual(
                    1,
                    provider.CallCount);
            }
        }

        [TestMethod]
        public void
            CloseDuringInFlightSuppressesLateFailure()
        {
            using (var provider =
                new BlockingProvider(true))
            {
                var coordinator =
                    new TopologyRefreshCoordinator(
                        provider);

                var first =
                    coordinator.RefreshAsync(
                        "cist",
                        CancellationToken.None);

                Assert.IsTrue(
                    provider.Entered.Wait(
                        TimeSpan.FromSeconds(2)),
                    "Background refresh did not start.");

                coordinator.Close();
                provider.Release.Set();

                var discarded =
                    first.GetAwaiter()
                        .GetResult();

                Assert.AreEqual(
                    TopologyRefreshExecutionKind.Discarded,
                    discarded.Kind);
            }
        }

        [TestMethod]
        public void
            CurrentFailureStillPropagates()
        {
            using (var provider =
                new BlockingProvider(true))
            {
                var coordinator =
                    new TopologyRefreshCoordinator(
                        provider);

                var refresh =
                    coordinator.RefreshAsync(
                        "cist",
                        CancellationToken.None);

                Assert.IsTrue(
                    provider.Entered.Wait(
                        TimeSpan.FromSeconds(2)),
                    "Background refresh did not start.");

                provider.Release.Set();

                try
                {
                    refresh.GetAwaiter()
                        .GetResult();

                    Assert.Fail(
                        "Expected refresh failure.");
                }
                catch (InvalidOperationException error)
                {
                    Assert.AreEqual(
                        "controlled refresh failure",
                        error.Message);
                }
            }
        }

        private static TopologyRefreshSnapshot
            Snapshot()
        {
            var capturedUtc =
                new DateTime(
                    2026,
                    9,
                    12,
                    1,
                    0,
                    0,
                    DateTimeKind.Utc);

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    capturedUtc,
                    new MapNode[0],
                    new MapLink[0]),
                new TopologyAlertSnapshot(
                    capturedUtc,
                    "cist",
                    new TopologyAlert[0]));
        }

        private sealed class BlockingProvider :
            ITopologyRefreshSnapshotProvider,
            IDisposable
        {
            private readonly bool
                _throwAfterRelease;

            public BlockingProvider(
                bool throwAfterRelease)
            {
                _throwAfterRelease =
                    throwAfterRelease;

                Entered =
                    new ManualResetEventSlim(false);

                Release =
                    new ManualResetEventSlim(false);
            }

            public ManualResetEventSlim Entered { get; }

            public ManualResetEventSlim Release { get; }

            public int CallCount { get; private set; }

            public int WorkerThreadId { get; private set; }

            public TopologyRefreshSnapshot
                GetSnapshot(
                    string stpInstanceId)
            {
                CallCount++;

                WorkerThreadId =
                    Thread.CurrentThread.ManagedThreadId;

                Entered.Set();

                if (!Release.Wait(
                    TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException(
                        "Controlled refresh was not released.");
                }

                if (_throwAfterRelease)
                {
                    throw new InvalidOperationException(
                        "controlled refresh failure");
                }

                return Snapshot();
            }

            public void Dispose()
            {
                Entered.Dispose();
                Release.Dispose();
            }
        }
    }
}
