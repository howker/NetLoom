using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.Alerts
{
    public sealed class TopologyAlertSnapshot
    {
        public TopologyAlertSnapshot(
            DateTime generatedUtc,
            string instanceId,
            IEnumerable<TopologyAlert> alerts)
        {
            if (generatedUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Generated time must be UTC.",
                    nameof(generatedUtc));
            }

            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            if (alerts == null)
            {
                throw new ArgumentNullException(
                    nameof(alerts));
            }

            var normalizedInstanceId =
                instanceId.Trim();

            var snapshot =
                alerts.ToArray();

            if (snapshot.Any(
                alert => alert == null))
            {
                throw new ArgumentException(
                    "Alerts cannot contain null.",
                    nameof(alerts));
            }

            if (snapshot.Any(
                alert =>
                    !string.Equals(
                        alert.InstanceId,
                        normalizedInstanceId,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "All alerts must belong to the snapshot STP instance.",
                    nameof(alerts));
            }

            var duplicateKey =
                snapshot
                    .GroupBy(
                        alert => alert.AlertKey,
                        StringComparer.Ordinal)
                    .FirstOrDefault(
                        group =>
                            group.Count() > 1);

            if (duplicateKey != null)
            {
                throw new ArgumentException(
                    "Alert keys must be unique within a snapshot.",
                    nameof(alerts));
            }

            GeneratedUtc = generatedUtc;
            InstanceId = normalizedInstanceId;

            Alerts =
                snapshot
                    .OrderByDescending(
                        alert =>
                            (int)alert.Severity)
                    .ThenBy(
                        alert =>
                            (int)alert.Kind)
                    .ThenBy(
                        alert =>
                            alert.AlertKey,
                        StringComparer.Ordinal)
                    .ToArray();
        }

        public DateTime GeneratedUtc { get; }

        public string InstanceId { get; }

        public IReadOnlyList<TopologyAlert>
            Alerts { get; }

        public bool HasCritical =>
            Alerts.Any(
                alert =>
                    alert.Severity ==
                    TopologyAlertSeverity.Critical);

        public bool HasWarning =>
            Alerts.Any(
                alert =>
                    alert.Severity ==
                    TopologyAlertSeverity.Warning);
    }
}