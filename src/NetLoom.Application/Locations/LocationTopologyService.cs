using System;
using System.Linq;
using NetLoom.Application.Topology;
using NetLoom.Domain.Locations;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.Locations
{
    public sealed class LocationTopologyService :
        ILocationTopologyService
    {
        private readonly ILocationRepository
            _locationRepository;

        private readonly IMaterializedTopologyRepository
            _topologyRepository;

        public LocationTopologyService(
            ILocationRepository locationRepository,
            IMaterializedTopologyRepository topologyRepository)
        {
            _locationRepository =
                locationRepository ??
                throw new ArgumentNullException(
                    nameof(locationRepository));

            _topologyRepository =
                topologyRepository ??
                throw new ArgumentNullException(
                    nameof(topologyRepository));
        }

        public LocationTopologySnapshot GetSnapshot()
        {
            var locations =
                _locationRepository
                    .GetAll()
                    .OrderBy(
                        item => item.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id)
                    .Select(
                        item =>
                            new LocationTopologyLocation(
                                item.Id,
                                item.ParentLocationId,
                                item.Name,
                                item.Description))
                    .ToArray();

            var devices =
                _topologyRepository
                    .GetDevices()
                    .Where(
                        item =>
                            !item.IsHidden &&
                            !item.IsArchived)
                    .OrderBy(
                        DisplayName,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id)
                    .Select(
                        item =>
                            new LocationTopologyDevice(
                                item.Id,
                                DisplayName(item),
                                item.LocationId,
                                item.DiscoveryOrigin))
                    .ToArray();

            return new LocationTopologySnapshot(
                locations,
                devices);
        }

        public Guid CreateLocation(
            Guid? parentLocationId,
            string name,
            string description)
        {
            var id = Guid.NewGuid();

            ValidateParentLocation(
                id,
                parentLocationId);

            _locationRepository.Save(
                new Location(
                    id,
                    parentLocationId,
                    name,
                    description));

            return id;
        }

        public void UpdateLocation(
            Guid locationId,
            Guid? parentLocationId,
            string name,
            string description)
        {
            RequireLocationId(
                locationId);

            if (_locationRepository.Get(
                    locationId) == null)
            {
                throw new InvalidOperationException(
                    "Location does not exist.");
            }

            ValidateParentLocation(
                locationId,
                parentLocationId);

            _locationRepository.Save(
                new Location(
                    locationId,
                    parentLocationId,
                    name,
                    description));
        }

        public void DeleteLocation(
            Guid locationId)
        {
            RequireLocationId(
                locationId);

            if (_locationRepository.Get(
                    locationId) == null)
            {
                throw new InvalidOperationException(
                    "Location does not exist.");
            }

            if (_locationRepository
                .GetAll()
                .Any(
                    item =>
                        item.ParentLocationId ==
                        locationId))
            {
                throw new InvalidOperationException(
                    "Location with child locations cannot be deleted.");
            }

            if (_topologyRepository
                .GetDevices()
                .Any(
                    item =>
                        item.LocationId ==
                        locationId))
            {
                throw new InvalidOperationException(
                    "Location with assigned devices cannot be deleted.");
            }

            _locationRepository.Delete(
                locationId);
        }

        public void AssignDevice(
            Guid deviceId,
            Guid? locationId)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            if (locationId.HasValue &&
                _locationRepository.Get(
                    locationId.Value) == null)
            {
                throw new InvalidOperationException(
                    "Location does not exist.");
            }

            var existing =
                _topologyRepository.GetDevice(
                    deviceId);

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "Device does not exist.");
            }

            _topologyRepository.SaveDevice(
                new TopologyDevice(
                    existing.Id,
                    locationId,
                    existing.CustomName,
                    existing.Category,
                    existing.DiscoveryOrigin,
                    existing.MonitoringCapability,
                    existing.VendorOverride,
                    existing.ModelOverride,
                    existing.Notes,
                    existing.IsHidden,
                    existing.IsArchived,
                    existing.FirstSeenUtc,
                    existing.LastSeenUtc,
                    existing.LastResolvedUtc,
                    existing.DiscoveredName,
                    existing.LldpChassisId,
                    existing.ManagementAddress));
        }


        private void ValidateParentLocation(
            Guid locationId,
            Guid? parentLocationId)
        {
            if (!parentLocationId.HasValue)
            {
                return;
            }

            if (parentLocationId.Value ==
                locationId)
            {
                throw new InvalidOperationException(
                    "Location cannot be its own parent.");
            }

            var locations =
                _locationRepository
                    .GetAll()
                    .ToDictionary(
                        item => item.Id);

            var currentId =
                parentLocationId;

            var visited =
                new System.Collections.Generic.HashSet<Guid>();

            while (currentId.HasValue)
            {
                if (currentId.Value ==
                    locationId)
                {
                    throw new InvalidOperationException(
                        "Location hierarchy cycle is not allowed.");
                }

                if (!visited.Add(
                        currentId.Value))
                {
                    throw new InvalidOperationException(
                        "Existing location hierarchy contains a cycle.");
                }

                Location current;

                if (!locations.TryGetValue(
                        currentId.Value,
                        out current))
                {
                    throw new InvalidOperationException(
                        "Parent location does not exist.");
                }

                currentId =
                    current.ParentLocationId;
            }
        }

        private static string DisplayName(
            TopologyDevice device)
        {
            if (!string.IsNullOrWhiteSpace(
                    device.CustomName))
            {
                return device.CustomName;
            }

            if (!string.IsNullOrWhiteSpace(
                    device.DiscoveredName))
            {
                return device.DiscoveredName;
            }

            if (!string.IsNullOrWhiteSpace(
                    device.ManagementAddress))
            {
                return device.ManagementAddress;
            }

            return device.Id.ToString("D");
        }

        private static void RequireLocationId(
            Guid locationId)
        {
            if (locationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Location id is required.",
                    nameof(locationId));
            }
        }
    }
}
