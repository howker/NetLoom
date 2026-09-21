using System;

namespace NetLoom.Application.MonitoringControl
{
    public sealed class MonitoringTargetSetPolicy
    {
        public MonitoringTargetSetPolicy(
            int maxConcurrentPolls,
            TimeSpan startupJitter)
        {
            if (maxConcurrentPolls < 1 ||
                maxConcurrentPolls > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentPolls));
            }

            if (startupJitter < TimeSpan.Zero ||
                startupJitter.TotalSeconds > int.MaxValue ||
                startupJitter.Ticks %
                    TimeSpan.TicksPerSecond != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startupJitter));
            }

            MaxConcurrentPolls =
                maxConcurrentPolls;
            StartupJitter =
                startupJitter;
        }

        public int MaxConcurrentPolls { get; }

        public TimeSpan StartupJitter { get; }
    }
}
