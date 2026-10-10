using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NetLoom.Application.Monitoring;
using NetLoom.Application.PollingPolicies;

namespace NetLoom.Engine
{
    internal sealed class EnginePolicyTargetSet
    {
        public IReadOnlyList<MonitoringScheduleTarget> Targets { get; }
        public IReadOnlyList<Guid> DisabledDeviceIds { get; }
        public IReadOnlyList<Guid> NothingToPollDeviceIds { get; }

        public EnginePolicyTargetSet(IReadOnlyList<MonitoringScheduleTarget> targets,
            IReadOnlyList<Guid> disabledDeviceIds, IReadOnlyList<Guid> nothingToPollDeviceIds)
        {
            Targets = targets;
            DisabledDeviceIds = disabledDeviceIds;
            NothingToPollDeviceIds = nothingToPollDeviceIds;
        }
    }

    internal sealed class EnginePollingPolicyGate
    {
        private readonly PollingPolicyResolver _resolver;
        private readonly TimeSpan _generalInterval;

        public EnginePollingPolicyGate(PollingPolicyResolver resolver, TimeSpan generalInterval)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            if (generalInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(generalInterval));
            _generalInterval = generalInterval;
        }

        public EnginePolicyTargetSet Apply(IReadOnlyList<MonitoringScheduleTarget> targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            var enabled = new List<MonitoringScheduleTarget>();
            var disabled = new List<Guid>();
            var empty = new List<Guid>();
            foreach (var target in targets)
            {
                var policy = _resolver.ResolveDevice(target.DeviceId).Policy;
                if (!policy.ActivePolling) { disabled.Add(target.DeviceId); continue; }
                var groups = PollingPolicySchedule.BuildGroups(policy, _generalInterval, target.Request.Kinds);
                if (groups.Count == 0) { empty.Add(target.DeviceId); continue; }
                enabled.Add(new MonitoringScheduleTarget(target.Request, groups, target.InitialDelay));
            }
            return new EnginePolicyTargetSet(enabled.AsReadOnly(), disabled.AsReadOnly(), empty.AsReadOnly());
        }

        public bool IsPollingDisabled(Guid? deviceId) =>
            deviceId.HasValue && !_resolver.ResolveDevice(deviceId.Value).Policy.ActivePolling;

        public IReadOnlyList<int> TcpPortsFor(Guid? deviceId) =>
            deviceId.HasValue ? _resolver.ResolveDevice(deviceId.Value).Policy.TcpPorts : _resolver.Default.TcpPorts;

        public static Func<MonitoringPollRequest, MonitoringPollResult> ComposePoll(
            Func<MonitoringPollRequest, MonitoringPollResult> snmpPoll,
            MonitoringAvailabilityChecker checker, EnginePollingPolicyGate gate)
        {
            if (snmpPoll == null) throw new ArgumentNullException(nameof(snmpPoll));
            if (checker == null) throw new ArgumentNullException(nameof(checker));
            if (gate == null) throw new ArgumentNullException(nameof(gate));
            return request =>
            {
                // Режим «без активного опроса» соблюдает сам Engine: устройству не уходят SNMP, ICMP и TCP; сведения от соседей принимаются как обычно (ADR-087).
                if (gate.IsPollingDisabled(request.DeviceId))
                    throw new InvalidOperationException("POLLING_DISABLED_BY_POLICY");
                return snmpPoll(request).WithAvailability(checker.Check(request.Address,
                    request.TimeoutMilliseconds, CancellationToken.None, gate.TcpPortsFor(request.DeviceId)));
            };
        }
    }
}
