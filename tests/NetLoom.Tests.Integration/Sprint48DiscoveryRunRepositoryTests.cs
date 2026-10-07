using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Discovery;
using NetLoom.Persistence.Sqlite.Repositories;
using NetLoom.Persistence.Sqlite.Topology;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint48DiscoveryRunRepositoryTests
    {
        private static readonly DateTime T0 =
            new DateTime(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void RunAndResultRoundTripAllFieldsAndNulls()
        {
            WithDatabase(
                factory =>
                {
                    var repository = new SqliteDiscoveryRunRepository(factory);
                    var deviceId = Guid.NewGuid();
                    new SqliteMaterializedTopologyRepository(factory).SaveDevice(
                        new TopologyDevice(
                            deviceId,
                            null,
                            "Stored switch",
                            DeviceCategory.Unknown,
                            DeviceDiscoveryOrigin.Automatic,
                            MonitoringCapability.Unknown,
                            null,
                            null,
                            null,
                            false,
                            false,
                            T0,
                            T0,
                            null));

                    var fullRun = Run(Guid.NewGuid(), T0, true);
                    var nullRun = Run(Guid.NewGuid(), T0.AddMinutes(1), false);
                    repository.SaveRun(fullRun);
                    repository.SaveRun(nullRun);

                    var fullResult = new DiscoveryRunResult(
                        fullRun.Id,
                        "10.0.0.1",
                        DiscoveryResultGroup.Error,
                        deviceId,
                        T0.AddSeconds(30),
                        true,
                        new[] { 22, 80 },
                        false,
                        SnmpTransportFailure.Authentication,
                        "switch-1",
                        "New description\r\nFirmware 2",
                        "1.3.6.1.4.1.99999",
                        8,
                        DiscoveryResultCompleteness.Partial,
                        DiscoveryPartialReason.NoInterfaces,
                        DiscoveryResultReason.OperatorIgnored,
                        "Synthetic reason",
                        new[]
                        {
                            new DiscoveryFieldChange("sysName", null, "switch-1"),
                            new DiscoveryFieldChange("sysDescription", "Old description", "New description"),
                            new DiscoveryFieldChange("sysObjectId", "old", null)
                        },
                        DiscoveryResultResolution.Ignored,
                        T0.AddMinutes(2));
                    var nullResult = EmptyResult(nullRun.Id, "10.0.0.2");
                    repository.SaveResult(fullResult);
                    repository.SaveResult(nullResult);

                    var restarted = new SqliteDiscoveryRunRepository(factory);
                    AssertRun(fullRun, restarted.GetRun(fullRun.Id));
                    AssertRun(nullRun, restarted.GetRun(nullRun.Id));
                    AssertResult(fullResult, restarted.GetResults(fullRun.Id).Single());
                    AssertResult(nullResult, restarted.GetResults(nullRun.Id).Single());
                    Assert.IsNull(restarted.GetRun(Guid.NewGuid()));
                });
        }

        [TestMethod]
        public void SavingRunPreservesResultsAndReplacingResultRemovesOldChanges()
        {
            WithDatabase(
                factory =>
                {
                    var repository = new SqliteDiscoveryRunRepository(factory);
                    var id = Guid.NewGuid();
                    repository.SaveRun(Run(id, T0, false));
                    var result = new DiscoveryRunResult(
                        id,
                        "10.0.0.1",
                        DiscoveryResultGroup.New,
                        null,
                        T0,
                        true,
                        new[] { 443 },
                        true,
                        null,
                        "switch-1",
                        null,
                        null,
                        8,
                        DiscoveryResultCompleteness.Ready,
                        DiscoveryPartialReason.None,
                        DiscoveryResultReason.None,
                        null,
                        new[] { new DiscoveryFieldChange("sysName", null, "switch-1") },
                        DiscoveryResultResolution.Pending,
                        null);
                    repository.SaveResult(result);
                    repository.SaveRun(Run(id, T0, true));
                    AssertResult(result, repository.GetResults(id).Single());

                    var replacement = EmptyResult(id, "10.0.0.1");
                    repository.SaveResult(replacement);
                    AssertResult(replacement, repository.GetResults(id).Single());
                    Assert.AreEqual(
                        0L,
                        Count(factory, "discovery_run_result_changes"));
                });
        }

        [TestMethod]
        public void LatestRunUsesStartTimeAndPruningCascadesResultsAndChanges()
        {
            WithDatabase(
                factory =>
                {
                    var repository = new SqliteDiscoveryRunRepository(factory);
                    Assert.IsNull(repository.GetLatestRun());
                    var oldest = Run(Guid.NewGuid(), T0, false);
                    var newest = Run(Guid.NewGuid(), T0.AddHours(2), false);
                    var middle = Run(Guid.NewGuid(), T0.AddHours(1), false);
                    repository.SaveRun(newest);
                    repository.SaveRun(oldest);
                    repository.SaveRun(middle);
                    repository.SaveResult(
                        new DiscoveryRunResult(
                            oldest.Id,
                            "10.0.0.1",
                            DiscoveryResultGroup.New,
                            null,
                            T0,
                            false,
                            new int[0],
                            false,
                            null,
                            null,
                            null,
                            null,
                            0,
                            DiscoveryResultCompleteness.Partial,
                            DiscoveryPartialReason.SnmpNoResponse,
                            DiscoveryResultReason.None,
                            null,
                            new[] { new DiscoveryFieldChange("sysName", null, "oldest") },
                            DiscoveryResultResolution.Pending,
                            null));
                    repository.SaveResult(EmptyResult(newest.Id, "10.0.0.2"));
                    repository.SaveResult(EmptyResult(middle.Id, "10.0.0.3"));

                    Assert.AreEqual(newest.Id, repository.GetLatestRun().Id);
                    repository.PruneRuns(2);

                    Assert.IsNull(repository.GetRun(oldest.Id));
                    Assert.AreEqual(0, repository.GetResults(oldest.Id).Count);
                    AssertRun(newest, repository.GetRun(newest.Id));
                    AssertRun(middle, repository.GetRun(middle.Id));
                    Assert.AreEqual(1, repository.GetResults(newest.Id).Count);
                    Assert.AreEqual(1, repository.GetResults(middle.Id).Count);
                    Assert.AreEqual(2L, Count(factory, "discovery_runs"));
                    Assert.AreEqual(2L, Count(factory, "discovery_run_results"));
                    Assert.AreEqual(0L, Count(factory, "discovery_run_result_changes"));
                });
        }

        [TestMethod]
        public void ExclusionSourceReadsOnlyRequestedProfileRules()
        {
            WithDatabase(
                factory =>
                {
                    var profileId = Guid.NewGuid();
                    var otherId = Guid.NewGuid();
                    var profiles = new AccessProfileRepository(factory);
                    profiles.Save(new AccessProfile(profileId, "Discovery", true, SnmpVersion.V2C, null));
                    profiles.Save(new AccessProfile(otherId, "Other", true, SnmpVersion.V2C, null));
                    var scope = new AccessProfileScopeRepository(factory);
                    scope.AddExclusion(profileId, AccessTargetKind.IpAddress, "10.0.0.5");
                    scope.AddExclusion(otherId, AccessTargetKind.Cidr, "10.1.0.0/24");

                    var rules = new SqliteDiscoveryExclusionSource(factory).GetRules(profileId);

                    Assert.AreEqual(1, rules.Count);
                    Assert.AreEqual("IpAddress", rules[0].TargetKind);
                    Assert.AreEqual("10.0.0.5", rules[0].TargetValue);
                    Assert.AreEqual(
                        0,
                        new SqliteDiscoveryExclusionSource(factory).GetRules(Guid.NewGuid()).Count);
                });
        }

        private static DiscoveryRunRecord Run(
            Guid id,
            DateTime startedUtc,
            bool full)
        {
            return new DiscoveryRunRecord(
                id,
                startedUtc,
                full ? startedUtc.AddMinutes(1) : (DateTime?)null,
                full ? DiscoveryControlState.Faulted : DiscoveryControlState.Running,
                full ? Guid.NewGuid() : (Guid?)null,
                full ? "Synthetic profile" : null,
                "10.0.0.0/28",
                16,
                full ? 12 : 0,
                full ? 7 : 0,
                full ? 5 : 0,
                full ? 1 : 0,
                full ? 2 : 0,
                full ? "Synthetic fault" : null);
        }

        private static DiscoveryRunResult EmptyResult(
            Guid runId,
            string address)
        {
            return new DiscoveryRunResult(
                runId,
                address,
                DiscoveryResultGroup.Excluded,
                null,
                T0,
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
                DiscoveryResultReason.None,
                null,
                new DiscoveryFieldChange[0],
                DiscoveryResultResolution.Pending,
                null);
        }

        private static void AssertRun(
            DiscoveryRunRecord expected,
            DiscoveryRunRecord actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.StartedUtc, actual.StartedUtc);
            Assert.AreEqual(DateTimeKind.Utc, actual.StartedUtc.Kind);
            Assert.AreEqual(expected.FinishedUtc, actual.FinishedUtc);
            if (actual.FinishedUtc.HasValue)
            {
                Assert.AreEqual(DateTimeKind.Utc, actual.FinishedUtc.Value.Kind);
            }
            Assert.AreEqual(expected.State, actual.State);
            Assert.AreEqual(expected.AccessProfileId, actual.AccessProfileId);
            Assert.AreEqual(expected.AccessProfileName, actual.AccessProfileName);
            Assert.AreEqual(expected.ScopeText, actual.ScopeText);
            Assert.AreEqual(expected.TotalAddresses, actual.TotalAddresses);
            Assert.AreEqual(expected.ProcessedAddresses, actual.ProcessedAddresses);
            Assert.AreEqual(expected.FoundCandidates, actual.FoundCandidates);
            Assert.AreEqual(expected.SnmpResponded, actual.SnmpResponded);
            Assert.AreEqual(expected.ErrorCount, actual.ErrorCount);
            Assert.AreEqual(expected.KnownUnchangedCount, actual.KnownUnchangedCount);
            Assert.AreEqual(expected.FaultMessage, actual.FaultMessage);
        }

        private static void AssertResult(
            DiscoveryRunResult expected,
            DiscoveryRunResult actual)
        {
            Assert.AreEqual(expected.RunId, actual.RunId);
            Assert.AreEqual(expected.Address, actual.Address);
            Assert.AreEqual(expected.Group, actual.Group);
            Assert.AreEqual(expected.DeviceId, actual.DeviceId);
            Assert.AreEqual(expected.ObservedUtc, actual.ObservedUtc);
            Assert.AreEqual(DateTimeKind.Utc, actual.ObservedUtc.Kind);
            Assert.AreEqual(expected.IcmpReachable, actual.IcmpReachable);
            CollectionAssert.AreEqual(expected.OpenTcpPorts.ToArray(), actual.OpenTcpPorts.ToArray());
            Assert.AreEqual(expected.SnmpResponded, actual.SnmpResponded);
            Assert.AreEqual(expected.SnmpError, actual.SnmpError);
            Assert.AreEqual(expected.SysName, actual.SysName);
            Assert.AreEqual(expected.SysDescription, actual.SysDescription);
            Assert.AreEqual(expected.SysObjectId, actual.SysObjectId);
            Assert.AreEqual(expected.InterfaceCount, actual.InterfaceCount);
            Assert.AreEqual(expected.Completeness, actual.Completeness);
            Assert.AreEqual(expected.PartialReason, actual.PartialReason);
            Assert.AreEqual(expected.Reason, actual.Reason);
            Assert.AreEqual(expected.ReasonDetail, actual.ReasonDetail);
            Assert.AreEqual(expected.Resolution, actual.Resolution);
            Assert.AreEqual(expected.ResolvedUtc, actual.ResolvedUtc);
            if (actual.ResolvedUtc.HasValue)
            {
                Assert.AreEqual(DateTimeKind.Utc, actual.ResolvedUtc.Value.Kind);
            }
            Assert.AreEqual(expected.Changes.Count, actual.Changes.Count);
            foreach (var expectedChange in expected.Changes)
            {
                var actualChange = actual.Changes.Single(change => change.Field == expectedChange.Field);
                Assert.AreEqual(expectedChange.OldValue, actualChange.OldValue);
                Assert.AreEqual(expectedChange.NewValue, actualChange.NewValue);
            }
        }

        private static long Count(
            SqliteConnectionFactory factory,
            string table)
        {
            using (var connection = factory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM " + table + ";";
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private static void WithDatabase(
            Action<SqliteConnectionFactory> action)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "netloom-s48-discovery-runs-" + Guid.NewGuid().ToString("N") + ".db");

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
