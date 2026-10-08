using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.TopologyMap
{
    public sealed class ParallelLinkEndpoints
    {
        public ParallelLinkEndpoints(
            string linkIdentity,
            string sourceNodeIdentity,
            string targetNodeIdentity)
        {
            if (string.IsNullOrWhiteSpace(linkIdentity))
                throw new ArgumentException("Link identity is required.", nameof(linkIdentity));
            if (string.IsNullOrWhiteSpace(sourceNodeIdentity))
                throw new ArgumentException("Source node identity is required.", nameof(sourceNodeIdentity));
            if (string.IsNullOrWhiteSpace(targetNodeIdentity))
                throw new ArgumentException("Target node identity is required.", nameof(targetNodeIdentity));

            LinkIdentity = linkIdentity;
            SourceNodeIdentity = sourceNodeIdentity;
            TargetNodeIdentity = targetNodeIdentity;
        }

        public string LinkIdentity { get; }
        public string SourceNodeIdentity { get; }
        public string TargetNodeIdentity { get; }
    }

    public sealed class ParallelLinkSlot
    {
        internal ParallelLinkSlot(int slot, int groupSize, bool sourceIsCanonicalFirst)
        {
            Slot = slot;
            GroupSize = groupSize;
            SourceIsCanonicalFirst = sourceIsCanonicalFirst;
        }

        public int Slot { get; }
        public int GroupSize { get; }
        public bool SourceIsCanonicalFirst { get; }
    }

    public static class ParallelLinkLayout
    {
        public static IReadOnlyDictionary<string, ParallelLinkSlot> Slots(
            IEnumerable<ParallelLinkEndpoints> links)
        {
            if (links == null)
                throw new ArgumentNullException(nameof(links));

            var result = new Dictionary<string, ParallelLinkSlot>(StringComparer.Ordinal);
            var groups = new Dictionary<Tuple<string, string>, List<ParallelLinkEndpoints>>();
            var identities = new HashSet<string>(StringComparer.Ordinal);

            foreach (var link in links)
            {
                if (link == null)
                    throw new ArgumentException("Link endpoints are required.", nameof(links));
                if (!identities.Add(link.LinkIdentity))
                    throw new InvalidOperationException("Duplicate stable link identity.");

                var comparison = StringComparer.Ordinal.Compare(
                    link.SourceNodeIdentity, link.TargetNodeIdentity);
                if (comparison == 0)
                {
                    result.Add(link.LinkIdentity, new ParallelLinkSlot(0, 1, true));
                    continue;
                }

                var group = comparison < 0
                    ? Tuple.Create(link.SourceNodeIdentity, link.TargetNodeIdentity)
                    : Tuple.Create(link.TargetNodeIdentity, link.SourceNodeIdentity);
                List<ParallelLinkEndpoints> members;
                if (!groups.TryGetValue(group, out members))
                {
                    members = new List<ParallelLinkEndpoints>();
                    groups.Add(group, members);
                }
                members.Add(link);
            }

            foreach (var members in groups.Values)
            {
                var ordered = members.OrderBy(link => link.LinkIdentity, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < ordered.Length; index++)
                {
                    var link = ordered[index];
                    result.Add(link.LinkIdentity, new ParallelLinkSlot(
                        (2 * index) - (ordered.Length - 1),
                        ordered.Length,
                        StringComparer.Ordinal.Compare(link.SourceNodeIdentity, link.TargetNodeIdentity) <= 0));
                }
            }

            return result;
        }

        public static double HalfSpacing(int groupSize, double spacing, double maxSpread)
        {
            if (groupSize <= 1)
                return 0.0;

            return Math.Min(spacing, maxSpread / (groupSize - 1)) / 2.0;
        }

        public static void Offset(
            double x1, double y1, double x2, double y2,
            int slot, double halfSpacing, bool sourceIsCanonicalFirst,
            out double ox1, out double oy1, out double ox2, out double oy2)
        {
            ox1 = x1;
            oy1 = y1;
            ox2 = x2;
            oy2 = y2;
            if (slot == 0 || halfSpacing == 0.0)
                return;

            var dx = x2 - x1;
            var dy = y2 - y1;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length == 0.0)
                return;

            // Направление нормали определяется каноническим порядком узлов пары.
            var distance = slot * halfSpacing * (sourceIsCanonicalFirst ? 1.0 : -1.0);
            var shiftX = -dy / length * distance;
            var shiftY = dx / length * distance;
            ox1 += shiftX;
            oy1 += shiftY;
            ox2 += shiftX;
            oy2 += shiftY;
        }
    }
}
