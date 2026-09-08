using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.GraphSafety;
using NetLoom.Contracts.Rings;

namespace NetLoom.Topology.Alerts
{
    public sealed class TopologyAlertEvaluator
    {
        public TopologyAlertSnapshot Evaluate(
            DateTime generatedUtc,
            ForwardingCycleAnalysis
                forwardingCycleAnalysis,
            IEnumerable<RingProtectionAnalysis>
                ringProtectionAnalyses)
        {
            if (forwardingCycleAnalysis == null)
            {
                throw new ArgumentNullException(
                    nameof(forwardingCycleAnalysis));
            }

            if (ringProtectionAnalyses == null)
            {
                throw new ArgumentNullException(
                    nameof(ringProtectionAnalyses));
            }

            var rings =
                ringProtectionAnalyses
                    .ToArray();

            if (rings.Any(
                ring => ring == null))
            {
                throw new ArgumentException(
                    "Ring protection analyses cannot contain null.",
                    nameof(ringProtectionAnalyses));
            }

            var instanceId =
                forwardingCycleAnalysis.InstanceId;

            if (rings.Any(
                ring =>
                    !string.Equals(
                        ring.InstanceId,
                        instanceId,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Forwarding-cycle and ring-protection analyses must use the same STP instance.",
                    nameof(ringProtectionAnalyses));
            }

            var duplicateRegion =
                rings
                    .GroupBy(
                        ring => ring.RegionKey,
                        StringComparer.Ordinal)
                    .FirstOrDefault(
                        group =>
                            group.Count() > 1);

            if (duplicateRegion != null)
            {
                throw new ArgumentException(
                    "Ring protection analyses must contain at most one result per region.",
                    nameof(ringProtectionAnalyses));
            }

            var cycleIds =
                new HashSet<Guid>(
                    forwardingCycleAnalysis
                        .ConfirmedCyclePhysicalLinkIds);

            ValidateUnprotectedRings(
                rings,
                cycleIds);

            var alerts =
                new List<TopologyAlert>();

            if (forwardingCycleAnalysis
                .HasConfirmedForwardingCycle)
            {
                var relatedRegions =
                    rings
                        .Where(
                            ring =>
                                ring.Status ==
                                RingProtectionStatus.Unprotected)
                        .Select(
                            ring =>
                                ring.RegionKey);

                alerts.Add(
                    new TopologyAlert(
                        BuildAlertKey(
                            TopologyAlertKind.ForwardingCycle,
                            instanceId,
                            null,
                            forwardingCycleAnalysis
                                .ConfirmedCyclePhysicalLinkIds),
                        TopologyAlertKind.ForwardingCycle,
                        TopologyAlertSeverity.Critical,
                        instanceId,
                        relatedRegions,
                        forwardingCycleAnalysis
                            .ConfirmedCyclePhysicalLinkIds,
                        new[]
                        {
                            TopologyAlertReason
                                .ConfirmedForwardingCycle
                        }));
            }

            foreach (var ring in
                rings
                    .Where(
                        item =>
                            item.Status ==
                            RingProtectionStatus.Degraded)
                    .OrderBy(
                        item =>
                            item.RegionKey,
                        StringComparer.Ordinal))
            {
                var reasons =
                    new List<TopologyAlertReason>();

                if (ring.DisabledPhysicalLinkIds.Count > 0)
                {
                    reasons.Add(
                        TopologyAlertReason
                            .DisabledRingLink);
                }

                if (ring.BlockingPhysicalLinkIds.Count > 1)
                {
                    reasons.Add(
                        TopologyAlertReason
                            .MultipleBlockingRingLinks);
                }

                if (reasons.Count == 0)
                {
                    throw new ArgumentException(
                        "Degraded ring analysis has no disabled link or multiple blocking links.",
                        nameof(ringProtectionAnalyses));
                }

                var physicalLinkIds =
                    ring.DisabledPhysicalLinkIds
                        .Concat(
                            ring.BlockingPhysicalLinkIds);

                alerts.Add(
                    new TopologyAlert(
                        BuildAlertKey(
                            TopologyAlertKind
                                .RingProtectionDegraded,
                            instanceId,
                            ring.RegionKey,
                            null),
                        TopologyAlertKind
                            .RingProtectionDegraded,
                        TopologyAlertSeverity.Warning,
                        instanceId,
                        new[]
                        {
                            ring.RegionKey
                        },
                        physicalLinkIds,
                        reasons));
            }

            return new TopologyAlertSnapshot(
                generatedUtc,
                instanceId,
                alerts);
        }

        private static void ValidateUnprotectedRings(
            IEnumerable<RingProtectionAnalysis> rings,
            ISet<Guid> confirmedCycleIds)
        {
            foreach (var ring in rings)
            {
                if (ring.Status !=
                    RingProtectionStatus.Unprotected)
                {
                    continue;
                }

                if (!ring.IsComplete ||
                    ring.ForwardingPhysicalLinkIds.Count == 0 ||
                    ring.ForwardingPhysicalLinkIds.Any(
                        id =>
                            !confirmedCycleIds.Contains(id)))
                {
                    throw new ArgumentException(
                        "Unprotected ring must be independently confirmed by forwarding-cycle analysis from the same snapshot.",
                        nameof(rings));
                }
            }
        }

        private static string BuildAlertKey(
            TopologyAlertKind kind,
            string instanceId,
            string regionKey,
            IEnumerable<Guid> physicalLinkIds)
        {
            var canonical =
                new StringBuilder();

            canonical.Append(
                ((int)kind).ToString(
                    System.Globalization
                        .CultureInfo.InvariantCulture));

            canonical.Append('\n');
            AppendString(
                canonical,
                instanceId);

            canonical.Append('\n');
            AppendString(
                canonical,
                regionKey);

            canonical.Append('\n');

            if (physicalLinkIds != null)
            {
                foreach (var id in
                    physicalLinkIds
                        .Distinct()
                        .OrderBy(value => value))
                {
                    canonical.Append(
                        id.ToString("D"));

                    canonical.Append('\n');
                }
            }

            byte[] hash;

            using (var sha256 =
                SHA256.Create())
            {
                hash =
                    sha256.ComputeHash(
                        Encoding.UTF8.GetBytes(
                            canonical.ToString()));
            }

            return
                "alert-v1-" +
                BitConverter
                    .ToString(hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
        }

        private static void AppendString(
            StringBuilder builder,
            string value)
        {
            if (value == null)
            {
                builder.Append("-1:");
                return;
            }

            builder.Append(
                value.Length.ToString(
                    System.Globalization
                        .CultureInfo.InvariantCulture));

            builder.Append(':');
            builder.Append(value);
        }
    }
}