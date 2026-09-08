using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.Lookup
{
    public enum MacIpLookupKind
    {
        Mac = 0,
        Ip = 1
    }

    public enum MacIpLookupCandidateStatus
    {
        FdbNotObserved = 0,
        ObservationUnbound = 1,
        BridgePortUnresolved = 2,
        BridgePortAmbiguous = 3,
        InterfaceNotMaterialized = 4,
        ResolvedInterface = 5
    }

    public sealed class MacIpLookupCandidate
    {
        public MacIpLookupCandidate(
            string ipAddress,
            string macAddress,
            Guid? deviceId,
            Guid? interfaceId,
            int? ifIndex,
            int? bridgePortIndex,
            Guid? fdbObservationId,
            DateTime? fdbCapturedUtc,
            string fdbSourceAddress,
            Guid? arpObservationId,
            DateTime? arpCapturedUtc,
            string arpSourceAddress,
            MacIpLookupCandidateStatus status)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
            {
                throw new ArgumentException(
                    "MAC address is required.",
                    nameof(macAddress));
            }

            RequireUtc(
                fdbCapturedUtc,
                nameof(fdbCapturedUtc));

            RequireUtc(
                arpCapturedUtc,
                nameof(arpCapturedUtc));

            IpAddress =
                string.IsNullOrWhiteSpace(ipAddress)
                    ? null
                    : ipAddress.Trim();

            MacAddress = macAddress.Trim();
            DeviceId = deviceId;
            InterfaceId = interfaceId;
            IfIndex = ifIndex;
            BridgePortIndex = bridgePortIndex;
            FdbObservationId = fdbObservationId;
            FdbCapturedUtc = fdbCapturedUtc;

            FdbSourceAddress =
                string.IsNullOrWhiteSpace(
                    fdbSourceAddress)
                    ? null
                    : fdbSourceAddress.Trim();

            ArpObservationId = arpObservationId;
            ArpCapturedUtc = arpCapturedUtc;

            ArpSourceAddress =
                string.IsNullOrWhiteSpace(
                    arpSourceAddress)
                    ? null
                    : arpSourceAddress.Trim();

            Status = status;
        }

        public string IpAddress { get; }

        public string MacAddress { get; }

        public Guid? DeviceId { get; }

        public Guid? InterfaceId { get; }

        public int? IfIndex { get; }

        public int? BridgePortIndex { get; }

        public Guid? FdbObservationId { get; }

        public DateTime? FdbCapturedUtc { get; }

        public string FdbSourceAddress { get; }

        public Guid? ArpObservationId { get; }

        public DateTime? ArpCapturedUtc { get; }

        public string ArpSourceAddress { get; }

        public MacIpLookupCandidateStatus Status { get; }

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

    public sealed class MacIpLookupResult
    {
        public MacIpLookupResult(
            MacIpLookupKind kind,
            string normalizedQuery,
            IEnumerable<MacIpLookupCandidate>
                candidates)
        {
            if (string.IsNullOrWhiteSpace(
                normalizedQuery))
            {
                throw new ArgumentException(
                    "Normalized query is required.",
                    nameof(normalizedQuery));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(
                    nameof(candidates));
            }

            Kind = kind;
            NormalizedQuery = normalizedQuery;
            Candidates = candidates.ToArray();
        }

        public MacIpLookupKind Kind { get; }

        public string NormalizedQuery { get; }

        public IReadOnlyList<MacIpLookupCandidate>
            Candidates { get; }
    }

    public interface IMacIpLookupReader
    {
        MacIpLookupResult FindByMac(
            string macAddress,
            int maxCandidates);

        MacIpLookupResult FindByIp(
            string ipAddress,
            int maxCandidates);
    }
}