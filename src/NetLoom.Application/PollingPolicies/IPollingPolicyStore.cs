using System;
using System.Collections.Generic;

namespace NetLoom.Application.PollingPolicies
{
    public interface IPollingPolicyStore
    {
        IReadOnlyList<PollingPolicy> GetPolicies();
        PollingPolicy GetPolicy(Guid policyId);
        void SavePolicy(PollingPolicy policy, DateTime nowUtc);
        PollingPolicyUsage GetUsage(Guid policyId);
        PollingPolicyDeleteOutcome DeletePolicy(Guid policyId);
        IReadOnlyList<PollingPolicyAssignment> GetAssignments();
        void Assign(PollingPolicySubjectKind kind, Guid subjectId, Guid? policyId, DateTime nowUtc);
        PollingPolicyResolver LoadResolver();
    }
}
