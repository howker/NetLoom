using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.TopologyMap
{
    public sealed class MapSnapshot
    {
        public MapSnapshot(
            DateTime generatedUtc,
            IEnumerable<MapNode> nodes,
            IEnumerable<MapLink> links)
        {
            if (generatedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Generated time must be UTC.",
                    nameof(generatedUtc));
            }

            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            if (links == null)
            {
                throw new ArgumentNullException(nameof(links));
            }

            GeneratedUtc = generatedUtc;
            Nodes = nodes.ToArray();
            Links = links.ToArray();
        }

        public DateTime GeneratedUtc { get; }

        public IReadOnlyList<MapNode> Nodes { get; }

        public IReadOnlyList<MapLink> Links { get; }
    }
}
