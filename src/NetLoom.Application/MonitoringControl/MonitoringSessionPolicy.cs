using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Monitoring;
using NetLoom.Domain.Access;

namespace NetLoom.Application.MonitoringControl
{
    public sealed class MonitoringSessionPolicy
    {
        public MonitoringSessionPolicy(
            TimeSpan interval,
            SnmpVersion version,
            int port,
            int timeoutMilliseconds,
            int retryCount,
            int maxRepetitions,
            IEnumerable<MonitoringPollKind> kinds,
            double? interfaceErrorRatePerMinuteThreshold = null,
            double? interfaceDiscardRatePerMinuteThreshold = null)
        {
            if (interval < TimeSpan.FromSeconds(1) ||
                interval.TotalSeconds > int.MaxValue ||
                interval.Ticks % TimeSpan.TicksPerSecond != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interval));
            }

            if (!Enum.IsDefined(
                typeof(SnmpVersion),
                version))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(version));
            }

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port));
            }

            if (timeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            }

            if (retryCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(retryCount));
            }

            if (maxRepetitions < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRepetitions));
            }

            var kindArray =
                (kinds ??
                    throw new ArgumentNullException(
                        nameof(kinds)))
                .Distinct()
                .ToArray();

            if (kindArray.Length == 0)
            {
                throw new ArgumentException(
                    "POLL_KIND_REQUIRED",
                    nameof(kinds));
            }

            if (kindArray.Any(
                kind => !Enum.IsDefined(
                    typeof(MonitoringPollKind),
                    kind)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kinds));
            }

            ValidateOptionalPositiveFinite(
                interfaceErrorRatePerMinuteThreshold,
                nameof(interfaceErrorRatePerMinuteThreshold));

            ValidateOptionalPositiveFinite(
                interfaceDiscardRatePerMinuteThreshold,
                nameof(interfaceDiscardRatePerMinuteThreshold));

            Interval = interval;
            Version = version;
            Port = port;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
            MaxRepetitions = maxRepetitions;
            Kinds = kindArray;
            InterfaceErrorRatePerMinuteThreshold =
                interfaceErrorRatePerMinuteThreshold;
            InterfaceDiscardRatePerMinuteThreshold =
                interfaceDiscardRatePerMinuteThreshold;
        }

        public TimeSpan Interval { get; }

        public SnmpVersion Version { get; }

        public int Port { get; }

        public int TimeoutMilliseconds { get; }

        public int RetryCount { get; }

        public int MaxRepetitions { get; }

        public IReadOnlyList<MonitoringPollKind> Kinds { get; }

        public double? InterfaceErrorRatePerMinuteThreshold { get; }

        public double? InterfaceDiscardRatePerMinuteThreshold { get; }

        private static void ValidateOptionalPositiveFinite(
            double? value,
            string parameterName)
        {
            if (!value.HasValue)
            {
                return;
            }

            if (double.IsNaN(value.Value) ||
                double.IsInfinity(value.Value) ||
                value.Value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
        }
    }
}
