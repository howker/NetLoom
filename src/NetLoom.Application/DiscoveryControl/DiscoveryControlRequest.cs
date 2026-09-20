using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Domain.Access;

namespace NetLoom.Application.DiscoveryControl
{
    public sealed class DiscoveryControlRequest
    {
        private static readonly int[] DefaultTcpPorts =
        {
            22,
            80,
            443
        };

        public DiscoveryControlRequest(
            string cidr,
            Guid accessProfileId,
            SnmpVersion version)
            : this(
                cidr,
                accessProfileId,
                version,
                161,
                750,
                0,
                10,
                500,
                500,
                DefaultTcpPorts,
                50,
                4096)
        {
        }

        public DiscoveryControlRequest(
            string cidr,
            Guid accessProfileId,
            SnmpVersion version,
            int port,
            int timeoutMilliseconds,
            int retryCount,
            int maxRepetitions,
            int icmpTimeoutMilliseconds,
            int tcpTimeoutMilliseconds,
            IEnumerable<int> tcpPorts,
            int interAddressDelayMilliseconds,
            int maxAddresses)
        {
            if (string.IsNullOrWhiteSpace(cidr))
            {
                throw new ArgumentException(
                    "DISCOVERY_CIDR_REQUIRED",
                    nameof(cidr));
            }

            if (accessProfileId == Guid.Empty)
            {
                throw new ArgumentException(
                    "DISCOVERY_ACCESS_PROFILE_ID_REQUIRED",
                    nameof(accessProfileId));
            }

            if (!Enum.IsDefined(
                typeof(SnmpVersion),
                version))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(version));
            }

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port));
            }

            if (timeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            }

            if (retryCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(retryCount));
            }

            if (maxRepetitions < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRepetitions));
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

            if (tcpPorts == null)
            {
                throw new ArgumentNullException(
                    nameof(tcpPorts));
            }

            var portArray =
                tcpPorts
                    .Distinct()
                    .ToArray();

            if (portArray.Length == 0 ||
                portArray.Any(
                    candidate =>
                        candidate < 1 ||
                        candidate > 65535))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tcpPorts));
            }

            if (interAddressDelayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interAddressDelayMilliseconds));
            }

            if (maxAddresses < 1 ||
                maxAddresses > 65536)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxAddresses));
            }

            Cidr = cidr.Trim();
            AccessProfileId = accessProfileId;
            Version = version;
            Port = port;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
            MaxRepetitions = maxRepetitions;
            IcmpTimeoutMilliseconds = icmpTimeoutMilliseconds;
            TcpTimeoutMilliseconds = tcpTimeoutMilliseconds;
            TcpPorts = portArray;
            InterAddressDelayMilliseconds =
                interAddressDelayMilliseconds;
            MaxAddresses = maxAddresses;
        }

        public string Cidr { get; }

        public Guid AccessProfileId { get; }

        public SnmpVersion Version { get; }

        public int Port { get; }

        public int TimeoutMilliseconds { get; }

        public int RetryCount { get; }

        public int MaxRepetitions { get; }

        public int IcmpTimeoutMilliseconds { get; }

        public int TcpTimeoutMilliseconds { get; }

        public IReadOnlyList<int> TcpPorts { get; }

        public int InterAddressDelayMilliseconds { get; }

        public int MaxAddresses { get; }
    }
}
