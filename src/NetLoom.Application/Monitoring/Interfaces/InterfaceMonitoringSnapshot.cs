using System;

namespace NetLoom.Application.Monitoring.Interfaces
{
    public sealed class InterfaceMonitoringSnapshot
    {
        public InterfaceMonitoringSnapshot(
            Guid? deviceId,
            int ifIndex,
            int? adminStatus,
            int? operStatus,
            DateTime capturedUtc,
            uint? inErrors,
            uint? outErrors,
            uint? inDiscards,
            uint? outDiscards,
            uint? counterDiscontinuityTimeTicks)
            : this(
                deviceId,
                ifIndex,
                adminStatus,
                operStatus,
                capturedUtc,
                inErrors,
                outErrors,
                inDiscards,
                outDiscards,
                counterDiscontinuityTimeTicks,
                null,
                null,
                null,
                null)
        {
        }

        public InterfaceMonitoringSnapshot(
            Guid? deviceId,
            int ifIndex,
            int? adminStatus,
            int? operStatus,
            DateTime capturedUtc,
            uint? inErrors = null,
            uint? outErrors = null,
            uint? inDiscards = null,
            uint? outDiscards = null,
            uint? counterDiscontinuityTimeTicks = null,
            string ifName = null,
            string ifDescription = null,
            string ifAlias = null,
            int? ifType = null)
        {
            if (deviceId.HasValue &&
                deviceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Interface monitoring DeviceId cannot be empty.",
                    nameof(deviceId));
            }

            if (ifIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifIndex));
            }

            if (capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface monitoring timestamp must be UTC.",
                    nameof(capturedUtc));
            }

            if (ifType.HasValue &&
                ifType.Value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ifType));
            }

            DeviceId = deviceId;
            IfIndex = ifIndex;
            AdminStatus = adminStatus;
            OperStatus = operStatus;
            CapturedUtc = capturedUtc;
            InErrors = inErrors;
            OutErrors = outErrors;
            InDiscards = inDiscards;
            OutDiscards = outDiscards;
            CounterDiscontinuityTimeTicks =
                counterDiscontinuityTimeTicks;
            IfName = Normalize(ifName);
            IfDescription = Normalize(ifDescription);
            IfAlias = Normalize(ifAlias);
            IfType = ifType;
        }

        public Guid? DeviceId { get; }

        public int IfIndex { get; }

        public int? AdminStatus { get; }

        public int? OperStatus { get; }

        public DateTime CapturedUtc { get; }

        public uint? InErrors { get; }

        public uint? OutErrors { get; }

        public uint? InDiscards { get; }

        public uint? OutDiscards { get; }

        public uint? CounterDiscontinuityTimeTicks { get; }

        public string IfName { get; }

        public string IfDescription { get; }

        public string IfAlias { get; }

        public int? IfType { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
