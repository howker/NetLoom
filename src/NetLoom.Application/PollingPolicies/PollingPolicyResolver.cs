using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.PollingPolicies
{
    public sealed class PollingPolicyResolver
    {
        private readonly Dictionary<Guid, PollingPolicy> _policies;
        private readonly Dictionary<Guid, Guid> _deviceAssignments;
        private readonly Dictionary<Guid, Guid> _locationAssignments;
        private readonly IReadOnlyDictionary<Guid, Guid?> _locationParents;
        private readonly IReadOnlyDictionary<Guid, Guid?> _deviceLocations;

        public PollingPolicy Default { get; }

        public PollingPolicyResolver(IEnumerable<PollingPolicy> policies, IEnumerable<PollingPolicyAssignment> assignments,
            IReadOnlyDictionary<Guid, Guid?> locationParents, IReadOnlyDictionary<Guid, Guid?> deviceLocations)
        {
            if (policies == null) throw new ArgumentNullException(nameof(policies));
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));
            _locationParents = locationParents ?? throw new ArgumentNullException(nameof(locationParents));
            _deviceLocations = deviceLocations ?? throw new ArgumentNullException(nameof(deviceLocations));
            _policies = policies.ToDictionary(policy => policy.Id);
            Default = _policies.Values.FirstOrDefault(policy => policy.IsDefault) ?? PollingPolicy.CreateDefault();
            _deviceAssignments = new Dictionary<Guid, Guid>();
            _locationAssignments = new Dictionary<Guid, Guid>();
            foreach (var assignment in assignments)
            {
                if (!_policies.ContainsKey(assignment.PolicyId)) continue;
                if (assignment.SubjectKind == PollingPolicySubjectKind.Device)
                    _deviceAssignments[assignment.SubjectId] = assignment.PolicyId;
                else if (assignment.SubjectKind == PollingPolicySubjectKind.Location)
                    _locationAssignments[assignment.SubjectId] = assignment.PolicyId;
            }
        }

        public EffectivePollingPolicy ResolveDevice(Guid deviceId)
        {
            Guid policyId;
            PollingPolicy policy;
            if (_deviceAssignments.TryGetValue(deviceId, out policyId) && _policies.TryGetValue(policyId, out policy))
                return new EffectivePollingPolicy(policy, PollingPolicySource.Device, null);
            Guid? locationId;
            if (!_deviceLocations.TryGetValue(deviceId, out locationId)) return DefaultResult();
            return ResolveLocationChain(locationId);
        }

        public EffectivePollingPolicy ResolveLocation(Guid locationId)
        {
            if (!_locationParents.ContainsKey(locationId)) return DefaultResult();
            return ResolveLocationChain(locationId);
        }

        private EffectivePollingPolicy ResolveLocationChain(Guid? locationId)
        {
            var visited = new HashSet<Guid>();
            while (locationId.HasValue && _locationParents.ContainsKey(locationId.Value) && visited.Add(locationId.Value))
            {
                Guid policyId;
                PollingPolicy policy;
                if (_locationAssignments.TryGetValue(locationId.Value, out policyId) && _policies.TryGetValue(policyId, out policy))
                    return new EffectivePollingPolicy(policy, PollingPolicySource.Location, locationId);
                locationId = _locationParents[locationId.Value];
            }
            return DefaultResult();
        }

        private EffectivePollingPolicy DefaultResult() => new EffectivePollingPolicy(Default, PollingPolicySource.Default, null);
    }
}
