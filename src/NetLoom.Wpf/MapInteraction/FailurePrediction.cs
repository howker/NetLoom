using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;

namespace NetLoom.Wpf.MapInteraction
{
    public enum FailurePredictionReason
    {
        None,
        PollingPointUnknown,
        TargetIsPollingPoint,
        TargetNotConnectedToPollingPoint
    }

    public enum FailureImpactCategory
    {
        CutOff,
        StandbyOnly,
        Unconfirmed
    }

    public sealed class FailurePredictionResult
    {
        public FailurePredictionResult(
            bool isDirectional,
            FailurePredictionReason reason,
            IReadOnlyDictionary<Guid, FailureImpactCategory> affectedDevices)
        {
            IsDirectional = isDirectional;
            Reason = reason;
            AffectedDevices = new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, FailureImpactCategory>(
                new SortedDictionary<Guid, FailureImpactCategory>(affectedDevices.ToDictionary(
                    pair => pair.Key, pair => pair.Value)));
            CutOffCount = AffectedDevices.Count(pair => pair.Value == FailureImpactCategory.CutOff);
            StandbyOnlyCount = AffectedDevices.Count(pair => pair.Value == FailureImpactCategory.StandbyOnly);
            UnconfirmedCount = AffectedDevices.Count(pair => pair.Value == FailureImpactCategory.Unconfirmed);
        }

        public bool IsDirectional { get; }
        public FailurePredictionReason Reason { get; }
        public IReadOnlyDictionary<Guid, FailureImpactCategory> AffectedDevices { get; }
        public int CutOffCount { get; }
        public int StandbyOnlyCount { get; }
        public int UnconfirmedCount { get; }
        public bool IsSinglePointOfFailure => CutOffCount > 0;
    }

    public static class FailurePrediction
    {
        private const int Unreachable = 3;

        public static FailurePredictionResult PredictDeviceFailure(
            IEnumerable<PhysicalLinkDiagnostic> links,
            Guid? pollingPointDeviceId,
            Guid failedDeviceId)
        {
            return Predict(Snapshot(links), pollingPointDeviceId, failedDeviceId, null);
        }

        public static FailurePredictionResult PredictLinkFailure(
            IEnumerable<PhysicalLinkDiagnostic> links,
            Guid? pollingPointDeviceId,
            Guid failedPhysicalLinkId)
        {
            return Predict(Snapshot(links), pollingPointDeviceId, null, failedPhysicalLinkId);
        }

        public static IReadOnlyList<Guid> SinglePointsOfFailureDevices(
            IEnumerable<PhysicalLinkDiagnostic> links,
            Guid? pollingPointDeviceId)
        {
            var snapshot = Snapshot(links);
            return snapshot.SelectMany(link => new[] { link.DeviceAId, link.DeviceBId })
                .Distinct().OrderBy(id => id)
                .Where(id => Predict(snapshot, pollingPointDeviceId, id, null).IsSinglePointOfFailure)
                .ToArray();
        }

        public static IReadOnlyList<Guid> SinglePointsOfFailureLinks(
            IEnumerable<PhysicalLinkDiagnostic> links,
            Guid? pollingPointDeviceId)
        {
            var snapshot = Snapshot(links);
            return snapshot.Select(link => link.PhysicalLinkId)
                .Distinct().OrderBy(id => id)
                .Where(id => Predict(snapshot, pollingPointDeviceId, null, id).IsSinglePointOfFailure)
                .ToArray();
        }

        private static PhysicalLinkDiagnostic[] Snapshot(IEnumerable<PhysicalLinkDiagnostic> links)
        {
            if (links == null) throw new ArgumentNullException(nameof(links));
            var snapshot = links.ToArray();
            if (snapshot.Any(link => link == null))
                throw new ArgumentException("Links cannot contain null.", nameof(links));
            return snapshot;
        }

        private static FailurePredictionResult Predict(
            PhysicalLinkDiagnostic[] links,
            Guid? pollingPointDeviceId,
            Guid? failedDeviceId,
            Guid? failedPhysicalLinkId)
        {
            if (!pollingPointDeviceId.HasValue || pollingPointDeviceId.Value == Guid.Empty)
                return Empty(FailurePredictionReason.PollingPointUnknown);
            if (failedDeviceId == pollingPointDeviceId)
                return Empty(FailurePredictionReason.TargetIsPollingPoint);

            var before = Qualities(links, pollingPointDeviceId.Value, null, null);
            if (failedDeviceId.HasValue && !before.ContainsKey(failedDeviceId.Value))
                return Empty(FailurePredictionReason.TargetNotConnectedToPollingPoint);
            if (failedPhysicalLinkId.HasValue)
            {
                var target = links.FirstOrDefault(link => link.PhysicalLinkId == failedPhysicalLinkId.Value);
                if (target != null && !before.ContainsKey(target.DeviceAId) &&
                    !before.ContainsKey(target.DeviceBId))
                    return Empty(FailurePredictionReason.TargetNotConnectedToPollingPoint);
            }
            var after = Qualities(links, pollingPointDeviceId.Value, failedDeviceId, failedPhysicalLinkId);
            var affected = new Dictionary<Guid, FailureImpactCategory>();
            foreach (var pair in before)
            {
                if (pair.Key == failedDeviceId) continue;
                int later;
                if (!after.TryGetValue(pair.Key, out later)) later = Unreachable;
                if (later <= pair.Value) continue;
                affected.Add(pair.Key, later == Unreachable
                    ? FailureImpactCategory.CutOff
                    : later == 2
                        ? FailureImpactCategory.StandbyOnly
                        : FailureImpactCategory.Unconfirmed);
            }
            return new FailurePredictionResult(true, FailurePredictionReason.None, affected);
        }

        private static FailurePredictionResult Empty(FailurePredictionReason reason)
        {
            return new FailurePredictionResult(false, reason,
                new Dictionary<Guid, FailureImpactCategory>());
        }

        private static Dictionary<Guid, int> Qualities(
            PhysicalLinkDiagnostic[] links,
            Guid origin,
            Guid? excludedDeviceId,
            Guid? excludedLinkId)
        {
            var qualities = new Dictionary<Guid, int> { { origin, 0 } };
            for (var limit = 0; limit <= 2; limit++)
            {
                var visited = new HashSet<Guid> { origin };
                var queue = new Queue<Guid>();
                queue.Enqueue(origin);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var link in links)
                    {
                        if (link.PhysicalLinkId == excludedLinkId ||
                            link.DeviceAId == excludedDeviceId ||
                            link.DeviceBId == excludedDeviceId ||
                            LinkQuality(link) > limit) continue;
                        Guid next;
                        if (link.DeviceAId == current) next = link.DeviceBId;
                        else if (link.DeviceBId == current) next = link.DeviceAId;
                        else continue;
                        if (!visited.Add(next)) continue;
                        queue.Enqueue(next);
                        if (!qualities.ContainsKey(next)) qualities.Add(next, limit);
                    }
                }
            }
            return qualities;
        }

        private static int LinkQuality(PhysicalLinkDiagnostic link)
        {
            if (Unavailable(link.StpStateA) || Unavailable(link.StpStateB)) return Unreachable;
            if (link.StpStateA == StpTreePortState.Blocking ||
                link.StpStateB == StpTreePortState.Blocking) return 2;
            return link.StpStateA == StpTreePortState.Forwarding &&
                link.StpStateB == StpTreePortState.Forwarding ? 0 : 1;
        }

        private static bool Unavailable(StpTreePortState state)
        {
            return state == StpTreePortState.Disabled || state == StpTreePortState.Broken;
        }
    }
}
