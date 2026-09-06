using System;
using System.Net;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Monitoring.Health
{
    public sealed class HealthCollectionRequest
    {
        public HealthCollectionRequest(
            Guid? deviceId,
            IPAddress address,
            int port,
            SnmpVersion version,
            SnmpCredentials credentials,
            int timeoutMilliseconds,
            int retryCount)
        {
            if (deviceId.HasValue &&
                deviceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Health DeviceId cannot be empty.",
                    nameof(deviceId));
            }

            Address =
                address ??
                throw new ArgumentNullException(
                    nameof(address));

            Credentials =
                credentials ??
                throw new ArgumentNullException(
                    nameof(credentials));

            if (port < 1 ||
                port > 65535)
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

            DeviceId = deviceId;
            Port = port;
            Version = version;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
        }

        public Guid? DeviceId { get; }

        public IPAddress Address { get; }

        public int Port { get; }

        public SnmpVersion Version { get; }

        public SnmpCredentials Credentials { get; }

        public int TimeoutMilliseconds { get; }

        public int RetryCount { get; }
    }
}
