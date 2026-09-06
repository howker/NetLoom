using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.StpTree
{
    public sealed class StpTreeSnapshot
    {
        public StpTreeSnapshot(
            Guid deviceId,
            Guid observationId,
            DateTime capturedUtc,
            string instanceId,
            string designatedRoot,
            long? rootCost,
            int? rootPortBridgePortIndex,
            int? rootPortIfIndex,
            Guid? rootInterfaceId,
            IEnumerable<StpTreePort> ports)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            if (observationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id is required.",
                    nameof(observationId));
            }

            if (capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Captured time must be UTC.",
                    nameof(capturedUtc));
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            if (rootCost.HasValue &&
                rootCost.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootCost));
            }

            if (rootPortBridgePortIndex.HasValue &&
                rootPortBridgePortIndex.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootPortBridgePortIndex));
            }

            if (rootPortIfIndex.HasValue &&
                rootPortIfIndex.Value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootPortIfIndex));
            }

            if (rootInterfaceId.HasValue &&
                rootInterfaceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Root interface id cannot be empty.",
                    nameof(rootInterfaceId));
            }

            if (ports == null)
            {
                throw new ArgumentNullException(
                    nameof(ports));
            }

            DeviceId = deviceId;
            ObservationId = observationId;
            CapturedUtc = capturedUtc;
            InstanceId = instanceId.Trim();
            DesignatedRoot = Normalize(designatedRoot);
            RootCost = rootCost;
            RootPortBridgePortIndex =
                rootPortBridgePortIndex;
            RootPortIfIndex = rootPortIfIndex;
            RootInterfaceId = rootInterfaceId;
            Ports = ports.ToArray();
        }

        public Guid DeviceId { get; }

        public Guid ObservationId { get; }

        public DateTime CapturedUtc { get; }

        public string InstanceId { get; }

        public string DesignatedRoot { get; }

        public long? RootCost { get; }

        public int? RootPortBridgePortIndex { get; }

        public int? RootPortIfIndex { get; }

        public Guid? RootInterfaceId { get; }

        public IReadOnlyList<StpTreePort> Ports { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
