using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf.MapInteraction
{
    public enum TopologyQualityGapKind
    {
        ObservedLink,
        InferredLink,
        OneSidedLldp,
        SyntheticInterface,
        ManualObservedConflict,
        LocationOverlap
    }

    public sealed class TopologyQualityReason
    {
        public TopologyQualityReason(TopologyQualityGapKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public TopologyQualityGapKind Kind { get; }
        public string Text { get; }
    }

    public sealed class TopologyQualityItem
    {
        public TopologyQualityItem(string subject, Guid? physicalLinkId, Guid? deviceId,
            IEnumerable<TopologyQualityReason> reasons, Guid? locationId = null, Guid? otherLocationId = null)
        {
            Subject = subject;
            PhysicalLinkId = physicalLinkId;
            DeviceId = deviceId;
            LocationId = locationId;
            OtherLocationId = otherLocationId;
            Reasons = Array.AsReadOnly(reasons.OrderBy(reason => reason.Kind)
                .ThenBy(reason => reason.Text, StringComparer.CurrentCulture).ToArray());
        }

        public string Subject { get; }
        public Guid? PhysicalLinkId { get; }
        public Guid? DeviceId { get; }
        public Guid? LocationId { get; }
        public Guid? OtherLocationId { get; }
        public IReadOnlyList<TopologyQualityReason> Reasons { get; }
    }

    public sealed class TopologyQualityReport
    {
        internal TopologyQualityReport(IEnumerable<TopologyQualityItem> items)
        {
            Items = Array.AsReadOnly(items.GroupBy(ObjectKey).Select(group =>
            {
                var first = group.First();
                return new TopologyQualityItem(first.Subject, first.PhysicalLinkId, first.DeviceId,
                    group.SelectMany(item => item.Reasons), first.LocationId, first.OtherLocationId);
            }).OrderByDescending(item => item.Reasons.Count)
                .ThenBy(item => item.Subject, StringComparer.CurrentCulture).ToArray());
        }

        public IReadOnlyList<TopologyQualityItem> Items { get; }
        public int Count => Items.Count;

        private static Tuple<Guid?, Guid?, Guid?, Guid?> ObjectKey(TopologyQualityItem item)
        {
            var firstLocation = item.LocationId;
            var secondLocation = item.OtherLocationId;
            // Пара размещений остаётся одним объектом независимо от порядка её концов.
            if (firstLocation.HasValue && secondLocation.HasValue &&
                firstLocation.Value.CompareTo(secondLocation.Value) > 0)
            {
                firstLocation = item.OtherLocationId;
                secondLocation = item.LocationId;
            }
            return Tuple.Create(item.PhysicalLinkId, item.DeviceId, firstLocation, secondLocation);
        }
    }

    public static class TopologyQualityProjection
    {
        public static TopologyQualityReport Build(NetworkDiagnosticSnapshot diagnostics, MapSnapshot map,
            IReadOnlyList<TopologyConflict> conflicts = null, IReadOnlyList<LocationOverlap> overlaps = null)
        {
            var items = new List<TopologyQualityItem>();
            foreach (var overlap in overlaps ?? new LocationOverlap[0])
                items.Add(new TopologyQualityItem(Value(overlap.First.Name), null, null,
                    new[] { new TopologyQualityReason(TopologyQualityGapKind.LocationOverlap,
                        UiText.Format(overlap.ChildOutsideParent ? "MapQualityLocationOutsideParentReason" :
                            "MapQualityLocationOverlapReason", Value(overlap.Second.Name))) },
                    overlap.First.Id, overlap.Second.Id));
            if (diagnostics == null || map == null || map.Nodes.Count == 0)
                return new TopologyQualityReport(items);

            foreach (var conflict in conflicts ?? TopologyConflictProjection.Build(diagnostics, null))
            {
                var manual = conflict.ManualLink;
                items.Add(new TopologyQualityItem(
                    UiText.Format("MapQualityLinkSubject", Value(manual.DeviceAName), Value(manual.DeviceBName)),
                    conflict.ManualLinkId, null,
                    new[] { new TopologyQualityReason(TopologyQualityGapKind.ManualObservedConflict,
                        UiText.Format("TopologyConflictQualityReason", Endpoints(manual, conflict.SharedDeviceId),
                            Endpoints(conflict.ObservedLink, conflict.SharedDeviceId))) }));
            }

            var nodes = map.Nodes.Where(node => node.DeviceId.HasValue)
                .GroupBy(node => node.DeviceId.Value).ToDictionary(group => group.Key, group => group.First());
            var devices = diagnostics.Devices.ToDictionary(device => device.DeviceId);
            var seenInterfaces = new HashSet<Guid>();

            foreach (var link in diagnostics.Links)
            {
                var reasons = new List<TopologyQualityReason>();
                if (link.Strength == DiagnosticLinkStrength.Observed)
                    reasons.Add(new TopologyQualityReason(TopologyQualityGapKind.ObservedLink,
                        UiText.Get("MapQualityObservedReason")));
                else if (link.Strength == DiagnosticLinkStrength.Inferred)
                    reasons.Add(new TopologyQualityReason(TopologyQualityGapKind.InferredLink,
                        UiText.Get("MapQualityInferredReason")));

                if (link.LldpReporting == DiagnosticLldpReporting.OnlySideA ||
                    link.LldpReporting == DiagnosticLldpReporting.OnlySideB)
                {
                    var reporter = link.LldpReporting == DiagnosticLldpReporting.OnlySideA
                        ? link.DeviceAName : link.DeviceBName;
                    reasons.Add(new TopologyQualityReason(TopologyQualityGapKind.OneSidedLldp,
                        UiText.Format("MapQualityLldpReporter", Value(reporter))));
                }

                if (reasons.Count > 0)
                    items.Add(new TopologyQualityItem(UiText.Format("MapQualityLinkSubject",
                        Value(link.DeviceAName), Value(link.DeviceBName)), link.PhysicalLinkId, null, reasons));

                AddSyntheticInterface(link.DeviceAId, link.InterfaceAId, nodes, devices, seenInterfaces, items);
                AddSyntheticInterface(link.DeviceBId, link.InterfaceBId, nodes, devices, seenInterfaces, items);
            }

            return new TopologyQualityReport(items);
        }

        private static void AddSyntheticInterface(Guid deviceId, Guid? interfaceId,
            IDictionary<Guid, MapNode> nodes, IDictionary<Guid, DeviceDiagnostic> devices,
            ISet<Guid> seenInterfaces, ICollection<TopologyQualityItem> items)
        {
            MapNode node;
            DeviceDiagnostic device;
            if (!interfaceId.HasValue || !nodes.TryGetValue(deviceId, out node) ||
                node.Origin == MapNodeOrigin.Manual || !devices.TryGetValue(deviceId, out device))
                return;

            var port = device.Interfaces.FirstOrDefault(item => item.InterfaceId == interfaceId.Value);
            if (port == null || port.IfIndex.HasValue || !seenInterfaces.Add(port.InterfaceId))
                return;

            items.Add(new TopologyQualityItem(Value(device.DisplayName ?? node.Label), null, deviceId,
                new[] { new TopologyQualityReason(TopologyQualityGapKind.SyntheticInterface,
                    UiText.Format("MapQualitySyntheticPort", Value(port.DisplayName))) }));
        }

        private static string Endpoints(PhysicalLinkDiagnostic link, Guid firstDeviceId)
        {
            var firstIsA = link.DeviceAId == firstDeviceId;
            return UiText.Format("TopologyConflictEndpoints",
                Value(firstIsA ? link.DeviceAName : link.DeviceBName),
                Value(firstIsA ? link.InterfaceAName : link.InterfaceBName),
                Value(firstIsA ? link.DeviceBName : link.DeviceAName),
                Value(firstIsA ? link.InterfaceBName : link.InterfaceAName));
        }

        private static string Value(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? UiText.Get("DiagnosticValueAbsent") : value;
        }
    }
}
