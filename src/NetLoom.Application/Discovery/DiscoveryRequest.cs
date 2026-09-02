using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NetLoom.Application.Discovery
{
    public sealed class DiscoveryRequest
    {
        public DiscoveryRequest(
            IEnumerable<IPAddress> addresses,
            IEnumerable<IPAddress> exclusions,
            IEnumerable<int> tcpPorts,
            IEnumerable<DiscoverySnmpProfile> snmpProfiles,
            int icmpTimeoutMilliseconds,
            int tcpTimeoutMilliseconds)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            if (exclusions == null)
            {
                throw new ArgumentNullException(nameof(exclusions));
            }

            if (tcpPorts == null)
            {
                throw new ArgumentNullException(nameof(tcpPorts));
            }

            if (snmpProfiles == null)
            {
                throw new ArgumentNullException(nameof(snmpProfiles));
            }

            if (icmpTimeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(icmpTimeoutMilliseconds));
            }

            if (tcpTimeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tcpTimeoutMilliseconds));
            }

            var addressArray = addresses
                .Where(address => address != null)
                .Distinct()
                .ToArray();

            var exclusionArray = exclusions
                .Where(address => address != null)
                .Distinct()
                .ToArray();

            var portArray = tcpPorts
                .Distinct()
                .ToArray();

            if (portArray.Any(port => port < 1 || port > 65535))
            {
                throw new ArgumentOutOfRangeException(nameof(tcpPorts));
            }

            Addresses = addressArray;
            Exclusions = exclusionArray;
            TcpPorts = portArray;
            SnmpProfiles = snmpProfiles.ToArray();
            IcmpTimeoutMilliseconds = icmpTimeoutMilliseconds;
            TcpTimeoutMilliseconds = tcpTimeoutMilliseconds;
        }

        public IReadOnlyList<IPAddress> Addresses { get; }

        public IReadOnlyList<IPAddress> Exclusions { get; }

        public IReadOnlyList<int> TcpPorts { get; }

        public IReadOnlyList<DiscoverySnmpProfile> SnmpProfiles { get; }

        public int IcmpTimeoutMilliseconds { get; }

        public int TcpTimeoutMilliseconds { get; }
    }
}
