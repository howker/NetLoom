using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationOutboxEvent
    {
        public InterfaceDegradationOutboxEvent(
            Guid deviceId,
            int ifIndex,
            DateTime capturedUtc,
            InterfaceDegradationTransitionKind transitionKind,
            InterfaceDegradationStatus? previousStatus,
            string previousEvidenceFingerprint,
            InterfaceDegradationStatus currentStatus,
            string currentEvidenceFingerprint,
            double? errorRatePerMinute,
            double? discardRatePerMinute,
            IEnumerable<InterfaceDegradationReason> reasons)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface degradation outbox event requires a stable DeviceId.",
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
                    "Interface degradation outbox timestamp must be UTC.",
                    nameof(capturedUtc));
            }

            if (transitionKind !=
                    InterfaceDegradationTransitionKind.FirstAppearance &&
                transitionKind !=
                    InterfaceDegradationTransitionKind.Changed &&
                transitionKind !=
                    InterfaceDegradationTransitionKind.Resolved)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transitionKind));
            }

            ValidateStatus(
                previousStatus,
                nameof(previousStatus));

            if (currentStatus !=
                    InterfaceDegradationStatus.Healthy &&
                currentStatus !=
                    InterfaceDegradationStatus.Degraded)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentStatus));
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

            var previousFingerprint =
                Normalize(
                    previousEvidenceFingerprint);

            var currentFingerprint =
                Normalize(
                    currentEvidenceFingerprint);

            ValidateFingerprint(
                previousStatus,
                previousFingerprint,
                "previousEvidenceFingerprint");

            ValidateFingerprint(
                currentStatus,
                currentFingerprint,
                "currentEvidenceFingerprint");

            ValidateTransition(
                transitionKind,
                previousStatus,
                previousFingerprint,
                currentStatus,
                currentFingerprint);

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
                    "Interface degradation outbox reasons must be known.",
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

            if (currentStatus ==
                    InterfaceDegradationStatus.Healthy &&
                normalizedReasons.Length > 0)
            {
                throw new ArgumentException(
                    "Resolved interface degradation event cannot contain current degradation reasons.",
                    nameof(reasons));
            }

            if (currentStatus ==
                    InterfaceDegradationStatus.Degraded &&
                !hasThresholdReason)
            {
                throw new ArgumentException(
                    "Degraded interface outbox event requires a threshold reason.",
                    nameof(reasons));
            }

            DeviceId = deviceId;
            IfIndex = ifIndex;
            CapturedUtc = capturedUtc;
            TransitionKind = transitionKind;
            PreviousStatus = previousStatus;
            PreviousEvidenceFingerprint =
                previousFingerprint;
            CurrentStatus = currentStatus;
            CurrentEvidenceFingerprint =
                currentFingerprint;
            ErrorRatePerMinute = errorRatePerMinute;
            DiscardRatePerMinute = discardRatePerMinute;
            Reasons = normalizedReasons;

            EventKey =
                BuildEventKey(
                    deviceId,
                    ifIndex,
                    capturedUtc,
                    transitionKind,
                    previousStatus,
                    previousFingerprint,
                    currentStatus,
                    currentFingerprint);
        }

        public string EventKey { get; }

        public Guid DeviceId { get; }

        public int IfIndex { get; }

        public DateTime CapturedUtc { get; }

        public InterfaceDegradationTransitionKind
            TransitionKind { get; }

        public InterfaceDegradationStatus?
            PreviousStatus { get; }

        public string PreviousEvidenceFingerprint { get; }

        public InterfaceDegradationStatus CurrentStatus { get; }

        public string CurrentEvidenceFingerprint { get; }

        public double? ErrorRatePerMinute { get; }

        public double? DiscardRatePerMinute { get; }

        public IReadOnlyList<InterfaceDegradationReason>
            Reasons { get; }

        public static InterfaceDegradationOutboxEvent
            FromTransition(
                InterfaceDegradationTransition transition)
        {
            if (transition == null)
            {
                throw new ArgumentNullException(
                    nameof(transition));
            }

            if (!transition.HasStateChange ||
                transition.CurrentState == null)
            {
                throw new ArgumentException(
                    "Only state-changing determinate transitions can become outbox events.",
                    nameof(transition));
            }

            return new InterfaceDegradationOutboxEvent(
                transition.Classification.DeviceId,
                transition.Classification.IfIndex,
                transition.Classification.CapturedUtc,
                transition.Kind,
                transition.PreviousState == null
                    ? (InterfaceDegradationStatus?)null
                    : transition.PreviousState.Status,
                transition.PreviousState == null
                    ? string.Empty
                    : transition.PreviousState
                        .EvidenceFingerprint,
                transition.CurrentState.Status,
                transition.CurrentState
                    .EvidenceFingerprint,
                transition.Classification
                    .ErrorRatePerMinute,
                transition.Classification
                    .DiscardRatePerMinute,
                transition.Classification
                    .Reasons);
        }

        private static void ValidateTransition(
            InterfaceDegradationTransitionKind transitionKind,
            InterfaceDegradationStatus? previousStatus,
            string previousFingerprint,
            InterfaceDegradationStatus currentStatus,
            string currentFingerprint)
        {
            switch (transitionKind)
            {
                case InterfaceDegradationTransitionKind
                    .FirstAppearance:
                    if (currentStatus !=
                            InterfaceDegradationStatus.Degraded ||
                        (previousStatus.HasValue &&
                         previousStatus.Value !=
                            InterfaceDegradationStatus.Healthy))
                    {
                        throw new ArgumentException(
                            "FirstAppearance outbox event requires a Degraded current state and no previous state or Healthy previous state.");
                    }

                    break;

                case InterfaceDegradationTransitionKind.Changed:
                    if (previousStatus !=
                            InterfaceDegradationStatus.Degraded ||
                        currentStatus !=
                            InterfaceDegradationStatus.Degraded ||
                        string.Equals(
                            previousFingerprint,
                            currentFingerprint,
                            StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "Changed outbox event requires different previous and current Degraded evidence.");
                    }

                    break;

                case InterfaceDegradationTransitionKind.Resolved:
                    if (previousStatus !=
                            InterfaceDegradationStatus.Degraded ||
                        currentStatus !=
                            InterfaceDegradationStatus.Healthy)
                    {
                        throw new ArgumentException(
                            "Resolved outbox event requires Degraded previous state and Healthy current state.");
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(transitionKind));
            }
        }

        private static void ValidateStatus(
            InterfaceDegradationStatus? status,
            string parameterName)
        {
            if (!status.HasValue)
            {
                return;
            }

            if (status.Value !=
                    InterfaceDegradationStatus.Healthy &&
                status.Value !=
                    InterfaceDegradationStatus.Degraded)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
        }

        private static void ValidateFingerprint(
            InterfaceDegradationStatus? status,
            string fingerprint,
            string parameterName)
        {
            if (!status.HasValue)
            {
                if (fingerprint.Length > 0)
                {
                    throw new ArgumentException(
                        "Missing previous state cannot contain an evidence fingerprint.",
                        parameterName);
                }

                return;
            }

            if (status.Value ==
                    InterfaceDegradationStatus.Healthy &&
                fingerprint.Length > 0)
            {
                throw new ArgumentException(
                    "Healthy interface degradation state cannot contain an evidence fingerprint.",
                    parameterName);
            }

            if (status.Value ==
                    InterfaceDegradationStatus.Degraded &&
                fingerprint.Length == 0)
            {
                throw new ArgumentException(
                    "Degraded interface degradation state requires an evidence fingerprint.",
                    parameterName);
            }
        }

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

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static string BuildEventKey(
            Guid deviceId,
            int ifIndex,
            DateTime capturedUtc,
            InterfaceDegradationTransitionKind transitionKind,
            InterfaceDegradationStatus? previousStatus,
            string previousFingerprint,
            InterfaceDegradationStatus currentStatus,
            string currentFingerprint)
        {
            return string.Join(
                "|",
                new[]
                {
                    "interface-degradation",
                    deviceId.ToString("D"),
                    ifIndex.ToString(
                        CultureInfo.InvariantCulture),
                    capturedUtc.Ticks.ToString(
                        CultureInfo.InvariantCulture),
                    ((int)transitionKind).ToString(
                        CultureInfo.InvariantCulture),
                    previousStatus.HasValue
                        ? ((int)previousStatus.Value)
                            .ToString(
                                CultureInfo.InvariantCulture)
                        : "none",
                    previousFingerprint,
                    ((int)currentStatus).ToString(
                        CultureInfo.InvariantCulture),
                    currentFingerprint
                });
        }
    }
}
