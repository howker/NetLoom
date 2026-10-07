using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Discovery;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;

namespace NetLoom.Application.DiscoveryInbox
{
    public sealed class DiscoveryRunJournal :
        IDiscoveryRunJournal,
        IDiscoveryCandidateMaterializer
    {
        private readonly object _sync = new object();
        private readonly IDiscoveryRunRepository _runs;
        private readonly IDiscoveryTopologyReader _topology;
        private readonly IDiscoveryCandidateMaterializer _materializer;
        private readonly IDiscoveryExclusionSource _exclusions;
        private readonly Dictionary<Guid, HashSet<string>> _scopes =
            new Dictionary<Guid, HashSet<string>>();

        private readonly Dictionary<Guid, RetryState> _retries =
            new Dictionary<Guid, RetryState>();

        public DiscoveryRunJournal(
            IDiscoveryRunRepository runs,
            IDiscoveryTopologyReader topology,
            IDiscoveryCandidateMaterializer materializer,
            IDiscoveryExclusionSource exclusions)
        {
            _runs = runs ??
                throw new ArgumentNullException(
                    nameof(runs));
            _topology = topology ??
                throw new ArgumentNullException(
                    nameof(topology));
            _materializer = materializer ??
                throw new ArgumentNullException(
                    nameof(materializer));
            _exclusions = exclusions ??
                throw new ArgumentNullException(
                    nameof(exclusions));
        }

        public DiscoveryRunJournal(
            IDiscoveryCandidateMaterializer materializer)
            : this(
                new InMemoryDiscoveryRunRepository(),
                new EmptyDiscoveryTopologyReader(),
                materializer,
                new EmptyDiscoveryExclusionSource())
        {
        }

        public Guid Materialize(
            DiscoveryCandidateSnapshot candidate,
            DateTime observedUtc)
        {
            lock (_sync)
            {
                return _materializer.Materialize(
                    candidate,
                    observedUtc);
            }
        }

        public DiscoveryRunStart BeginRun(
            DiscoveryControlRequest request,
            string accessProfileName,
            DateTime startedUtc)
        {
            lock (_sync)
            {
                if (request == null)
                {
                    throw new ArgumentNullException(
                        nameof(request));
                }

                RequireUtc(startedUtc, nameof(startedUtc));

                var addresses = request.UsesAddressRange
                    ? Ipv4RangeExpander.Expand(
                        request.StartAddress,
                        request.EndAddress,
                        request.SubnetMask,
                        request.MaxAddresses)
                    : Ipv4CidrExpander.Expand(
                        request.Cidr,
                        request.MaxAddresses);

                var scopeText = request.UsesAddressRange
                    ? request.StartAddress + " \u2013 " +
                        request.EndAddress + " / " + request.SubnetMask
                    : request.Cidr;
                var runId = Guid.NewGuid();

                _runs.SaveRun(
                    new DiscoveryRunRecord(
                        runId,
                        startedUtc,
                        null,
                        DiscoveryControlState.Running,
                        request.AccessProfileId,
                        accessProfileName,
                        scopeText,
                        addresses.Count,
                        0,
                        0,
                        0,
                        0,
                        0,
                        null));
                _runs.PruneRuns(50);

                foreach (var obsoleteId in _scopes.Keys
                    .Where(id => _runs.GetRun(id) == null).ToArray())
                {
                    _scopes.Remove(obsoleteId);
                }

                _scopes[runId] = new HashSet<string>(
                    addresses.Select(address => address.ToString()),
                    StringComparer.OrdinalIgnoreCase);

                var rules = _exclusions.GetRules(
                    request.AccessProfileId);
                var excluded = new List<string>();

                var ignoredDevices = _topology.GetDevices()
                    .Where(device => device.IgnoredUtc.HasValue &&
                        !string.IsNullOrWhiteSpace(device.ManagementAddress))
                    .GroupBy(device => device.ManagementAddress, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key,
                        group => group.OrderBy(device => device.IgnoredUtc).ThenBy(device => device.Id).First(),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var address in addresses)
                {
                    var rule = rules.FirstOrDefault(
                        item => item.Matches(address));

                    var addressText = address.ToString();
                    TopologyDevice ignoredDevice;
                    if (rule == null)
                    {
                        if (ignoredDevices.TryGetValue(addressText, out ignoredDevice))
                        {
                            excluded.Add(addressText);
                            _runs.SaveResult(NonCandidateResult(runId, addressText,
                                DiscoveryResultGroup.Excluded, ignoredDevice.Id, startedUtc,
                                DiscoveryResultReason.OperatorIgnored,
                                ignoredDevice.IgnoredUtc.Value.ToString("o", CultureInfo.InvariantCulture)));
                        }
                        continue;
                    }

                    excluded.Add(addressText);
                    _runs.SaveResult(
                        NonCandidateResult(
                            runId,
                            addressText,
                            DiscoveryResultGroup.Excluded,
                            null,
                            startedUtc,
                            DiscoveryResultReason.ProfileExclusion,
                            rule.TargetKind + ":" + rule.TargetValue));
                }

                return new DiscoveryRunStart(
                    runId,
                    excluded);
            }
        }

        public DiscoveryRunStart BeginRetry(Guid runId, string address, DateTime startedUtc)
        {
            lock (_sync)
            {
                RequireRun(runId);
                RequireUtc(startedUtc, nameof(startedUtc));
                var addresses = Ipv4RangeExpander.Expand(
                    address, address, "255.255.255.255", 1);
                if (_retries.ContainsKey(runId) || _scopes.ContainsKey(runId))
                {
                    throw new InvalidOperationException("DISCOVERY_RUN_ACTIVE");
                }

                _retries.Add(runId, new RetryState(addresses.Single().ToString()));
                return new DiscoveryRunStart(runId, new string[0]);
            }
        }

        public DiscoveryRunRecord GetRun(Guid runId)
        {
            lock (_sync)
            {
                return _runs.GetRun(runId);
            }
        }

        public DiscoveryRunResult RecordCandidate(
            Guid runId,
            DiscoveryCandidateSnapshot candidate,
            DateTime observedUtc)
        {
            lock (_sync)
            {
                if (candidate == null)
                {
                    throw new ArgumentNullException(
                        nameof(candidate));
                }

                RequireUtc(observedUtc, nameof(observedUtc));

                var run = RequireRun(runId);
                var address = candidate.Address.ToString();
                RetryState retry;
                if (_retries.TryGetValue(runId, out retry) && !Same(retry.Address, address))
                {
                    throw new InvalidOperationException("DISCOVERY_RETRY_ADDRESS_MISMATCH");
                }
                var devices = _topology.GetDevices()
                    .Where(device => !device.IsArchived).ToArray();
                var interfaces = _topology.GetInterfaces().ToArray();
                var matches = devices.Where(
                    device => Same(device.ManagementAddress, address)).ToArray();
                var changes = new List<DiscoveryFieldChange>();
                var reason = DiscoveryResultReason.None;
                string reasonDetail = null;
                Guid? deviceId = null;
                DiscoveryResultGroup group;

                if (matches.Length > 1)
                {
                    group = DiscoveryResultGroup.Ambiguous;
                    reason = DiscoveryResultReason.DuplicateManagementAddress;
                    reasonDetail = string.Join(
                        ", ",
                        matches.Select(DeviceName));
                }
                else
                {
                    var existing = matches.SingleOrDefault();

                    if (existing != null &&
                        existing.DiscoveryOrigin == DeviceDiscoveryOrigin.Automatic)
                    {
                        AddChange(
                            changes,
                            "sysName",
                            existing.DiscoveredName,
                            candidate.SysName);
                        AddChange(
                            changes,
                            "sysDescription",
                            existing.SystemDescription,
                            candidate.SysDescription);
                        AddChange(
                            changes,
                            "sysObjectId",
                            existing.SystemObjectId,
                            candidate.SysObjectId);

                        var oldInterfaceCount = interfaces.Count(
                            item => item.DeviceId == existing.Id && !item.IsHidden);

                        if (oldInterfaceCount > 0 && candidate.InterfaceCount > 0)
                        {
                            AddChange(
                                changes,
                                "interfaces",
                                oldInterfaceCount.ToString(CultureInfo.InvariantCulture),
                                candidate.InterfaceCount.ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    if (candidate.SnmpError.HasValue)
                    {
                        group = DiscoveryResultGroup.Error;
                    }
                    else if (existing != null)
                    {
                        group = existing.DiscoveryOrigin == DeviceDiscoveryOrigin.Automatic &&
                            changes.Count > 0
                                ? DiscoveryResultGroup.Changed
                                : DiscoveryResultGroup.KnownUnchanged;
                    }
                    else
                    {
                        group = DiscoveryResultGroup.New;
                        var nameMatch = devices.FirstOrDefault(
                            device => !Same(device.ManagementAddress, address) &&
                                !string.IsNullOrWhiteSpace(candidate.SysName) &&
                                (Same(device.DiscoveredName, candidate.SysName) ||
                                 Same(device.CustomName, candidate.SysName)));

                        if (nameMatch != null)
                        {
                            group = DiscoveryResultGroup.Ambiguous;
                            reason = DiscoveryResultReason.NameMatchesOtherDevice;
                            reasonDetail = DeviceName(nameMatch) +
                                " (" + nameMatch.ManagementAddress + ")";
                        }
                    }
                }

                var completeness = DiscoveryResultCompleteness.NotApplicable;
                var partialReason = DiscoveryPartialReason.None;

                if (group == DiscoveryResultGroup.New ||
                    group == DiscoveryResultGroup.Changed ||
                    (group == DiscoveryResultGroup.Ambiguous &&
                     reason == DiscoveryResultReason.NameMatchesOtherDevice))
                {
                    partialReason = !candidate.SnmpResponded
                        ? DiscoveryPartialReason.SnmpNoResponse
                        : string.IsNullOrWhiteSpace(candidate.SysName)
                            ? DiscoveryPartialReason.NoSysName
                            : candidate.InterfaceCount == 0
                                ? DiscoveryPartialReason.NoInterfaces
                                : DiscoveryPartialReason.None;
                    completeness = partialReason == DiscoveryPartialReason.None
                        ? DiscoveryResultCompleteness.Ready
                        : DiscoveryResultCompleteness.Partial;
                }

                if (matches.Length <= 1)
                {
                    deviceId = _materializer.Materialize(
                        candidate,
                        observedUtc);
                }

                var result = new DiscoveryRunResult(
                    runId,
                    address,
                    group,
                    deviceId,
                    observedUtc,
                    candidate.IcmpReachable,
                    candidate.OpenTcpPorts,
                    candidate.SnmpResponded,
                    candidate.SnmpError,
                    candidate.SysName,
                    candidate.SysDescription,
                    candidate.SysObjectId,
                    candidate.InterfaceCount,
                    completeness,
                    partialReason,
                    reason,
                    reasonDetail,
                    changes,
                    DiscoveryResultResolution.Pending,
                    null);

                _runs.SaveResult(result);
                if (retry != null)
                {
                    retry.CandidateRecorded = true;
                }
                _runs.SaveRun(
                    WithResults(
                        run,
                        run.State,
                        run.FinishedUtc,
                        run.TotalAddresses,
                        run.ProcessedAddresses,
                        run.FaultMessage));

                return result;
            }
        }

        public DiscoveryRunRecord FinishRun(
            Guid runId,
            DiscoveryControlSnapshot finalSnapshot,
            DateTime finishedUtc)
        {
            lock (_sync)
            {
                if (finalSnapshot == null)
                {
                    throw new ArgumentNullException(
                        nameof(finalSnapshot));
                }

                RequireUtc(finishedUtc, nameof(finishedUtc));

                var run = RequireRun(runId);
                RetryState retry;
                if (_retries.TryGetValue(runId, out retry))
                {
                    if (!retry.CandidateRecorded)
                    {
                        var device = _topology.GetDevices().FirstOrDefault(
                            item => !item.IsArchived && Same(item.ManagementAddress, retry.Address));
                        if (device == null)
                        {
                            _runs.DeleteResult(runId, retry.Address);
                        }
                        else
                        {
                            _runs.SaveResult(NonCandidateResult(
                                runId, retry.Address, DiscoveryResultGroup.Missing,
                                device.Id, finishedUtc, DiscoveryResultReason.NotFoundInRun,
                                device.LastSeenUtc?.ToString("o", CultureInfo.InvariantCulture)));
                        }
                    }

                    var updated = WithResults(run, run.State, run.FinishedUtc,
                        run.TotalAddresses, run.ProcessedAddresses, run.FaultMessage);
                    _runs.SaveRun(updated);
                    _retries.Remove(runId);
                    return updated;
                }

                HashSet<string> scope;

                if (finalSnapshot.State == DiscoveryControlState.Completed &&
                    _scopes.TryGetValue(runId, out scope))
                {
                    var recorded = new HashSet<string>(
                        _runs.GetResults(runId).Select(result => result.Address),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var device in _topology.GetDevices())
                    {
                        if (device.IsHidden || device.IsArchived ||
                            string.IsNullOrWhiteSpace(device.ManagementAddress) ||
                            !scope.Contains(device.ManagementAddress) ||
                            recorded.Contains(device.ManagementAddress))
                        {
                            continue;
                        }

                        _runs.SaveResult(
                            NonCandidateResult(
                                runId,
                                device.ManagementAddress,
                                DiscoveryResultGroup.Missing,
                                device.Id,
                                finishedUtc,
                                DiscoveryResultReason.NotFoundInRun,
                                device.LastSeenUtc.HasValue
                                    ? device.LastSeenUtc.Value.ToString(
                                        "o",
                                        CultureInfo.InvariantCulture)
                                    : null));
                        recorded.Add(device.ManagementAddress);
                    }
                }

                var finished = WithResults(
                    run,
                    finalSnapshot.State,
                    finishedUtc,
                    finalSnapshot.TotalAddresses,
                    finalSnapshot.ProcessedAddresses,
                    finalSnapshot.FaultMessage);
                _runs.SaveRun(finished);
                _scopes.Remove(runId);

                return finished;
            }
        }

        public DiscoveryRunRecord GetLatestRun()
        {
            lock (_sync)
            {
                return _runs.GetLatestRun();
            }
        }

        public IReadOnlyList<DiscoveryRunResult> GetResults(
            Guid runId)
        {
            lock (_sync)
            {
                return _runs.GetResults(runId);
            }
        }

        public DiscoveryRunRecord CloseInterruptedRuns(
            DateTime nowUtc)
        {
            lock (_sync)
            {
                RequireUtc(nowUtc, nameof(nowUtc));

                var run = _runs.GetLatestRun();

                if (run == null ||
                    (run.State != DiscoveryControlState.Running &&
                     run.State != DiscoveryControlState.Starting &&
                     run.State != DiscoveryControlState.Stopping))
                {
                    return run;
                }

                var interrupted = new DiscoveryRunRecord(
                    run.Id,
                    run.StartedUtc,
                    nowUtc,
                    DiscoveryControlState.Faulted,
                    run.AccessProfileId,
                    run.AccessProfileName,
                    run.ScopeText,
                    run.TotalAddresses,
                    run.ProcessedAddresses,
                    run.FoundCandidates,
                    run.SnmpResponded,
                    run.ErrorCount,
                    run.KnownUnchangedCount,
                    "DISCOVERY_INTERRUPTED");
                _runs.SaveRun(interrupted);
                _scopes.Remove(run.Id);

                return interrupted;
            }
        }

        private sealed class RetryState
        {
            public RetryState(string address)
            {
                Address = address;
            }

            public string Address { get; }
            public bool CandidateRecorded { get; set; }
        }

        private DiscoveryRunRecord RequireRun(
            Guid runId)
        {
            return _runs.GetRun(runId) ??
                throw new InvalidOperationException(
                    "DISCOVERY_RUN_NOT_FOUND");
        }

        private DiscoveryRunRecord WithResults(
            DiscoveryRunRecord run,
            DiscoveryControlState state,
            DateTime? finishedUtc,
            int totalAddresses,
            int processedAddresses,
            string faultMessage)
        {
            var candidates = _runs.GetResults(run.Id)
                .Where(result => result.Group != DiscoveryResultGroup.Excluded &&
                    result.Group != DiscoveryResultGroup.Missing).ToArray();

            return new DiscoveryRunRecord(
                run.Id,
                run.StartedUtc,
                finishedUtc,
                state,
                run.AccessProfileId,
                run.AccessProfileName,
                run.ScopeText,
                totalAddresses,
                processedAddresses,
                candidates.Length,
                candidates.Count(result => result.SnmpResponded),
                candidates.Count(result => result.Group == DiscoveryResultGroup.Error),
                candidates.Count(result => result.Group == DiscoveryResultGroup.KnownUnchanged),
                faultMessage);
        }

        private static DiscoveryRunResult NonCandidateResult(
            Guid runId,
            string address,
            DiscoveryResultGroup group,
            Guid? deviceId,
            DateTime observedUtc,
            DiscoveryResultReason reason,
            string reasonDetail)
        {
            return new DiscoveryRunResult(
                runId,
                address,
                group,
                deviceId,
                observedUtc,
                false,
                new int[0],
                false,
                null,
                null,
                null,
                null,
                0,
                DiscoveryResultCompleteness.NotApplicable,
                DiscoveryPartialReason.None,
                reason,
                reasonDetail,
                new DiscoveryFieldChange[0],
                DiscoveryResultResolution.Pending,
                null);
        }

        private static void AddChange(
            ICollection<DiscoveryFieldChange> changes,
            string field,
            string oldValue,
            string newValue)
        {
            if (!string.IsNullOrWhiteSpace(newValue) &&
                !Same(oldValue?.Trim(), newValue.Trim()))
            {
                changes.Add(
                    new DiscoveryFieldChange(
                        field,
                        oldValue,
                        newValue));
            }
        }

        private static string DeviceName(
            TopologyDevice device)
        {
            return device.CustomName ??
                device.DiscoveredName ??
                device.ManagementAddress;
        }

        private static bool Same(
            string first,
            string second)
        {
            return string.Equals(
                first,
                second,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void RequireUtc(
            DateTime value,
            string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Timestamp must be UTC.",
                    parameterName);
            }
        }
    }
}
