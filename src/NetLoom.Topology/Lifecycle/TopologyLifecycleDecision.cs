using System;

namespace NetLoom.Topology.Lifecycle
{
    public sealed class TopologyLifecycleDecision
    {
        public TopologyLifecycleDecision(
            TopologyLifecycleState state)
        {
            State = state ??
                throw new ArgumentNullException(nameof(state));
        }

        public TopologyLifecycleState State { get; }

        public bool ShouldDelete
        {
            get { return false; }
        }
    }
}
