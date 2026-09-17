using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.MapLayout
{
    public static class MapLayoutScope
    {
        public static readonly Guid PhysicalTopologyMapId =
            new Guid(
                "00000000-0000-0000-0000-000000000036");

        public const string PhysicalTopologyMapName =
            "Physical topology";
    }

    public sealed class MapViewportLayout
    {
        public MapViewportLayout(
            double zoom,
            double panX,
            double panY)
        {
            if (!IsFinite(zoom) || zoom <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(zoom));
            }

            if (!IsFinite(panX))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(panX));
            }

            if (!IsFinite(panY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(panY));
            }

            Zoom = zoom;
            PanX = panX;
            PanY = panY;
        }

        public double Zoom { get; }

        public double PanX { get; }

        public double PanY { get; }

        private static bool IsFinite(
            double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }

    public sealed class MapDeviceLayout
    {
        public MapDeviceLayout(
            Guid deviceId,
            double x,
            double y,
            bool isLocked)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x));
            }

            if (!IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(y));
            }

            DeviceId = deviceId;
            X = x;
            Y = y;
            IsLocked = isLocked;
        }

        public Guid DeviceId { get; }

        public double X { get; }

        public double Y { get; }

        public bool IsLocked { get; }

        private static bool IsFinite(
            double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }

    public sealed class MapLocationLayout
    {
        public MapLocationLayout(
            Guid locationId,
            double x,
            double y,
            double width,
            double height,
            bool isCollapsed,
            bool isLocked)
        {
            if (locationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Location id is required.",
                    nameof(locationId));
            }

            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x));
            }

            if (!IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(y));
            }

            if (!IsFinite(width) || width <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width));
            }

            if (!IsFinite(height) || height <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height));
            }

            LocationId = locationId;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            IsCollapsed = isCollapsed;
            IsLocked = isLocked;
        }

        public Guid LocationId { get; }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public bool IsCollapsed { get; }

        public bool IsLocked { get; }

        private static bool IsFinite(
            double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }

    public sealed class MapLayoutSnapshot
    {
        public MapLayoutSnapshot(
            Guid mapId,
            MapViewportLayout viewport,
            IEnumerable<MapDeviceLayout> devices)
            : this(
                mapId,
                viewport,
                devices,
                new MapLocationLayout[0])
        {
        }

        public MapLayoutSnapshot(
            Guid mapId,
            MapViewportLayout viewport,
            IEnumerable<MapDeviceLayout> devices,
            IEnumerable<MapLocationLayout> locations)
        {
            if (mapId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Map id is required.",
                    nameof(mapId));
            }

            if (viewport == null)
            {
                throw new ArgumentNullException(
                    nameof(viewport));
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

            if (materializedDevices
                .GroupBy(item => item.DeviceId)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Map layout contains duplicate device ids.",
                    nameof(devices));
            }

            var materializedLocations =
                locations.ToArray();

            if (materializedLocations
                .GroupBy(item => item.LocationId)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Map layout contains duplicate location ids.",
                    nameof(locations));
            }

            MapId = mapId;
            Viewport = viewport;
            Devices = materializedDevices;
            Locations = materializedLocations;
        }

        public Guid MapId { get; }

        public MapViewportLayout Viewport { get; }

        public IReadOnlyList<MapDeviceLayout> Devices { get; }

        public IReadOnlyList<MapLocationLayout> Locations { get; }
    }

    public interface IMapLayoutStore
    {
        MapLayoutSnapshot Load(
            Guid mapId);

        void SaveViewport(
            Guid mapId,
            MapViewportLayout viewport);

        void SaveDevice(
            Guid mapId,
            MapDeviceLayout deviceLayout);
    }

    public interface IMapLocationLayoutStore
    {
        void SaveLocation(
            Guid mapId,
            MapLocationLayout locationLayout);
    }
}
