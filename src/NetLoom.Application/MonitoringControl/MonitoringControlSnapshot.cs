using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.MonitoringControl
{
    public sealed class MonitoringControlSnapshot
    {
        public MonitoringControlSnapshot(
            MonitoringControlState state,
            MonitoringTarget activeTarget,
            DateTime? lastSuccessfulPollUtc,
            string faultMessage,
            MonitoringCycleProgress currentCycle = null,
            MonitoringCycleProgress lastCompletedCycle = null,
            IEnumerable<MonitoringTargetOutcome> targetOutcomes = null)
        {
            if (!Enum.IsDefined(
                typeof(MonitoringControlState),
                state))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state));
            }

            if (lastSuccessfulPollUtc.HasValue &&
                lastSuccessfulPollUtc.Value.Kind !=
                    DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "LAST_SUCCESSFUL_POLL_MUST_BE_UTC",
                    nameof(lastSuccessfulPollUtc));
            }

            State = state;
            ActiveTarget = activeTarget;
            LastSuccessfulPollUtc =
                lastSuccessfulPollUtc;
            FaultMessage = Normalize(
                faultMessage);
            CurrentCycle = currentCycle;
            LastCompletedCycle = lastCompletedCycle;
            TargetOutcomes =
                (targetOutcomes ??
                    Enumerable.Empty<MonitoringTargetOutcome>())
                .Where(
                    outcome => outcome != null)
                .ToArray();
        }

        public MonitoringControlState State { get; }

        public MonitoringTarget ActiveTarget { get; }

        public DateTime? LastSuccessfulPollUtc { get; }

        public string FaultMessage { get; }

        // Sprint 47: прогресс цикла опроса текущего сеанса; null, если сеанс не начинался.
        // После остановки остаются данные последнего сеанса — их показывает итог последнего цикла.
        public MonitoringCycleProgress CurrentCycle { get; }

        public MonitoringCycleProgress LastCompletedCycle { get; }

        public IReadOnlyList<MonitoringTargetOutcome> TargetOutcomes { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
