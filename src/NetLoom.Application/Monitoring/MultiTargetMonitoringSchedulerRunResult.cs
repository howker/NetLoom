using System;

namespace NetLoom.Application.Monitoring
{
    public sealed class MultiTargetMonitoringSchedulerRunResult
    {
        public MultiTargetMonitoringSchedulerRunResult(
            int completedPolls,
            int backpressureSkips,
            bool cancellationRequested)
        {
            if (completedPolls < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedPolls));
            }

            if (backpressureSkips < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(backpressureSkips));
            }

            CompletedPolls =
                completedPolls;

            BackpressureSkips =
                backpressureSkips;

            CancellationRequested =
                cancellationRequested;
        }

        public int CompletedPolls { get; }

        public int BackpressureSkips { get; }

        public bool CancellationRequested { get; }
    }
}
