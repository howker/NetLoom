using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Contracts.Alerts
{
    public enum TopologyAlertSeverity
    {
        Unknown = 0,
        Warning = 1,
        Critical = 2
    }

    public enum TopologyAlertKind
    {
        Unknown = 0,
        ForwardingCycle = 1,
        RingProtectionDegraded = 2,

        // Sprint 47 (Г3 полевой проверки Sprint 46): опрашиваемое устройство перестало отвечать.
        DeviceUnreachable = 3
    }

    public enum TopologyAlertReason
    {
        Unknown = 0,
        ConfirmedForwardingCycle = 1,
        DisabledRingLink = 2,
        MultipleBlockingRingLinks = 3,
        NoPollResponse = 4
    }

    public sealed class TopologyAlert
    {
        public TopologyAlert(
            string alertKey,
            TopologyAlertKind kind,
            TopologyAlertSeverity severity,
            string instanceId,
            IEnumerable<string> relatedRegionKeys,
            IEnumerable<Guid> physicalLinkIds,
            IEnumerable<TopologyAlertReason> reasons)
            : this(
                alertKey,
                kind,
                severity,
                instanceId,
                relatedRegionKeys,
                physicalLinkIds,
                reasons,
                Enumerable.Empty<Guid>())
        {
        }

        // Предупреждение может касаться устройства, а не связи (DeviceUnreachable): тогда связей нет,
        // А устройство названо в deviceIds.
        public TopologyAlert(
            string alertKey,
            TopologyAlertKind kind,
            TopologyAlertSeverity severity,
            string instanceId,
            IEnumerable<string> relatedRegionKeys,
            IEnumerable<Guid> physicalLinkIds,
            IEnumerable<TopologyAlertReason> reasons,
            IEnumerable<Guid> deviceIds)
        {
            if (string.IsNullOrWhiteSpace(
                alertKey))
            {
                throw new ArgumentException(
                    "Alert key is required.",
                    nameof(alertKey));
            }

            if (kind ==
                TopologyAlertKind.Unknown)
            {
                throw new ArgumentException(
                    "Alert kind must be known.",
                    nameof(kind));
            }

            if (severity ==
                TopologyAlertSeverity.Unknown)
            {
                throw new ArgumentException(
                    "Alert severity must be known.",
                    nameof(severity));
            }

            if (string.IsNullOrWhiteSpace(
                instanceId))
            {
                throw new ArgumentException(
                    "STP instance id is required.",
                    nameof(instanceId));
            }

            if (relatedRegionKeys == null)
            {
                throw new ArgumentNullException(
                    nameof(relatedRegionKeys));
            }

            if (physicalLinkIds == null)
            {
                throw new ArgumentNullException(
                    nameof(physicalLinkIds));
            }

            if (reasons == null)
            {
                throw new ArgumentNullException(
                    nameof(reasons));
            }

            if (deviceIds == null)
            {
                throw new ArgumentNullException(
                    nameof(deviceIds));
            }

            AlertKey = alertKey.Trim();
            Kind = kind;
            Severity = severity;
            InstanceId = instanceId.Trim();

            RelatedRegionKeys =
                NormalizeRegionKeys(
                    relatedRegionKeys);

            PhysicalLinkIds =
                NormalizePhysicalLinkIds(
                    physicalLinkIds);

            Reasons =
                NormalizeReasons(
                    reasons);
            DeviceIds =
                NormalizeIds(
                    deviceIds,
                    "Device ids cannot be empty.");

            if (PhysicalLinkIds.Count == 0 &&
                Kind != TopologyAlertKind.DeviceUnreachable)
            {
                throw new ArgumentException(
                    "Alert must reference at least one physical link.",
                    nameof(physicalLinkIds));
            }

            if (Reasons.Count == 0)
            {
                throw new ArgumentException(
                    "Alert must contain at least one reason.",
                    nameof(reasons));
            }

            ValidateKindSeverity();
        }

        public string AlertKey { get; }

        public TopologyAlertKind Kind { get; }

        public TopologyAlertSeverity Severity { get; }

        public string InstanceId { get; }

        public IReadOnlyList<string>
            RelatedRegionKeys { get; }

        public IReadOnlyList<Guid>
            PhysicalLinkIds { get; }

        public IReadOnlyList<TopologyAlertReason>
            Reasons { get; }

        public IReadOnlyList<Guid>
            DeviceIds { get; }

        private void ValidateKindSeverity()
        {
            if (Kind ==
                    TopologyAlertKind.ForwardingCycle &&
                Severity !=
                    TopologyAlertSeverity.Critical)
            {
                throw new ArgumentException(
                    "Forwarding-cycle alert must be Critical.");
            }

            if (Kind ==
                    TopologyAlertKind.RingProtectionDegraded &&
                Severity !=
                    TopologyAlertSeverity.Warning)
            {
                throw new ArgumentException(
                    "Degraded-ring alert must be Warning.");
            }

            if (Kind ==
                    TopologyAlertKind.RingProtectionDegraded &&
                RelatedRegionKeys.Count != 1)
            {
                throw new ArgumentException(
                    "Degraded-ring alert must reference exactly one region.");
            }

            if (Kind ==
                    TopologyAlertKind.DeviceUnreachable &&
                (Severity !=
                     TopologyAlertSeverity.Warning ||
                 DeviceIds.Count != 1))
            {
                throw new ArgumentException(
                    "Unreachable-device alert must be a Warning about exactly one device.");
            }
        }

        private static string[] NormalizeRegionKeys(
            IEnumerable<string> values)
        {
            var result =
                values
                    .Select(
                        value =>
                            string.IsNullOrWhiteSpace(value)
                                ? null
                                : value.Trim())
                    .ToArray();

            if (result.Any(
                value => value == null))
            {
                throw new ArgumentException(
                    "Related region keys cannot be blank.");
            }

            return result
                .Distinct(
                    StringComparer.Ordinal)
                .OrderBy(
                    value => value,
                    StringComparer.Ordinal)
                .ToArray();
        }

        private static Guid[] NormalizePhysicalLinkIds(
            IEnumerable<Guid> values)
        {
            return NormalizeIds(
                values,
                "Physical link ids cannot be empty.");
        }

        private static Guid[] NormalizeIds(
            IEnumerable<Guid> values,
            string emptyMessage)
        {
            var result =
                values
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

            if (result.Any(
                id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    emptyMessage);
            }

            return result;
        }

        private static TopologyAlertReason[] NormalizeReasons(
            IEnumerable<TopologyAlertReason> values)
        {
            var result =
                values
                    .Distinct()
                    .OrderBy(
                        value => (int)value)
                    .ToArray();

            if (result.Any(
                value =>
                    value ==
                    TopologyAlertReason.Unknown))
            {
                throw new ArgumentException(
                    "Alert reasons must be known.");
            }

            return result;
        }
    }
}