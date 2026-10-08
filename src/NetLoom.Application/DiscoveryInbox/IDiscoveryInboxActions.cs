using System;
using System.Collections.Generic;

namespace NetLoom.Application.DiscoveryInbox
{
    public enum DiscoveryInboxAction { Accept, Ignore, MarkUnmanaged, AssignPlacement }

    public sealed class DiscoveryInboxActionResult
    {
        public DiscoveryInboxActionResult(int applied, int skipped)
        {
            Applied = applied;
            Skipped = skipped;
        }

        public int Applied { get; }
        public int Skipped { get; }
    }

    public interface IDiscoveryInboxActions
    {
        DiscoveryInboxActionResult Accept(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc);
        DiscoveryInboxActionResult Ignore(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc);
        DiscoveryInboxActionResult MarkUnmanaged(Guid runId, IReadOnlyCollection<string> addresses, DateTime nowUtc);
        DiscoveryInboxActionResult AssignPlacement(Guid runId, IReadOnlyCollection<string> addresses,
            DateTime nowUtc, Guid locationId);
        DiscoveryInboxActionResult UndoIgnore(Guid runId, string address, DateTime nowUtc);
    }
}
