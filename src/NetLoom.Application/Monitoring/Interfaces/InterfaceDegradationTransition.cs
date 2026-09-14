using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationTransition
    {
        internal InterfaceDegradationTransition(
            InterfaceDegradationClassification classification,
            InterfaceDegradationState previousState,
            InterfaceDegradationState currentState,
            InterfaceDegradationTransitionKind kind)
        {
            Classification =
                classification ??
                throw new ArgumentNullException(
                    nameof(classification));

            PreviousState =
                previousState;

            CurrentState =
                currentState;

            Kind = kind;
        }

        public InterfaceDegradationClassification
            Classification { get; }

        public InterfaceDegradationState
            PreviousState { get; }

        public InterfaceDegradationState
            CurrentState { get; }

        public InterfaceDegradationTransitionKind
            Kind { get; }

        public bool HasStateChange
        {
            get
            {
                return
                    Kind ==
                        InterfaceDegradationTransitionKind
                            .FirstAppearance ||
                    Kind ==
                        InterfaceDegradationTransitionKind
                            .Changed ||
                    Kind ==
                        InterfaceDegradationTransitionKind
                            .Resolved;
            }
        }
    }
}
