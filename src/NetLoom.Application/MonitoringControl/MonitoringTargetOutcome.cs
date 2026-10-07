using System;
using System.Net;

namespace NetLoom.Application.MonitoringControl
{
    // Итог последней попытки опроса одного устройства в текущем сеансе мониторинга.
    // Не сохраняется в базу: после перезапуска сеанс начинается заново.
    public sealed class MonitoringTargetOutcome
    {
        public MonitoringTargetOutcome(
            Guid deviceId,
            IPAddress targetAddress,
            DateTime lastAttemptUtc,
            bool lastAttemptSucceeded,
            bool lastAttemptSkipped,
            int consecutiveFailures,
            DateTime? lastSuccessUtc)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "DEVICE_ID_REQUIRED",
                    nameof(deviceId));
            }

            if (lastAttemptUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "LAST_ATTEMPT_MUST_BE_UTC",
                    nameof(lastAttemptUtc));
            }

            if (lastSuccessUtc.HasValue &&
                lastSuccessUtc.Value.Kind !=
                    DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "LAST_SUCCESS_MUST_BE_UTC",
                    nameof(lastSuccessUtc));
            }

            if (consecutiveFailures < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(consecutiveFailures));
            }

            DeviceId = deviceId;
            TargetAddress = targetAddress;
            LastAttemptUtc = lastAttemptUtc;
            LastAttemptSucceeded = lastAttemptSucceeded;
            LastAttemptSkipped = lastAttemptSkipped;
            ConsecutiveFailures = consecutiveFailures;
            LastSuccessUtc = lastSuccessUtc;
        }

        public Guid DeviceId { get; }

        public IPAddress TargetAddress { get; }

        public DateTime LastAttemptUtc { get; }

        public bool LastAttemptSucceeded { get; }

        // Опрос пропущен из-за ограничения одновременных опросов: устройство не спрашивали.
        public bool LastAttemptSkipped { get; }

        // Подряд идущие опросы без единого успешного шага; пропуск счёт не меняет.
        public int ConsecutiveFailures { get; }

        // Последний успешный опрос в этом сеансе; более раннее время знает база.
        public DateTime? LastSuccessUtc { get; }
    }
}
