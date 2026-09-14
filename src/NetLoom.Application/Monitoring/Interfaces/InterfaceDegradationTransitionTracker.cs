using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationTransitionTracker :
        IInterfaceDegradationTransitionProcessor
    {
        private readonly IInterfaceDegradationStateStore
            _stateStore;

        private readonly InterfaceDegradationTransitionEvaluator
            _evaluator;

        public InterfaceDegradationTransitionTracker(
            IInterfaceDegradationStateStore stateStore)
        {
            _stateStore =
                stateStore ??
                throw new ArgumentNullException(
                    nameof(stateStore));

            _evaluator =
                new InterfaceDegradationTransitionEvaluator();
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

                return _evaluator.Evaluate(
                    classification,
                    previous);
            }

            var current =
                InterfaceDegradationState
                    .FromClassification(
                        classification);

            var previousState =
                _stateStore
                    .ReplaceAndGetPrevious(
                        current);

            return _evaluator.Evaluate(
                classification,
                previousState);
        }
    }
}
