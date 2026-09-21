using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetLoom.Application.Monitoring;

namespace NetLoom.Engine
{
    internal sealed class EngineMultiTargetMonitoringRunner
    {
        private readonly Func<MonitoringPollRequest, MonitoringPollResult>
            _poll;

        private readonly Func<TimeSpan, CancellationToken, Task>
            _delay;

        private readonly object _outputGate;
        private readonly object _completionGate;

        public EngineMultiTargetMonitoringRunner(
            Func<MonitoringPollRequest, MonitoringPollResult> poll,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _poll =
                poll ??
                throw new ArgumentNullException(
                    nameof(poll));

            _delay = delay;

            _outputGate =
                new object();

            _completionGate =
                new object();
        }

        public MultiTargetMonitoringSchedulerRunResult Run(
            IReadOnlyList<MonitoringScheduleTarget> targets,
            MonitoringConcurrencyPolicy concurrency,
            CancellationToken cancellationToken,
            TextWriter writer,
            Action<MonitoringScheduleTarget, MonitoringPollResult>
                onPollCompleted = null,
            Action<MonitoringScheduleTarget>
                onBackpressureSkipped = null)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(
                    nameof(writer));
            }

            var scheduler =
                _delay == null
                    ? new MultiTargetMonitoringScheduler(
                        _poll)
                    : new MultiTargetMonitoringScheduler(
                        _poll,
                        _delay);

            return scheduler.Run(
                targets,
                concurrency,
                cancellationToken,
                target =>
                {
                    lock (_outputGate)
                    {
                        EngineMachineOutput
                            .WriteTargetPollStarted(
                                writer,
                                target);
                    }
                },
                (target, result) =>
                {
                    lock (_outputGate)
                    {
                        EngineMachineOutput
                            .WriteTargetPollCompleted(
                                writer,
                                target,
                                result);
                    }

                    if (onPollCompleted != null)
                    {
                        lock (_completionGate)
                        {
                            onPollCompleted(
                                target,
                                result);
                        }
                    }
                },
                target =>
                {
                    lock (_outputGate)
                    {
                        EngineMachineOutput
                            .WriteTargetBackpressureSkipped(
                                writer,
                                target);
                    }

                    if (onBackpressureSkipped != null)
                    {
                        lock (_completionGate)
                        {
                            onBackpressureSkipped(
                                target);
                        }
                    }
                });
        }
    }
}
