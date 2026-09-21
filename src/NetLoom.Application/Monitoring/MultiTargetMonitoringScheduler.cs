using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Application.Monitoring
{
    public sealed class MultiTargetMonitoringScheduler
    {
        private readonly Func<MonitoringPollRequest, MonitoringPollResult>
            _poll;

        private readonly Func<TimeSpan, CancellationToken, Task>
            _delay;

        public MultiTargetMonitoringScheduler(
            Func<MonitoringPollRequest, MonitoringPollResult> poll,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _poll = poll ??
                throw new ArgumentNullException(
                    nameof(poll));

            _delay = delay ??
                Delay;
        }

        public MultiTargetMonitoringSchedulerRunResult Run(
            IReadOnlyList<MonitoringScheduleTarget> targets,
            MonitoringConcurrencyPolicy concurrency,
            CancellationToken cancellationToken,
            Action<MonitoringScheduleTarget> onPollStarting = null,
            Action<MonitoringScheduleTarget, MonitoringPollResult> onPollCompleted = null,
            Action<MonitoringScheduleTarget> onBackpressureSkipped = null)
        {
            Validate(
                targets,
                concurrency);

            var completedPolls = 0;
            var backpressureSkips = 0;

            using (var linkedCancellation =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken))
            using (var pollSlots =
                new SemaphoreSlim(
                    concurrency.MaxConcurrentPolls,
                    concurrency.MaxConcurrentPolls))
            {
                var workers =
                    new List<Task>(
                        targets.Count);

                foreach (var target in targets)
                {
                    workers.Add(
                        Task.Run(
                            () =>
                                RunTargetAsync(
                                    target,
                                    pollSlots,
                                    linkedCancellation,
                                    () =>
                                        Interlocked.Increment(
                                            ref completedPolls),
                                    () =>
                                        Interlocked.Increment(
                                            ref backpressureSkips),
                                    onPollStarting,
                                    onPollCompleted,
                                    onBackpressureSkipped)));
                }

                Task.WhenAll(
                        workers)
                    .GetAwaiter()
                    .GetResult();
            }

            return new MultiTargetMonitoringSchedulerRunResult(
                Volatile.Read(
                    ref completedPolls),
                Volatile.Read(
                    ref backpressureSkips),
                cancellationToken.IsCancellationRequested);
        }

        private async Task RunTargetAsync(
            MonitoringScheduleTarget target,
            SemaphoreSlim pollSlots,
            CancellationTokenSource cancellation,
            Action recordCompleted,
            Action recordBackpressureSkip,
            Action<MonitoringScheduleTarget> onPollStarting,
            Action<MonitoringScheduleTarget, MonitoringPollResult> onPollCompleted,
            Action<MonitoringScheduleTarget> onBackpressureSkipped)
        {
            try
            {
                if (!await WaitForAsync(
                        target.InitialDelay,
                        cancellation.Token)
                    .ConfigureAwait(false))
                {
                    return;
                }

                while (!cancellation
                    .IsCancellationRequested)
                {
                    if (!pollSlots.Wait(
                            0))
                    {
                        recordBackpressureSkip();

                        onBackpressureSkipped?.Invoke(
                            target);

                        if (!await WaitForAsync(
                                target.Cadence,
                                cancellation.Token)
                            .ConfigureAwait(false))
                        {
                            return;
                        }

                        continue;
                    }

                    try
                    {
                        if (cancellation
                            .IsCancellationRequested)
                        {
                            return;
                        }

                        onPollStarting?.Invoke(
                            target);

                        var result =
                            _poll(
                                target.Request);

                        recordCompleted();

                        onPollCompleted?.Invoke(
                            target,
                            result);
                    }
                    finally
                    {
                        pollSlots.Release();
                    }

                    if (!await WaitForAsync(
                            target.Cadence,
                            cancellation.Token)
                        .ConfigureAwait(false))
                    {
                        return;
                    }
                }
            }
            catch
            {
                cancellation.Cancel();
                throw;
            }
        }

        private async Task<bool> WaitForAsync(
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            if (cancellationToken
                .IsCancellationRequested)
            {
                return false;
            }

            if (delay <= TimeSpan.Zero)
            {
                return true;
            }

            try
            {
                await _delay(
                        delay,
                        cancellationToken)
                    .ConfigureAwait(false);

                return !cancellationToken
                    .IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                if (!cancellationToken
                    .IsCancellationRequested)
                {
                    throw;
                }

                return false;
            }
        }

        private static void Validate(
            IReadOnlyList<MonitoringScheduleTarget> targets,
            MonitoringConcurrencyPolicy concurrency)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(
                    nameof(targets));
            }

            if (concurrency == null)
            {
                throw new ArgumentNullException(
                    nameof(concurrency));
            }

            if (targets.Count == 0)
            {
                throw new ArgumentException(
                    "At least one monitoring target is required.",
                    nameof(targets));
            }

            var deviceIds =
                new HashSet<Guid>();

            foreach (var target in targets)
            {
                if (target == null)
                {
                    throw new ArgumentException(
                        "Monitoring target cannot be null.",
                        nameof(targets));
                }

                if (!deviceIds.Add(
                        target.DeviceId))
                {
                    throw new ArgumentException(
                        "Duplicate monitoring DeviceId is not allowed.",
                        nameof(targets));
                }
            }
        }

        private static Task Delay(
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            return Task.Delay(
                delay,
                cancellationToken);
        }
    }
}
