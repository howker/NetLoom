using System;
using System.Collections.Generic;
using NetLoom.Application.Monitoring.Health;
using NetLoom.Application.Monitoring.Interfaces;

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
                null,
                null)
        {
        }

        public MonitoringPollStepResult(
            MonitoringPollKind kind,
            bool succeeded,
            string errorType,
            string errorMessage,
            HealthSnapshot healthSnapshot)
            : this(
                kind,
                succeeded,
                errorType,
                errorMessage,
                healthSnapshot,
                null)
        {
        }

        public MonitoringPollStepResult(
            MonitoringPollKind kind,
            bool succeeded,
            string errorType,
            string errorMessage,
            HealthSnapshot healthSnapshot,
            IReadOnlyList<InterfaceMonitoringSnapshot> interfaceSnapshots)
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

            if (!succeeded &&
                interfaceSnapshots != null &&
                interfaceSnapshots.Count > 0)
            {
                throw new ArgumentException(
                    "Failed poll step cannot contain Interface snapshots.");
            }

            Kind = kind;
            Succeeded = succeeded;
            ErrorType = Normalize(errorType);
            ErrorMessage = Normalize(errorMessage);
            HealthSnapshot = healthSnapshot;
            InterfaceSnapshots =
                interfaceSnapshots ??
                Array.Empty<InterfaceMonitoringSnapshot>();
        }

        public MonitoringPollKind Kind { get; }

        public bool Succeeded { get; }

        public string ErrorType { get; }

        public string ErrorMessage { get; }

        public HealthSnapshot HealthSnapshot { get; }

        public IReadOnlyList<InterfaceMonitoringSnapshot>
            InterfaceSnapshots { get; }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
