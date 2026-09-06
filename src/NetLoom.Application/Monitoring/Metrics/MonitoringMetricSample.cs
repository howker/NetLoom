using System;

namespace NetLoom.Application.Monitoring.Metrics
{
    public sealed class MonitoringMetricSample
    {
        public MonitoringMetricSample(
            Guid deviceId,
            Guid? interfaceId,
            MonitoringMetricKind kind,
            DateTime capturedUtc,
            double value)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Metric sample requires a stable device id.",
                    nameof(deviceId));
            }

            if (interfaceId.HasValue &&
                interfaceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Metric sample interface id cannot be empty.",
                    nameof(interfaceId));
            }

            if (capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Metric sample timestamp must be UTC.",
                    nameof(capturedUtc));
            }

            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value));
            }

            DeviceId = deviceId;
            InterfaceId = interfaceId;
            Kind = kind;
            CapturedUtc = capturedUtc;
            Value = value;
        }

        public Guid DeviceId { get; }

        public Guid? InterfaceId { get; }

        public MonitoringMetricKind Kind { get; }

        public DateTime CapturedUtc { get; }

        public double Value { get; }
    }
}
