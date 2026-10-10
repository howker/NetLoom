using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Monitoring;

namespace NetLoom.Application.PollingPolicies
{
    public static class PollingPolicySchedule
    {
        public static IReadOnlyList<MonitoringScheduleGroup> BuildGroups(PollingPolicy policy, TimeSpan generalInterval,
            IReadOnlyList<MonitoringPollKind> enabledKinds)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (generalInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(generalInterval));
            if (enabledKinds == null) throw new ArgumentNullException(nameof(enabledKinds));
            if (!policy.ActivePolling) return Array.Empty<MonitoringScheduleGroup>();

            var schedules = new[] { policy.StateSchedule, policy.TopologySchedule };
            var categories = new[] { PollingPolicy.StateKinds, PollingPolicy.TopologyKinds };
            var entries = new List<Tuple<TimeSpan, bool, MonitoringPollKind>>();
            for (var category = 0; category < schedules.Length; category++)
            {
                var schedule = schedules[category];
                if (schedule.Mode == PollingScheduleMode.Off) continue;
                var cadence = schedule.Mode == PollingScheduleMode.Interval
                    ? TimeSpan.FromSeconds(schedule.IntervalSeconds.Value) : generalInterval;
                foreach (var kind in enabledKinds.Where(kind => categories[category].Contains(kind)))
                    entries.Add(Tuple.Create(cadence, schedule.Mode == PollingScheduleMode.Once, kind));
            }

            return entries.GroupBy(entry => Tuple.Create(entry.Item1, entry.Item2))
                .Select(group => new MonitoringScheduleGroup(
                    enabledKinds.Where(kind => group.Any(entry => entry.Item3 == kind)).Distinct(),
                    group.Key.Item1, group.Key.Item2))
                .ToArray();
        }
    }
}
