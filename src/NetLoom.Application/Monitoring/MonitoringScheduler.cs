using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringScheduler
    {
        private readonly Func<MonitoringPollRequest, MonitoringPollResult> _poll;
        private readonly Action<TimeSpan, CancellationToken> _wait;

        public MonitoringScheduler(
            MonitoringRuntime runtime,
            Action<TimeSpan, CancellationToken> wait = null)
            : this((runtime ?? throw new ArgumentNullException(nameof(runtime))).PollOnce, wait)
        {
        }

        public MonitoringScheduler(
            Func<MonitoringPollRequest, MonitoringPollResult> poll,
            Action<TimeSpan, CancellationToken> wait = null)
        {
            _poll = poll ?? throw new ArgumentNullException(nameof(poll));

            _wait =
                wait ??
                Wait;
        }

        public MonitoringSchedulerRunResult Run(
            MonitoringPollRequest request,
            TimeSpan interval,
            CancellationToken cancellationToken,
            Action<MonitoringPollResult> onCycleCompleted = null,
            Action onCycleStarting = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interval));
            }

            var completedCycles = 0;
            MonitoringPollResult lastResult = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (onCycleStarting != null)
                {
                    onCycleStarting();
                }

                lastResult =
                    _poll(
                        request);

                completedCycles++;

                if (onCycleCompleted != null)
                {
                    onCycleCompleted(
                        lastResult);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    _wait(
                        interval,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    break;
                }
            }

            return new MonitoringSchedulerRunResult(
                completedCycles,
                cancellationToken.IsCancellationRequested,
                lastResult);
        }

        private static void Wait(
            TimeSpan interval,
            CancellationToken cancellationToken)
        {
            Task.Delay(
                    interval,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
    }
}
