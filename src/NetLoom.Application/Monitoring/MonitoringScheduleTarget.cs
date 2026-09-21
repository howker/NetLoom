using System;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringScheduleTarget
    {
        public MonitoringScheduleTarget(
            MonitoringPollRequest request,
            TimeSpan cadence,
            TimeSpan initialDelay)
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

            if (cadence <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cadence));
            }

            if (initialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialDelay));
            }

            Cadence = cadence;
            InitialDelay = initialDelay;
        }

        public Guid DeviceId =>
            Request.DeviceId.Value;

        public MonitoringPollRequest Request { get; }

        public TimeSpan Cadence { get; }

        public TimeSpan InitialDelay { get; }
    }
}
