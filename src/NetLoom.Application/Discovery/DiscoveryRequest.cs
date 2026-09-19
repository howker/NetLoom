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
            DiscoverySnmpProfile snmpProfile,
            int icmpTimeoutMilliseconds,
            int tcpTimeoutMilliseconds,
            int interAddressDelayMilliseconds)
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

            SnmpProfile = snmpProfile ??
                throw new ArgumentNullException(nameof(snmpProfile));

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

            if (interAddressDelayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interAddressDelayMilliseconds));
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
            IcmpTimeoutMilliseconds = icmpTimeoutMilliseconds;
            TcpTimeoutMilliseconds = tcpTimeoutMilliseconds;
            InterAddressDelayMilliseconds =
                interAddressDelayMilliseconds;
        }

        public IReadOnlyList<IPAddress> Addresses { get; }

        public IReadOnlyList<IPAddress> Exclusions { get; }

        public IReadOnlyList<int> TcpPorts { get; }

        public DiscoverySnmpProfile SnmpProfile { get; }

        public int IcmpTimeoutMilliseconds { get; }

        public int TcpTimeoutMilliseconds { get; }

        public int InterAddressDelayMilliseconds { get; }
    }
}
