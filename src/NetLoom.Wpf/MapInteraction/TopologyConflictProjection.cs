using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Topology;
using NetLoom.Contracts.Diagnostics;

namespace NetLoom.Wpf.MapInteraction
{
    public sealed class TopologyConflict
    {
        internal TopologyConflict(PhysicalLinkDiagnostic manual, PhysicalLinkDiagnostic observed,
            Guid sharedDeviceId, Guid sharedInterfaceId)
        {
            ManualLink = manual;
            ObservedLink = observed;
            SharedDeviceId = sharedDeviceId;
            SharedInterfaceId = sharedInterfaceId;
        }

        public Guid ManualLinkId => ManualLink.PhysicalLinkId;
        public Guid ObservedLinkId => ObservedLink.PhysicalLinkId;
        public Guid SharedDeviceId { get; }
        public Guid SharedInterfaceId { get; }
        public PhysicalLinkDiagnostic ManualLink { get; }
        public PhysicalLinkDiagnostic ObservedLink { get; }
    }

    public static class TopologyConflictProjection
    {
        public static IReadOnlyList<TopologyConflict> Build(NetworkDiagnosticSnapshot diagnostics,
            IReadOnlyCollection<TopologyConflictKey> acknowledged)
        {
            if (diagnostics == null) return Array.Empty<TopologyConflict>();
            var excluded = new HashSet<TopologyConflictKey>(acknowledged ?? Array.Empty<TopologyConflictKey>());
            var observedPorts = diagnostics.Links.Where(link =>
                    link.Strength == DiagnosticLinkStrength.Confirmed ||
                    link.Strength == DiagnosticLinkStrength.Observed ||
                    link.Strength == DiagnosticLinkStrength.Inferred)
                .SelectMany(Endpoints).Where(endpoint => HasPort(endpoint.InterfaceId))
                .ToLookup(endpoint => Tuple.Create(endpoint.DeviceId, endpoint.InterfaceId.Value));
            var conflicts = new Dictionary<TopologyConflictKey, TopologyConflict>();

            foreach (var manual in diagnostics.Links.Where(link => link.Strength == DiagnosticLinkStrength.Manual))
            {
                foreach (var port in Endpoints(manual).Where(endpoint => HasPort(endpoint.InterfaceId)))
                {
                    foreach (var observed in observedPorts[Tuple.Create(port.DeviceId, port.InterfaceId.Value)])
                    {
                        var key = new TopologyConflictKey(manual.PhysicalLinkId, observed.Link.PhysicalLinkId);
                        if (excluded.Contains(key) || conflicts.ContainsKey(key)) continue;
                        // Неизвестный второй порт ручной связи не опровергается уточнением из опроса.
                        var differs = port.OtherDeviceId != observed.OtherDeviceId ||
                            (HasPort(port.OtherInterfaceId) && port.OtherInterfaceId != observed.OtherInterfaceId);
                        if (differs)
                            conflicts.Add(key, new TopologyConflict(manual, observed.Link, port.DeviceId, port.InterfaceId.Value));
                    }
                }
            }

            return Array.AsReadOnly(conflicts.Values
                .OrderBy(conflict => DeviceName(conflict.ManualLink, conflict.SharedDeviceId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(conflict => OtherName(conflict.ManualLink, conflict.SharedDeviceId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(conflict => OtherName(conflict.ObservedLink, conflict.SharedDeviceId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(conflict => conflict.ManualLinkId).ThenBy(conflict => conflict.ObservedLinkId).ToArray());
        }

        private static bool HasPort(Guid? id) => id.HasValue && id.Value != Guid.Empty;
        private static string DeviceName(PhysicalLinkDiagnostic link, Guid deviceId) =>
            deviceId == link.DeviceAId ? link.DeviceAName : link.DeviceBName;
        private static string OtherName(PhysicalLinkDiagnostic link, Guid deviceId) =>
            deviceId == link.DeviceAId ? link.DeviceBName : link.DeviceAName;

        private static IEnumerable<Endpoint> Endpoints(PhysicalLinkDiagnostic link)
        {
            yield return new Endpoint(link, link.DeviceAId, link.InterfaceAId, link.DeviceBId, link.InterfaceBId);
            yield return new Endpoint(link, link.DeviceBId, link.InterfaceBId, link.DeviceAId, link.InterfaceAId);
        }

        private sealed class Endpoint
        {
            public Endpoint(PhysicalLinkDiagnostic link, Guid deviceId, Guid? interfaceId,
                Guid otherDeviceId, Guid? otherInterfaceId)
            {
                Link = link;
                DeviceId = deviceId;
                InterfaceId = interfaceId;
                OtherDeviceId = otherDeviceId;
                OtherInterfaceId = otherInterfaceId;
            }
            public PhysicalLinkDiagnostic Link { get; }
            public Guid DeviceId { get; }
            public Guid? InterfaceId { get; }
            public Guid OtherDeviceId { get; }
            public Guid? OtherInterfaceId { get; }
        }
    }
}
