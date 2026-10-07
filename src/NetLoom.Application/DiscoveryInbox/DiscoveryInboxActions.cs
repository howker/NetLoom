using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Locations;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class DiscoveryInboxActions : IDiscoveryInboxActions
    {
        private readonly IDiscoveryRunRepository _runs;
        private readonly IMaterializedTopologyRepository _topology;
        private readonly IDeviceConfirmationStore _confirmation;
        private readonly IDeviceIgnoreStore _ignore;
        private readonly ILocationTopologyService _locations;

        public DiscoveryInboxActions(IDiscoveryRunRepository runs,
            IMaterializedTopologyRepository topology, IDeviceConfirmationStore confirmation,
            IDeviceIgnoreStore ignore, ILocationTopologyService locations)
        {
            _runs = runs ?? throw new ArgumentNullException(nameof(runs));
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            _ignore = ignore ?? throw new ArgumentNullException(nameof(ignore));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        }

        public static bool CanApply(DiscoveryInboxAction action, DiscoveryRunResult row)
        {
            if (row == null || row.Resolution != DiscoveryResultResolution.Pending ||
                row.Group == DiscoveryResultGroup.Excluded) return false;
            switch (action)
            {
                case DiscoveryInboxAction.Accept:
                    return row.DeviceId.HasValue || row.Group == DiscoveryResultGroup.Missing;
                case DiscoveryInboxAction.Ignore:
                case DiscoveryInboxAction.AssignPlacement:
                    return row.DeviceId.HasValue;
                case DiscoveryInboxAction.MarkUnmanaged:
                    return row.DeviceId.HasValue && row.Group != DiscoveryResultGroup.Missing;
                default:
                    return false;
            }
        }

        public DiscoveryInboxActionResult Accept(Guid runId,
            IReadOnlyCollection<string> addresses, DateTime nowUtc)
        {
            return Apply(runId, addresses, nowUtc, DiscoveryInboxAction.Accept,
                DiscoveryResultResolution.Accepted, (row, device) =>
                {
                    if (row.Group != DiscoveryResultGroup.Missing)
                        _confirmation.SetUnconfirmed(device.Id, false);
                });
        }

        public DiscoveryInboxActionResult Ignore(Guid runId,
            IReadOnlyCollection<string> addresses, DateTime nowUtc)
        {
            return Apply(runId, addresses, nowUtc, DiscoveryInboxAction.Ignore,
                DiscoveryResultResolution.Ignored,
                (row, device) => _ignore.SetIgnored(device.Id, nowUtc));
        }

        public DiscoveryInboxActionResult MarkUnmanaged(Guid runId,
            IReadOnlyCollection<string> addresses, DateTime nowUtc)
        {
            return Apply(runId, addresses, nowUtc, DiscoveryInboxAction.MarkUnmanaged,
                DiscoveryResultResolution.Unmanaged, (row, device) =>
                {
                    _topology.SaveDevice(new TopologyDevice(device.Id, device.LocationId,
                        device.CustomName, device.Category, device.DiscoveryOrigin,
                        MonitoringCapability.None, device.VendorOverride, device.ModelOverride,
                        device.Notes, device.IsHidden, device.IsArchived, device.FirstSeenUtc,
                        device.LastSeenUtc, device.LastResolvedUtc, device.DiscoveredName,
                        device.LldpChassisId, device.ManagementAddress, device.SystemDescription,
                        device.SystemObjectId, device.IsUnconfirmed, device.IgnoredUtc));
                    _confirmation.SetUnconfirmed(device.Id, false);
                });
        }

        public DiscoveryInboxActionResult AssignPlacement(Guid runId,
            IReadOnlyCollection<string> addresses, DateTime nowUtc, Guid locationId)
        {
            if (locationId == Guid.Empty)
                throw new ArgumentException("Location id is required.", nameof(locationId));
            return Apply(runId, addresses, nowUtc, DiscoveryInboxAction.AssignPlacement,
                DiscoveryResultResolution.Placed, (row, device) =>
                {
                    _locations.AssignDevice(device.Id, locationId);
                    _confirmation.SetUnconfirmed(device.Id, false);
                });
        }

        public DiscoveryInboxActionResult UndoIgnore(Guid runId, string address, DateTime nowUtc)
        {
            RequireUtc(nowUtc);
            var row = _runs.GetResults(runId).FirstOrDefault(item => item.Address == address);
            if (row == null || !row.DeviceId.HasValue ||
                (row.Resolution != DiscoveryResultResolution.Ignored &&
                 !(row.Group == DiscoveryResultGroup.Excluded &&
                   row.Reason == DiscoveryResultReason.OperatorIgnored)) ||
                _topology.GetDevice(row.DeviceId.Value) == null)
                return new DiscoveryInboxActionResult(0, 1);

            _ignore.SetIgnored(row.DeviceId.Value, null);
            if (row.Group == DiscoveryResultGroup.Excluded &&
                row.Reason == DiscoveryResultReason.OperatorIgnored)
                _runs.DeleteResult(runId, address);
            else
                _runs.SetResolution(runId, address, DiscoveryResultResolution.Pending, null);
            return new DiscoveryInboxActionResult(1, 0);
        }

        private DiscoveryInboxActionResult Apply(Guid runId, IReadOnlyCollection<string> addresses,
            DateTime nowUtc, DiscoveryInboxAction action, DiscoveryResultResolution resolution,
            Action<DiscoveryRunResult, TopologyDevice> update)
        {
            if (addresses == null) throw new ArgumentNullException(nameof(addresses));
            RequireUtc(nowUtc);
            var rows = _runs.GetResults(runId).ToDictionary(row => row.Address, StringComparer.Ordinal);
            var applied = 0;
            var skipped = 0;
            foreach (var address in addresses.Distinct(StringComparer.Ordinal))
            {
                DiscoveryRunResult row;
                if (address == null || !rows.TryGetValue(address, out row) || !CanApply(action, row))
                {
                    skipped++;
                    continue;
                }
                var device = row.DeviceId.HasValue ? _topology.GetDevice(row.DeviceId.Value) : null;
                if (device == null && !(action == DiscoveryInboxAction.Accept &&
                    row.Group == DiscoveryResultGroup.Missing))
                {
                    skipped++;
                    continue;
                }
                update(row, device);
                _runs.SetResolution(runId, row.Address, resolution, nowUtc);
                applied++;
            }
            return new DiscoveryInboxActionResult(applied, skipped);
        }

        private static void RequireUtc(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Timestamp must be UTC.", nameof(nowUtc));
        }
    }
}
