using System;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringScheduleTarget
    {
        public MonitoringScheduleTarget(MonitoringPollRequest request, TimeSpan cadence, TimeSpan initialDelay)
            : this(request, new[] { new MonitoringScheduleGroup(request?.Kinds ?? throw new ArgumentNullException(nameof(request)), cadence, false) }, initialDelay)
        {
        }

        public MonitoringScheduleTarget(MonitoringPollRequest request, System.Collections.Generic.IEnumerable<MonitoringScheduleGroup> groups, TimeSpan initialDelay)
        {
            Request = request ??
                throw new ArgumentNullException(
                    nameof(request));

            if (!request.DeviceId.HasValue)
            {
                throw new ArgumentException(
                    "A multi-target schedule requires stable DeviceId.",
                    nameof(request));
            }

            if (initialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialDelay));
            }

            if (groups == null) throw new ArgumentNullException(nameof(groups));
            var selected = System.Linq.Enumerable.ToArray(groups);
            if (selected.Length == 0 || System.Linq.Enumerable.Any(selected, group => group == null))
                throw new ArgumentException("At least one group is required.", nameof(groups));
            var seen = new System.Collections.Generic.HashSet<MonitoringPollKind>();
            foreach (var group in selected)
                foreach (var kind in group.Kinds)
                    if (!System.Linq.Enumerable.Contains(request.Kinds, kind) || !seen.Add(kind))
                        throw new ArgumentException("Group kinds must be unique and enabled in the request.", nameof(groups));
            Groups = Array.AsReadOnly(selected);
            Cadence = selected[0].Cadence;
            foreach (var group in selected)
                if (group.Cadence < Cadence) Cadence = group.Cadence;
            InitialDelay = initialDelay;
        }

        public Guid DeviceId =>
            Request.DeviceId.Value;

        public MonitoringPollRequest Request { get; }

        public TimeSpan Cadence { get; }

        public System.Collections.Generic.IReadOnlyList<MonitoringScheduleGroup> Groups { get; }

        public TimeSpan InitialDelay { get; }
    }
}
