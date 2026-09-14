using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceDegradationTransition
    {
        internal InterfaceDegradationTransition(
            InterfaceDegradationClassification classification,
            InterfaceDegradationState previousState,
            InterfaceDegradationTransitionKind kind)
        {
            Classification =
                classification ??
                throw new ArgumentNullException(
                    nameof(classification));

            PreviousState =
                previousState;

            Kind = kind;
        }

        public InterfaceDegradationClassification
            Classification { get; }

        public InterfaceDegradationState
            PreviousState { get; }

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
