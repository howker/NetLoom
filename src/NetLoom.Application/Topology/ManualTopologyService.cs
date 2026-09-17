using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.Topology
{
    public sealed class ManualTopologyService :
        IManualTopologyService
    {
        private readonly IMaterializedTopologyRepository
            _repository;

        private readonly IManualTopologyAuditStore
            _auditStore;

        private readonly ManualTopologyFactory
            _factory;

        private readonly Func<DateTime>
            _utcNow;

        public ManualTopologyService(
            IMaterializedTopologyRepository repository,
            IManualTopologyAuditStore auditStore)
            : this(
                repository,
                auditStore,
                () => DateTime.UtcNow)
        {
        }

        public ManualTopologyService(
            IMaterializedTopologyRepository repository,
            IManualTopologyAuditStore auditStore,
            Func<DateTime> utcNow)
        {
            _repository = repository ??
                throw new ArgumentNullException(
                    nameof(repository));

            _auditStore = auditStore ??
                throw new ArgumentNullException(
                    nameof(auditStore));

            _utcNow = utcNow ??
                throw new ArgumentNullException(
                    nameof(utcNow));

            _factory =
                new ManualTopologyFactory();
        }

        public ManualTopologyEditorSnapshot
            GetSnapshot()
        {
            var devices =
                _repository.GetDevices();

            var interfaces =
                _repository.GetInterfaces();

            var links =
                _repository.GetPhysicalLinks();

            var deviceItems =
                new List<ManualTopologyDeviceItem>(
                    devices.Count);

            foreach (var device in devices)
            {
                deviceItems.Add(
                    new ManualTopologyDeviceItem(
                        device.Id,
                        DeviceDisplayName(device),
                        ToEditorCategory(
                            device.Category),
                        device.Notes,
                        device.DiscoveryOrigin ==
                            DeviceDiscoveryOrigin.Manual));
            }

            var portItems =
                new List<ManualTopologyPortItem>(
                    interfaces.Count);

            foreach (var networkInterface in interfaces)
            {
                portItems.Add(
                    new ManualTopologyPortItem(
                        networkInterface.Id,
                        networkInterface.DeviceId,
                        InterfaceDisplayName(
                            networkInterface),
                        FirstNonEmpty(
                            networkInterface.MediaTypeOverride,
                            networkInterface.MediaTypeAuto),
                        networkInterface.IsManual));
            }

            var linkItems =
                new List<ManualTopologyLinkItem>(
                    links.Count);

            foreach (var link in links)
            {
                linkItems.Add(
                    new ManualTopologyLinkItem(
                        link.Id,
                        link.DeviceAId,
                        link.InterfaceAId,
                        link.DeviceBId,
                        link.InterfaceBId,
                        link.MediaTypeResolved,
                        link.Notes,
                        link.Strength ==
                            PhysicalLinkStrength.Manual));
            }

            return new ManualTopologyEditorSnapshot(
                deviceItems,
                portItems,
                linkItems);
        }

        public Guid CreateDevice(
            string name,
            ManualTopologyDeviceCategory category,
            string notes)
        {
            var now = NowUtc();

            var device =
                _factory.CreateDevice(
                    Guid.NewGuid(),
                    null,
                    RequireText(
                        name,
                        "Manual device name is required."),
                    ToDomainCategory(category),
                    Normalize(notes));

            _repository.SaveDevice(
                device);

            RecordManualAction(
                now);

            return device.Id;
        }

        public void UpdateDevice(
            Guid deviceId,
            string name,
            ManualTopologyDeviceCategory category,
            string notes)
        {
            RequireId(
                deviceId,
                nameof(deviceId));

            var existing =
                _repository.GetDevice(
                    deviceId);

            RequireManualDevice(
                existing);

            var updated =
                new TopologyDevice(
                    existing.Id,
                    existing.LocationId,
                    RequireText(
                        name,
                        "Manual device name is required."),
                    ToDomainCategory(category),
                    DeviceDiscoveryOrigin.Manual,
                    MonitoringCapability.None,
                    existing.VendorOverride,
                    existing.ModelOverride,
                    Normalize(notes),
                    existing.IsHidden,
                    existing.IsArchived,
                    existing.FirstSeenUtc,
                    existing.LastSeenUtc,
                    existing.LastResolvedUtc,
                    existing.DiscoveredName,
                    existing.LldpChassisId);

            _repository.SaveDevice(
                updated);

            RecordManualAction(
                NowUtc());
        }

        public void DeleteDevice(
            Guid deviceId)
        {
            RequireId(
                deviceId,
                nameof(deviceId));

            RequireManualDevice(
                _repository.GetDevice(
                    deviceId));

            _repository.DeleteManualDevice(
                deviceId);

            RecordManualAction(
                NowUtc());
        }

        public Guid CreatePort(
            Guid deviceId,
            string name,
            string mediaType)
        {
            RequireId(
                deviceId,
                nameof(deviceId));

            RequireManualDevice(
                _repository.GetDevice(
                    deviceId));

            var networkInterface =
                _factory.CreateInterface(
                    Guid.NewGuid(),
                    deviceId,
                    RequireText(
                        name,
                        "Manual port name is required."),
                    Normalize(mediaType));

            _repository.SaveInterface(
                networkInterface);

            RecordManualAction(
                NowUtc());

            return networkInterface.Id;
        }

        public void UpdatePort(
            Guid interfaceId,
            string name,
            string mediaType)
        {
            RequireId(
                interfaceId,
                nameof(interfaceId));

            var existing =
                FindInterface(
                    interfaceId);

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "Interface does not exist.");
            }

            if (!existing.IsManual)
            {
                throw new InvalidOperationException(
                    "Automatic interfaces are read-only.");
            }

            RequireManualDevice(
                _repository.GetDevice(
                    existing.DeviceId));

            var updated =
                new DeviceInterface(
                    existing.Id,
                    existing.DeviceId,
                    existing.IfIndex,
                    existing.IfName,
                    existing.IfDescription,
                    existing.IfAlias,
                    RequireText(
                        name,
                        "Manual port name is required."),
                    existing.MacAddress,
                    existing.AdminStatus,
                    existing.OperStatus,
                    existing.SpeedBps,
                    existing.MediaTypeAuto,
                    Normalize(mediaType),
                    true,
                    existing.IsHidden,
                    existing.FirstSeenUtc,
                    existing.LastSeenUtc,
                    existing.LldpPortId,
                    existing.LldpPortDescription);

            _repository.SaveInterface(
                updated);

            RecordManualAction(
                NowUtc());
        }

        public void DeletePort(
            Guid interfaceId)
        {
            RequireId(
                interfaceId,
                nameof(interfaceId));

            var existing =
                FindInterface(
                    interfaceId);

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "Interface does not exist.");
            }

            if (!existing.IsManual)
            {
                throw new InvalidOperationException(
                    "Automatic interfaces are read-only.");
            }

            _repository.DeleteManualInterface(
                interfaceId);

            RecordManualAction(
                NowUtc());
        }

        public Guid CreateLink(
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            string mediaType,
            string notes)
        {
            RequireId(
                deviceAId,
                nameof(deviceAId));

            RequireId(
                deviceBId,
                nameof(deviceBId));

            RequireExistingDevice(
                deviceAId);

            RequireExistingDevice(
                deviceBId);

            RequireInterfaceEndpoint(
                deviceAId,
                interfaceAId);

            RequireInterfaceEndpoint(
                deviceBId,
                interfaceBId);

            var now =
                NowUtc();

            var candidate =
                _factory.CreateLink(
                    Guid.NewGuid(),
                    deviceAId,
                    interfaceAId,
                    deviceBId,
                    interfaceBId,
                    Normalize(mediaType),
                    Normalize(notes),
                    now);

            foreach (var existing in
                _repository.GetPhysicalLinks())
            {
                if (EndpointsCompatible(
                    existing,
                    candidate))
                {
                    throw new InvalidOperationException(
                        "A compatible physical link already exists.");
                }
            }

            var persisted =
                _repository.SavePhysicalLink(
                    candidate);

            RecordManualAction(
                now);

            return persisted.Id;
        }

        public void UpdateLink(
            Guid physicalLinkId,
            string mediaType,
            string notes)
        {
            RequireId(
                physicalLinkId,
                nameof(physicalLinkId));

            var existing =
                FindLink(
                    physicalLinkId);

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "Physical link does not exist.");
            }

            if (existing.Strength !=
                PhysicalLinkStrength.Manual)
            {
                throw new InvalidOperationException(
                    "Automatic physical links are read-only.");
            }

            var now =
                NowUtc();

            var updated =
                new PhysicalLink(
                    existing.Id,
                    existing.DeviceAId,
                    existing.InterfaceAId,
                    existing.DeviceBId,
                    existing.InterfaceBId,
                    PhysicalLinkStrength.Manual,
                    PhysicalLinkFreshness.Fresh,
                    Normalize(mediaType),
                    existing.SpeedBpsResolved,
                    "Manual/User",
                    existing.FirstSeenUtc,
                    now,
                    now,
                    existing.ResolverVersion,
                    existing.IsHidden,
                    existing.IsArchived,
                    Normalize(notes));

            var persisted =
                _repository.SavePhysicalLink(
                    updated);

            if (persisted.Id != existing.Id)
            {
                throw new InvalidOperationException(
                    "Manual link identity changed during metadata update.");
            }

            RecordManualAction(
                now);
        }

        public void DeleteLink(
            Guid physicalLinkId)
        {
            RequireId(
                physicalLinkId,
                nameof(physicalLinkId));

            var existing =
                FindLink(
                    physicalLinkId);

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "Physical link does not exist.");
            }

            if (existing.Strength !=
                PhysicalLinkStrength.Manual)
            {
                throw new InvalidOperationException(
                    "Automatic physical links are read-only.");
            }

            _repository.DeleteManualPhysicalLink(
                physicalLinkId);

            RecordManualAction(
                NowUtc());
        }

        private void RecordManualAction(
            DateTime capturedUtc)
        {
            _auditStore.Record(
                _factory.CreateObservation(
                    Guid.NewGuid(),
                    capturedUtc));
        }

        private DateTime NowUtc()
        {
            var value =
                _utcNow();

            if (value.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Manual topology clock must return UTC.");
            }

            return value;
        }

        private void RequireExistingDevice(
            Guid deviceId)
        {
            if (_repository.GetDevice(
                deviceId) == null)
            {
                throw new InvalidOperationException(
                    "Device does not exist.");
            }
        }

        private void RequireInterfaceEndpoint(
            Guid deviceId,
            Guid? interfaceId)
        {
            if (!interfaceId.HasValue)
            {
                return;
            }

            if (interfaceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface id cannot be empty.",
                    nameof(interfaceId));
            }

            var networkInterface =
                FindInterface(
                    interfaceId.Value);

            if (networkInterface == null)
            {
                throw new InvalidOperationException(
                    "Interface does not exist.");
            }

            if (networkInterface.DeviceId !=
                deviceId)
            {
                throw new InvalidOperationException(
                    "Interface belongs to another device.");
            }
        }

        private DeviceInterface FindInterface(
            Guid interfaceId)
        {
            foreach (var item in
                _repository.GetInterfaces())
            {
                if (item.Id == interfaceId)
                {
                    return item;
                }
            }

            return null;
        }

        private PhysicalLink FindLink(
            Guid physicalLinkId)
        {
            foreach (var item in
                _repository.GetPhysicalLinks())
            {
                if (item.Id == physicalLinkId)
                {
                    return item;
                }
            }

            return null;
        }

        private static void RequireManualDevice(
            TopologyDevice device)
        {
            if (device == null)
            {
                throw new InvalidOperationException(
                    "Device does not exist.");
            }

            if (device.DiscoveryOrigin !=
                DeviceDiscoveryOrigin.Manual)
            {
                throw new InvalidOperationException(
                    "Automatic devices are read-only.");
            }
        }

        private static bool EndpointsCompatible(
            PhysicalLink left,
            PhysicalLink right)
        {
            return
                left.DeviceAId == right.DeviceAId &&
                left.DeviceBId == right.DeviceBId &&
                InterfaceCompatible(
                    left.InterfaceAId,
                    right.InterfaceAId) &&
                InterfaceCompatible(
                    left.InterfaceBId,
                    right.InterfaceBId);
        }

        private static bool InterfaceCompatible(
            Guid? left,
            Guid? right)
        {
            return
                !left.HasValue ||
                !right.HasValue ||
                left.Value == right.Value;
        }

        private static string DeviceDisplayName(
            TopologyDevice device)
        {
            return FirstNonEmpty(
                device.CustomName,
                device.DiscoveredName,
                device.Id.ToString("D"));
        }

        private static string InterfaceDisplayName(
            DeviceInterface networkInterface)
        {
            return FirstNonEmpty(
                networkInterface.CustomName,
                networkInterface.IfName,
                networkInterface.LldpPortId,
                networkInterface.IfDescription,
                networkInterface.IfIndex.HasValue
                    ? networkInterface.IfIndex.Value.ToString(
                        CultureInfo.InvariantCulture)
                    : null,
                networkInterface.Id.ToString("D"));
        }

        private static string FirstNonEmpty(
            params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(
                    value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(
                value)
                ? null
                : value.Trim();
        }

        private static string RequireText(
            string value,
            string message)
        {
            var normalized =
                Normalize(value);

            if (normalized == null)
            {
                throw new ArgumentException(
                    message);
            }

            return normalized;
        }

        private static void RequireId(
            Guid id,
            string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Identifier is required.",
                    parameterName);
            }
        }

        private static DeviceCategory
            ToDomainCategory(
                ManualTopologyDeviceCategory category)
        {
            switch (category)
            {
                case ManualTopologyDeviceCategory.MediaConverter:
                    return DeviceCategory.MediaConverter;

                case ManualTopologyDeviceCategory.UnmanagedSwitch:
                    return DeviceCategory.UnmanagedSwitch;

                case ManualTopologyDeviceCategory.OpticalConverter:
                    return DeviceCategory.OpticalConverter;

                case ManualTopologyDeviceCategory.PassiveNetworkEquipment:
                    return DeviceCategory.PassiveNetworkEquipment;

                case ManualTopologyDeviceCategory.Unknown:
                    return DeviceCategory.Unknown;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(category));
            }
        }

        private static ManualTopologyDeviceCategory
            ToEditorCategory(
                DeviceCategory category)
        {
            switch (category)
            {
                case DeviceCategory.MediaConverter:
                    return ManualTopologyDeviceCategory.MediaConverter;

                case DeviceCategory.UnmanagedSwitch:
                    return ManualTopologyDeviceCategory.UnmanagedSwitch;

                case DeviceCategory.OpticalConverter:
                    return ManualTopologyDeviceCategory.OpticalConverter;

                case DeviceCategory.PassiveNetworkEquipment:
                    return ManualTopologyDeviceCategory.PassiveNetworkEquipment;

                default:
                    return ManualTopologyDeviceCategory.Unknown;
            }
        }
    }
}
