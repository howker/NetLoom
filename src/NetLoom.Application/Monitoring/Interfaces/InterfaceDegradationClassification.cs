using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationClassification
    {
        internal InterfaceDegradationClassification(
            Guid deviceId,
            int ifIndex,
            DateTime capturedUtc,
            InterfaceDegradationStatus status,
            double? errorRatePerMinute,
            double? discardRatePerMinute,
            IEnumerable<InterfaceDegradationReason> reasons)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface degradation classification requires a stable DeviceId.",
                    nameof(deviceId));
            }

            if (ifIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation timestamp must be UTC.",
                    nameof(capturedUtc));
            }

            ValidateRate(
                errorRatePerMinute,
                nameof(errorRatePerMinute));

            ValidateRate(
                discardRatePerMinute,
                nameof(discardRatePerMinute));

            if (reasons == null)
            {
                throw new ArgumentNullException(
                    nameof(reasons));
            }

            var normalizedReasons =
                reasons
                    .Distinct()
                    .OrderBy(
                        reason => (int)reason)
                    .ToArray();

            if (normalizedReasons.Any(
                reason =>
                    reason ==
                    InterfaceDegradationReason.Unknown))
            {
                throw new ArgumentException(
                    "Interface degradation reasons must be known.",
                    nameof(reasons));
            }

            var hasThresholdReason =
                normalizedReasons.Any(
                    reason =>
                        reason ==
                            InterfaceDegradationReason
                                .ErrorRateThresholdExceeded ||
                        reason ==
                            InterfaceDegradationReason
                                .DiscardRateThresholdExceeded);

            if (status ==
                    InterfaceDegradationStatus.Healthy &&
                normalizedReasons.Length > 0)
            {
                throw new ArgumentException(
                    "Healthy interface degradation classification cannot contain reasons.",
                    nameof(reasons));
            }

            if (status ==
                    InterfaceDegradationStatus.Degraded &&
                !hasThresholdReason)
            {
                throw new ArgumentException(
                    "Degraded interface classification requires a threshold reason.",
                    nameof(reasons));
            }

            if (status !=
                    InterfaceDegradationStatus.Degraded &&
                hasThresholdReason)
            {
                throw new ArgumentException(
                    "Threshold reasons require Degraded status.",
                    nameof(reasons));
            }

            if (status ==
                    InterfaceDegradationStatus.Indeterminate &&
                normalizedReasons.Length == 0)
            {
                throw new ArgumentException(
                    "Indeterminate interface classification requires a reason.",
                    nameof(reasons));
            }

            DeviceId = deviceId;
            IfIndex = ifIndex;
            CapturedUtc = capturedUtc;
            Status = status;
            ErrorRatePerMinute = errorRatePerMinute;
            DiscardRatePerMinute = discardRatePerMinute;
            Reasons = normalizedReasons;
        }

        public Guid DeviceId { get; }

        public int IfIndex { get; }

        public DateTime CapturedUtc { get; }

        public InterfaceDegradationStatus Status { get; }

        public double? ErrorRatePerMinute { get; }

        public double? DiscardRatePerMinute { get; }

        public IReadOnlyList<InterfaceDegradationReason>
            Reasons { get; }

        private static void ValidateRate(
            double? value,
            string parameterName)
        {
            if (!value.HasValue)
            {
                return;
            }

            if (double.IsNaN(value.Value) ||
                double.IsInfinity(value.Value) ||
                value.Value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
        }
    }
}
