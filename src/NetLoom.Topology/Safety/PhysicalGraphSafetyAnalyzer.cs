using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.GraphSafety;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Stp;

namespace NetLoom.Topology.Safety
{
    public sealed class PhysicalGraphSafetyAnalyzer
    {
        public IReadOnlyList<PhysicalLinkFailureImpact>
            AnalyzePhysicalFailures(
                IEnumerable<PhysicalLink> links)
        {
            var edges =
                BuildEligibleEdges(
                    links);

            var adjacency =
                BuildAdjacency(
                    edges);

            var bridges =
                FindBridges(
                    edges,
                    adjacency);

            return edges
                .OrderBy(edge => edge.Link.Id)
                .Select(
                    edge =>
                    {
                        if (!bridges.Contains(
                            edge.Index))
                        {
                            return
                                new PhysicalLinkFailureImpact(
                                    edge.Link.Id,
                                    edge.Link.DeviceAId,
                                    edge.Link.DeviceBId,
                                    new Guid[0],
                                    new Guid[0]);
                        }

                        var sideA =
                            CollectReachable(
                                adjacency,
                                edge.Link.DeviceAId,
                                edge.Index);

                        var sideB =
                            CollectReachable(
                                adjacency,
                                edge.Link.DeviceBId,
                                edge.Index);

                        return
                            new PhysicalLinkFailureImpact(
                                edge.Link.Id,
                                edge.Link.DeviceAId,
                                edge.Link.DeviceBId,
                                sideA,
                                sideB);
                    })
                .ToArray();
        }

        public ForwardingCycleAnalysis
            AnalyzeForwardingCycles(
                IEnumerable<PhysicalLink> links,
                IEnumerable<StpTreeSnapshot> stpSnapshots,
                string instanceId)
        {
            var endpointResolver =
                new StpEndpointStateResolver(
                    stpSnapshots,
                    instanceId);

            var normalizedInstanceId =
                endpointResolver.InstanceId;

            var edges =
                BuildEligibleEdges(
                    links);

            var forwarding =
                new List<GraphEdge>();

            var unresolved =
                new List<Guid>();

            foreach (var edge in edges)
            {
                var endpointA =
                    endpointResolver.Resolve(
                        edge.Link.DeviceAId,
                        edge.Link.InterfaceAId);

                var endpointB =
                    endpointResolver.Resolve(
                        edge.Link.DeviceBId,
                        edge.Link.InterfaceBId);

                if (endpointA ==
                        StpEndpointState.Unresolved ||
                    endpointB ==
                        StpEndpointState.Unresolved)
                {
                    unresolved.Add(
                        edge.Link.Id);

                    continue;
                }

                if (endpointA ==
                        StpEndpointState.Forwarding &&
                    endpointB ==
                        StpEndpointState.Forwarding)
                {
                    forwarding.Add(
                        edge);
                }
            }

            var forwardingArray =
                forwarding
                    .OrderBy(
                        edge =>
                            edge.Link.Id)
                    .ToArray();

            var forwardingAdjacency =
                BuildAdjacency(
                    forwardingArray);

            var forwardingBridges =
                FindBridges(
                    forwardingArray,
                    forwardingAdjacency);

            var cycleEdges =
                forwardingArray
                    .Where(
                        edge =>
                            !forwardingBridges.Contains(
                                edge.Index))
                    .Select(
                        edge =>
                            edge.Link.Id)
                    .OrderBy(id => id)
                    .ToArray();

            return new ForwardingCycleAnalysis(
                normalizedInstanceId,
                cycleEdges,
                unresolved);
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

        private static HashSet<int> FindBridges(
            IEnumerable<GraphEdge> edges,
            IDictionary<Guid,List<GraphEdge>> adjacency)
        {
            var discovery =
                new Dictionary<Guid,int>();

            var low =
                new Dictionary<Guid,int>();

            var bridges =
                new HashSet<int>();

            var time = 0;

            foreach (var deviceId in
                adjacency.Keys.OrderBy(id => id))
            {
                if (discovery.ContainsKey(
                    deviceId))
                {
                    continue;
                }

                FindBridgesDepthFirst(
                    deviceId,
                    -1,
                    adjacency,
                    discovery,
                    low,
                    bridges,
                    ref time);
            }

            return bridges;
        }

        private static void FindBridgesDepthFirst(
            Guid deviceId,
            int parentEdgeIndex,
            IDictionary<Guid,List<GraphEdge>> adjacency,
            IDictionary<Guid,int> discovery,
            IDictionary<Guid,int> low,
            ISet<int> bridges,
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
                    FindBridgesDepthFirst(
                        other,
                        edge.Index,
                        adjacency,
                        discovery,
                        low,
                        bridges,
                        ref time);

                    low[deviceId] =
                        Math.Min(
                            low[deviceId],
                            low[other]);

                    if (low[other] >
                        discovery[deviceId])
                    {
                        bridges.Add(
                            edge.Index);
                    }
                }
                else
                {
                    low[deviceId] =
                        Math.Min(
                            low[deviceId],
                            otherDiscovery);
                }
            }
        }

        private static Guid[] CollectReachable(
            IDictionary<Guid,List<GraphEdge>> adjacency,
            Guid start,
            int excludedEdgeIndex)
        {
            var visited =
                new HashSet<Guid>();

            var stack =
                new Stack<Guid>();

            stack.Push(
                start);

            while (stack.Count > 0)
            {
                var current =
                    stack.Pop();

                if (!visited.Add(
                    current))
                {
                    continue;
                }

                List<GraphEdge> incident;

                if (!adjacency.TryGetValue(
                    current,
                    out incident))
                {
                    continue;
                }

                foreach (var edge in incident)
                {
                    if (edge.Index ==
                        excludedEdgeIndex)
                    {
                        continue;
                    }

                    var other =
                        OtherDevice(
                            edge.Link,
                            current);

                    if (!visited.Contains(
                        other))
                    {
                        stack.Push(
                            other);
                    }
                }
            }

            return visited
                .OrderBy(id => id)
                .ToArray();
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
