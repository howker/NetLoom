using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.Locations
{
    public sealed class LocationTopologyLocation
    {
        public LocationTopologyLocation(
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

    public sealed class LocationTopologyDevice
    {
        public LocationTopologyDevice(
            Guid deviceId,
            string displayName,
            Guid? locationId,
            DeviceDiscoveryOrigin origin)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Device display name is required.",
                    nameof(displayName));
            }

            DeviceId = deviceId;
            DisplayName = displayName;
            LocationId = locationId;
            Origin = origin;
        }

        public Guid DeviceId { get; }

        public string DisplayName { get; }

        public Guid? LocationId { get; }

        public DeviceDiscoveryOrigin Origin { get; }
    }

    public sealed class LocationTopologySnapshot
    {
        public LocationTopologySnapshot(
            IEnumerable<LocationTopologyLocation> locations,
            IEnumerable<LocationTopologyDevice> devices)
        {
            if (locations == null)
            {
                throw new ArgumentNullException(
                    nameof(locations));
            }

            if (devices == null)
            {
                throw new ArgumentNullException(
                    nameof(devices));
            }

            Locations = locations.ToArray();
            Devices = devices.ToArray();
        }

        public IReadOnlyList<LocationTopologyLocation>
            Locations { get; }

        public IReadOnlyList<LocationTopologyDevice>
            Devices { get; }
    }

    public interface ILocationTopologyService
    {
        LocationTopologySnapshot GetSnapshot();

        Guid CreateLocation(
            Guid? parentLocationId,
            string name,
            string description);

        void UpdateLocation(
            Guid locationId,
            Guid? parentLocationId,
            string name,
            string description);

        void DeleteLocation(
            Guid locationId);

        void AssignDevice(
            Guid deviceId,
            Guid? locationId);
    }
}
