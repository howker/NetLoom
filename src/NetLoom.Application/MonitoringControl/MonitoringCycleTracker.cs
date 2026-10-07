using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.MonitoringControl
{
    // Sprint 47: цикл опроса из событий отдельных устройств.
    // Расписание набора не знает общего цикла: каждое устройство опрашивается по своему таймеру.
    // Цикл k завершён, когда каждое устройство набора прошло k-ю попытку — опрошено или пропущено.
    // Устройство, ушедшее на следующую попытку раньше других, не сбивает счёт: исходы хранятся по номеру попытки.
    // Класс не потокобезопасен: вызывающий держит свою блокировку.
    public sealed class MonitoringCycleTracker
    {
        private readonly Dictionary<Guid, TargetState> _targets;
        private readonly List<Guid> _order;
        private readonly Dictionary<int, DateTime> _cycleStartedUtc;
        private int _cycleNumber;

        public MonitoringCycleTracker(
            IEnumerable<MonitoringTarget> targets)
            : this(
                targets,
                null)
        {
        }

        // Новый сеанс продолжает серию неудач прошлого сеанса для тех же устройств:
        // Перезапуск мониторинга не должен «устранять» предупреждение «Устройство не отвечает».
        // Перенесённые итоги не входят в счёт цикла — он начинается с нуля.
        public MonitoringCycleTracker(
            IEnumerable<MonitoringTarget> targets,
            IEnumerable<MonitoringTargetOutcome> previousOutcomes)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(
                    nameof(targets));
            }

            _targets =
                new Dictionary<Guid, TargetState>();
            _order =
                new List<Guid>();
            _cycleStartedUtc =
                new Dictionary<int, DateTime>();

            foreach (var target in targets)
            {
                if (target == null)
                {
                    throw new ArgumentException(
                        "MONITORING_TARGET_REQUIRED",
                        nameof(targets));
                }

                if (_targets.ContainsKey(
                        target.DeviceId))
                {
                    throw new ArgumentException(
                        "DUPLICATE_MONITORING_TARGET",
                        nameof(targets));
                }

                _targets.Add(
                    target.DeviceId,
                    new TargetState(
                        target));
                _order.Add(
                    target.DeviceId);
            }

            if (_targets.Count == 0)
            {
                throw new ArgumentException(
                    "MONITORING_TARGET_REQUIRED",
                    nameof(targets));
            }

            _cycleNumber = 1;

            foreach (var previous in
                previousOutcomes ??
                Enumerable.Empty<MonitoringTargetOutcome>())
            {
                TargetState state;

                if (previous == null ||
                    !_targets.TryGetValue(
                        previous.DeviceId,
                        out state))
                {
                    continue;
                }

                state.LastAttemptUtc =
                    previous.LastAttemptUtc;
                state.LastOutcome =
                    previous.LastAttemptSucceeded
                        ? AttemptOutcome.Succeeded
                        : previous.LastAttemptSkipped
                            ? AttemptOutcome.Skipped
                            : AttemptOutcome.Failed;
                state.ConsecutiveFailures =
                    previous.ConsecutiveFailures;
                state.LastSuccessUtc =
                    previous.LastSuccessUtc;
            }
        }

        public MonitoringCycleProgress Current =>
            BuildProgress(
                _cycleNumber,
                null);

        public MonitoringCycleProgress LastCompleted { get; private set; }

        public IReadOnlyList<MonitoringTargetOutcome> Outcomes =>
            _order
                .Select(
                    id => _targets[id])
                .Where(
                    state => state.LastAttemptUtc.HasValue)
                .Select(
                    state =>
                        new MonitoringTargetOutcome(
                            state.Target.DeviceId,
                            state.Target.TargetAddress,
                            state.LastAttemptUtc.Value,
                            state.LastOutcome ==
                                AttemptOutcome.Succeeded,
                            state.LastOutcome ==
                                AttemptOutcome.Skipped,
                            state.ConsecutiveFailures,
                            state.LastSuccessUtc))
                .ToArray();

        public bool TargetStarted(
            Guid deviceId,
            DateTime startedUtc)
        {
            RequireUtc(
                startedUtc,
                nameof(startedUtc));

            TargetState state;

            if (!_targets.TryGetValue(
                    deviceId,
                    out state))
            {
                return false;
            }

            state.InProgressSinceUtc =
                startedUtc;

            MarkCycleStarted(
                state.Attempts + 1,
                startedUtc);

            return false;
        }

        // Возвращает true, если этим событием завершился цикл (см. LastCompleted).
        public bool TargetCompleted(
            Guid deviceId,
            DateTime completedUtc,
            bool anySucceeded)
        {
            RequireUtc(
                completedUtc,
                nameof(completedUtc));

            TargetState state;

            if (!_targets.TryGetValue(
                    deviceId,
                    out state))
            {
                return false;
            }

            // Опрос мог начаться до подписки на события — тогда начало цикла не записано.
            MarkCycleStarted(
                state.Attempts + 1,
                state.InProgressSinceUtc ??
                    completedUtc);

            state.InProgressSinceUtc = null;

            if (anySucceeded)
            {
                state.ConsecutiveFailures = 0;
                state.LastSuccessUtc =
                    completedUtc;
            }
            else
            {
                state.ConsecutiveFailures++;
            }

            return RecordAttempt(
                state,
                anySucceeded
                    ? AttemptOutcome.Succeeded
                    : AttemptOutcome.Failed,
                completedUtc);
        }

        // Пропуск: опрос не выполнялся из-за ограничения одновременных опросов.
        public bool TargetSkipped(
            Guid deviceId,
            DateTime skippedUtc)
        {
            RequireUtc(
                skippedUtc,
                nameof(skippedUtc));

            TargetState state;

            if (!_targets.TryGetValue(
                    deviceId,
                    out state))
            {
                return false;
            }

            MarkCycleStarted(
                state.Attempts + 1,
                skippedUtc);

            return RecordAttempt(
                state,
                AttemptOutcome.Skipped,
                skippedUtc);
        }

        private bool RecordAttempt(
            TargetState state,
            AttemptOutcome outcome,
            DateTime attemptUtc)
        {
            state.Attempts++;
            state.OutcomeByAttempt[
                state.Attempts] =
                    outcome;
            state.LastOutcome = outcome;
            state.LastAttemptUtc = attemptUtc;

            if (_targets.Values.Any(
                    item => item.Attempts < _cycleNumber))
            {
                return false;
            }

            LastCompleted =
                BuildProgress(
                    _cycleNumber,
                    attemptUtc);

            // Исходы и начала завершённых циклов больше не нужны.
            foreach (var item in _targets.Values)
            {
                item.OutcomeByAttempt.Remove(
                    _cycleNumber);
            }

            _cycleStartedUtc.Remove(
                _cycleNumber);

            _cycleNumber++;

            return true;
        }

        private void MarkCycleStarted(
            int cycleNumber,
            DateTime startedUtc)
        {
            DateTime existing;

            if (!_cycleStartedUtc.TryGetValue(
                    cycleNumber,
                    out existing) ||
                startedUtc < existing)
            {
                _cycleStartedUtc[cycleNumber] =
                    startedUtc;
            }
        }

        private MonitoringCycleProgress BuildProgress(
            int cycleNumber,
            DateTime? completedUtc)
        {
            var succeeded = 0;
            var failed = 0;
            var skipped = 0;

            foreach (var state in _targets.Values)
            {
                AttemptOutcome outcome;

                if (!state.OutcomeByAttempt.TryGetValue(
                        cycleNumber,
                        out outcome))
                {
                    continue;
                }

                switch (outcome)
                {
                    case AttemptOutcome.Succeeded:
                        succeeded++;
                        break;

                    case AttemptOutcome.Failed:
                        failed++;
                        break;

                    default:
                        skipped++;
                        break;
                }
            }

            DateTime startedUtc;

            var started =
                _cycleStartedUtc.TryGetValue(
                    cycleNumber,
                    out startedUtc)
                    ? startedUtc
                    : (DateTime?)null;

            return new MonitoringCycleProgress(
                cycleNumber,
                _targets.Count,
                succeeded,
                failed,
                skipped,
                _order
                    .Select(
                        id => _targets[id])
                    .Where(
                        state => state.InProgressSinceUtc.HasValue)
                    .OrderBy(
                        state => state.InProgressSinceUtc.Value)
                    .Select(
                        state => state.Target),
                started,
                completedUtc);
        }

        private static void RequireUtc(
            DateTime value,
            string parameterName)
        {
            if (value.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "CYCLE_TIME_MUST_BE_UTC",
                    parameterName);
            }
        }

        private enum AttemptOutcome
        {
            Succeeded = 0,
            Failed = 1,
            Skipped = 2
        }

        private sealed class TargetState
        {
            public TargetState(
                MonitoringTarget target)
            {
                Target = target;
                OutcomeByAttempt =
                    new Dictionary<int, AttemptOutcome>();
            }

            public MonitoringTarget Target { get; }

            public int Attempts { get; set; }

            public Dictionary<int, AttemptOutcome> OutcomeByAttempt { get; }

            public AttemptOutcome LastOutcome { get; set; }

            public DateTime? LastAttemptUtc { get; set; }

            public DateTime? InProgressSinceUtc { get; set; }

            public int ConsecutiveFailures { get; set; }

            public DateTime? LastSuccessUtc { get; set; }
        }
    }
}
