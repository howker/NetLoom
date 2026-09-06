using System;
using NetLoom.Domain.Observations;

namespace NetLoom.Domain.Topology
{
    public sealed class ManualTopologyFactory
    {
        public TopologyDevice CreateDevice(
            Guid id,
            Guid? locationId,
            string name,
            DeviceCategory category,
            string notes)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Manual device name is required.",
                    nameof(name));
            }

            return new TopologyDevice(
                id,
                locationId,
                name,
                category,
                DeviceDiscoveryOrigin.Manual,
                MonitoringCapability.None,
                null,
                null,
                notes,
                false,
                false,
                null,
                null,
                null);
        }

        public DeviceInterface CreateInterface(
            Guid id,
            Guid deviceId,
            string name,
            string mediaTypeOverride)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Manual interface name is required.",
                    nameof(name));
            }

            return new DeviceInterface(
                id,
                deviceId,
                null,
                null,
                null,
                null,
                name,
                null,
                null,
                null,
                null,
                null,
                mediaTypeOverride,
                true,
                false,
                null,
                null);
        }

        public Observation CreateObservation(
            Guid id,
            DateTime capturedUtc)
        {
            return new Observation(
                id,
                ObservationKind.Manual,
                "User",
                capturedUtc);
        }

        public PhysicalLink CreateLink(
            Guid id,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            string mediaType,
            string notes,
            DateTime createdUtc)
        {
            if (createdUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Created time must be UTC.",
                    nameof(createdUtc));
            }

            return new PhysicalLink(
                id,
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId,
                PhysicalLinkStrength.Manual,
                PhysicalLinkFreshness.Fresh,
                mediaType,
                null,
                "Manual/User",
                createdUtc,
                createdUtc,
                createdUtc,
                null,
                false,
                false,
                notes);
        }
    }
}
