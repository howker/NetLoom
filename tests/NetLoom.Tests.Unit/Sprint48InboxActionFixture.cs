using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Locations;

namespace NetLoom.Tests.Unit
{
    internal static class Sprint48InboxActionFixture
    {
        internal static InMemoryDiscoveryRunRepository Repository(Sprint48DiscoveryInboxFixture data)
        {
            var repository = new InMemoryDiscoveryRunRepository();
            repository.SaveRun(data.Run);
            foreach (var row in data.Results())
                repository.SaveResult(Copy(row, row.Group == DiscoveryResultGroup.New ||
                    row.Group == DiscoveryResultGroup.Changed ? (Guid?)Guid.NewGuid() : row.DeviceId));
            return repository;
        }

        internal static DiscoveryRunResult Copy(DiscoveryRunResult row, Guid? deviceId,
            DiscoveryResultResolution resolution = DiscoveryResultResolution.Pending, DateTime? resolvedUtc = null) =>
            new DiscoveryRunResult(row.RunId, row.Address, row.Group, deviceId, row.ObservedUtc,
                row.IcmpReachable, row.OpenTcpPorts, row.SnmpResponded, row.SnmpError,
                row.SysName, row.SysDescription, row.SysObjectId, row.InterfaceCount,
                row.Completeness, row.PartialReason, row.Reason, row.ReasonDetail, row.Changes, resolution, resolvedUtc);
    }

    internal sealed class Sprint48RecordingInboxActions : IDiscoveryInboxActions
    {
        private readonly InMemoryDiscoveryRunRepository _repository;
        internal Sprint48RecordingInboxActions(InMemoryDiscoveryRunRepository repository) { _repository = repository; }
        internal DiscoveryInboxAction? LastAction { get; private set; }
        internal Guid RunId { get; private set; }
        internal string[] Addresses { get; private set; }
        internal DateTime NowUtc { get; private set; }
        internal Guid? LocationId { get; private set; }

        public DiscoveryInboxActionResult Accept(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc) =>
            Apply(runId, addresses, nowUtc, DiscoveryInboxAction.Accept, DiscoveryResultResolution.Accepted);
        public DiscoveryInboxActionResult Ignore(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc) =>
            Apply(runId, addresses, nowUtc, DiscoveryInboxAction.Ignore, DiscoveryResultResolution.Ignored);
        public DiscoveryInboxActionResult MarkUnmanaged(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc) =>
            Apply(runId, addresses, nowUtc, DiscoveryInboxAction.MarkUnmanaged, DiscoveryResultResolution.Unmanaged);
        public DiscoveryInboxActionResult AssignPlacement(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc, Guid locationId)
        {
            LocationId = locationId;
            return Apply(runId, addresses, nowUtc, DiscoveryInboxAction.AssignPlacement, DiscoveryResultResolution.Placed);
        }
        public DiscoveryInboxActionResult UndoIgnore(Guid runId, string address, DateTime nowUtc)
        {
            var row = _repository.GetResults(runId).Single(item => item.Address == address);
            if (row.Group == DiscoveryResultGroup.Excluded) _repository.DeleteResult(runId, address);
            else _repository.SetResolution(runId, address, DiscoveryResultResolution.Pending, null);
            return new DiscoveryInboxActionResult(1, 0);
        }
        private DiscoveryInboxActionResult Apply(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc,
            DiscoveryInboxAction action, DiscoveryResultResolution resolution)
        {
            LastAction = action;
            RunId = runId;
            Addresses = addresses.ToArray();
            NowUtc = nowUtc;
            var rows = _repository.GetResults(runId).Where(row => addresses.Contains(row.Address) &&
                DiscoveryInboxActions.CanApply(action, row)).ToArray();
            foreach (var row in rows) _repository.SetResolution(runId, row.Address, resolution, nowUtc);
            return new DiscoveryInboxActionResult(rows.Length, addresses.Count - rows.Length);
        }
    }

    internal sealed class Sprint48InboxLocations : ILocationTopologyService
    {
        private readonly List<LocationTopologyLocation> _locations = new List<LocationTopologyLocation>();
        internal Sprint48InboxLocations(bool populate = true)
        {
            if (!populate) return;
            var site = CreateLocation(null, "АГПЗ", null);
            var station = CreateLocation(site, "ГПП-1", null);
            CreateLocation(station, "Серверная", null);
        }
        public LocationTopologySnapshot GetSnapshot() => new LocationTopologySnapshot(_locations, new LocationTopologyDevice[0]);
        public Guid CreateLocation(Guid? parentLocationId, string name, string description)
        {
            var id = Guid.NewGuid();
            _locations.Add(new LocationTopologyLocation(id, parentLocationId, name, description));
            return id;
        }
        public void UpdateLocation(Guid locationId, Guid? parentLocationId, string name, string description) => throw new NotSupportedException();
        public void DeleteLocation(Guid locationId) => throw new NotSupportedException();
        public void AssignDevice(Guid deviceId, Guid? locationId) => throw new NotSupportedException();
    }
}
