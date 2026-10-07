using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.MonitoringControl
{
    // Прогресс одного цикла опроса: каждое устройство набора опрошено (или пропущено) один раз.
    // Sprint 47: «N / всего», успешно, ошибок, осталось, текущее устройство, начало и конец цикла.
    public sealed class MonitoringCycleProgress
    {
        public MonitoringCycleProgress(
            int cycleNumber,
            int totalTargets,
            int succeeded,
            int failed,
            int skipped,
            IEnumerable<MonitoringTarget> inProgress,
            DateTime? startedUtc,
            DateTime? completedUtc)
        {
            if (cycleNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleNumber));
            }

            if (totalTargets < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalTargets));
            }

            if (succeeded < 0 ||
                failed < 0 ||
                skipped < 0 ||
                succeeded + failed + skipped > totalTargets)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(succeeded),
                    "CYCLE_COUNTS_OUT_OF_RANGE");
            }

            ValidateUtc(
                startedUtc,
                nameof(startedUtc));
            ValidateUtc(
                completedUtc,
                nameof(completedUtc));

            if (completedUtc.HasValue &&
                succeeded + failed + skipped != totalTargets)
            {
                throw new ArgumentException(
                    "COMPLETED_CYCLE_MUST_COVER_ALL_TARGETS",
                    nameof(completedUtc));
            }

            CycleNumber = cycleNumber;
            TotalTargets = totalTargets;
            Succeeded = succeeded;
            Failed = failed;
            Skipped = skipped;
            InProgress =
                (inProgress ??
                    throw new ArgumentNullException(
                        nameof(inProgress)))
                .Where(
                    target => target != null)
                .ToArray();
            StartedUtc = startedUtc;
            CompletedUtc = completedUtc;
        }

        public int CycleNumber { get; }

        public int TotalTargets { get; }

        public int Succeeded { get; }

        public int Failed { get; }

        public int Skipped { get; }

        public int Done =>
            Succeeded +
            Failed +
            Skipped;

        public int Remaining =>
            TotalTargets -
            Done;

        public IReadOnlyList<MonitoringTarget> InProgress { get; }

        public DateTime? StartedUtc { get; }

        public DateTime? CompletedUtc { get; }

        public bool IsCompleted =>
            CompletedUtc.HasValue;

        private static void ValidateUtc(
            DateTime? value,
            string parameterName)
        {
            if (value.HasValue &&
                value.Value.Kind !=
                    DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "CYCLE_TIME_MUST_BE_UTC",
                    parameterName);
            }
        }
    }
}
