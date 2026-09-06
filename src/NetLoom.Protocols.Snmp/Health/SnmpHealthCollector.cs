using System;
using System.Collections.Generic;
using NetLoom.Application.Monitoring.Health;
using NetLoom.Application.Snmp;

namespace NetLoom.Protocols.Snmp.Health
{
    public sealed class SnmpHealthCollector :
        IHealthCollector
    {
        private const string SysUpTime =
            "1.3.6.1.2.1.1.3.0";

        private readonly ISnmpTransport _transport;
        private readonly Func<DateTime> _utcNow;

        public SnmpHealthCollector(
            ISnmpTransport transport,
            Func<DateTime> utcNow = null)
        {
            _transport =
                transport ??
                throw new ArgumentNullException(
                    nameof(transport));

            _utcNow =
                utcNow ??
                (() => DateTime.UtcNow);
        }

        public HealthSnapshot Collect(
            HealthCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            IReadOnlyList<SnmpVariable> variables =
                _transport.Get(
                    new SnmpGetRequest(
                        request.Address,
                        request.Port,
                        request.Version,
                        request.Credentials,
                        new[]
                        {
                            SysUpTime
                        },
                        request.TimeoutMilliseconds,
                        request.RetryCount));

            var capturedUtc =
                _utcNow();

            if (capturedUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Health collector clock must return UTC.");
            }

            string uptimeText = null;

            foreach (var variable in variables)
            {
                if (string.Equals(
                    variable.Oid,
                    SysUpTime,
                    StringComparison.Ordinal))
                {
                    uptimeText =
                        variable.DisplayValue;

                    break;
                }
            }

            return new HealthSnapshot(
                request.DeviceId,
                request.Address,
                capturedUtc,
                HealthStatus.Up,
                SnmpHealthUptimeParser.Parse(
                    uptimeText));
        }
    }
}
