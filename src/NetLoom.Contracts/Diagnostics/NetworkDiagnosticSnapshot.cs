using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Contracts.Diagnostics
{
    public enum DiagnosticDegradationStatus
    {
        Unknown = 0,
        Healthy = 1,
        Degraded = 2
    }

    public enum DiagnosticDegradationReason
    {
        Unknown = 0,
        NoBaseline = 1,
        CounterDiscontinuity = 2,
        IncompleteCounterData = 3,
        ErrorRateThresholdExceeded = 4,
        DiscardRateThresholdExceeded = 5
    }

    public enum DiagnosticLinkStrength
    {
        Confirmed = 0,
        Observed = 1,
        Inferred = 2,
        Manual = 3
    }


    public enum DiagnosticRawAvailability
    {
        Unknown = 0,
        NotApplicable = 1,
        Available = 2,
        Expired = 3
    }

    public sealed class DiagnosticEvidenceItem
    {
        public DiagnosticEvidenceItem(
            MapEvidenceKind kind,
            MapEvidenceStrength strength,
            Guid? observationId,
            DateTime? capturedUtc,
            string sourceAddress,
            string detail,
            DiagnosticRawAvailability rawAvailability)
        {
            if (observationId.HasValue &&
                observationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id cannot be empty.",
                    nameof(observationId));
            }

            if (capturedUtc.HasValue &&
                capturedUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Captured time must be UTC.",
                    nameof(capturedUtc));
            }

            if (!observationId.HasValue &&
                rawAvailability != DiagnosticRawAvailability.NotApplicable)
            {
                throw new ArgumentException(
                    "Evidence without observation id must have NotApplicable raw availability.",
                    nameof(rawAvailability));
            }

            if (observationId.HasValue &&
                rawAvailability == DiagnosticRawAvailability.NotApplicable)
            {
                throw new ArgumentException(
                    "Evidence with observation id must have a raw availability state.",
                    nameof(rawAvailability));
            }

            Kind = kind;
            Strength = strength;
            ObservationId = observationId;
            CapturedUtc = capturedUtc;
            SourceAddress = Normalize(sourceAddress);
            Detail = Normalize(detail);
            RawAvailability = rawAvailability;
        }

        public MapEvidenceKind Kind { get; }

        public MapEvidenceStrength Strength { get; }

        public Guid? ObservationId { get; }

        public DateTime? CapturedUtc { get; }

        public string SourceAddress { get; }

        public string Detail { get; }

        public DiagnosticRawAvailability RawAvailability { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }

    public sealed class NetworkDiagnosticSnapshot
    {
        public NetworkDiagnosticSnapshot(
            DateTime generatedUtc,
            IEnumerable<DeviceDiagnostic> devices,
            IEnumerable<PhysicalLinkDiagnostic> links)
        {
            if (generatedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Generated time must be UTC.",
                    nameof(generatedUtc));
            }

            if (devices == null)
            {
                throw new ArgumentNullException(
                    nameof(devices));
            }

            if (links == null)
            {
                throw new ArgumentNullException(
                    nameof(links));
            }

            var deviceSnapshot = devices.ToArray();
            var linkSnapshot = links.ToArray();

            if (deviceSnapshot.Any(item => item == null) ||
                linkSnapshot.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Diagnostic snapshots cannot contain null items.");
            }

            if (deviceSnapshot
                    .GroupBy(item => item.DeviceId)
                    .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Diagnostic device ids must be unique.",
                    nameof(devices));
            }

            if (linkSnapshot
                    .GroupBy(item => item.PhysicalLinkId)
                    .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Diagnostic physical-link ids must be unique.",
                    nameof(links));
            }

            GeneratedUtc = generatedUtc;
            Devices = deviceSnapshot;
            Links = linkSnapshot;
        }

        public DateTime GeneratedUtc { get; }

        public IReadOnlyList<DeviceDiagnostic> Devices { get; }

        public IReadOnlyList<PhysicalLinkDiagnostic> Links { get; }
    }

    public sealed class DeviceDiagnostic
    {
        public DeviceDiagnostic(
            Guid deviceId,
            string displayName,
            string secondaryText,
            string locationName,
            DateTime? lastSeenUtc,
            DateTime? lastResolvedUtc,
            IEnumerable<InterfaceDiagnostic> interfaces)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            RequireUtc(lastSeenUtc, nameof(lastSeenUtc));
            RequireUtc(lastResolvedUtc, nameof(lastResolvedUtc));

            if (interfaces == null)
            {
                throw new ArgumentNullException(
                    nameof(interfaces));
            }

            var interfaceSnapshot = interfaces.ToArray();

            if (interfaceSnapshot.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Diagnostic interfaces cannot contain null items.",
                    nameof(interfaces));
            }

            if (interfaceSnapshot.Any(item => item.DeviceId != deviceId))
            {
                throw new ArgumentException(
                    "Diagnostic interface belongs to another device.",
                    nameof(interfaces));
            }

            DeviceId = deviceId;
            DisplayName = Normalize(displayName);
            SecondaryText = Normalize(secondaryText);
            LocationName = Normalize(locationName);
            LastSeenUtc = lastSeenUtc;
            LastResolvedUtc = lastResolvedUtc;
            Interfaces = interfaceSnapshot;
        }

        public Guid DeviceId { get; }

        public string DisplayName { get; }

        public string SecondaryText { get; }

        public string LocationName { get; }

        public DateTime? LastSeenUtc { get; }

        public DateTime? LastResolvedUtc { get; }

        public IReadOnlyList<InterfaceDiagnostic> Interfaces { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static void RequireUtc(
            DateTime? value,
            string parameterName)
        {
            if (value.HasValue &&
                value.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    parameterName);
            }
        }
    }

    public sealed class InterfaceDiagnostic
    {
        public InterfaceDiagnostic(
            Guid interfaceId,
            Guid deviceId,
            int? ifIndex,
            string displayName,
            string macAddress,
            string adminStatus,
            string operStatus,
            long? speedBps,
            DateTime? lastSeenUtc,
            StpTreePortState stpState,
            DiagnosticDegradationStatus degradationStatus,
            DateTime? degradationCapturedUtc,
            IEnumerable<DiagnosticDegradationReason> degradationReasons)
        {
            if (interfaceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface id is required.",
                    nameof(interfaceId));
            }

            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            if (ifIndex.HasValue && ifIndex.Value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (speedBps.HasValue && speedBps.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedBps));
            }

            RequireUtc(lastSeenUtc, nameof(lastSeenUtc));
            RequireUtc(
                degradationCapturedUtc,
                nameof(degradationCapturedUtc));

            if (degradationReasons == null)
            {
                throw new ArgumentNullException(
                    nameof(degradationReasons));
            }

            var reasons = degradationReasons
                .Distinct()
                .OrderBy(value => (int)value)
                .ToArray();

            if (degradationStatus == DiagnosticDegradationStatus.Healthy &&
                reasons.Length > 0)
            {
                throw new ArgumentException(
                    "Healthy diagnostic state cannot contain degradation reasons.",
                    nameof(degradationReasons));
            }

            InterfaceId = interfaceId;
            DeviceId = deviceId;
            IfIndex = ifIndex;
            DisplayName = Normalize(displayName);
            MacAddress = Normalize(macAddress);
            AdminStatus = Normalize(adminStatus);
            OperStatus = Normalize(operStatus);
            SpeedBps = speedBps;
            LastSeenUtc = lastSeenUtc;
            StpState = stpState;
            DegradationStatus = degradationStatus;
            DegradationCapturedUtc = degradationCapturedUtc;
            DegradationReasons = reasons;
        }

        public Guid InterfaceId { get; }

        public Guid DeviceId { get; }

        public int? IfIndex { get; }

        public string DisplayName { get; }

        public string MacAddress { get; }

        public string AdminStatus { get; }

        public string OperStatus { get; }

        public long? SpeedBps { get; }

        public DateTime? LastSeenUtc { get; }

        public StpTreePortState StpState { get; }

        public DiagnosticDegradationStatus DegradationStatus { get; }

        public DateTime? DegradationCapturedUtc { get; }

        public IReadOnlyList<DiagnosticDegradationReason>
            DegradationReasons { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static void RequireUtc(
            DateTime? value,
            string parameterName)
        {
            if (value.HasValue &&
                value.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    parameterName);
            }
        }
    }

    public sealed class PhysicalLinkDiagnostic
    {
        public PhysicalLinkDiagnostic(
            Guid physicalLinkId,
            Guid deviceAId,
            Guid deviceBId,
            Guid? interfaceAId,
            Guid? interfaceBId,
            string deviceAName,
            string deviceBName,
            string interfaceAName,
            string interfaceBName,
            DiagnosticLinkStrength strength,
            MapFreshness freshness,
            string mediaType,
            long? speedBps,
            string sourceSummary,
            DateTime lastSeenUtc,
            DateTime? lastConfirmedUtc,
            StpTreePortState stpStateA,
            StpTreePortState stpStateB,
            IEnumerable<DiagnosticEvidenceItem> evidence,
            bool isBridge,
            int sideADeviceCount,
            int sideBDeviceCount,
            long separatedDevicePairCount)
        {
            if (physicalLinkId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(physicalLinkId));
            }

            if (deviceAId == Guid.Empty ||
                deviceBId == Guid.Empty ||
                deviceAId == deviceBId)
            {
                throw new ArgumentException(
                    "Physical link endpoints must be distinct stable devices.");
            }

            if (lastSeenUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Last seen must be UTC.",
                    nameof(lastSeenUtc));
            }

            if (lastConfirmedUtc.HasValue &&
                lastConfirmedUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Last confirmed must be UTC.",
                    nameof(lastConfirmedUtc));
            }

            if (speedBps.HasValue && speedBps.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedBps));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(
                    nameof(evidence));
            }

            var evidenceSnapshot =
                evidence.ToArray();

            if (evidenceSnapshot.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Diagnostic evidence cannot contain null items.",
                    nameof(evidence));
            }

            if (sideADeviceCount < 0 || sideBDeviceCount < 0 ||
                separatedDevicePairCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sideADeviceCount));
            }

            if (!isBridge &&
                (sideADeviceCount != 0 ||
                 sideBDeviceCount != 0 ||
                 separatedDevicePairCount != 0))
            {
                throw new ArgumentException(
                    "Non-bridge link cannot expose bridge partitions.");
            }

            PhysicalLinkId = physicalLinkId;
            DeviceAId = deviceAId;
            DeviceBId = deviceBId;
            InterfaceAId = interfaceAId;
            InterfaceBId = interfaceBId;
            DeviceAName = Normalize(deviceAName);
            DeviceBName = Normalize(deviceBName);
            InterfaceAName = Normalize(interfaceAName);
            InterfaceBName = Normalize(interfaceBName);
            Strength = strength;
            Freshness = freshness;
            MediaType = Normalize(mediaType);
            SpeedBps = speedBps;
            SourceSummary = Normalize(sourceSummary);
            LastSeenUtc = lastSeenUtc;
            LastConfirmedUtc = lastConfirmedUtc;
            StpStateA = stpStateA;
            StpStateB = stpStateB;
            Evidence = evidenceSnapshot;
            IsBridge = isBridge;
            SideADeviceCount = sideADeviceCount;
            SideBDeviceCount = sideBDeviceCount;
            SeparatedDevicePairCount = separatedDevicePairCount;
        }

        public Guid PhysicalLinkId { get; }

        public Guid DeviceAId { get; }

        public Guid DeviceBId { get; }

        public Guid? InterfaceAId { get; }

        public Guid? InterfaceBId { get; }

        public string DeviceAName { get; }

        public string DeviceBName { get; }

        public string InterfaceAName { get; }

        public string InterfaceBName { get; }

        public DiagnosticLinkStrength Strength { get; }

        public MapFreshness Freshness { get; }

        public string MediaType { get; }

        public long? SpeedBps { get; }

        public string SourceSummary { get; }

        public DateTime LastSeenUtc { get; }

        public DateTime? LastConfirmedUtc { get; }

        public StpTreePortState StpStateA { get; }

        public StpTreePortState StpStateB { get; }

        public IReadOnlyList<DiagnosticEvidenceItem> Evidence { get; }

        public bool IsBridge { get; }

        public int SideADeviceCount { get; }

        public int SideBDeviceCount { get; }

        public long SeparatedDevicePairCount { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
