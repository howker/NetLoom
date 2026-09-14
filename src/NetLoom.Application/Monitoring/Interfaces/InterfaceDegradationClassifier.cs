using System;
using System.Collections.Generic;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationClassifier
    {
        public InterfaceDegradationClassification Classify(
            InterfaceCounterEvaluation evaluation,
            InterfaceDegradationPolicy policy)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(
                    nameof(evaluation));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(
                    nameof(policy));
            }

            switch (evaluation.Delta.Status)
            {
                case InterfaceCounterDeltaStatus.NoBaseline:
                    return Indeterminate(
                        evaluation,
                        InterfaceDegradationReason.NoBaseline);

                case InterfaceCounterDeltaStatus.Discontinuity:
                    return Indeterminate(
                        evaluation,
                        InterfaceDegradationReason
                            .CounterDiscontinuity);

                case InterfaceCounterDeltaStatus.Valid:
                    return ClassifyValid(
                        evaluation,
                        policy);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(evaluation),
                        "Unknown interface counter delta status.");
            }
        }

        private static InterfaceDegradationClassification
            ClassifyValid(
                InterfaceCounterEvaluation evaluation,
                InterfaceDegradationPolicy policy)
        {
            if (!evaluation.Delta.Interval.HasValue ||
                evaluation.Delta.Interval.Value <=
                    TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Valid interface counter delta requires a positive interval.",
                    nameof(evaluation));
            }

            var interval =
                evaluation.Delta.Interval.Value;

            var errorRate =
                RatePerMinute(
                    evaluation.Delta.InErrors,
                    evaluation.Delta.OutErrors,
                    interval);

            var discardRate =
                RatePerMinute(
                    evaluation.Delta.InDiscards,
                    evaluation.Delta.OutDiscards,
                    interval);

            var reasons =
                new List<InterfaceDegradationReason>();

            var incomplete =
                false;

            if (policy.ErrorRatePerMinuteThreshold.HasValue)
            {
                if (!errorRate.HasValue)
                {
                    incomplete = true;
                }
                else if (errorRate.Value >=
                    policy.ErrorRatePerMinuteThreshold.Value)
                {
                    reasons.Add(
                        InterfaceDegradationReason
                            .ErrorRateThresholdExceeded);
                }
            }

            if (policy.DiscardRatePerMinuteThreshold.HasValue)
            {
                if (!discardRate.HasValue)
                {
                    incomplete = true;
                }
                else if (discardRate.Value >=
                    policy.DiscardRatePerMinuteThreshold.Value)
                {
                    reasons.Add(
                        InterfaceDegradationReason
                            .DiscardRateThresholdExceeded);
                }
            }

            if (incomplete)
            {
                reasons.Add(
                    InterfaceDegradationReason
                        .IncompleteCounterData);
            }

            var degraded =
                reasons.Contains(
                    InterfaceDegradationReason
                        .ErrorRateThresholdExceeded) ||
                reasons.Contains(
                    InterfaceDegradationReason
                        .DiscardRateThresholdExceeded);

            var status =
                degraded
                    ? InterfaceDegradationStatus.Degraded
                    : incomplete
                        ? InterfaceDegradationStatus
                            .Indeterminate
                        : InterfaceDegradationStatus.Healthy;

            return new InterfaceDegradationClassification(
                evaluation.DeviceId,
                evaluation.IfIndex,
                evaluation.CapturedUtc,
                status,
                errorRate,
                discardRate,
                reasons);
        }

        private static InterfaceDegradationClassification
            Indeterminate(
                InterfaceCounterEvaluation evaluation,
                InterfaceDegradationReason reason)
        {
            return new InterfaceDegradationClassification(
                evaluation.DeviceId,
                evaluation.IfIndex,
                evaluation.CapturedUtc,
                InterfaceDegradationStatus.Indeterminate,
                null,
                null,
                new[]
                {
                    reason
                });
        }

        private static double? RatePerMinute(
            ulong? inbound,
            ulong? outbound,
            TimeSpan interval)
        {
            if (!inbound.HasValue ||
                !outbound.HasValue)
            {
                return null;
            }

            var total =
                (double)inbound.Value +
                outbound.Value;

            return
                total *
                TimeSpan.FromMinutes(1).Ticks /
                interval.Ticks;
        }
    }
}
