using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Wpf.Discovery;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint48DiscoveryInboxProjectionTests
    {
        [TestMethod]
        public void GroupsAreOrderedAndUnchangedOnlyAppearsInSummary()
        {
            var data = new Sprint48DiscoveryInboxFixture();
            var groups = data.Build();
            CollectionAssert.AreEqual(Sprint48DiscoveryInboxFixture.Order,
                groups.Select(group => group.Group).ToArray());
            CollectionAssert.AreEqual(new[] { 4, 2, 2, 2, 1, 3 },
                groups.Select(group => group.Count).ToArray());
            foreach (var group in groups)
            {
                Assert.AreEqual(UiText.Get("DiscoveryInboxGroup" + group.Group), group.Title);
                foreach (var row in group.Rows)
                {
                    Assert.AreEqual(group.Group != DiscoveryResultGroup.Excluded, row.CanRetry);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxRetryAutomationName", row.Address),
                        row.RetryAutomationName);
                }
            }
            Assert.AreEqual(string.Join(UiText.Get("DiscoveryInboxSeparator"), new[]
            {
                UiText.Format("DiscoveryInboxSummaryNew", 4),
                UiText.Format("DiscoveryInboxSummaryChanged", 2),
                UiText.Format("DiscoveryInboxSummaryAmbiguous", 2),
                UiText.Format("DiscoveryInboxSummaryMissing", 2),
                UiText.Format("DiscoveryInboxSummaryExcluded", 1),
                UiText.Format("DiscoveryInboxSummaryError", 3),
                UiText.Format("DiscoveryInboxSummaryUnchanged", 19)
            }), DiscoveryInboxProjection.BuildSummary(groups, data.Run));
            Assert.AreEqual(UiText.Format("DiscoveryInboxTitle",
                data.Started.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)),
                DiscoveryInboxProjection.BuildTitle(data.Run));
            Assert.AreEqual(0, DiscoveryInboxProjection.Build(
                new[] { data.Result(DiscoveryResultGroup.KnownUnchanged, "10.0.0.1") },
                data.Run, id => data.Profile.Name, data.Now).Count);
            Assert.AreEqual(0, DiscoveryInboxProjection.Build(new DiscoveryRunResult[0],
                data.Run, id => data.Profile.Name, data.Now).Count);
        }

        [TestMethod]
        public void AddressesUseNumericIpv4Order()
        {
            var data = new Sprint48DiscoveryInboxFixture();
            var results = new[] { "10.0.0.100", "10.0.0.10", "10.0.0.2" }
                .Select(address => data.Result(DiscoveryResultGroup.New, address)).ToArray();
            var groups = DiscoveryInboxProjection.Build(results, data.Run, id => null, data.Now);
            CollectionAssert.AreEqual(new[] { "10.0.0.2", "10.0.0.10", "10.0.0.100" },
                groups.Single().Rows.Select(row => row.Address).ToArray());
        }

        [TestMethod]
        public void ReasonsAndCompletenessUseLocalizedResources()
        {
            foreach (var language in new[] { "ru-RU", "en-US" })
            {
                var previous = CultureInfo.CurrentUICulture;
                try
                {
                    CultureInfo.CurrentUICulture = new CultureInfo(language);
                    var data = new Sprint48DiscoveryInboxFixture();
                    var rows = data.Build().SelectMany(group => group.Rows)
                        .ToDictionary(row => row.Address);
                    var separator = UiText.Get("DiscoveryInboxSeparator");
                    Assert.AreEqual(string.Join(separator, new[]
                    {
                        UiText.Get("DiscoveryInboxIcmp"), UiText.Format("DiscoveryInboxTcp", "22, 443"),
                        UiText.Get("DiscoveryInboxSnmp"), UiText.FormatCount("DiscoveryInboxInterfaces", 24)
                    }), rows["10.48.228.14"].Reason);
                    Assert.AreEqual(UiText.Get("DiscoveryInboxReady"), rows["10.48.228.14"].Completeness);
                    Assert.AreEqual(UiText.Get("DiscoveryInboxPartialSnmp"), rows["10.48.228.16"].Completeness);
                    Assert.AreEqual(UiText.Get("DiscoveryInboxPartialName"), rows["10.48.228.17"].Completeness);
                    Assert.AreEqual(UiText.Get("DiscoveryUnnamedCandidate"), rows["10.48.228.17"].Name);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxChanged", string.Join(
                        UiText.Get("DiscoveryInboxChangeSeparator"), new[]
                        {
                            UiText.Format("DiscoveryInboxTextChange", UiText.Get("DiscoveryInboxFieldDescription"),
                                "Cisco IOS 15.2", "Cisco IOS 15.4"),
                            UiText.Format("DiscoveryInboxNumberChange", UiText.Get("DiscoveryInboxFieldInterfaces"), "24", "26")
                        })), rows["10.48.228.20"].Reason);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxChanged", UiText.Format(
                        "DiscoveryInboxTextChange", UiText.Get("DiscoveryInboxFieldName"), "kb-sw-old", "kb-sw-21")),
                        rows["10.48.228.21"].Reason);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxDuplicateAddress", "kb-sw-03, ps2-sw-03"),
                        rows["10.48.228.30"].Reason);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxMatchingName", "kb-sw-03 (10.48.228.13)"),
                        rows["10.48.228.31"].Reason);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxMissingLastSeen",
                        UiText.FormatCount("InspectorRelativeDays", 3)), rows["10.48.228.40"].Reason);
                    Assert.AreEqual(UiText.Get("DiscoveryInboxMissingNoData"),
                        rows["10.48.228.41"].Reason);
                    Assert.AreEqual(UiText.Format("DiscoveryInboxProfileExclusion", data.Profile.Name, "10.48.228.9"),
                        rows["10.48.228.9"].Reason);
                    var errors = new[] { "DiscoveryErrorSnmpAuthentication", "DiscoveryErrorSnmpTimeout", "DiscoveryErrorSnmpProtocol" };
                    for (var index = 0; index < errors.Length; index++)
                    {
                        Assert.AreEqual(UiText.Format("DiscoveryCandidateErrorSummary", null,
                            UiText.Get(errors[index]), data.Profile.Name,
                            data.Observed.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)),
                            rows["10.48.228." + (50 + index)].Reason);
                    }
                }
                finally { CultureInfo.CurrentUICulture = previous; }
            }
        }

        [TestMethod]
        public void AdditionalReasonsNamesAndFullDescriptionsArePreserved()
        {
            var data = new Sprint48DiscoveryInboxFixture();
            var longDescription = new string('A', 70) + "\nSecond line";
            var changed = data.Result(DiscoveryResultGroup.Changed, "10.0.0.1",
                changes: new[] { new DiscoveryFieldChange("sysDescription", longDescription, "Moxa EDS"),
                    new DiscoveryFieldChange("sysObjectId", "1.3.6.1", "1.3.6.2") });
            var partial = data.Result(DiscoveryResultGroup.New, "10.0.0.2",
                partial: DiscoveryPartialReason.NoInterfaces, interfaces: 0, name: null, deviceId: Guid.NewGuid());
            var ignored = data.Result(DiscoveryResultGroup.Excluded, "10.0.0.3",
                reason: DiscoveryResultReason.OperatorIgnored);
            var groups = DiscoveryInboxProjection.Build(new[] { changed, partial, ignored }, data.Run,
                id => data.Profile.Name, data.Now, id => id.HasValue ? "ps2-sw-03" : null);
            var rows = groups.SelectMany(group => group.Rows).ToDictionary(row => row.Address);
            Assert.AreEqual("ps2-sw-03", rows[partial.Address].Name);
            Assert.AreEqual(UiText.Get("DiscoveryInboxPartialInterfaces"), rows[partial.Address].Completeness);
            Assert.AreEqual(UiText.Format("DiscoveryInboxIgnored",
                data.Observed.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)), rows[ignored.Address].Reason);
            var objectChange = UiText.Format("DiscoveryInboxTextChange", UiText.Get("DiscoveryInboxFieldObjectId"),
                "1.3.6.1", "1.3.6.2");
            Assert.AreEqual(UiText.Format("DiscoveryInboxChanged", string.Join(UiText.Get("DiscoveryInboxChangeSeparator"),
                new[] { UiText.Format("DiscoveryInboxTextChange", UiText.Get("DiscoveryInboxFieldDescription"),
                    new string('A', 60) + UiText.Get("DiscoveryInboxEllipsis"), "Moxa EDS"), objectChange })),
                rows[changed.Address].Reason);
            StringAssert.Contains(rows[changed.Address].FullReason, longDescription);
            Assert.AreEqual(string.Empty, rows[ignored.Address].Completeness);
            foreach (var failure in new[] { SnmpTransportFailure.Socket, SnmpTransportFailure.UnsupportedCredentials })
            {
                var result = data.Result(DiscoveryResultGroup.Error, "10.0.0.4", error: failure);
                var row = DiscoveryInboxProjection.Build(new[] { result }, data.Run, id => null, data.Now).Single().Rows.Single();
                Assert.AreEqual(UiText.Format("DiscoveryCandidateErrorSummary", result.SysName,
                    UiText.Get(failure == SnmpTransportFailure.Socket ? "DiscoveryErrorSnmpSocket" : "DiscoveryErrorSnmpUnsupported"),
                    data.Profile.Name, data.Observed.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)), row.Reason);
            }
        }
    }

    internal sealed class Sprint48DiscoveryInboxFixture
    {
        internal static readonly DiscoveryResultGroup[] Order =
        {
            DiscoveryResultGroup.New, DiscoveryResultGroup.Changed, DiscoveryResultGroup.Ambiguous,
            DiscoveryResultGroup.Missing, DiscoveryResultGroup.Excluded, DiscoveryResultGroup.Error
        };

        internal readonly AccessProfile Profile = new AccessProfile(
            Guid.NewGuid(), "Площадка А", true, SnmpVersion.V2C, null);
        internal readonly Guid RunId = Guid.NewGuid();
        internal readonly DateTime Started = new DateTime(2026, 10, 7, 9, 12, 5, DateTimeKind.Local).ToUniversalTime();
        internal DateTime Observed => Started.AddMinutes(2);
        internal DateTime Now => Started.AddMinutes(4).AddSeconds(37);
        internal DiscoveryRunRecord Run => new DiscoveryRunRecord(RunId, Started, Now,
            DiscoveryControlState.Completed, Profile.Id, Profile.Name,
            "10.48.228.1 – 10.48.228.254 / 255.255.255.0", 254, 254, 30, 26, 3, 19, null);

        internal InMemoryDiscoveryRunRepository Repository()
        {
            var repository = new InMemoryDiscoveryRunRepository();
            repository.SaveRun(Run);
            foreach (var result in Results()) repository.SaveResult(result);
            return repository;
        }

        internal IReadOnlyList<DiscoveryInboxGroup> Build()
        {
            return DiscoveryInboxProjection.Build(Results(), Run, id => Profile.Name, Now);
        }

        internal DiscoveryRunResult[] Results()
        {
            var results = new List<DiscoveryRunResult>
            {
                Result(DiscoveryResultGroup.New, "10.48.228.14", name: "kb-sw-14", interfaces: 24),
                Result(DiscoveryResultGroup.New, "10.48.228.15", name: "ps2-sw-03", interfaces: 26),
                Result(DiscoveryResultGroup.New, "10.48.228.16", name: "kb-sw-16", snmp: false,
                    partial: DiscoveryPartialReason.SnmpNoResponse),
                Result(DiscoveryResultGroup.New, "10.48.228.17", name: null, partial: DiscoveryPartialReason.NoSysName),
                Result(DiscoveryResultGroup.Changed, "10.48.228.20", name: "kb-sw-20", interfaces: 26,
                    changes: new[] { new DiscoveryFieldChange("sysDescription", "Cisco IOS 15.2", "Cisco IOS 15.4"),
                        new DiscoveryFieldChange("interfaces", "24", "26") }),
                Result(DiscoveryResultGroup.Changed, "10.48.228.21", name: "kb-sw-21",
                    changes: new[] { new DiscoveryFieldChange("sysName", "kb-sw-old", "kb-sw-21") }),
                Result(DiscoveryResultGroup.Ambiguous, "10.48.228.30", name: "kb-sw-30",
                    reason: DiscoveryResultReason.DuplicateManagementAddress, detail: "kb-sw-03, ps2-sw-03"),
                Result(DiscoveryResultGroup.Ambiguous, "10.48.228.31", name: "kb-sw-03",
                    reason: DiscoveryResultReason.NameMatchesOtherDevice, detail: "kb-sw-03 (10.48.228.13)"),
                Result(DiscoveryResultGroup.Missing, "10.48.228.40", name: "kb-sw-40",
                    reason: DiscoveryResultReason.NotFoundInRun, detail: Now.AddDays(-3).ToString("o")),
                Result(DiscoveryResultGroup.Missing, "10.48.228.41", name: "ps2-sw-41",
                    reason: DiscoveryResultReason.NotFoundInRun),
                Result(DiscoveryResultGroup.Excluded, "10.48.228.9", reason: DiscoveryResultReason.ProfileExclusion,
                    detail: "IpAddress:10.48.228.9"),
                Result(DiscoveryResultGroup.Error, "10.48.228.50", name: "kb-sw-50", snmp: false,
                    error: SnmpTransportFailure.Authentication),
                Result(DiscoveryResultGroup.Error, "10.48.228.51", name: "kb-sw-51", snmp: false,
                    error: SnmpTransportFailure.Timeout),
                Result(DiscoveryResultGroup.Error, "10.48.228.52", name: "ps2-sw-52", snmp: false,
                    error: SnmpTransportFailure.Protocol)
            };
            for (var index = 0; index < 19; index++)
                results.Add(Result(DiscoveryResultGroup.KnownUnchanged, "10.48.228." + (100 + index)));
            return results.ToArray();
        }

        internal DiscoveryRunResult Result(DiscoveryResultGroup group, string address,
            string name = "kb-sw-01", bool snmp = true, int interfaces = 24,
            DiscoveryPartialReason partial = DiscoveryPartialReason.None,
            DiscoveryResultReason reason = DiscoveryResultReason.None, string detail = null,
            IReadOnlyList<DiscoveryFieldChange> changes = null, SnmpTransportFailure? error = null,
            Guid? deviceId = null)
        {
            var candidate = group != DiscoveryResultGroup.Missing && group != DiscoveryResultGroup.Excluded;
            var completeness = group == DiscoveryResultGroup.New || group == DiscoveryResultGroup.Changed
                ? (partial == DiscoveryPartialReason.None ? DiscoveryResultCompleteness.Ready : DiscoveryResultCompleteness.Partial)
                : DiscoveryResultCompleteness.NotApplicable;
            return new DiscoveryRunResult(RunId, address, group, deviceId, Observed,
                candidate, candidate && snmp ? new[] { 22, 443 } : new int[0], candidate && snmp, error,
                name, address.EndsWith("15", StringComparison.Ordinal) ? "MikroTik RouterOS 7.16" :
                    address.EndsWith("17", StringComparison.Ordinal) ? "Moxa EDS-408A" : "Cisco IOS 15.4",
                "1.3.6.1.4.1.9", candidate && snmp ? interfaces : 0, completeness, partial,
                reason, detail, changes ?? new DiscoveryFieldChange[0], DiscoveryResultResolution.Pending, null);
        }
    }
}
