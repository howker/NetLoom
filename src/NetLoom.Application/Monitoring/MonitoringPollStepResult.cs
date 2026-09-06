using System;
using NetLoom.Application.Monitoring.Health;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringPollStepResult
    {
        public MonitoringPollStepResult(
            MonitoringPollKind kind,
            bool succeeded,
            string errorType,
            string errorMessage)
            : this(
                kind,
                succeeded,
                errorType,
                errorMessage,
                null)
        {
        }

        public MonitoringPollStepResult(
            MonitoringPollKind kind,
            bool succeeded,
            string errorType,
            string errorMessage,
            HealthSnapshot healthSnapshot)
        {
            if (succeeded &&
                (!string.IsNullOrWhiteSpace(errorType) ||
                 !string.IsNullOrWhiteSpace(errorMessage)))
            {
                throw new ArgumentException(
                    "Successful poll step cannot contain an error.");
            }

            if (!succeeded &&
                healthSnapshot != null)
            {
                throw new ArgumentException(
                    "Failed poll step cannot contain a Health snapshot.");
            }

            Kind = kind;
            Succeeded = succeeded;
            ErrorType = Normalize(errorType);
            ErrorMessage = Normalize(errorMessage);
            HealthSnapshot = healthSnapshot;
        }

        public MonitoringPollKind Kind { get; }

        public bool Succeeded { get; }

        public string ErrorType { get; }

        public string ErrorMessage { get; }

        public HealthSnapshot HealthSnapshot { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
