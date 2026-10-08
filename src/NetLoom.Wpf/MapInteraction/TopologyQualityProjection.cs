using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        ManualObservedConflict
    }

    public sealed class TopologyQualityGap
    {
        public TopologyQualityGap(TopologyQualityGapKind kind, Guid? physicalLinkId,
            Guid? deviceId, string subject, string detail)
        {
            Kind = kind;
            PhysicalLinkId = physicalLinkId;
            DeviceId = deviceId;
            Subject = subject;
            Detail = detail;
        }

        public TopologyQualityGapKind Kind { get; }
        public Guid? PhysicalLinkId { get; }
        public Guid? DeviceId { get; }
        public string Subject { get; }
        public string Detail { get; }
    }

    public sealed class TopologyQualityReport
    {
        internal TopologyQualityReport(IEnumerable<TopologyQualityGap> gaps)
        {
            Gaps = Array.AsReadOnly(gaps.OrderBy(gap => gap.Kind)
                .ThenBy(gap => gap.Subject, StringComparer.CurrentCulture).ToArray());
            Counts = new ReadOnlyDictionary<TopologyQualityGapKind, int>(
                Enum.GetValues(typeof(TopologyQualityGapKind)).Cast<TopologyQualityGapKind>()
                    .ToDictionary(kind => kind, kind => Gaps.Count(gap => gap.Kind == kind)));
        }

        public IReadOnlyList<TopologyQualityGap> Gaps { get; }
        public int Count => Gaps.Count;
        public IReadOnlyDictionary<TopologyQualityGapKind, int> Counts { get; }
    }

    public static class TopologyQualityProjection
    {
        public static TopologyQualityReport Build(NetworkDiagnosticSnapshot diagnostics, MapSnapshot map,
            IReadOnlyList<TopologyConflict> conflicts = null)
        {
            var gaps = new List<TopologyQualityGap>();
            if (diagnostics == null || map == null || map.Nodes.Count == 0)
                return new TopologyQualityReport(gaps);

            foreach (var conflict in conflicts ?? TopologyConflictProjection.Build(diagnostics, null))
            {
                var manual = conflict.ManualLink;
                var observed = conflict.ObservedLink;
                var shared = conflict.SharedDeviceId;
                gaps.Add(new TopologyQualityGap(TopologyQualityGapKind.ManualObservedConflict,
                    conflict.ManualLinkId, null,
                    UiText.Format("TopologyConflictQualitySubject",
                        Value(shared == manual.DeviceAId ? manual.DeviceAName : manual.DeviceBName),
                        Value(shared == manual.DeviceAId ? manual.DeviceBName : manual.DeviceAName),
                        Value(shared == observed.DeviceAId ? observed.DeviceAName : observed.DeviceBName),
                        Value(shared == observed.DeviceAId ? observed.DeviceBName : observed.DeviceAName)),
                    UiText.Format("MapQualityLinkPorts", Value(manual.InterfaceAName), Value(manual.InterfaceBName))));
            }

            var nodes = map.Nodes.Where(node => node.DeviceId.HasValue)
                .GroupBy(node => node.DeviceId.Value).ToDictionary(group => group.Key, group => group.First());
            var devices = diagnostics.Devices.ToDictionary(device => device.DeviceId);
            var seenInterfaces = new HashSet<Guid>();

            foreach (var link in diagnostics.Links)
            {
                var subject = UiText.Format("MapQualityLinkSubject",
                    Value(link.DeviceAName), Value(link.DeviceBName));
                var ports = UiText.Format("MapQualityLinkPorts",
                    Value(link.InterfaceAName), Value(link.InterfaceBName));

                if (link.Strength == DiagnosticLinkStrength.Observed)
                    gaps.Add(new TopologyQualityGap(TopologyQualityGapKind.ObservedLink,
                        link.PhysicalLinkId, null, subject, ports));
                else if (link.Strength == DiagnosticLinkStrength.Inferred)
                    gaps.Add(new TopologyQualityGap(TopologyQualityGapKind.InferredLink,
                        link.PhysicalLinkId, null, subject, ports));

                if (link.LldpReporting == DiagnosticLldpReporting.OnlySideA ||
                    link.LldpReporting == DiagnosticLldpReporting.OnlySideB)
                {
                    var reporter = link.LldpReporting == DiagnosticLldpReporting.OnlySideA
                        ? link.DeviceAName : link.DeviceBName;
                    gaps.Add(new TopologyQualityGap(TopologyQualityGapKind.OneSidedLldp,
                        link.PhysicalLinkId, null, subject,
                        UiText.Format("MapQualityLldpReporter", Value(reporter))));
                }

                AddSyntheticInterface(link.DeviceAId, link.InterfaceAId, nodes, devices, seenInterfaces, gaps);
                AddSyntheticInterface(link.DeviceBId, link.InterfaceBId, nodes, devices, seenInterfaces, gaps);
            }

            return new TopologyQualityReport(gaps);
        }

        private static void AddSyntheticInterface(Guid deviceId, Guid? interfaceId,
            IDictionary<Guid, MapNode> nodes, IDictionary<Guid, DeviceDiagnostic> devices,
            ISet<Guid> seenInterfaces, ICollection<TopologyQualityGap> gaps)
        {
            MapNode node;
            DeviceDiagnostic device;
            if (!interfaceId.HasValue || !nodes.TryGetValue(deviceId, out node) ||
                node.Origin == MapNodeOrigin.Manual || !devices.TryGetValue(deviceId, out device))
                return;

            var port = device.Interfaces.FirstOrDefault(item => item.InterfaceId == interfaceId.Value);
            if (port == null || port.IfIndex.HasValue || !seenInterfaces.Add(port.InterfaceId))
                return;

            gaps.Add(new TopologyQualityGap(TopologyQualityGapKind.SyntheticInterface,
                null, deviceId, Value(device.DisplayName ?? node.Label),
                UiText.Format("MapQualitySyntheticPort", Value(port.DisplayName))));
        }

        private static string Value(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? UiText.Get("DiagnosticValueAbsent") : value;
        }
    }
}
