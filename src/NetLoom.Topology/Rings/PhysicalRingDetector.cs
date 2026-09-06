using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetLoom.Contracts.Rings;
using NetLoom.Domain.Topology;

namespace NetLoom.Topology.Rings
{
    public sealed class PhysicalRingDetector
    {
        public IReadOnlyList<PhysicalRing> Detect(
            IEnumerable<PhysicalLink> links)
        {
            if (links == null)
            {
                throw new ArgumentNullException(
                    nameof(links));
            }

            var input =
                links.ToArray();

            if (input.Any(link => link == null))
            {
                throw new ArgumentException(
                    "Physical link collection cannot contain null.",
                    nameof(links));
            }

            var eligible =
                input
                    .Where(
                        link =>
                            !link.IsArchived &&
                            link.DeviceAId !=
                                link.DeviceBId)
                    .GroupBy(
                        link => link.LinkKey,
                        StringComparer.Ordinal)
                    .Select(
                        group =>
                            group
                                .OrderBy(link => link.Id)
                                .First())
                    .OrderBy(link => link.Id)
                    .ToArray();

            var forest =
                new Dictionary<
                    Guid,
                    List<PhysicalLink>>();

            var sets =
                new DisjointSet();

            var rings =
                new Dictionary<
                    string,
                    PhysicalRing>(
                        StringComparer.Ordinal);

            foreach (var link in eligible)
            {
                EnsureVertex(
                    forest,
                    link.DeviceAId);

                EnsureVertex(
                    forest,
                    link.DeviceBId);

                if (sets.Find(link.DeviceAId) !=
                    sets.Find(link.DeviceBId))
                {
                    sets.Union(
                        link.DeviceAId,
                        link.DeviceBId);

                    forest[link.DeviceAId].Add(link);
                    forest[link.DeviceBId].Add(link);

                    continue;
                }

                var path =
                    FindTreePath(
                        link.DeviceAId,
                        link.DeviceBId,
                        forest);

                if (path.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Connected physical graph has no spanning-tree path.");
                }

                var linkIds =
                    path
                        .Select(item => item.Id)
                        .Concat(new[] { link.Id })
                        .Distinct()
                        .OrderBy(id => id)
                        .ToArray();

                if (linkIds.Length < 2)
                {
                    continue;
                }

                var deviceIds =
                    path
                        .SelectMany(
                            item =>
                                new[]
                                {
                                    item.DeviceAId,
                                    item.DeviceBId
                                })
                        .Concat(
                            new[]
                            {
                                link.DeviceAId,
                                link.DeviceBId
                            })
                        .Distinct()
                        .OrderBy(id => id)
                        .ToArray();

                if (deviceIds.Length < 2)
                {
                    continue;
                }

                var ringKey =
                    BuildRingKey(linkIds);

                rings[ringKey] =
                    new PhysicalRing(
                        ringKey,
                        deviceIds,
                        linkIds);
            }

            return
                rings.Values
                    .OrderBy(
                        ring => ring.RingKey,
                        StringComparer.Ordinal)
                    .ToArray();
        }

        private static void EnsureVertex(
            IDictionary<Guid, List<PhysicalLink>> forest,
            Guid deviceId)
        {
            if (!forest.ContainsKey(deviceId))
            {
                forest[deviceId] =
                    new List<PhysicalLink>();
            }
        }

        private static IReadOnlyList<PhysicalLink>
            FindTreePath(
                Guid source,
                Guid target,
                IDictionary<Guid, List<PhysicalLink>> forest)
        {
            var queue =
                new Queue<Guid>();

            var visited =
                new HashSet<Guid>();

            var previousDevice =
                new Dictionary<Guid, Guid>();

            var previousLink =
                new Dictionary<Guid, PhysicalLink>();

            queue.Enqueue(source);
            visited.Add(source);

            while (queue.Count > 0)
            {
                var current =
                    queue.Dequeue();

                if (current == target)
                {
                    break;
                }

                List<PhysicalLink> edges;

                if (!forest.TryGetValue(
                        current,
                        out edges))
                {
                    continue;
                }

                foreach (var edge in
                    edges.OrderBy(item => item.Id))
                {
                    var next =
                        OtherDevice(
                            edge,
                            current);

                    if (!visited.Add(next))
                    {
                        continue;
                    }

                    previousDevice[next] = current;
                    previousLink[next] = edge;
                    queue.Enqueue(next);
                }
            }

            if (!visited.Contains(target))
            {
                return new PhysicalLink[0];
            }

            var result =
                new List<PhysicalLink>();

            var cursor =
                target;

            while (cursor != source)
            {
                PhysicalLink edge;
                Guid previous;

                if (!previousLink.TryGetValue(
                        cursor,
                        out edge) ||
                    !previousDevice.TryGetValue(
                        cursor,
                        out previous))
                {
                    throw new InvalidOperationException(
                        "Incomplete spanning-tree path.");
                }

                result.Add(edge);
                cursor = previous;
            }

            result.Reverse();

            return result;
        }

        private static Guid OtherDevice(
            PhysicalLink link,
            Guid deviceId)
        {
            if (link.DeviceAId == deviceId)
            {
                return link.DeviceBId;
            }

            if (link.DeviceBId == deviceId)
            {
                return link.DeviceAId;
            }

            throw new InvalidOperationException(
                "Physical link is not incident to the requested device.");
        }

        private static string BuildRingKey(
            IEnumerable<Guid> physicalLinkIds)
        {
            var payload =
                string.Join(
                    "|",
                    physicalLinkIds
                        .OrderBy(id => id)
                        .Select(
                            id =>
                                id.ToString("N")));

            using (var sha256 =
                SHA256.Create())
            {
                var hash =
                    sha256.ComputeHash(
                        Encoding.UTF8.GetBytes(
                            payload));

                var builder =
                    new StringBuilder(
                        "pring-v1-",
                        9 + (hash.Length * 2));

                foreach (var value in hash)
                {
                    builder.Append(
                        value.ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private sealed class DisjointSet
        {
            private readonly Dictionary<Guid, Guid>
                _parent =
                    new Dictionary<Guid, Guid>();

            public Guid Find(Guid value)
            {
                Guid parent;

                if (!_parent.TryGetValue(
                        value,
                        out parent))
                {
                    _parent[value] = value;
                    return value;
                }

                if (parent == value)
                {
                    return value;
                }

                var root =
                    Find(parent);

                _parent[value] = root;

                return root;
            }

            public void Union(
                Guid left,
                Guid right)
            {
                var leftRoot =
                    Find(left);

                var rightRoot =
                    Find(right);

                if (leftRoot == rightRoot)
                {
                    return;
                }

                var leftToken =
                    leftRoot.ToString("N");

                var rightToken =
                    rightRoot.ToString("N");

                if (string.CompareOrdinal(
                        leftToken,
                        rightToken) <= 0)
                {
                    _parent[rightRoot] =
                        leftRoot;
                }
                else
                {
                    _parent[leftRoot] =
                        rightRoot;
                }
            }
        }
    }
}
