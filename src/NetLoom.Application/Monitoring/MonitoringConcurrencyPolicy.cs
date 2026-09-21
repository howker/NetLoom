using System;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringConcurrencyPolicy
    {
        public MonitoringConcurrencyPolicy(
            int maxConcurrentPolls)
        {
            if (maxConcurrentPolls < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentPolls));
            }

            MaxConcurrentPolls =
                maxConcurrentPolls;
        }

        public int MaxConcurrentPolls { get; }
    }
}
