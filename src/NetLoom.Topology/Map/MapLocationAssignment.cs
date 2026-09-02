using System;

namespace NetLoom.Topology.Map
{
    public sealed class MapLocationAssignment
    {
        public MapLocationAssignment(
            string nodeKey,
            Guid locationId)
        {
            if (string.IsNullOrWhiteSpace(nodeKey))
            {
                throw new ArgumentException(
                    "Map node key is required.",
                    nameof(nodeKey));
            }

            if (locationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Location id is required.",
                    nameof(locationId));
            }

            NodeKey = nodeKey;
            LocationId = locationId;
        }

        public string NodeKey { get; }

        public Guid LocationId { get; }
    }
}
