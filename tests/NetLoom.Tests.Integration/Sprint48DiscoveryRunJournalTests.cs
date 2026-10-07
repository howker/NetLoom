using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Snmp;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Discovery;
using NetLoom.Persistence.Sqlite.Repositories;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint48DiscoveryRunJournalTests
    {
        private static readonly DateTime T0 =
            new DateTime(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void NewSnmpCandidateIsReadyAndCreatesDevice()
        {
            WithDatabase(
                factory =>
                {
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), "Synthetic profile", T0);
                    var result = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1"),
                        T0.AddSeconds(1));

                    Assert.AreEqual(DiscoveryResultGroup.New, result.Group);
                    Assert.AreEqual(DiscoveryResultCompleteness.Ready, result.Completeness);
                    Assert.AreEqual(DiscoveryPartialReason.None, result.PartialReason);
                    Assert.AreEqual(DiscoveryResultResolution.Pending, result.Resolution);
                    Assert.IsNull(result.ResolvedUtc);
                    Assert.AreEqual(8, result.InterfaceCount);
                    Assert.IsTrue(result.DeviceId.HasValue);
                    var stored = new SqliteMaterializedTopologyRepository(factory)
                        .GetDevice(result.DeviceId.Value);
                    Assert.IsNotNull(stored);
                    Assert.AreEqual("10.0.0.1", stored.ManagementAddress);
                    Assert.AreEqual("switch-1", stored.DiscoveredName);
                    Assert.AreEqual(1, journal.GetLatestRun().FoundCandidates);
                    Assert.AreEqual(1, journal.GetLatestRun().SnmpResponded);
                    Assert.AreEqual(10, journal.GetLatestRun().TotalAddresses);
                    Assert.AreEqual("10.0.0.1 \u2013 10.0.0.10 / 255.255.255.0", journal.GetLatestRun().ScopeText);
                    Assert.AreEqual(result.DeviceId, journal.GetResults(start.RunId).Single().DeviceId);
                });
        }

        [TestMethod]
        public void IncompleteCandidatesUseFirstApplicablePartialReason()
        {
            WithDatabase(
                factory =>
                {
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), null, T0);
                    var icmpOnly = journal.RecordCandidate(
                        start.RunId,
                        Candidate(
                            "10.0.0.1",
                            null,
                            false,
                            0,
                            description: null,
                            objectId: null),
                        T0.AddSeconds(1));
                    var noName = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.2", null, true, 0),
                        T0.AddSeconds(2));
                    var noInterfaces = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.3", "switch-3", true, 0),
                        T0.AddSeconds(3));

                    foreach (var result in new[] { icmpOnly, noName, noInterfaces })
                    {
                        Assert.AreEqual(DiscoveryResultGroup.New, result.Group);
                        Assert.AreEqual(DiscoveryResultCompleteness.Partial, result.Completeness);
                    }
                    Assert.IsTrue(icmpOnly.IcmpReachable);
                    Assert.AreEqual(0, icmpOnly.OpenTcpPorts.Count);
                    Assert.AreEqual(DiscoveryPartialReason.SnmpNoResponse, icmpOnly.PartialReason);
                    Assert.AreEqual(DiscoveryPartialReason.NoSysName, noName.PartialReason);
                    Assert.AreEqual(DiscoveryPartialReason.NoInterfaces, noInterfaces.PartialReason);
                });
        }

        [TestMethod]
        public void ChangedDescriptionUsesValueBeforeMaterialization()
        {
            WithDatabase(
                factory =>
                {
                    var topology = new SqliteMaterializedTopologyRepository(factory);
                    var device = Device("10.0.0.1", "switch-1", "Old description");
                    topology.SaveDevice(device);
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), null, T0);
                    var result = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1", description: "New description"),
                        T0.AddSeconds(1));

                    Assert.AreEqual(DiscoveryResultGroup.Changed, result.Group);
                    Assert.AreEqual(DiscoveryResultCompleteness.Ready, result.Completeness);
                    Assert.AreEqual(device.Id, result.DeviceId);
                    var change = result.Changes.Single();
                    Assert.AreEqual("sysDescription", change.Field);
                    Assert.AreEqual("Old description", change.OldValue);
                    Assert.AreEqual("New description", change.NewValue);
                    Assert.AreEqual("New description", topology.GetDevice(device.Id).SystemDescription);
                    var saved = journal.GetResults(start.RunId).Single().Changes.Single();
                    Assert.AreEqual(change.OldValue, saved.OldValue);
                    Assert.AreEqual(change.NewValue, saved.NewValue);
                });
        }

        [TestMethod]
        public void ChangesIncludeIdentityAndVisibleInterfaceCount()
        {
            WithDatabase(
                factory =>
                {
                    var topology = new SqliteMaterializedTopologyRepository(factory);
                    var device = Device("10.0.0.1", "old-name", "Old description", "old-object-id");
                    topology.SaveDevice(device);
                    topology.SaveInterface(Interface(device.Id, 1, false));
                    topology.SaveInterface(Interface(device.Id, 2, false));
                    topology.SaveInterface(Interface(device.Id, 3, true));
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), null, T0);
                    var result = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1", "new-name", true, 4, description: "New description"),
                        T0.AddSeconds(1));

                    Assert.AreEqual(DiscoveryResultGroup.Changed, result.Group);
                    CollectionAssert.AreEquivalent(
                        new[] { "sysName", "sysDescription", "sysObjectId", "interfaces" },
                        result.Changes.Select(change => change.Field).ToArray());
                    var interfaceChange = result.Changes.Single(change => change.Field == "interfaces");
                    Assert.AreEqual("2", interfaceChange.OldValue);
                    Assert.AreEqual("4", interfaceChange.NewValue);
                });
        }

        [TestMethod]
        public void KnownDeviceIgnoresCaseWhitespaceAndAbsentNewValues()
        {
            WithDatabase(
                factory =>
                {
                    var topology = new SqliteMaterializedTopologyRepository(factory);
                    var device = Device("10.0.0.1", "SWITCH-1", "DESCRIPTION");
                    topology.SaveDevice(device);
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), null, T0);
                    var result = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1", " switch-1 ", description: " description "),
                        T0.AddSeconds(1));

                    Assert.AreEqual(DiscoveryResultGroup.KnownUnchanged, result.Group);
                    Assert.AreEqual(DiscoveryResultCompleteness.NotApplicable, result.Completeness);
                    Assert.AreEqual(0, result.Changes.Count);
                    Assert.AreEqual(1, journal.GetLatestRun().KnownUnchangedCount);

                    var repeated = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1", null, true, 0, description: null, objectId: null),
                        T0.AddSeconds(2));

                    Assert.AreEqual(DiscoveryResultGroup.KnownUnchanged, repeated.Group);
                    Assert.AreEqual(0, repeated.Changes.Count);
                    Assert.AreEqual(1, journal.GetLatestRun().FoundCandidates);
                    Assert.AreEqual(1, journal.GetLatestRun().SnmpResponded);
                    Assert.AreEqual(1, journal.GetLatestRun().KnownUnchangedCount);
                });
        }

        [TestMethod]
        public void DuplicateManagementAddressIsAmbiguousWithoutMaterialization()
        {
            WithDatabase(
                factory =>
                {
                    var topology = new SqliteMaterializedTopologyRepository(factory);
                    var first = Device("10.0.0.1", "first");
                    var second = Device("10.0.0.1", "second");
                    topology.SaveDevice(first);
                    topology.SaveDevice(second);
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), null, T0);
                    var result = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1"),
                        T0.AddSeconds(1));

                    Assert.AreEqual(DiscoveryResultGroup.Ambiguous, result.Group);
                    Assert.AreEqual(DiscoveryResultReason.DuplicateManagementAddress, result.Reason);
                    CollectionAssert.AreEquivalent(
                        new[] { "first", "second" },
                        result.ReasonDetail.Split(new[] { ", " }, StringSplitOptions.None));
                    Assert.IsNull(result.DeviceId);
                    Assert.AreEqual(DiscoveryResultCompleteness.NotApplicable, result.Completeness);
                    Assert.AreEqual(2, topology.GetDevices().Count);
                    Assert.AreEqual(first.DiscoveredName, topology.GetDevice(first.Id).DiscoveredName);
                    Assert.AreEqual(first.LastSeenUtc, topology.GetDevice(first.Id).LastSeenUtc);
                    Assert.AreEqual(second.LastSeenUtc, topology.GetDevice(second.Id).LastSeenUtc);
                });
        }

        [TestMethod]
        public void MatchingNameAtOtherAddressIsAmbiguousButMaterialized()
        {
            WithDatabase(
                factory =>
                {
                    var topology = new SqliteMaterializedTopologyRepository(factory);
                    topology.SaveDevice(Device("10.0.0.2", "SWITCH-1"));
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), null, T0);
                    var result = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1"),
                        T0.AddSeconds(1));

                    Assert.AreEqual(DiscoveryResultGroup.Ambiguous, result.Group);
                    Assert.AreEqual(DiscoveryResultReason.NameMatchesOtherDevice, result.Reason);
                    Assert.AreEqual("SWITCH-1 (10.0.0.2)", result.ReasonDetail);
                    Assert.AreEqual(DiscoveryResultCompleteness.Ready, result.Completeness);
                    Assert.IsTrue(result.DeviceId.HasValue);
                    Assert.AreEqual(2, topology.GetDevices().Count);
                });
        }

        [TestMethod]
        public void AuthenticationFailureIsErrorAndUpdatesCounter()
        {
            WithDatabase(
                factory =>
                {
                    var journal = Journal(factory);
                    var start = journal.BeginRun(Request(), null, T0);
                    var result = journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.1", null, false, 0, SnmpTransportFailure.Authentication),
                        T0.AddSeconds(1));

                    Assert.AreEqual(DiscoveryResultGroup.Error, result.Group);
                    Assert.AreEqual(SnmpTransportFailure.Authentication, result.SnmpError);
                    Assert.AreEqual(DiscoveryResultCompleteness.NotApplicable, result.Completeness);
                    Assert.AreEqual(DiscoveryPartialReason.None, result.PartialReason);
                    Assert.AreEqual(1, journal.GetLatestRun().ErrorCount);
                    Assert.AreEqual(1, journal.GetLatestRun().FoundCandidates);
                    Assert.AreEqual(0, journal.GetLatestRun().SnmpResponded);
                });
        }

        [TestMethod]
        public void CompletedRunFindsMissingButStoppedRunDoesNot()
        {
            WithDatabase(
                factory =>
                {
                    var topology = new SqliteMaterializedTopologyRepository(factory);
                    var missing = Device("10.0.0.2", "missing");
                    topology.SaveDevice(missing);
                    topology.SaveDevice(Device("10.0.0.3", "hidden", hidden: true));
                    topology.SaveDevice(Device("10.0.0.4", "archived", archived: true));
                    topology.SaveDevice(Device("10.0.0.20", "outside"));
                    var journal = Journal(factory);
                    var request = Request();
                    var completed = journal.BeginRun(request, null, T0);
                    journal.RecordCandidate(completed.RunId, Candidate("10.0.0.1"), T0.AddSeconds(1));
                    var finished = journal.FinishRun(
                        completed.RunId,
                        FinalSnapshot(request, DiscoveryControlState.Completed),
                        T0.AddMinutes(1));

                    var results = journal.GetResults(completed.RunId);
                    Assert.AreEqual(2, results.Count);
                    var missingResult = results.Single(result => result.Group == DiscoveryResultGroup.Missing);
                    Assert.AreEqual(missing.Id, missingResult.DeviceId);
                    Assert.AreEqual("10.0.0.2", missingResult.Address);
                    Assert.AreEqual(DiscoveryResultReason.NotFoundInRun, missingResult.Reason);
                    Assert.AreEqual(T0.ToString("o", CultureInfo.InvariantCulture), missingResult.ReasonDetail);
                    Assert.AreEqual(T0.AddMinutes(1), missingResult.ObservedUtc);
                    Assert.AreEqual(10, finished.TotalAddresses);
                    Assert.AreEqual(10, finished.ProcessedAddresses);
                    Assert.AreEqual(1, finished.FoundCandidates);

                    var stopped = journal.BeginRun(request, null, T0.AddMinutes(2));
                    journal.FinishRun(
                        stopped.RunId,
                        FinalSnapshot(request, DiscoveryControlState.Stopped),
                        T0.AddMinutes(3));

                    Assert.AreEqual(0, journal.GetResults(stopped.RunId).Count);
                });
        }

        [TestMethod]
        public void ProfileExclusionIsReturnedRecordedAndNeverMissing()
        {
            WithDatabase(
                factory =>
                {
                    var profileId = Guid.NewGuid();
                    new AccessProfileRepository(factory).Save(
                        new AccessProfile(profileId, "Discovery", true, SnmpVersion.V2C, null));
                    new AccessProfileScopeRepository(factory).AddExclusion(
                        profileId,
                        AccessTargetKind.IpAddress,
                        "10.0.0.5");
                    new SqliteMaterializedTopologyRepository(factory).SaveDevice(
                        Device("10.0.0.5", "excluded"));
                    var request = Request(profileId);
                    var journal = Journal(factory);
                    var start = journal.BeginRun(request, "Discovery", T0);

                    CollectionAssert.AreEqual(new[] { "10.0.0.5" }, start.ExcludedAddresses.ToArray());
                    var result = journal.GetResults(start.RunId).Single();
                    Assert.AreEqual(DiscoveryResultGroup.Excluded, result.Group);
                    Assert.AreEqual(DiscoveryResultReason.ProfileExclusion, result.Reason);
                    Assert.AreEqual("IpAddress:10.0.0.5", result.ReasonDetail);
                    Assert.AreEqual(T0, result.ObservedUtc);
                    Assert.AreEqual(0, journal.GetLatestRun().FoundCandidates);

                    journal.FinishRun(
                        start.RunId,
                        FinalSnapshot(request, DiscoveryControlState.Completed),
                        T0.AddMinutes(1));
                    Assert.AreEqual(DiscoveryResultGroup.Excluded, journal.GetResults(start.RunId).Single().Group);
                });
        }

        [TestMethod]
        public void CompletedRunAndCountersSurviveNewJournalInstance()
        {
            WithDatabase(
                factory =>
                {
                    var journal = Journal(factory);
                    var request = Request();
                    var start = journal.BeginRun(request, "Synthetic profile", T0);
                    journal.RecordCandidate(start.RunId, Candidate("10.0.0.1"), T0.AddSeconds(1));
                    journal.RecordCandidate(
                        start.RunId,
                        Candidate("10.0.0.2", null, false, 0, SnmpTransportFailure.Authentication),
                        T0.AddSeconds(2));
                    journal.FinishRun(
                        start.RunId,
                        FinalSnapshot(request, DiscoveryControlState.Completed, 2),
                        T0.AddMinutes(1));

                    var restarted = Journal(new SqliteConnectionFactory(factory.DatabasePath));
                    var latest = restarted.GetLatestRun();
                    Assert.AreEqual(start.RunId, latest.Id);
                    Assert.AreEqual(DiscoveryControlState.Completed, latest.State);
                    Assert.AreEqual(T0.AddMinutes(1), latest.FinishedUtc);
                    Assert.AreEqual("Synthetic profile", latest.AccessProfileName);
                    Assert.AreEqual(2, latest.FoundCandidates);
                    Assert.AreEqual(1, latest.SnmpResponded);
                    Assert.AreEqual(1, latest.ErrorCount);
                    Assert.AreEqual(0, latest.KnownUnchangedCount);
                    Assert.AreEqual(2, restarted.GetResults(start.RunId).Count);
                    Assert.AreEqual(latest.Id, restarted.CloseInterruptedRuns(T0.AddMinutes(2)).Id);
                    Assert.AreEqual(DiscoveryControlState.Completed, restarted.GetLatestRun().State);
                });
        }

        [TestMethod]
        public void RestartedJournalDoesNotInferMissingFromScopeText()
        {
            WithDatabase(
                factory =>
                {
                    new SqliteMaterializedTopologyRepository(factory).SaveDevice(
                        Device("10.0.0.1", "missing"));
                    var request = Request();
                    var start = Journal(factory).BeginRun(request, null, T0);
                    var restarted = Journal(factory);
                    restarted.FinishRun(
                        start.RunId,
                        FinalSnapshot(request, DiscoveryControlState.Completed),
                        T0.AddMinutes(1));

                    Assert.AreEqual(0, restarted.GetResults(start.RunId).Count);
                });
        }

        [TestMethod]
        public void InterruptedStartingRunningAndStoppingRunsAreClosedAsFaulted()
        {
            WithDatabase(
                factory =>
                {
                    Assert.IsNull(Journal(factory).CloseInterruptedRuns(T0));
                    var repository = new SqliteDiscoveryRunRepository(factory);
                    var states = new[]
                    {
                        DiscoveryControlState.Starting,
                        DiscoveryControlState.Running,
                        DiscoveryControlState.Stopping
                    };

                    for (var index = 0; index < states.Length; index++)
                    {
                        var id = Guid.NewGuid();
                        repository.SaveRun(
                            new DiscoveryRunRecord(
                                id,
                                T0.AddMinutes(index),
                                null,
                                states[index],
                                null,
                                null,
                                "10.0.0.0/30",
                                4,
                                2,
                                0,
                                0,
                                0,
                                0,
                                null));
                        var finishedUtc = T0.AddHours(1);
                        var closed = Journal(factory).CloseInterruptedRuns(finishedUtc);

                        Assert.AreEqual(id, closed.Id);
                        Assert.AreEqual(DiscoveryControlState.Faulted, closed.State);
                        Assert.AreEqual("DISCOVERY_INTERRUPTED", closed.FaultMessage);
                        Assert.AreEqual(finishedUtc, closed.FinishedUtc);
                        Assert.AreEqual(2, closed.ProcessedAddresses);
                        Assert.AreEqual(DiscoveryControlState.Faulted, repository.GetRun(id).State);
                        Assert.AreEqual(0, repository.GetResults(id).Count);
                    }
                });
        }

        [TestMethod]
        public void CidrScopeUsesExistingExpander()
        {
            WithDatabase(
                factory =>
                {
                    var request = new DiscoveryControlRequest(
                        "10.0.0.0/30",
                        Guid.NewGuid(),
                        SnmpVersion.V2C);
                    var journal = Journal(factory);
                    journal.BeginRun(request, null, T0);

                    Assert.AreEqual("10.0.0.0/30", journal.GetLatestRun().ScopeText);
                    Assert.AreEqual(4, journal.GetLatestRun().TotalAddresses);
                });
        }

        [TestMethod]
        public void RetryReplacesErrorAndKeepsOriginalRunMetadata()
        {
            foreach (var expected in new[] { DiscoveryResultGroup.New,
                DiscoveryResultGroup.Changed, DiscoveryResultGroup.KnownUnchanged })
            {
                WithDatabase(factory =>
                {
                    var topology = new SqliteMaterializedTopologyRepository(factory);
                    var request = Request();
                    var journal = Journal(factory);
                    var runId = journal.BeginRun(request, "Synthetic profile", T0).RunId;
                    if (expected == DiscoveryResultGroup.New)
                    {
                        // Журнал содержит ошибку адреса, у которого пока нет устройства в базе.
                        new SqliteDiscoveryRunRepository(factory).SaveResult(new DiscoveryRunResult(
                            runId, "10.0.0.1", DiscoveryResultGroup.Error, null, T0,
                            true, new int[0], false, SnmpTransportFailure.Authentication,
                            null, null, null, 0, DiscoveryResultCompleteness.NotApplicable,
                            DiscoveryPartialReason.None, DiscoveryResultReason.None, null,
                            new DiscoveryFieldChange[0], DiscoveryResultResolution.Pending, null));
                    }
                    else
                    {
                        journal.RecordCandidate(runId,
                            Candidate("10.0.0.1", null, false, 0, SnmpTransportFailure.Authentication),
                            T0.AddSeconds(1));
                    }
                    var original = journal.FinishRun(runId,
                        FinalSnapshot(request, DiscoveryControlState.Completed), T0.AddMinutes(2));
                    Assert.AreEqual(1, original.ErrorCount);
                    if (expected == DiscoveryResultGroup.KnownUnchanged)
                    {
                        var device = topology.GetDevices().Single();
                        topology.SaveDevice(new TopologyDevice(device.Id, null, null,
                            DeviceCategory.Unknown, DeviceDiscoveryOrigin.Automatic,
                            MonitoringCapability.Unknown, null, null, null, false, false,
                            T0, T0, T0, "switch-1", null, "10.0.0.1",
                            "Description", "1.3.6.1.4.1.99999"));
                    }

                    var retry = journal.BeginRetry(runId, "10.0.0.1", T0.AddMinutes(3));
                    Assert.AreEqual(runId, retry.RunId);
                    Assert.AreEqual(0, retry.ExcludedAddresses.Count);
                    var result = journal.RecordCandidate(runId, Candidate("10.0.0.1"), T0.AddMinutes(3));
                    Assert.AreEqual(expected, result.Group);
                    var updated = journal.FinishRun(runId,
                        new DiscoveryControlSnapshot(DiscoveryControlState.Completed,
                            null, request.AccessProfileId, 1, 1, 1, null, null), T0.AddMinutes(4));
                    Assert.AreEqual(0, updated.ErrorCount);
                    Assert.AreEqual(1, updated.FoundCandidates);
                    Assert.AreEqual(1, updated.SnmpResponded);
                    Assert.AreEqual(expected == DiscoveryResultGroup.KnownUnchanged ? 1 : 0,
                        updated.KnownUnchangedCount);
                    Assert.AreEqual(original.StartedUtc, updated.StartedUtc);
                    Assert.AreEqual(original.FinishedUtc, updated.FinishedUtc);
                    Assert.AreEqual(original.State, updated.State);
                    Assert.AreEqual(original.TotalAddresses, updated.TotalAddresses);
                    Assert.AreEqual(original.ProcessedAddresses, updated.ProcessedAddresses);
                    Assert.AreEqual(original.ScopeText, updated.ScopeText);
                    Assert.AreEqual(1, journal.GetResults(runId).Count);
                });
            }
        }

        [TestMethod]
        public void RetryWithoutResponseMakesKnownAddressMissing()
        {
            WithDatabase(factory =>
            {
                var topology = new SqliteMaterializedTopologyRepository(factory);
                var device = Device("10.0.0.1", "switch-1");
                topology.SaveDevice(device);
                var journal = Journal(factory);
                var request = Request();
                var runId = journal.BeginRun(request, null, T0).RunId;
                journal.RecordCandidate(runId, Candidate("10.0.0.1"), T0.AddSeconds(1));
                var original = journal.FinishRun(runId,
                    FinalSnapshot(request, DiscoveryControlState.Completed), T0.AddMinutes(1));
                var lastSeen = topology.GetDevice(device.Id).LastSeenUtc;
                journal.BeginRetry(runId, "10.0.0.1", T0.AddMinutes(2));
                var updated = journal.FinishRun(runId,
                    FinalSnapshot(request, DiscoveryControlState.Completed, 0), T0.AddMinutes(3));
                var result = journal.GetResults(runId).Single();
                Assert.AreEqual(DiscoveryResultGroup.Missing, result.Group);
                Assert.AreEqual(DiscoveryResultReason.NotFoundInRun, result.Reason);
                Assert.AreEqual(lastSeen?.ToString("o", CultureInfo.InvariantCulture), result.ReasonDetail);
                Assert.AreEqual(0, updated.FoundCandidates);
                Assert.AreEqual(0, updated.SnmpResponded);
                Assert.AreEqual(0, updated.KnownUnchangedCount);
                Assert.AreEqual(original.FinishedUtc, updated.FinishedUtc);
                Assert.AreEqual(original.ProcessedAddresses, updated.ProcessedAddresses);
                Assert.AreEqual(original.TotalAddresses, updated.TotalAddresses);
                Assert.AreEqual(device.Id, topology.GetDevice(device.Id).Id);
            });
        }

        [TestMethod]
        public void RetryWithoutResponseRemovesUnknownAddressAndItsChanges()
        {
            WithDatabase(factory =>
            {
                var journal = Journal(factory);
                var request = Request();
                var runId = journal.BeginRun(request, null, T0).RunId;
                var repository = new SqliteDiscoveryRunRepository(factory);
                repository.SaveResult(new DiscoveryRunResult(runId, "10.0.0.1",
                    DiscoveryResultGroup.Changed, null, T0, true, new int[0], true, null,
                    "switch-1", null, null, 0, DiscoveryResultCompleteness.Partial,
                    DiscoveryPartialReason.NoInterfaces, DiscoveryResultReason.None, null,
                    new[] { new DiscoveryFieldChange("sysName", "old", "new") },
                    DiscoveryResultResolution.Pending, null));
                journal.FinishRun(runId, FinalSnapshot(request, DiscoveryControlState.Completed), T0.AddMinutes(1));
                journal.BeginRetry(runId, "10.0.0.1", T0.AddMinutes(2));
                var updated = journal.FinishRun(runId,
                    FinalSnapshot(request, DiscoveryControlState.Completed, 0), T0.AddMinutes(3));
                Assert.AreEqual(0, repository.GetResults(runId).Count);
                Assert.AreEqual(0, updated.FoundCandidates);
                using (var connection = factory.OpenReadOnlyConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM discovery_run_result_changes WHERE run_id = @id;";
                    command.Parameters.AddWithValue("@id", runId.ToString("D"));
                    Assert.AreEqual(0L, Convert.ToInt64(command.ExecuteScalar()));
                }
                // Отметка повтора снята: тот же адрес можно проверить ещё раз.
                journal.BeginRetry(runId, "10.0.0.1", T0.AddMinutes(4));
            });
        }

        private static DiscoveryRunJournal Journal(
            SqliteConnectionFactory factory)
        {
            var topology = new SqliteMaterializedTopologyRepository(factory);

            return new DiscoveryRunJournal(
                new SqliteDiscoveryRunRepository(factory),
                new MaterializedTopologyDiscoveryReader(topology),
                new DiscoveryCandidateTopologyMaterializer(topology),
                new SqliteDiscoveryExclusionSource(factory));
        }

        private static DiscoveryControlRequest Request(
            Guid? profileId = null)
        {
            return new DiscoveryControlRequest(
                "10.0.0.1",
                "10.0.0.10",
                "255.255.255.0",
                profileId ?? Guid.NewGuid(),
                SnmpVersion.V2C);
        }

        private static DiscoveryCandidateSnapshot Candidate(
            string address,
            string name = "switch-1",
            bool snmpResponded = true,
            int interfaceCount = 8,
            SnmpTransportFailure? error = null,
            string description = "Description",
            string objectId = "1.3.6.1.4.1.99999")
        {
            return new DiscoveryCandidateSnapshot(
                IPAddress.Parse(address),
                null,
                true,
                snmpResponded,
                snmpResponded ? new[] { 22 } : new int[0],
                name,
                description,
                objectId,
                null,
                interfaceCount,
                error);
        }

        private static TopologyDevice Device(
            string address,
            string name,
            string description = "Description",
            string objectId = "1.3.6.1.4.1.99999",
            bool hidden = false,
            bool archived = false)
        {
            return new TopologyDevice(
                Guid.NewGuid(),
                null,
                null,
                DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic,
                MonitoringCapability.Unknown,
                null,
                null,
                null,
                hidden,
                archived,
                T0,
                T0,
                T0,
                name,
                null,
                address,
                description,
                objectId);
        }

        private static DeviceInterface Interface(
            Guid deviceId,
            int ifIndex,
            bool hidden)
        {
            return new DeviceInterface(
                Guid.NewGuid(),
                deviceId,
                ifIndex,
                "port-" + ifIndex.ToString(CultureInfo.InvariantCulture),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                hidden,
                T0,
                T0);
        }

        private static DiscoveryControlSnapshot FinalSnapshot(
            DiscoveryControlRequest request,
            DiscoveryControlState state,
            int foundCandidates = 1)
        {
            return new DiscoveryControlSnapshot(
                state,
                null,
                request.AccessProfileId,
                10,
                10,
                foundCandidates,
                null,
                null);
        }

        private static void WithDatabase(
            Action<SqliteConnectionFactory> action)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "netloom-s48-discovery-journal-" + Guid.NewGuid().ToString("N") + ".db");

            try
            {
                var factory = new SqliteConnectionFactory(path);
                new DatabaseInitializer(factory).Initialize();
                action(factory);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
            }
        }
    }
}
