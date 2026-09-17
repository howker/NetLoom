using System;
using System.Collections.Generic;

namespace NetLoom.Application.Topology
{
    public enum ManualTopologyDeviceCategory
    {
        Unknown,
        MediaConverter,
        UnmanagedSwitch,
        OpticalConverter,
        PassiveNetworkEquipment
    }

    public sealed class ManualTopologyEditorSnapshot
    {
        public ManualTopologyEditorSnapshot(
            IReadOnlyList<ManualTopologyDeviceItem> devices,
            IReadOnlyList<ManualTopologyPortItem> ports,
            IReadOnlyList<ManualTopologyLinkItem> links)
        {
            Devices = devices ??
                throw new ArgumentNullException(nameof(devices));

            Ports = ports ??
                throw new ArgumentNullException(nameof(ports));

            Links = links ??
                throw new ArgumentNullException(nameof(links));
        }

        public IReadOnlyList<ManualTopologyDeviceItem>
            Devices { get; }

        public IReadOnlyList<ManualTopologyPortItem>
            Ports { get; }

        public IReadOnlyList<ManualTopologyLinkItem>
            Links { get; }
    }

    public sealed class ManualTopologyDeviceItem
    {
        public ManualTopologyDeviceItem(
            Guid deviceId,
            string displayName,
            ManualTopologyDeviceCategory category,
            string notes,
            bool isManual)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
            Category = category;
            Notes = notes;
            IsManual = isManual;
        }

        public Guid DeviceId { get; }

        public string DisplayName { get; }

        public ManualTopologyDeviceCategory Category { get; }

        public string Notes { get; }

        public bool IsManual { get; }

        public bool CanEdit
        {
            get { return IsManual; }
        }
    }

    public sealed class ManualTopologyPortItem
    {
        public ManualTopologyPortItem(
            Guid interfaceId,
            Guid deviceId,
            string displayName,
            string mediaType,
            bool isManual)
        {
            InterfaceId = interfaceId;
            DeviceId = deviceId;
            DisplayName = displayName;
            MediaType = mediaType;
            IsManual = isManual;
        }

        public Guid InterfaceId { get; }

        public Guid DeviceId { get; }

        public string DisplayName { get; }

        public string MediaType { get; }

        public bool IsManual { get; }

        public bool CanEdit
        {
            get { return IsManual; }
        }
    }

    public sealed class ManualTopologyLinkItem
    {
        public ManualTopologyLinkItem(
            Guid physicalLinkId,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            string mediaType,
            string notes,
            bool isManual)
        {
            PhysicalLinkId = physicalLinkId;
            DeviceAId = deviceAId;
            InterfaceAId = interfaceAId;
            DeviceBId = deviceBId;
            InterfaceBId = interfaceBId;
            MediaType = mediaType;
            Notes = notes;
            IsManual = isManual;
        }

        public Guid PhysicalLinkId { get; }

        public Guid DeviceAId { get; }

        public Guid? InterfaceAId { get; }

        public Guid DeviceBId { get; }

        public Guid? InterfaceBId { get; }

        public string MediaType { get; }

        public string Notes { get; }

        public bool IsManual { get; }

        public bool CanEdit
        {
            get { return IsManual; }
        }
    }
}
