using System;
using System.Collections.Generic;

namespace NetLoom.Application.DiscoveryInbox
{
    public interface IDiscoveryRunRepository
    {
        void SaveRun(DiscoveryRunRecord run);

        DiscoveryRunRecord GetRun(Guid id);

        DiscoveryRunRecord GetLatestRun();

        void SaveResult(DiscoveryRunResult result);

        IReadOnlyList<DiscoveryRunResult> GetResults(Guid runId);

        void DeleteResult(Guid runId, string address);

        void PruneRuns(int keepLatest);
    }
}
