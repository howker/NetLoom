using System;

namespace NetLoom.Topology.Resolution
{
    public sealed class LinkEndpointClaim
    {
        public LinkEndpointClaim(
            string managementAddress,
            string systemName,
            string chassisId,
            string deviceIdClaim,
            LinkPortReferenceKind portReferenceKind,
            int? portIndex,
            string portId,
            string portDescription)
        {
            if (portIndex.HasValue &&
                portIndex.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(portIndex));
            }

            ManagementAddress = managementAddress;
            SystemName = systemName;
            ChassisId = chassisId;
            DeviceIdClaim = deviceIdClaim;
            PortReferenceKind = portReferenceKind;
            PortIndex = portIndex;
            PortId = portId;
            PortDescription = portDescription;
        }

        public string ManagementAddress { get; }

        public string SystemName { get; }

        public string ChassisId { get; }

        // ?????? identity claim ?????????.
        // ??  ?????????? NetLoom DeviceId.
        public string DeviceIdClaim { get; }

        public LinkPortReferenceKind PortReferenceKind { get; }

        public int? PortIndex { get; }

        public string PortId { get; }

        public string PortDescription { get; }
    }
}
