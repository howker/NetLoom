using System;
using System.Linq;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;

namespace NetLoom.Topology.Materialization
{
    public sealed class MonitoringTopologyMaterializer :
        IMonitoringTopologyMaterializer
    {
        private readonly IMaterializedTopologyRepository
            _repository;

        public MonitoringTopologyMaterializer(
            IMaterializedTopologyRepository repository)
        {
            _repository =
                repository ??
                throw new ArgumentNullException(
                    nameof(repository));
        }

        public void MaterializeDevice(
            Guid deviceId,
            DateTime observedUtc)
        {
            RequireDeviceId(deviceId);
            RequireUtc(observedUtc);

            var existing =
                _repository.GetDevice(
                    deviceId);

            if (existing != null &&
                existing.DiscoveryOrigin ==
                    DeviceDiscoveryOrigin.Manual)
            {
                return;
            }

            _repository.SaveDevice(
                new TopologyDevice(
                    deviceId,
                    existing == null
                        ? (Guid?)null
                        : existing.LocationId,
                    existing == null
                        ? null
                        : existing.CustomName,
                    existing == null
                        ? DeviceCategory.Unknown
                        : existing.Category,
                    existing == null
                        ? DeviceDiscoveryOrigin.Automatic
                        : existing.DiscoveryOrigin,
                    existing == null
                        ? MonitoringCapability.Unknown
                        : existing.MonitoringCapability,
                    existing == null
                        ? null
                        : existing.VendorOverride,
                    existing == null
                        ? null
                        : existing.ModelOverride,
                    existing == null
                        ? null
                        : existing.Notes,
                    existing != null &&
                        existing.IsHidden,
                    existing != null &&
                        existing.IsArchived,
                    observedUtc,
                    observedUtc,
                    observedUtc));
        }

        public void MaterializeInterface(
            Guid deviceId,
            int ifIndex,
            DateTime observedUtc)
        {
            RequireDeviceId(deviceId);
            RequireUtc(observedUtc);

            if (ifIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (_repository.GetDevice(deviceId) == null)
            {
                MaterializeDevice(
                    deviceId,
                    observedUtc);
            }

            var matches =
                _repository
                    .GetInterfaces()
                    .Where(
                        item =>
                            item.DeviceId == deviceId &&
                            item.IfIndex == ifIndex)
                    .ToArray();

            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    "Multiple materialized interfaces share DeviceId and ifIndex.");
            }

            var existing =
                matches.Length == 0
                    ? null
                    : matches[0];

            if (existing != null &&
                existing.IsManual)
            {
                return;
            }

            _repository.SaveInterface(
                new DeviceInterface(
                    existing == null
                        ? Guid.NewGuid()
                        : existing.Id,
                    deviceId,
                    ifIndex,
                    existing == null
                        ? null
                        : existing.IfName,
                    existing == null
                        ? null
                        : existing.IfDescription,
                    existing == null
                        ? null
                        : existing.IfAlias,
                    existing == null
                        ? null
                        : existing.CustomName,
                    existing == null
                        ? null
                        : existing.MacAddress,
                    existing == null
                        ? null
                        : existing.AdminStatus,
                    existing == null
                        ? null
                        : existing.OperStatus,
                    existing == null
                        ? (long?)null
                        : existing.SpeedBps,
                    existing == null
                        ? null
                        : existing.MediaTypeAuto,
                    existing == null
                        ? null
                        : existing.MediaTypeOverride,
                    false,
                    existing != null &&
                        existing.IsHidden,
                    observedUtc,
                    observedUtc));
        }

        private static void RequireDeviceId(
            Guid deviceId)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }
        }

        private static void RequireUtc(
            DateTime observedUtc)
        {
            if (observedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Observed time must be UTC.",
                    nameof(observedUtc));
            }
        }
    }
}
