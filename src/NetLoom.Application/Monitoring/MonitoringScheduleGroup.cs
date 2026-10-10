using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringScheduleGroup
    {
        public MonitoringScheduleGroup(IEnumerable<MonitoringPollKind> kinds, TimeSpan cadence, bool pollsOnce)
        {
            if (kinds == null) throw new ArgumentNullException(nameof(kinds));
            var selected = kinds.ToArray();
            if (selected.Length == 0 || selected.Distinct().Count() != selected.Length)
                throw new ArgumentException("Group kinds must be nonempty and unique.", nameof(kinds));
            if (cadence <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cadence));
            Kinds = Array.AsReadOnly(selected);
            Cadence = cadence;
            PollsOnce = pollsOnce;
        }

        public IReadOnlyList<MonitoringPollKind> Kinds { get; }
        public TimeSpan Cadence { get; }
        public bool PollsOnce { get; }
    }
}
