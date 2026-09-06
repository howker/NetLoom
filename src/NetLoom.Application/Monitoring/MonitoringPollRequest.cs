using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringPollRequest
    {
        public MonitoringPollRequest(
            IPAddress address,
            int port,
            SnmpVersion version,
            SnmpCredentials credentials,
            int timeoutMilliseconds,
            int retryCount,
            int maxRepetitions,
            IEnumerable<MonitoringPollKind> kinds)
        {
            Address = address ??
                throw new ArgumentNullException(nameof(address));

            Credentials = credentials ??
                throw new ArgumentNullException(nameof(credentials));

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
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

            if (kinds == null)
            {
                throw new ArgumentNullException(nameof(kinds));
            }

            var selectedKinds =
                kinds
                    .Distinct()
                    .ToArray();

            if (selectedKinds.Length == 0)
            {
                throw new ArgumentException(
                    "At least one monitoring poll kind is required.",
                    nameof(kinds));
            }

            Port = port;
            Version = version;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
            MaxRepetitions = maxRepetitions;
            Kinds = selectedKinds;
        }

        public IPAddress Address { get; }

        public int Port { get; }

        public SnmpVersion Version { get; }

        public SnmpCredentials Credentials { get; }

        public int TimeoutMilliseconds { get; }

        public int RetryCount { get; }

        public int MaxRepetitions { get; }

        public IReadOnlyList<MonitoringPollKind> Kinds { get; }
    }
}
