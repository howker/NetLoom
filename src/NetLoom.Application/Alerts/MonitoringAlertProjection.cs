using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.MonitoringControl;
using NetLoom.Contracts.Alerts;

namespace NetLoom.Application.Alerts
{
    // Sprint 47 (Г3 полевой проверки Sprint 46): предупреждение «Устройство не отвечает» из итогов опроса.
    // Источник — текущий сеанс мониторинга, а не база: неудачные опросы не сохраняются.
    // Предупреждения схемы и опроса объединяются в один снимок, чтобы карточки, счётчик, лента,
    // «Оборудование», карта и инспектор брали проблему из одного источника (UI_DESIGN_RULES §2).
    public static class MonitoringAlertProjection
    {
        // Один пропущенный ответ бывает и в здоровой сети: тревога — после двух опросов подряд без ответа.
        // Один таймаут устройство не удаляет и предупреждения не поднимает (AI_DEVELOPMENT_RULES).
        public const int ConsecutiveFailuresForAlert = 2;

        public static IReadOnlyList<TopologyAlert> BuildUnreachableAlerts(
            string instanceId,
            IEnumerable<MonitoringTargetOutcome> outcomes)
        {
            if (string.IsNullOrWhiteSpace(
                    instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            if (outcomes == null)
            {
                throw new ArgumentNullException(
                    nameof(outcomes));
            }

            return outcomes
                .Where(
                    outcome =>
                        outcome != null &&
                        outcome.ConsecutiveFailures >=
                            ConsecutiveFailuresForAlert)
                .GroupBy(
                    outcome => outcome.DeviceId)
                .Select(
                    group =>
                        new TopologyAlert(
                            UnreachableAlertKey(
                                group.Key),
                            TopologyAlertKind.DeviceUnreachable,
                            TopologyAlertSeverity.Warning,
                            instanceId,
                            Enumerable.Empty<string>(),
                            Enumerable.Empty<Guid>(),
                            new[]
                            {
                                TopologyAlertReason.NoPollResponse
                            },
                            new[]
                            {
                                group.Key
                            }))
                .ToArray();
        }

        public static TopologyAlertSnapshot Merge(
            TopologyAlertSnapshot topology,
            IEnumerable<MonitoringTargetOutcome> outcomes)
        {
            if (topology == null)
            {
                throw new ArgumentNullException(
                    nameof(topology));
            }

            var unreachable =
                BuildUnreachableAlerts(
                    topology.InstanceId,
                    outcomes ??
                    Enumerable.Empty<MonitoringTargetOutcome>());

            if (unreachable.Count == 0)
            {
                return topology;
            }

            return new TopologyAlertSnapshot(
                topology.GeneratedUtc,
                topology.InstanceId,
                topology.Alerts
                    .Concat(
                        unreachable));
        }

        public static string UnreachableAlertKey(
            Guid deviceId)
        {
            return "device-unreachable|" +
                   deviceId.ToString("D");
        }
    }
}
