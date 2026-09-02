using System;

namespace NetLoom.Domain.Locations
{
    public sealed class Location
    {
        public Location(
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

            if (parentLocationId == id)
            {
                throw new ArgumentException(
                    "Location cannot be its own parent.",
                    nameof(parentLocationId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Location name is required.",
                    nameof(name));
            }

            Id = id;
            ParentLocationId = parentLocationId;
            Name = name.Trim();
            Description =
                string.IsNullOrWhiteSpace(description)
                    ? null
                    : description.Trim();
        }

        public Guid Id { get; }

        public Guid? ParentLocationId { get; }

        public string Name { get; }

        public string Description { get; }
    }
}
