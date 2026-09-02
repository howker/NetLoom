using System;

namespace NetLoom.Topology.Lifecycle
{
    public sealed class TopologyLifecyclePolicy
    {
        private readonly TimeSpan _agingAfter;
        private readonly TimeSpan _staleAfter;

        public TopologyLifecyclePolicy(
            TimeSpan agingAfter,
            TimeSpan staleAfter)
        {
            if (agingAfter <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(agingAfter));
            }

            if (staleAfter <= agingAfter)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(staleAfter));
            }

            _agingAfter = agingAfter;
            _staleAfter = staleAfter;
        }

        public TopologyLifecycleDecision CreateDiscovered(
            string subjectKey,
            DateTime observedUtc,
            DateTime nowUtc)
        {
            RequireUtc(
                observedUtc,
                nameof(observedUtc));

            RequireUtc(
                nowUtc,
                nameof(nowUtc));

            if (nowUtc < observedUtc)
            {
                throw new ArgumentException(
                    "Current time cannot be before observation.",
                    nameof(nowUtc));
            }

            return Keep(
                new TopologyLifecycleState(
                    subjectKey,
                    TopologyLifecycleOrigin.Discovered,
                    observedUtc,
                    observedUtc,
                    Evaluate(
                        observedUtc,
                        nowUtc)));
        }

        public TopologyLifecycleDecision CreateManual(
            string subjectKey,
            DateTime createdUtc)
        {
            RequireUtc(
                createdUtc,
                nameof(createdUtc));

            return Keep(
                new TopologyLifecycleState(
                    subjectKey,
                    TopologyLifecycleOrigin.Manual,
                    createdUtc,
                    createdUtc,
                    TopologyFreshness.Fresh));
        }

        public TopologyLifecycleDecision RecordEvidence(
            TopologyLifecycleState current,
            DateTime observedUtc,
            DateTime nowUtc)
        {
            RequireState(current);

            RequireUtc(
                observedUtc,
                nameof(observedUtc));

            RequireUtc(
                nowUtc,
                nameof(nowUtc));

            if (current.Origin ==
                TopologyLifecycleOrigin.Manual)
            {
                return Keep(current);
            }

            var lastSeenUtc =
                observedUtc > current.LastSeenUtc
                    ? observedUtc
                    : current.LastSeenUtc;

            if (nowUtc < lastSeenUtc)
            {
                throw new ArgumentException(
                    "Current time cannot be before last seen.",
                    nameof(nowUtc));
            }

            return Keep(
                new TopologyLifecycleState(
                    current.SubjectKey,
                    current.Origin,
                    current.FirstSeenUtc,
                    lastSeenUtc,
                    Evaluate(
                        lastSeenUtc,
                        nowUtc)));
        }

        public TopologyLifecycleDecision RecordNoEvidence(
            TopologyLifecycleState current,
            DateTime nowUtc)
        {
            return AdvanceWithoutObservation(
                current,
                nowUtc);
        }

        public TopologyLifecycleDecision RecordPollFailure(
            TopologyLifecycleState current,
            DateTime nowUtc)
        {
            return AdvanceWithoutObservation(
                current,
                nowUtc);
        }

        private TopologyLifecycleDecision
            AdvanceWithoutObservation(
                TopologyLifecycleState current,
                DateTime nowUtc)
        {
            RequireState(current);

            RequireUtc(
                nowUtc,
                nameof(nowUtc));

            if (nowUtc < current.LastSeenUtc)
            {
                throw new ArgumentException(
                    "Current time cannot be before last seen.",
                    nameof(nowUtc));
            }

            if (current.Origin ==
                TopologyLifecycleOrigin.Manual)
            {
                return Keep(current);
            }

            return Keep(
                new TopologyLifecycleState(
                    current.SubjectKey,
                    current.Origin,
                    current.FirstSeenUtc,
                    current.LastSeenUtc,
                    Evaluate(
                        current.LastSeenUtc,
                        nowUtc)));
        }

        private TopologyFreshness Evaluate(
            DateTime lastSeenUtc,
            DateTime nowUtc)
        {
            var age =
                nowUtc - lastSeenUtc;

            if (age >= _staleAfter)
            {
                return TopologyFreshness.Stale;
            }

            if (age >= _agingAfter)
            {
                return TopologyFreshness.Aging;
            }

            return TopologyFreshness.Fresh;
        }

        private static TopologyLifecycleDecision Keep(
            TopologyLifecycleState state)
        {
            return new TopologyLifecycleDecision(state);
        }

        private static void RequireState(
            TopologyLifecycleState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
        }

        private static void RequireUtc(
            DateTime value,
            string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    parameterName);
            }
        }
    }
}
