using System;
using System.Net;

namespace NetLoom.Application.Monitoring.Health
{
    public sealed class HealthSnapshot
    {
        public HealthSnapshot(
            Guid? deviceId,
            IPAddress sourceAddress,
            DateTime capturedUtc,
            HealthStatus status,
            TimeSpan? uptime)
        {
            if (deviceId.HasValue &&
                deviceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Health device id cannot be empty.",
                    nameof(deviceId));
            }

            SourceAddress =
                sourceAddress ??
                throw new ArgumentNullException(
                    nameof(sourceAddress));

            if (capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Health timestamp must be UTC.",
                    nameof(capturedUtc));
            }

            if (uptime.HasValue &&
                uptime.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(uptime));
            }

            DeviceId = deviceId;
            CapturedUtc = capturedUtc;
            Status = status;
            Uptime = uptime;
        }

        public Guid? DeviceId { get; }

        public IPAddress SourceAddress { get; }

        public DateTime CapturedUtc { get; }

        public HealthStatus Status { get; }

        public TimeSpan? Uptime { get; }
    }
}
