using System;
using System.Net;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Snmp
{
    public sealed class SnmpWalkRequest
    {
        public SnmpWalkRequest(
            IPAddress address,
            int port,
            SnmpVersion version,
            SnmpCredentials credentials,
            string rootOid,
            int timeoutMilliseconds,
            int retryCount,
            int maxRepetitions)
        {
            Address = address ??
                throw new ArgumentNullException(nameof(address));

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            Credentials = credentials ??
                throw new ArgumentNullException(nameof(credentials));

            if (string.IsNullOrWhiteSpace(rootOid))
            {
                throw new ArgumentException(
                    "Root OID is required.",
                    nameof(rootOid));
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

            if (maxRepetitions < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRepetitions));
            }

            Port = port;
            Version = version;
            RootOid = rootOid;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
            MaxRepetitions = maxRepetitions;
        }

        public IPAddress Address { get; }

        public int Port { get; }

        public SnmpVersion Version { get; }

        public SnmpCredentials Credentials { get; }

        public string RootOid { get; }

        public int TimeoutMilliseconds { get; }

        public int RetryCount { get; }

        public int MaxRepetitions { get; }
    }
}
