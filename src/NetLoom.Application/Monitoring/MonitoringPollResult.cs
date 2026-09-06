using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringPollResult
    {
        public MonitoringPollResult(
            IPAddress address,
            DateTime startedUtc,
            DateTime completedUtc,
            IEnumerable<MonitoringPollStepResult> steps)
        {
            Address = address ??
                throw new ArgumentNullException(nameof(address));

            RequireUtc(startedUtc, nameof(startedUtc));
            RequireUtc(completedUtc, nameof(completedUtc));

            if (completedUtc < startedUtc)
            {
                throw new ArgumentException(
                    "Completion time cannot be before start time.",
                    nameof(completedUtc));
            }

            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            var snapshot =
                steps.ToArray();

            if (snapshot.Length == 0)
            {
                throw new ArgumentException(
                    "At least one poll step result is required.",
                    nameof(steps));
            }

            StartedUtc = startedUtc;
            CompletedUtc = completedUtc;
            Steps = snapshot;
        }

        public IPAddress Address { get; }

        public DateTime StartedUtc { get; }

        public DateTime CompletedUtc { get; }

        public IReadOnlyList<MonitoringPollStepResult> Steps { get; }

        public bool AnySucceeded
        {
            get
            {
                return Steps.Any(step => step.Succeeded);
            }
        }

        public bool AllSucceeded
        {
            get
            {
                return Steps.All(step => step.Succeeded);
            }
        }

        private static void RequireUtc(
            DateTime value,
            string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    parameterName);
            }
        }
    }
}
