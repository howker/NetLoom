using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.Monitoring
{
    // Sprint 47: доступность устройства при опросе — отдельно ICMP и TCP, SNMP даёт сам опрос.
    // Не входит в шаги опроса: ответ на ping не делает опрос «успешным», иначе устройство
    // С отказавшим SNMP никогда не получило бы предупреждение «Устройство не отвечает».
    public sealed class MonitoringAvailability
    {
        public MonitoringAvailability(
            bool? icmpReachable,
            IEnumerable<int> checkedTcpPorts,
            IEnumerable<int> openTcpPorts)
        {
            CheckedTcpPorts =
                Normalize(
                    checkedTcpPorts,
                    nameof(checkedTcpPorts));
            OpenTcpPorts =
                Normalize(
                    openTcpPorts,
                    nameof(openTcpPorts));

            if (OpenTcpPorts.Any(
                    port => !CheckedTcpPorts.Contains(port)))
            {
                throw new ArgumentException(
                    "OPEN_TCP_PORT_MUST_BE_CHECKED",
                    nameof(openTcpPorts));
            }

            IcmpReachable = icmpReachable;
        }

        // null — ICMP не проверялся (ошибка зонда или проверка не запускалась).
        public bool? IcmpReachable { get; }

        public IReadOnlyList<int> CheckedTcpPorts { get; }

        public IReadOnlyList<int> OpenTcpPorts { get; }

        private static int[] Normalize(
            IEnumerable<int> ports,
            string parameterName)
        {
            if (ports == null)
            {
                throw new ArgumentNullException(
                    parameterName);
            }

            var result =
                ports
                    .Distinct()
                    .OrderBy(port => port)
                    .ToArray();

            if (result.Any(
                    port => port < 1 || port > 65535))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }

            return result;
        }
    }
}
