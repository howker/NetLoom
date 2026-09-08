using System;

namespace NetLoom.Contracts.TopologyMap
{
    public sealed class MapNode
    {
        public MapNode(
            string key,
            string label,
            string secondaryText,
            double x,
            double y,
            Guid? locationId = null,
            MapNodeOrigin origin = MapNodeOrigin.Unknown,
            MapMonitoringCapability monitoringCapability =
                MapMonitoringCapability.Unknown,
            MapNodeCategory category = MapNodeCategory.Unknown,
            Guid? deviceId = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "Map presentation key is required.",
                    nameof(key));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "Map node label is required.",
                    nameof(label));
            }

            Key = key;
            Label = label;
            SecondaryText = secondaryText;
            X = x;
            Y = y;
            LocationId = locationId;
            Origin = origin;
            MonitoringCapability = monitoringCapability;
            Category = category;
            DeviceId = deviceId;
        }

        // Presentation key only.
        // It is not a NetLoom DeviceId.
        public string Key { get; }

        public string Label { get; }

        public string SecondaryText { get; }

        public double X { get; }

        public double Y { get; }

        public Guid? LocationId { get; }

        public MapNodeOrigin Origin { get; }

        public MapMonitoringCapability MonitoringCapability { get; }

        public MapNodeCategory Category { get; }

        // Stable NetLoom identity when this node comes from
        // materialized topology. It is separate from Key.
        public Guid? DeviceId { get; }
    }
}
