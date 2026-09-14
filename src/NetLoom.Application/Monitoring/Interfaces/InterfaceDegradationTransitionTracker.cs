using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationTransitionTracker
    {
        private readonly IInterfaceDegradationStateStore
            _stateStore;

        public InterfaceDegradationTransitionTracker(
            IInterfaceDegradationStateStore stateStore)
        {
            _stateStore =
                stateStore ??
                throw new ArgumentNullException(
                    nameof(stateStore));
        }

        public InterfaceDegradationTransition Observe(
            InterfaceDegradationClassification classification)
        {
            if (classification == null)
            {
                throw new ArgumentNullException(
                    nameof(classification));
            }

            if (classification.Status ==
                InterfaceDegradationStatus.Indeterminate)
            {
                var previous =
                    _stateStore.Load(
                        classification.DeviceId,
                        classification.IfIndex);

                return new InterfaceDegradationTransition(
                    classification,
                    previous,
                    InterfaceDegradationTransitionKind
                        .Indeterminate);
            }

            var current =
                InterfaceDegradationState
                    .FromClassification(
                        classification);

            var previousState =
                _stateStore
                    .ReplaceAndGetPrevious(
                        current);

            var kind =
                ClassifyTransition(
                    previousState,
                    current);

            return new InterfaceDegradationTransition(
                classification,
                previousState,
                kind);
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
