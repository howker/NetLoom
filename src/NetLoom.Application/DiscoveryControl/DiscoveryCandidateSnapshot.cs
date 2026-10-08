using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NetLoom.Application.Snmp;

namespace NetLoom.Application.DiscoveryControl
{
    public sealed class DiscoveryCandidateSnapshot
    {
        public DiscoveryCandidateSnapshot(
            IPAddress address,
            Guid? accessProfileId,
            bool icmpReachable,
            bool snmpResponded,
            IEnumerable<int> openTcpPorts,
            string sysName,
            string sysDescription,
            string sysObjectId,
            string sysLocation,
            int interfaceCount,
            SnmpTransportFailure? snmpError = null)
        {
            Address = address ??
                throw new ArgumentNullException(
                    nameof(address));

            if (accessProfileId.HasValue &&
                accessProfileId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "DISCOVERY_ACCESS_PROFILE_ID_REQUIRED",
                    nameof(accessProfileId));
            }

            if (openTcpPorts == null)
            {
                throw new ArgumentNullException(
                    nameof(openTcpPorts));
            }

            var ports =
                openTcpPorts
                    .Distinct()
                    .ToArray();

            if (ports.Any(
                port =>
                    port < 1 ||
                    port > 65535))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(openTcpPorts));
            }

            if (interfaceCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interfaceCount));
            }

            if (snmpResponded && snmpError.HasValue)
            {
                throw new ArgumentException(
                    "SNMP response cannot have a transport error.",
                    nameof(snmpError));
            }

            SnmpError = snmpError;
            AccessProfileId = accessProfileId;
            IcmpReachable = icmpReachable;
            SnmpResponded = snmpResponded;
            OpenTcpPorts = ports;
            SysName = Normalize(sysName);
            SysDescription = Normalize(sysDescription);
            SysObjectId = Normalize(sysObjectId);
            SysLocation = Normalize(sysLocation);
            InterfaceCount = interfaceCount;
        }

        public IPAddress Address { get; }

        public Guid? AccessProfileId { get; }

        public bool IcmpReachable { get; }

        public bool SnmpResponded { get; }

        public SnmpTransportFailure? SnmpError { get; }

        public IReadOnlyList<int> OpenTcpPorts { get; }

        public string SysName { get; }

        public string SysDescription { get; }

        public string SysObjectId { get; }

        public string SysLocation { get; }

        public int InterfaceCount { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
