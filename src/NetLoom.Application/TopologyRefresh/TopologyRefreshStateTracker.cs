using System;

namespace NetLoom.Application.TopologyRefresh
{
    public sealed class TopologyRefreshStateTracker
    {
        private TopologyRefreshStateKind _kind =
            TopologyRefreshStateKind.NeverLoaded;

        private TopologyRefreshSnapshot
            _lastSuccessfulSnapshot;

        private DateTime? _lastSuccessUtc;

        public TopologyRefreshState Current
        {
            get
            {
                return new TopologyRefreshState(
                    _kind,
                    _lastSuccessfulSnapshot,
                    _lastSuccessUtc);
            }
        }

        public TopologyRefreshState ObserveSuccess(
            TopologyRefreshSnapshot snapshot,
            DateTime succeededUtc)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            if (succeededUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Successful refresh time must be UTC.",
                    nameof(succeededUtc));
            }

            _lastSuccessfulSnapshot =
                snapshot;

            _lastSuccessUtc =
                succeededUtc;

            _kind =
                TopologyRefreshStateKind.Current;

            return Current;
        }

        public TopologyRefreshState ObserveFailure()
        {
            _kind =
                _lastSuccessfulSnapshot == null
                    ? TopologyRefreshStateKind.InitialFailure
                    : TopologyRefreshStateKind.Stale;

            return Current;
        }
    }
}
