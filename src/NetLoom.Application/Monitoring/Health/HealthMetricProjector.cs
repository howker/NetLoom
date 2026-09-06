using System;
using System.Collections.Generic;
using NetLoom.Application.Monitoring.Metrics;

namespace NetLoom.Application.Monitoring.Health
{
    public sealed class HealthMetricProjector
    {
        public IReadOnlyList<MonitoringMetricSample> Project(
            HealthSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            if (!snapshot.DeviceId.HasValue)
            {
                return Array.Empty<MonitoringMetricSample>();
            }

            var samples =
                new List<MonitoringMetricSample>();

            if (snapshot.Status == HealthStatus.Up)
            {
                samples.Add(
                    new MonitoringMetricSample(
                        snapshot.DeviceId.Value,
                        null,
                        MonitoringMetricKind.HealthAvailability,
                        snapshot.CapturedUtc,
                        1.0));
            }
            else if (snapshot.Status == HealthStatus.Down)
            {
                samples.Add(
                    new MonitoringMetricSample(
                        snapshot.DeviceId.Value,
                        null,
                        MonitoringMetricKind.HealthAvailability,
                        snapshot.CapturedUtc,
                        0.0));
            }

            if (snapshot.Uptime.HasValue)
            {
                samples.Add(
                    new MonitoringMetricSample(
                        snapshot.DeviceId.Value,
                        null,
                        MonitoringMetricKind.HealthUptimeSeconds,
                        snapshot.CapturedUtc,
                        snapshot.Uptime.Value.TotalSeconds));
            }

            return samples;
        }
    }
}
