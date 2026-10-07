using System;
using System.Collections.Generic;
using NetLoom.Application.DiscoveryControl;

namespace NetLoom.Application.DiscoveryInbox
{
    public interface IDiscoveryRunJournal
    {
        DiscoveryRunStart BeginRun(
            DiscoveryControlRequest request,
            string accessProfileName,
            DateTime startedUtc);

        DiscoveryRunStart BeginRetry(Guid runId, string address, DateTime startedUtc);

        DiscoveryRunRecord GetRun(Guid runId);

        DiscoveryRunResult RecordCandidate(
            Guid runId,
            DiscoveryCandidateSnapshot candidate,
            DateTime observedUtc);

        DiscoveryRunRecord FinishRun(
            Guid runId,
            DiscoveryControlSnapshot finalSnapshot,
            DateTime finishedUtc);

        DiscoveryRunRecord GetLatestRun();

        IReadOnlyList<DiscoveryRunResult> GetResults(Guid runId);

        DiscoveryRunRecord CloseInterruptedRuns(DateTime nowUtc);
    }
}
