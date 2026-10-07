using System;
using System.Collections.Generic;

namespace NetLoom.Application.DiscoveryInbox
{
    public interface IDiscoveryExclusionSource
    {
        IReadOnlyList<DiscoveryExclusionRule> GetRules(
            Guid accessProfileId);
    }
}
