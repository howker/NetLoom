using System;

namespace NetLoom.Application.TopologyRefresh
{
    public enum TopologyRefreshStateKind
    {
        NeverLoaded = 0,
        Current = 1,
        Stale = 2,
        InitialFailure = 3
    }

    public sealed class TopologyRefreshState
    {
        internal TopologyRefreshState(
            TopologyRefreshStateKind kind,
            TopologyRefreshSnapshot snapshot,
            DateTime? lastSuccessUtc)
        {
            Kind = kind;
            Snapshot = snapshot;
            LastSuccessUtc = lastSuccessUtc;
        }

        public TopologyRefreshStateKind Kind { get; }

        public TopologyRefreshSnapshot Snapshot { get; }

        public DateTime? LastSuccessUtc { get; }
    }
}
