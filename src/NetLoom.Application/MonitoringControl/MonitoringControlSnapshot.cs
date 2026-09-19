using System;

namespace NetLoom.Application.MonitoringControl
{
    public sealed class MonitoringControlSnapshot
    {
        public MonitoringControlSnapshot(
            MonitoringControlState state,
            MonitoringTarget activeTarget,
            DateTime? lastSuccessfulPollUtc,
            string faultMessage)
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
        }

        public MonitoringControlState State { get; }

        public MonitoringTarget ActiveTarget { get; }

        public DateTime? LastSuccessfulPollUtc { get; }

        public string FaultMessage { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
