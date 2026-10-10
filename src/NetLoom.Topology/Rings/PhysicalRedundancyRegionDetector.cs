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
    public sealed class PhysicalRedundancyRegionDetector
    {
        public IReadOnlyList<PhysicalRedundancyRegion>
            Detect(
                IEnumerable<PhysicalLink> links)
        {
            var edges =
                BuildEligibleEdges(
                    links);

            var adjacency =
                BuildAdjacency(
                    edges);

            var discovery =
                new Dictionary<Guid,int>();

            var low =
                new Dictionary<Guid,int>();

            var edgeStack =
                new Stack<GraphEdge>();

            var components =
                new List<GraphEdge[]>();

            var time = 0;

            foreach (var deviceId in
                adjacency.Keys.OrderBy(id => id))
            {
                if (discovery.ContainsKey(
                    deviceId))
                {
                    continue;
                }

                DetectBlocksDepthFirst(
                    deviceId,
                    -1,
                    adjacency,
                    discovery,
                    low,
                    edgeStack,
                    components,
                    ref time);

                if (edgeStack.Count > 0)
                {
                    components.Add(
                        PopRemaining(
                            edgeStack));
                }
            }

            return components
                .Where(
                    component =>
                        component.Length >= 2)
                .Select(
                    BuildRegion)
                .Where(
                    region =>
                        region != null)
                .OrderBy(
                    region =>
                        region.RegionKey,
                    StringComparer.Ordinal)
                .ToArray();
        }

        // Выделяет кольца, замкнутые через пару связанных ядер, внутри составного региона.
        // Параллельные связи одной пары устройств сводятся в один «пучок».
        public IReadOnlyList<PhysicalRedundancyRegion>
            DetectCorePairRings(
                PhysicalRedundancyRegion region,
                IEnumerable<PhysicalLink> links)
        {
            if (region == null)
            {
                throw new ArgumentNullException(
                    nameof(region));
            }

            if (links == null)
            {
                throw new ArgumentNullException(
                    nameof(links));
            }

            if (region.Kind !=
                PhysicalRedundancyRegionKind.Composite)
            {
                return new PhysicalRedundancyRegion[0];
            }

            var regionLinkIds =
                new HashSet<Guid>(
                    region.PhysicalLinkIds);

            var regionDevices =
                new HashSet<Guid>(
                    region.DeviceIds);

            var edges =
                BuildEligibleEdges(
                    links)
                    .Where(
                        edge =>
                            regionLinkIds.Contains(
                                edge.Link.Id) &&
                            regionDevices.Contains(
                                edge.Link.DeviceAId) &&
                            regionDevices.Contains(
                                edge.Link.DeviceBId))
                    .ToArray();

            var bundles =
                new Dictionary<string,List<Guid>>(
                    StringComparer.Ordinal);

            var neighbors =
                new Dictionary<Guid,SortedSet<Guid>>();

            foreach (var edge in edges)
            {
                var a =
                    edge.Link.DeviceAId;

                var b =
                    edge.Link.DeviceBId;

                var pairKey =
                    BundleKey(
                        a,
                        b);

                List<Guid> bundle;

                if (!bundles.TryGetValue(
                    pairKey,
                    out bundle))
                {
                    bundle =
                        new List<Guid>();

                    bundles.Add(
                        pairKey,
                        bundle);
                }

                bundle.Add(
                    edge.Link.Id);

                AddNeighbor(
                    neighbors,
                    a,
                    b);

                AddNeighbor(
                    neighbors,
                    b,
                    a);
            }

            if (neighbors.Count < 3)
            {
                return new PhysicalRedundancyRegion[0];
            }

            var result =
                new Dictionary<string,PhysicalRedundancyRegion>(
                    StringComparer.Ordinal);

            if (neighbors.Values.All(
                set => set.Count == 2))
            {
                // Граф пучков — простой цикл: одно кольцо из всех пучков.
                var multiBundles =
                    bundles
                        .Where(
                            pair =>
                                pair.Value.Count >= 2)
                        .Select(
                            pair => pair.Key)
                        .ToArray();

                var cores =
                    new Guid[0];

                if (multiBundles.Length == 1)
                {
                    cores =
                        BundleDevices(
                            multiBundles[0]);
                }

                AddRing(
                    result,
                    neighbors.Keys,
                    bundles.Values.SelectMany(
                        ids => ids),
                    cores);

                return result.Values
                    .OrderBy(
                        item => item.RegionKey,
                        StringComparer.Ordinal)
                    .ToArray();
            }

            foreach (var anchor in
                neighbors
                    .Where(
                        pair => pair.Value.Count >= 3)
                    .Select(
                        pair => pair.Key)
                    .OrderBy(id => id))
            {
                foreach (var first in
                    neighbors[anchor])
                {
                    var chainDevices =
                        new List<Guid>();

                    var chainBundleKeys =
                        new List<string>
                        {
                            BundleKey(
                                anchor,
                                first)
                        };

                    var previous =
                        anchor;

                    var current =
                        first;

                    var steps = 0;

                    while (neighbors[current].Count == 2 &&
                        steps <= neighbors.Count)
                    {
                        steps++;

                        chainDevices.Add(
                            current);

                        var next =
                            neighbors[current]
                                .First(
                                    id => id != previous);

                        chainBundleKeys.Add(
                            BundleKey(
                                current,
                                next));

                        previous =
                            current;

                        current =
                            next;
                    }

                    if (neighbors[current].Count < 3 ||
                        current == anchor ||
                        chainBundleKeys.Count < 2)
                    {
                        continue;
                    }

                    var closingKey =
                        BundleKey(
                            anchor,
                            current);

                    if (!bundles.ContainsKey(
                        closingKey))
                    {
                        // Без прямой связи между концами цепочки кольца нет.
                        continue;
                    }

                    var ringBundleKeys =
                        chainBundleKeys
                            .Concat(
                                new[]
                                {
                                    closingKey
                                })
                            .Distinct(
                                StringComparer.Ordinal)
                            .ToArray();

                    AddRing(
                        result,
                        chainDevices
                            .Concat(
                                new[]
                                {
                                    anchor,
                                    current
                                }),
                        ringBundleKeys.SelectMany(
                            key => bundles[key]),
                        new[]
                        {
                            anchor,
                            current
                        });
                }
            }

            return result.Values
                .OrderBy(
                    item => item.RegionKey,
                    StringComparer.Ordinal)
                .ToArray();
        }

        private static void AddRing(
            IDictionary<string,PhysicalRedundancyRegion> result,
            IEnumerable<Guid> deviceIds,
            IEnumerable<Guid> physicalLinkIds,
            IEnumerable<Guid> coreDeviceIds)
        {
            var linkIds =
                physicalLinkIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            var devices =
                deviceIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            if (linkIds.Length < 2 ||
                devices.Length < 3)
            {
                return;
            }

            var key =
                BuildKey(
                    "pring-corepair-v1-",
                    linkIds);

            if (result.ContainsKey(key))
            {
                return;
            }

            result.Add(
                key,
                new PhysicalRedundancyRegion(
                    key,
                    PhysicalRedundancyRegionKind.CorePairRing,
                    devices,
                    linkIds,
                    coreDeviceIds));
        }

        private static void AddNeighbor(
            IDictionary<Guid,SortedSet<Guid>> neighbors,
            Guid deviceId,
            Guid neighborId)
        {
            SortedSet<Guid> set;

            if (!neighbors.TryGetValue(
                deviceId,
                out set))
            {
                set =
                    new SortedSet<Guid>();

                neighbors.Add(
                    deviceId,
                    set);
            }

            set.Add(
                neighborId);
        }

        private static string BundleKey(
            Guid first,
            Guid second)
        {
            return first.CompareTo(second) <= 0
                ? first.ToString("N") + "|" + second.ToString("N")
                : second.ToString("N") + "|" + first.ToString("N");
        }

        private static Guid[] BundleDevices(
            string bundleKey)
        {
            return bundleKey
                .Split('|')
                .Select(
                    value =>
                        Guid.ParseExact(
                            value,
                            "N"))
                .ToArray();
        }

        private static GraphEdge[] BuildEligibleEdges(
            IEnumerable<PhysicalLink> links)
        {
            if (links == null)
            {
                throw new ArgumentNullException(
                    nameof(links));
            }

            var input =
                links.ToArray();

            if (input.Any(
                link => link == null))
            {
                throw new ArgumentException(
                    "Physical links cannot contain null.",
                    nameof(links));
            }

            return input
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
                            .OrderBy(
                                link => link.Id)
                            .First())
                .OrderBy(
                    link => link.Id)
                .Select(
                    (link,index) =>
                        new GraphEdge(
                            index,
                            link))
                .ToArray();
        }

        private static Dictionary<Guid,List<GraphEdge>>
            BuildAdjacency(
                IEnumerable<GraphEdge> edges)
        {
            var result =
                new Dictionary<
                    Guid,
                    List<GraphEdge>>();

            foreach (var edge in edges)
            {
                AddAdjacency(
                    result,
                    edge.Link.DeviceAId,
                    edge);

                AddAdjacency(
                    result,
                    edge.Link.DeviceBId,
                    edge);
            }

            foreach (var item in result)
            {
                item.Value.Sort(
                    (left,right) =>
                        left.Link.Id.CompareTo(
                            right.Link.Id));
            }

            return result;
        }

        private static void AddAdjacency(
            IDictionary<Guid,List<GraphEdge>> adjacency,
            Guid deviceId,
            GraphEdge edge)
        {
            List<GraphEdge> list;

            if (!adjacency.TryGetValue(
                deviceId,
                out list))
            {
                list =
                    new List<GraphEdge>();

                adjacency.Add(
                    deviceId,
                    list);
            }

            list.Add(
                edge);
        }

        private static void DetectBlocksDepthFirst(
            Guid deviceId,
            int parentEdgeIndex,
            IDictionary<Guid,List<GraphEdge>> adjacency,
            IDictionary<Guid,int> discovery,
            IDictionary<Guid,int> low,
            Stack<GraphEdge> edgeStack,
            ICollection<GraphEdge[]> components,
            ref int time)
        {
            time++;

            discovery[deviceId] =
                time;

            low[deviceId] =
                time;

            List<GraphEdge> incident;

            if (!adjacency.TryGetValue(
                deviceId,
                out incident))
            {
                return;
            }

            foreach (var edge in incident)
            {
                if (edge.Index ==
                    parentEdgeIndex)
                {
                    continue;
                }

                var other =
                    OtherDevice(
                        edge.Link,
                        deviceId);

                int otherDiscovery;

                if (!discovery.TryGetValue(
                    other,
                    out otherDiscovery))
                {
                    edgeStack.Push(
                        edge);

                    DetectBlocksDepthFirst(
                        other,
                        edge.Index,
                        adjacency,
                        discovery,
                        low,
                        edgeStack,
                        components,
                        ref time);

                    low[deviceId] =
                        Math.Min(
                            low[deviceId],
                            low[other]);

                    if (low[other] >=
                        discovery[deviceId])
                    {
                        components.Add(
                            PopThrough(
                                edgeStack,
                                edge.Index));
                    }
                }
                else if (otherDiscovery <
                    discovery[deviceId])
                {
                    edgeStack.Push(
                        edge);

                    low[deviceId] =
                        Math.Min(
                            low[deviceId],
                            otherDiscovery);
                }
            }
        }

        private static GraphEdge[] PopThrough(
            Stack<GraphEdge> edgeStack,
            int boundaryEdgeIndex)
        {
            var result =
                new List<GraphEdge>();

            while (edgeStack.Count > 0)
            {
                var edge =
                    edgeStack.Pop();

                result.Add(
                    edge);

                if (edge.Index ==
                    boundaryEdgeIndex)
                {
                    break;
                }
            }

            return result
                .OrderBy(
                    edge => edge.Link.Id)
                .ToArray();
        }

        private static GraphEdge[] PopRemaining(
            Stack<GraphEdge> edgeStack)
        {
            var result =
                new List<GraphEdge>();

            while (edgeStack.Count > 0)
            {
                result.Add(
                    edgeStack.Pop());
            }

            return result
                .OrderBy(
                    edge => edge.Link.Id)
                .ToArray();
        }

        private static PhysicalRedundancyRegion
            BuildRegion(
                IEnumerable<GraphEdge> component)
        {
            var edges =
                component
                    .GroupBy(
                        edge => edge.Link.Id)
                    .Select(
                        group => group.First())
                    .OrderBy(
                        edge => edge.Link.Id)
                    .ToArray();

            if (edges.Length < 2)
            {
                return null;
            }

            var deviceIds =
                edges
                    .SelectMany(
                        edge =>
                            new[]
                            {
                                edge.Link.DeviceAId,
                                edge.Link.DeviceBId
                            })
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            if (deviceIds.Length < 2)
            {
                return null;
            }

            var kind =
                Classify(
                    edges,
                    deviceIds);

            var physicalLinkIds =
                edges
                    .Select(
                        edge => edge.Link.Id)
                    .OrderBy(id => id)
                    .ToArray();

            return new PhysicalRedundancyRegion(
                BuildRegionKey(
                    physicalLinkIds),
                kind,
                deviceIds,
                physicalLinkIds);
        }

        private static PhysicalRedundancyRegionKind
            Classify(
                IEnumerable<GraphEdge> edges,
                IReadOnlyCollection<Guid> deviceIds)
        {
            var edgeArray =
                edges.ToArray();

            if (deviceIds.Count == 2)
            {
                return
                    PhysicalRedundancyRegionKind
                        .ParallelLinks;
            }

            var degree =
                deviceIds.ToDictionary(
                    id => id,
                    id => 0);

            foreach (var edge in edgeArray)
            {
                degree[edge.Link.DeviceAId]++;
                degree[edge.Link.DeviceBId]++;
            }

            if (edgeArray.Length ==
                    deviceIds.Count &&
                degree.Values.All(
                    value => value == 2))
            {
                return
                    PhysicalRedundancyRegionKind
                        .SimpleRing;
            }

            return
                PhysicalRedundancyRegionKind
                    .Composite;
        }

        private static Guid OtherDevice(
            PhysicalLink link,
            Guid deviceId)
        {
            return link.DeviceAId ==
                deviceId
                ? link.DeviceBId
                : link.DeviceAId;
        }

        private static string BuildRegionKey(
            IEnumerable<Guid> physicalLinkIds)
        {
            return BuildKey(
                "pring-region-v1-",
                physicalLinkIds);
        }

        private static string BuildKey(
            string prefix,
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
                        prefix,
                        prefix.Length + (hash.Length * 2));

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

        private sealed class GraphEdge
        {
            public GraphEdge(
                int index,
                PhysicalLink link)
            {
                Index =
                    index;

                Link =
                    link;
            }

            public int Index { get; }

            public PhysicalLink Link { get; }
        }
    }
}
