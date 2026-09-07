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
                        "pring-region-v1-",
                        16 + (hash.Length * 2));

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
