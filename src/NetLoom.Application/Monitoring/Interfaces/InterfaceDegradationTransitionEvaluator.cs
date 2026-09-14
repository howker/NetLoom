using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationTransitionEvaluator
    {
        public InterfaceDegradationTransition Evaluate(
            InterfaceDegradationClassification classification,
            InterfaceDegradationState previousState)
        {
            if (classification == null)
            {
                throw new ArgumentNullException(
                    nameof(classification));
            }

            if (previousState != null &&
                (previousState.DeviceId != classification.DeviceId ||
                 previousState.IfIndex != classification.IfIndex))
            {
                throw new ArgumentException(
                    "Previous interface degradation state identity does not match the classification.",
                    nameof(previousState));
            }

            if (classification.Status ==
                InterfaceDegradationStatus.Indeterminate)
            {
                return new InterfaceDegradationTransition(
                    classification,
                    previousState,
                    null,
                    InterfaceDegradationTransitionKind
                        .Indeterminate);
            }

            var currentState =
                InterfaceDegradationState
                    .FromClassification(
                        classification);

            return new InterfaceDegradationTransition(
                classification,
                previousState,
                currentState,
                ClassifyTransition(
                    previousState,
                    currentState));
        }

        private static InterfaceDegradationTransitionKind
            ClassifyTransition(
                InterfaceDegradationState previous,
                InterfaceDegradationState current)
        {
            if (previous == null)
            {
                return current.Status ==
                    InterfaceDegradationStatus.Degraded
                    ? InterfaceDegradationTransitionKind
                        .FirstAppearance
                    : InterfaceDegradationTransitionKind
                        .Unchanged;
            }

            if (previous.Status == current.Status)
            {
                if (current.Status ==
                        InterfaceDegradationStatus.Degraded &&
                    !string.Equals(
                        previous.EvidenceFingerprint,
                        current.EvidenceFingerprint,
                        StringComparison.Ordinal))
                {
                    return
                        InterfaceDegradationTransitionKind
                            .Changed;
                }

                return
                    InterfaceDegradationTransitionKind
                        .Unchanged;
            }

            if (previous.Status ==
                    InterfaceDegradationStatus.Degraded &&
                current.Status ==
                    InterfaceDegradationStatus.Healthy)
            {
                return
                    InterfaceDegradationTransitionKind
                        .Resolved;
            }

            if (previous.Status ==
                    InterfaceDegradationStatus.Healthy &&
                current.Status ==
                    InterfaceDegradationStatus.Degraded)
            {
                return
                    InterfaceDegradationTransitionKind
                        .FirstAppearance;
            }

            throw new InvalidOperationException(
                "Unsupported interface degradation state transition.");
        }
    }
}
