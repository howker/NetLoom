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

    public sealed class MapLayoutSnapshot
    {
        public MapLayoutSnapshot(
            Guid mapId,
            MapViewportLayout viewport,
            IEnumerable<MapDeviceLayout> devices)
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

            var materialized =
                devices.ToArray();

            if (materialized
                .GroupBy(item => item.DeviceId)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Map layout contains duplicate device ids.",
                    nameof(devices));
            }

            MapId = mapId;
            Viewport = viewport;
            Devices = materialized;
        }

        public Guid MapId { get; }

        public MapViewportLayout Viewport { get; }

        public IReadOnlyList<MapDeviceLayout> Devices { get; }
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
}
