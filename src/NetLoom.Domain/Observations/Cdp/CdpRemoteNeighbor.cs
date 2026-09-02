using System;

namespace NetLoom.Domain.Observations.Cdp
{
    public sealed class CdpRemoteNeighbor
    {
        public CdpRemoteNeighbor(
            int cacheIfIndex,
            int deviceIndex,
            int? addressType,
            string address,
            string version,
            string deviceId,
            string devicePort,
            string platform,
            string capabilities,
            int? nativeVlan,
            int? duplex,
            string systemName,
            string systemObjectId,
            int? primaryManagementAddressType,
            string primaryManagementAddress,
            string physicalLocation,
            long? lastChange)
        {
            if (cacheIfIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cacheIfIndex));
            }

            if (deviceIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deviceIndex));
            }

            CacheIfIndex = cacheIfIndex;
            DeviceIndex = deviceIndex;
            AddressType = addressType;
            Address = address;
            Version = version;
            DeviceId = deviceId;
            DevicePort = devicePort;
            Platform = platform;
            Capabilities = capabilities;
            NativeVlan = nativeVlan;
            Duplex = duplex;
            SystemName = systemName;
            SystemObjectId = systemObjectId;
            PrimaryManagementAddressType =
                primaryManagementAddressType;
            PrimaryManagementAddress =
                primaryManagementAddress;
            PhysicalLocation = physicalLocation;
            LastChange = lastChange;
        }

        public int CacheIfIndex { get; }

        public int DeviceIndex { get; }

        public int? AddressType { get; }

        public string Address { get; }

        public string Version { get; }

        public string DeviceId { get; }

        public string DevicePort { get; }

        public string Platform { get; }

        public string Capabilities { get; }

        public int? NativeVlan { get; }

        public int? Duplex { get; }

        public string SystemName { get; }

        public string SystemObjectId { get; }

        public int? PrimaryManagementAddressType { get; }

        public string PrimaryManagementAddress { get; }

        public string PhysicalLocation { get; }

        public long? LastChange { get; }
    }
}
