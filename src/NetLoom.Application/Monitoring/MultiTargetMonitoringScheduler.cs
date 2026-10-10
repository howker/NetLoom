using System;
using System.Collections.Generic;
using System.Linq;
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

                var due = target.Groups.Select(group => new GroupDue(group)).ToList();
                var elapsed = TimeSpan.Zero;
                while (!cancellation.IsCancellationRequested && due.Count > 0)
                {
                    var ready = due.Where(item => item.Due <= elapsed).ToArray();
                    if (ready.Length == 0)
                    {
                        var wait = due.Min(item => item.Due.Ticks) - elapsed.Ticks;
                        if (!await WaitForAsync(TimeSpan.FromTicks(wait), cancellation.Token).ConfigureAwait(false)) return;
                        elapsed += TimeSpan.FromTicks(wait);
                        continue;
                    }
                    if (!pollSlots.Wait(
                            0))
                    {
                        recordBackpressureSkip();

                        onBackpressureSkipped?.Invoke(
                            target);

                        foreach (var item in ready) item.Due = elapsed + item.Group.Cadence;
                        continue;
                    }

                    MonitoringPollResult result;

                    try
                    {
                        if (cancellation
                            .IsCancellationRequested)
                        {
                            return;
                        }

                        onPollStarting?.Invoke(
                            target);

                        var selected = new HashSet<MonitoringPollKind>(ready.SelectMany(item => item.Group.Kinds));
                        var request = ready.Length == due.Count && selected.SetEquals(target.Request.Kinds)
                            ? target.Request
                            : new MonitoringPollRequest(target.Request.Address, target.Request.Port,
                                target.Request.Version, target.Request.Credentials, target.Request.TimeoutMilliseconds,
                                target.Request.RetryCount, target.Request.MaxRepetitions,
                                target.Request.Kinds.Where(selected.Contains), target.Request.DeviceId);
                        result = _poll(request);

                        recordCompleted();
                    }
                    finally
                    {
                        pollSlots.Release();
                    }

                    onPollCompleted?.Invoke(
                        target,
                        result);

                    foreach (var item in ready)
                    {
                        if (item.Group.PollsOnce) due.Remove(item);
                        else item.Due = elapsed + item.Group.Cadence;
                    }
                }
            }
            catch
            {
                cancellation.Cancel();
                throw;
            }
        }

        private sealed class GroupDue
        {
            public GroupDue(MonitoringScheduleGroup group) { Group = group; }
            public MonitoringScheduleGroup Group { get; }
            public TimeSpan Due { get; set; }
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
