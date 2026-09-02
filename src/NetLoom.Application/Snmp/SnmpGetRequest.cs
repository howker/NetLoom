using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Snmp
{
    public sealed class SnmpGetRequest
    {
        public SnmpGetRequest(
            IPAddress address,
            int port,
            SnmpVersion version,
            SnmpCredentials credentials,
            IEnumerable<string> oids,
            int timeoutMilliseconds,
            int retryCount)
        {
            Address = address ??
                throw new ArgumentNullException(nameof(address));

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (oids == null)
            {
                throw new ArgumentNullException(nameof(oids));
            }

            if (timeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            }

            if (retryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retryCount));
            }

            var oidArray = oids.ToArray();

            if (oidArray.Length == 0)
            {
                throw new ArgumentException(
                    "At least one OID is required.",
                    nameof(oids));
            }

            if (oidArray.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "OID cannot be empty.",
                    nameof(oids));
            }

            Port = port;
            Version = version;
            Credentials = credentials;
            Oids = oidArray;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
        }

        public IPAddress Address { get; }

        public int Port { get; }

        public SnmpVersion Version { get; }

        public SnmpCredentials Credentials { get; }

        public IReadOnlyList<string> Oids { get; }

        public int TimeoutMilliseconds { get; }

        public int RetryCount { get; }
    }
}
