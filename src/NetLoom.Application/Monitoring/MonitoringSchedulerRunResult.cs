using System;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringSchedulerRunResult
    {
        public MonitoringSchedulerRunResult(
            int completedCycles,
            bool cancellationRequested,
            MonitoringPollResult lastResult)
        {
            if (completedCycles < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedCycles));
            }

            if (completedCycles == 0 &&
                lastResult != null)
            {
                throw new ArgumentException(
                    "A zero-cycle scheduler result cannot contain a last result.",
                    nameof(lastResult));
            }

            if (completedCycles > 0 &&
                lastResult == null)
            {
                throw new ArgumentNullException(
                    nameof(lastResult));
            }

            CompletedCycles = completedCycles;
            CancellationRequested = cancellationRequested;
            LastResult = lastResult;
        }

        public int CompletedCycles { get; }

        public bool CancellationRequested { get; }

        public MonitoringPollResult LastResult { get; }
    }
}
