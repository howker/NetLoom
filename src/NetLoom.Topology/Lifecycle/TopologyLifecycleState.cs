using System;

namespace NetLoom.Topology.Lifecycle
{
    public sealed class TopologyLifecycleState
    {
        public TopologyLifecycleState(
            string subjectKey,
            TopologyLifecycleOrigin origin,
            DateTime firstSeenUtc,
            DateTime lastSeenUtc,
            TopologyFreshness freshness)
        {
            if (string.IsNullOrWhiteSpace(subjectKey))
            {
                throw new ArgumentException(
                    "Subject key is required.",
                    nameof(subjectKey));
            }

            RequireUtc(
                firstSeenUtc,
                nameof(firstSeenUtc));

            RequireUtc(
                lastSeenUtc,
                nameof(lastSeenUtc));

            if (lastSeenUtc < firstSeenUtc)
            {
                throw new ArgumentException(
                    "Last seen cannot be before first seen.",
                    nameof(lastSeenUtc));
            }

            SubjectKey = subjectKey;
            Origin = origin;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            Freshness = freshness;
        }

        public string SubjectKey { get; }

        public TopologyLifecycleOrigin Origin { get; }

        public DateTime FirstSeenUtc { get; }

        public DateTime LastSeenUtc { get; }

        public TopologyFreshness Freshness { get; }

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
