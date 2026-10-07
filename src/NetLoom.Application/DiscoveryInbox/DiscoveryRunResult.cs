using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Snmp;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class DiscoveryRunResult
    {
        public DiscoveryRunResult(
            Guid runId,
            string address,
            DiscoveryResultGroup group,
            Guid? deviceId,
            DateTime observedUtc,
            bool icmpReachable,
            IReadOnlyList<int> openTcpPorts,
            bool snmpResponded,
            SnmpTransportFailure? snmpError,
            string sysName,
            string sysDescription,
            string sysObjectId,
            int interfaceCount,
            DiscoveryResultCompleteness completeness,
            DiscoveryPartialReason partialReason,
            DiscoveryResultReason reason,
            string reasonDetail,
            IReadOnlyList<DiscoveryFieldChange> changes,
            DiscoveryResultResolution resolution,
            DateTime? resolvedUtc)
        {
            if (runId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Run id is required.",
                    nameof(runId));
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException(
                    "Address is required.",
                    nameof(address));
            }

            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id must not be empty.",
                    nameof(deviceId));
            }

            if (observedUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    nameof(observedUtc));
            }

            if (resolvedUtc.HasValue &&
                resolvedUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    nameof(resolvedUtc));
            }

            RequireEnum(group, nameof(group));
            RequireEnum(completeness, nameof(completeness));
            RequireEnum(partialReason, nameof(partialReason));
            RequireEnum(reason, nameof(reason));
            RequireEnum(resolution, nameof(resolution));

            if (snmpError.HasValue)
            {
                RequireEnum(snmpError.Value, nameof(snmpError));
            }

            if (interfaceCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interfaceCount));
            }

            if (openTcpPorts == null)
            {
                throw new ArgumentNullException(
                    nameof(openTcpPorts));
            }

            var ports = openTcpPorts.ToArray();

            if (ports.Any(port => port < 1 || port > 65535))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(openTcpPorts));
            }

            if (changes == null)
            {
                throw new ArgumentNullException(
                    nameof(changes));
            }

            var fieldChanges = changes.ToArray();

            if (fieldChanges.Any(change => change == null) ||
                fieldChanges.Select(change => change.Field)
                    .Distinct(StringComparer.Ordinal).Count() !=
                fieldChanges.Length)
            {
                throw new ArgumentException(
                    "Changes must have unique fields and must not be null.",
                    nameof(changes));
            }

            RunId = runId;
            Address = address;
            Group = group;
            DeviceId = deviceId;
            ObservedUtc = observedUtc;
            IcmpReachable = icmpReachable;
            OpenTcpPorts = Array.AsReadOnly(ports);
            SnmpResponded = snmpResponded;
            SnmpError = snmpError;
            SysName = sysName;
            SysDescription = sysDescription;
            SysObjectId = sysObjectId;
            InterfaceCount = interfaceCount;
            Completeness = completeness;
            PartialReason = partialReason;
            Reason = reason;
            ReasonDetail = reasonDetail;
            Changes = Array.AsReadOnly(fieldChanges);
            Resolution = resolution;
            ResolvedUtc = resolvedUtc;
        }

        public Guid RunId { get; }

        public string Address { get; }

        public DiscoveryResultGroup Group { get; }

        public Guid? DeviceId { get; }

        public DateTime ObservedUtc { get; }

        public bool IcmpReachable { get; }

        public IReadOnlyList<int> OpenTcpPorts { get; }

        public bool SnmpResponded { get; }

        public SnmpTransportFailure? SnmpError { get; }

        public string SysName { get; }

        public string SysDescription { get; }

        public string SysObjectId { get; }

        public int InterfaceCount { get; }

        public DiscoveryResultCompleteness Completeness { get; }

        public DiscoveryPartialReason PartialReason { get; }

        public DiscoveryResultReason Reason { get; }

        public string ReasonDetail { get; }

        public IReadOnlyList<DiscoveryFieldChange> Changes { get; }

        public DiscoveryResultResolution Resolution { get; }

        public DateTime? ResolvedUtc { get; }

        private static void RequireEnum<T>(
            T value,
            string parameterName)
            where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
        }
    }
}
