using System;
using System.Net;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Observations.Fdb
{
    public sealed class FdbCollectionRequest
    {
        public FdbCollectionRequest(
            IPAddress address,
            int port,
            SnmpVersion version,
            SnmpCredentials credentials,
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
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
            MaxRepetitions = maxRepetitions;
        }

        public IPAddress Address { get; }
        public int Port { get; }
        public SnmpVersion Version { get; }
        public SnmpCredentials Credentials { get; }
        public int TimeoutMilliseconds { get; }
        public int RetryCount { get; }
        public int MaxRepetitions { get; }
    }
}
