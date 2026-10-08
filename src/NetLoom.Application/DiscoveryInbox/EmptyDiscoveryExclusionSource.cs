using System;
using System.Collections.Generic;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class EmptyDiscoveryExclusionSource :
        IDiscoveryExclusionSource
    {
        public IReadOnlyList<DiscoveryExclusionRule> GetRules(
            Guid accessProfileId)
        {
            return Array.AsReadOnly(
                new DiscoveryExclusionRule[0]);
        }
    }
}
