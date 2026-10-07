using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class InMemoryDiscoveryRunRepository :
        IDiscoveryRunRepository
    {
        private readonly object _sync = new object();
        private readonly Dictionary<Guid, DiscoveryRunRecord> _runs =
            new Dictionary<Guid, DiscoveryRunRecord>();
        private readonly Dictionary<Guid, Dictionary<string, DiscoveryRunResult>>
            _results =
                new Dictionary<Guid, Dictionary<string, DiscoveryRunResult>>();

        public void SaveRun(
            DiscoveryRunRecord run)
        {
            if (run == null)
            {
                throw new ArgumentNullException(
                    nameof(run));
            }

            lock (_sync)
            {
                _runs[run.Id] = run;
            }
        }

        public DiscoveryRunRecord GetRun(
            Guid id)
        {
            lock (_sync)
            {
                DiscoveryRunRecord run;
                return _runs.TryGetValue(id, out run)
                    ? run
                    : null;
            }
        }

        public DiscoveryRunRecord GetLatestRun()
        {
            lock (_sync)
            {
                return _runs.Values
                    .OrderByDescending(run => run.StartedUtc)
                    .ThenByDescending(run => run.Id.ToString("D"), StringComparer.Ordinal)
                    .FirstOrDefault();
            }
        }

        public void SaveResult(
            DiscoveryRunResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            lock (_sync)
            {
                if (!_runs.ContainsKey(result.RunId))
                {
                    throw new InvalidOperationException(
                        "DISCOVERY_RUN_NOT_FOUND");
                }

                Dictionary<string, DiscoveryRunResult> results;

                if (!_results.TryGetValue(result.RunId, out results))
                {
                    results = new Dictionary<string, DiscoveryRunResult>(
                        StringComparer.Ordinal);
                    _results.Add(result.RunId, results);
                }

                results[result.Address] = result;
            }
        }

        public IReadOnlyList<DiscoveryRunResult> GetResults(
            Guid runId)
        {
            lock (_sync)
            {
                Dictionary<string, DiscoveryRunResult> results;

                return Array.AsReadOnly(
                    _results.TryGetValue(runId, out results)
                        ? results.Values.OrderBy(
                            result => result.Address,
                            StringComparer.Ordinal).ToArray()
                        : new DiscoveryRunResult[0]);
            }
        }

        public void DeleteResult(Guid runId, string address)
        {
            lock (_sync)
            {
                Dictionary<string, DiscoveryRunResult> results;
                if (_results.TryGetValue(runId, out results))
                {
                    results.Remove(address);
                }
            }
        }

        public void PruneRuns(
            int keepLatest)
        {
            if (keepLatest < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(keepLatest));
            }

            lock (_sync)
            {
                var obsoleteIds = _runs.Values
                    .OrderByDescending(run => run.StartedUtc)
                    .ThenByDescending(run => run.Id.ToString("D"), StringComparer.Ordinal)
                    .Skip(keepLatest)
                    .Select(run => run.Id)
                    .ToArray();

                foreach (var id in obsoleteIds)
                {
                    _runs.Remove(id);
                    _results.Remove(id);
                }
            }
        }
    }
}
