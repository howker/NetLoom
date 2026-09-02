using System;

namespace NetLoom.Contracts.TopologyMap
{
    public sealed class MapLocation
    {
        public MapLocation(
            Guid id,
            Guid? parentLocationId,
            string name,
            string description)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Location id is required.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Location name is required.",
                    nameof(name));
            }

            Id = id;
            ParentLocationId = parentLocationId;
            Name = name;
            Description = description;
        }

        public Guid Id { get; }

        public Guid? ParentLocationId { get; }

        public string Name { get; }

        public string Description { get; }
    }
}
