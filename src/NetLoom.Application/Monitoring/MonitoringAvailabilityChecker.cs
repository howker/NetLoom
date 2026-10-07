using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using NetLoom.Application.Discovery;

namespace NetLoom.Application.Monitoring
{
    // Sprint 47: проверка ICMP и TCP при каждом опросе тем же зондом, что и у обнаружения.
    // Сбой зонда (не отмена) не превращается в «недоступно»: эта часть остаётся «не проверено».
    public sealed class MonitoringAvailabilityChecker
    {
        // Те же порты управления, что обнаружение проверяет по умолчанию: SSH, HTTP, HTTPS.
        public static readonly IReadOnlyList<int> DefaultTcpPorts =
            new[]
            {
                22,
                80,
                443
            };

        private readonly INetworkDiscoveryProbe _probe;
        private readonly IReadOnlyList<int> _tcpPorts;

        public MonitoringAvailabilityChecker(
            INetworkDiscoveryProbe probe,
            IEnumerable<int> tcpPorts = null)
        {
            _probe =
                probe ??
                throw new ArgumentNullException(
                    nameof(probe));

            _tcpPorts =
                (tcpPorts ?? DefaultTcpPorts)
                    .Distinct()
                    .OrderBy(port => port)
                    .ToArray();

            if (_tcpPorts.Any(
                    port => port < 1 || port > 65535))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tcpPorts));
            }
        }

        public MonitoringAvailability Check(
            IPAddress address,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (address == null)
            {
                throw new ArgumentNullException(
                    nameof(address));
            }

            if (timeoutMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            }

            bool? icmp;

            try
            {
                icmp =
                    _probe.IsIcmpReachable(
                        address,
                        timeoutMilliseconds,
                        cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                icmp = null;
            }

            IReadOnlyList<int> checkedPorts;
            IReadOnlyList<int> openPorts;

            try
            {
                openPorts =
                    _probe.FindOpenTcpPorts(
                        address,
                        _tcpPorts,
                        timeoutMilliseconds,
                        cancellationToken);
                checkedPorts =
                    _tcpPorts;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                checkedPorts =
                    new int[0];
                openPorts =
                    new int[0];
            }

            return new MonitoringAvailability(
                icmp,
                checkedPorts,
                openPorts.Where(
                    checkedPorts.Contains));
        }
    }
}
