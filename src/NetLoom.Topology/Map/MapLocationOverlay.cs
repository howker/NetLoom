using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Locations;

namespace NetLoom.Topology.Map
{
    public sealed class MapLocationOverlay
    {
        public MapSnapshot Apply(
            MapSnapshot source,
            IEnumerable<Location> locations,
            IEnumerable<MapLocationAssignment> assignments)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (locations == null)
            {
                throw new ArgumentNullException(nameof(locations));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            var sourceLocations =
                locations.ToArray();

            var locationById =
                sourceLocations.ToDictionary(
                    location => location.Id);

            var nodeKeys =
                new HashSet<string>(
                    source.Nodes.Select(node => node.Key),
                    StringComparer.Ordinal);

            var assignmentByNode =
                new Dictionary<string, Guid>(
                    StringComparer.Ordinal);

            foreach (var assignment in assignments)
            {
                if (!nodeKeys.Contains(
                    assignment.NodeKey))
                {
                    throw new InvalidOperationException(
                        "Location assignment references an unknown map node.");
                }

                if (!locationById.ContainsKey(
                    assignment.LocationId))
                {
                    throw new InvalidOperationException(
                        "Location assignment references an unknown location.");
                }

                Guid existing;

                if (assignmentByNode.TryGetValue(
                        assignment.NodeKey,
                        out existing) &&
                    existing != assignment.LocationId)
                {
                    throw new InvalidOperationException(
                        "Map node has conflicting location assignments.");
                }

                assignmentByNode[
                    assignment.NodeKey] =
                    assignment.LocationId;
            }

            var nodes =
                source.Nodes
                    .Select(
                        node =>
                        {
                            Guid locationId;

                            return new MapNode(
                                node.Key,
                                node.Label,
                                node.SecondaryText,
                                node.X,
                                node.Y,
                                assignmentByNode.TryGetValue(
                                    node.Key,
                                    out locationId)
                                        ? (Guid?)locationId
                                        : null,
                                node.Origin,
                                node.MonitoringCapability,
                                node.Category
                            );
                        })
                    .ToArray();

            var mapLocations =
                sourceLocations
                    .OrderBy(location => location.Name)
                    .ThenBy(location => location.Id)
                    .Select(
                        location =>
                            new MapLocation(
                                location.Id,
                                location.ParentLocationId,
                                location.Name,
                                location.Description))
                    .ToArray();

            return new MapSnapshot(
                source.GeneratedUtc,
                nodes,
                source.Links,
                mapLocations);
        }
    }
}
