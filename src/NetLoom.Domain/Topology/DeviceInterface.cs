using System;

namespace NetLoom.Domain.Topology
{
    public sealed class DeviceInterface
    {
        public DeviceInterface(
            Guid id,
            Guid deviceId,
            int? ifIndex,
            string ifName,
            string ifDescription,
            string ifAlias,
            string customName,
            string macAddress,
            string adminStatus,
            string operStatus,
            long? speedBps,
            string mediaTypeAuto,
            string mediaTypeOverride,
            bool isManual,
            bool isHidden,
            DateTime? firstSeenUtc,
            DateTime? lastSeenUtc,
            string lldpPortId = null,
            string lldpPortDescription = null,
            int? ifType = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface id is required.",
                    nameof(id));
            }

            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            if (ifIndex.HasValue &&
                ifIndex.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (speedBps.HasValue &&
                speedBps.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedBps));
            }

            if (ifType.HasValue &&
                ifType.Value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifType));
            }

            RequireUtc(firstSeenUtc, nameof(firstSeenUtc));
            RequireUtc(lastSeenUtc, nameof(lastSeenUtc));

            if (firstSeenUtc.HasValue &&
                lastSeenUtc.HasValue &&
                lastSeenUtc.Value < firstSeenUtc.Value)
            {
                throw new ArgumentException(
                    "Last seen cannot be before first seen.",
                    nameof(lastSeenUtc));
            }

            Id = id;
            DeviceId = deviceId;
            IfIndex = ifIndex;
            IfName = Normalize(ifName);
            IfDescription = Normalize(ifDescription);
            IfAlias = Normalize(ifAlias);
            CustomName = Normalize(customName);
            MacAddress = Normalize(macAddress);
            AdminStatus = Normalize(adminStatus);
            OperStatus = Normalize(operStatus);
            SpeedBps = speedBps;
            MediaTypeAuto = Normalize(mediaTypeAuto);
            MediaTypeOverride = Normalize(mediaTypeOverride);
            IsManual = isManual;
            IsHidden = isHidden;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            LldpPortId = Normalize(lldpPortId);
            LldpPortDescription =
                Normalize(lldpPortDescription);
            IfType = ifType;
        }

        public Guid Id { get; }

        public Guid DeviceId { get; }

        public int? IfIndex { get; }

        public string IfName { get; }

        public string IfDescription { get; }

        public string IfAlias { get; }

        public string CustomName { get; }

        public string MacAddress { get; }

        public string AdminStatus { get; }

        public string OperStatus { get; }

        public long? SpeedBps { get; }

        public string MediaTypeAuto { get; }

        public string MediaTypeOverride { get; }

        public bool IsManual { get; }

        public bool IsHidden { get; }

        public DateTime? FirstSeenUtc { get; }

        public DateTime? LastSeenUtc { get; }

        public string LldpPortId { get; }

        public string LldpPortDescription { get; }

        public int? IfType { get; }

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
}
