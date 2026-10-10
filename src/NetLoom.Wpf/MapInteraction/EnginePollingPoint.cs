using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Lookup;

namespace NetLoom.Wpf.MapInteraction
{
    // ADR-085: точка опроса Engine — устройство, на порту доступа которого виден MAC машины Engine.
    public enum EnginePollingPointStatus
    {
        // Устройство точки опроса определено.
        Determined = 0,
        // MAC машины не виден ни на одном порту.
        NotFound = 1,
        // MAC машины виден на портах доступа нескольких устройств.
        Ambiguous = 2,
        // MAC машины виден только на портах, которые являются концами физических связей.
        NoAccessPort = 3
    }

    public sealed class EnginePollingPointResult
    {
        public EnginePollingPointResult(EnginePollingPointStatus status, Guid? deviceId)
        {
            Status = status;
            DeviceId = deviceId;
        }

        public EnginePollingPointStatus Status { get; }

        // Задано только при статусе Determined.
        public Guid? DeviceId { get; }
    }

    public static class EnginePollingPoint
    {
        // Порт доступа — порт кандидата, который не является концом известной физической связи.
        // Если MAC виден на портах доступа нескольких устройств, точку опроса не угадываем.
        public static EnginePollingPointResult Resolve(
            IEnumerable<MacIpLookupCandidate> candidates,
            ISet<Guid> linkInterfaceIds)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (linkInterfaceIds == null) throw new ArgumentNullException(nameof(linkInterfaceIds));
            var resolved = candidates
                .Where(candidate => candidate != null &&
                    candidate.Status == MacIpLookupCandidateStatus.ResolvedInterface &&
                    candidate.DeviceId.HasValue && candidate.InterfaceId.HasValue)
                .ToArray();
            if (resolved.Length == 0)
                return new EnginePollingPointResult(EnginePollingPointStatus.NotFound, null);
            var accessDevices = resolved
                .Where(candidate => !linkInterfaceIds.Contains(candidate.InterfaceId.Value))
                .Select(candidate => candidate.DeviceId.Value)
                .Distinct()
                .ToArray();
            if (accessDevices.Length == 0)
                return new EnginePollingPointResult(EnginePollingPointStatus.NoAccessPort, null);
            if (accessDevices.Length > 1)
                return new EnginePollingPointResult(EnginePollingPointStatus.Ambiguous, null);
            return new EnginePollingPointResult(EnginePollingPointStatus.Determined, accessDevices[0]);
        }
    }
}
