using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;

namespace NetLoom.Application.Export
{
    public sealed class TopologyExportLayoutSnapshot
    {
        public TopologyExportLayoutSnapshot(
            Guid mapId,
            IEnumerable<MapDeviceLayout> devices,
            IEnumerable<MapLocationLayout> locations)
        {
            if (mapId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Map id is required.",
                    nameof(mapId));
            }

            if (devices == null)
            {
                throw new ArgumentNullException(
                    nameof(devices));
            }

            if (locations == null)
            {
                throw new ArgumentNullException(
                    nameof(locations));
            }

            var materializedDevices =
                devices.ToArray();

            if (materializedDevices.Any(
                    item => item == null))
            {
                throw new ArgumentException(
                    "Export device layouts cannot contain null items.",
                    nameof(devices));
            }

            if (materializedDevices
                .GroupBy(item => item.DeviceId)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Export layout contains duplicate device ids.",
                    nameof(devices));
            }

            var materializedLocations =
                locations.ToArray();

            if (materializedLocations.Any(
                    item => item == null))
            {
                throw new ArgumentException(
                    "Export location layouts cannot contain null items.",
                    nameof(locations));
            }

            if (materializedLocations
                .GroupBy(item => item.LocationId)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Export layout contains duplicate location ids.",
                    nameof(locations));
            }

            MapId = mapId;
            Devices = materializedDevices;
            Locations = materializedLocations;
        }

        public Guid MapId { get; }

        public IReadOnlyList<MapDeviceLayout> Devices { get; }

        public IReadOnlyList<MapLocationLayout> Locations { get; }

        public static TopologyExportLayoutSnapshot
            FromPersisted(
                Guid mapId,
                MapLayoutSnapshot persisted)
        {
            if (mapId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Map id is required.",
                    nameof(mapId));
            }

            if (persisted == null)
            {
                return new TopologyExportLayoutSnapshot(
                    mapId,
                    new MapDeviceLayout[0],
                    new MapLocationLayout[0]);
            }

            if (persisted.MapId != mapId)
            {
                throw new InvalidOperationException(
                    "Persisted map layout belongs to another map.");
            }

            var devices =
                persisted.Devices
                    .Select(
                        item =>
                            new MapDeviceLayout(
                                item.DeviceId,
                                item.X,
                                item.Y,
                                item.IsLocked))
                    .ToArray();

            var locations =
                persisted.Locations
                    .Select(
                        item =>
                            new MapLocationLayout(
                                item.LocationId,
                                item.X,
                                item.Y,
                                item.Width,
                                item.Height,
                                false,
                                item.IsLocked))
                    .ToArray();

            return new TopologyExportLayoutSnapshot(
                mapId,
                devices,
                locations);
        }
    }

    public sealed class TopologyExportSnapshot
    {
        public TopologyExportSnapshot(
            TopologyRefreshSnapshot topology,
            TopologyExportLayoutSnapshot layout)
        {
            Topology =
                topology ??
                throw new ArgumentNullException(
                    nameof(topology));

            Layout =
                layout ??
                throw new ArgumentNullException(
                    nameof(layout));
        }

        public TopologyRefreshSnapshot Topology { get; }

        public TopologyExportLayoutSnapshot Layout { get; }
    }

    public interface ITopologyExportSnapshotProvider
    {
        TopologyExportSnapshot GetSnapshot();
    }
}
