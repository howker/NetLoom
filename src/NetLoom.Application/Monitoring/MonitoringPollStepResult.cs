using System;

namespace NetLoom.Application.Monitoring
{
    public sealed class MonitoringPollStepResult
    {
        public MonitoringPollStepResult(
            MonitoringPollKind kind,
            bool succeeded,
            string errorType,
            string errorMessage)
        {
            if (succeeded &&
                (!string.IsNullOrWhiteSpace(errorType) ||
                 !string.IsNullOrWhiteSpace(errorMessage)))
            {
                throw new ArgumentException(
                    "Successful poll step cannot contain an error.");
            }

            Kind = kind;
            Succeeded = succeeded;
            ErrorType = Normalize(errorType);
            ErrorMessage = Normalize(errorMessage);
        }

        public MonitoringPollKind Kind { get; }

        public bool Succeeded { get; }

        public string ErrorType { get; }

        public string ErrorMessage { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
