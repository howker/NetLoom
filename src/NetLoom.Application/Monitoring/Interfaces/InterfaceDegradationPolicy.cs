using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationPolicy
    {
        public InterfaceDegradationPolicy(
            double? errorRatePerMinuteThreshold,
            double? discardRatePerMinuteThreshold)
        {
            if (!errorRatePerMinuteThreshold.HasValue &&
                !discardRatePerMinuteThreshold.HasValue)
            {
                throw new ArgumentException(
                    "At least one interface degradation threshold is required.");
            }

            ValidateThreshold(
                errorRatePerMinuteThreshold,
                nameof(errorRatePerMinuteThreshold));

            ValidateThreshold(
                discardRatePerMinuteThreshold,
                nameof(discardRatePerMinuteThreshold));

            ErrorRatePerMinuteThreshold =
                errorRatePerMinuteThreshold;

            DiscardRatePerMinuteThreshold =
                discardRatePerMinuteThreshold;
        }

        public double? ErrorRatePerMinuteThreshold { get; }

        public double? DiscardRatePerMinuteThreshold { get; }

        private static void ValidateThreshold(
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
                    parameterName,
                    "Interface degradation threshold must be a finite positive rate per minute.");
            }
        }
    }
}
