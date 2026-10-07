using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Snmp;
using NetLoom.Desktop.Discovery;
using NetLoom.Desktop.Monitoring;
using NetLoom.Domain.Access;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint48ProfileCheckHostingTests
    {
        [DataTestMethod]
        [DataRow("availability", "ok", "none", "14", "none", SnmpProfileCheckKind.Availability, SnmpProfileCheckStatus.Ok)]
        [DataRow("if-mib", "ok", "52", "none", "none", SnmpProfileCheckKind.IfMib, SnmpProfileCheckStatus.Ok)]
        [DataRow("lldp-mib", "ok", "18", "none", "none", SnmpProfileCheckKind.LldpMib, SnmpProfileCheckStatus.Ok)]
        [DataRow("q-bridge-mib", "partial", "none", "none", "none", SnmpProfileCheckKind.QBridgeMib, SnmpProfileCheckStatus.Partial)]
        [DataRow("system", "failed", "none", "none", "authentication", SnmpProfileCheckKind.System, SnmpProfileCheckStatus.Failed)]
        [DataRow("bridge-mib", "not-checked", "none", "none", "none", SnmpProfileCheckKind.BridgeMib, SnmpProfileCheckStatus.NotChecked)]
        public void ProfileMarkersParseTypedItems(string name, string status, string count, string ms, string failure,
            SnmpProfileCheckKind expectedKind, SnmpProfileCheckStatus expectedStatus)
        {
            SnmpProfileCheckItem item;
            bool completed;
            Assert.IsTrue(EngineProfileCheckMarkerParser.TryParse("NETLOOM_PROFILE_CHECK item=" + name
                + " status=" + status + " count=" + count + " ms=" + ms + " failure=" + failure, out item, out completed));
            Assert.IsFalse(completed);
            Assert.AreEqual(expectedKind, item.Kind);
            Assert.AreEqual(expectedStatus, item.Status);
            Assert.AreEqual(count == "none" ? (int?)null : int.Parse(count), item.Count);
            Assert.AreEqual(ms == "none" ? (long?)null : long.Parse(ms), item.Milliseconds);
            if (failure == "authentication") Assert.AreEqual(SnmpTransportFailure.Authentication, item.Failure);
        }

        [DataTestMethod]
        [DataRow("item=system status=invalid count=none ms=none failure=none")]
        [DataRow("item=system status=ok count=-1 ms=none failure=none")]
        [DataRow("item=system status=ok count=none ms=none failure=secret")]
        [DataRow("item=system status=ok count=none ms=none failure=authentication")]
        [DataRow("item=system status=ok count=none ms=none failure=none status=ok")]
        public void InvalidProfileMarkersAreRejected(string payload)
        {
            SnmpProfileCheckItem item;
            bool completed;
            Assert.IsFalse(EngineProfileCheckMarkerParser.TryParse("NETLOOM_PROFILE_CHECK " + payload, out item, out completed));
            Assert.IsNull(item);
        }

        [TestMethod]
        public void CompletedMarkerParsesWithoutAnItem()
        {
            SnmpProfileCheckItem item;
            bool completed;
            Assert.IsTrue(EngineProfileCheckMarkerParser.TryParse("NETLOOM_PROFILE_CHECK state=completed", out item, out completed));
            Assert.IsTrue(completed);
            Assert.IsNull(item);
        }

        [TestMethod]
        public void CheckCommandParsesExplicitOptionsAndDefaults()
        {
            var defaults = EngineCommandLine.Parse(new[] { "check-profile", "--address", "192.0.2.1" });
            Assert.AreEqual(161, defaults.Port);
            Assert.AreEqual(SnmpVersion.V2C, defaults.Version);
            Assert.AreEqual(750, defaults.TimeoutMilliseconds);
            Assert.AreEqual(0, defaults.RetryCount);
            Assert.AreEqual(10, defaults.MaxRepetitions);
            Assert.AreEqual(500, defaults.DiscoveryIcmpTimeoutMilliseconds);
            var explicitOptions = EngineCommandLine.Parse(new[] { "check-profile", "--address", "192.0.2.2",
                "--port", "1161", "--version", "V1", "--timeout-ms", "1200", "--retries", "2",
                "--max-repetitions", "20", "--icmp-timeout-ms", "600" });
            Assert.AreEqual(IPAddress.Parse("192.0.2.2"), explicitOptions.Address);
            Assert.AreEqual(1161, explicitOptions.Port);
            Assert.AreEqual(SnmpVersion.V1, explicitOptions.Version);
            Assert.AreEqual(1200, explicitOptions.TimeoutMilliseconds);
            Assert.AreEqual(2, explicitOptions.RetryCount);
            Assert.AreEqual(20, explicitOptions.MaxRepetitions);
            Assert.AreEqual(600, explicitOptions.DiscoveryIcmpTimeoutMilliseconds);
        }

        [DataTestMethod]
        [DataRow("--port", "0", "INVALID_PORT")]
        [DataRow("--version", "99", "INVALID_SNMP_VERSION")]
        [DataRow("--timeout-ms", "0", "INVALID_TIMEOUT")]
        [DataRow("--retries", "-1", "INVALID_RETRY_COUNT")]
        [DataRow("--max-repetitions", "0", "INVALID_MAX_REPETITIONS")]
        [DataRow("--icmp-timeout-ms", "0", "INVALID_ICMP_TIMEOUT")]
        public void InvalidCheckOptionsUseDiscoveryErrorCodes(string option, string value, string expected)
        {
            var error = Assert.ThrowsExactly<ArgumentException>(() => EngineCommandLine.Parse(new[]
                { "check-profile", "--address", "192.0.2.1", option, value }));
            Assert.AreEqual(expected, error.Message);
        }

        [TestMethod]
        public async Task EnteredCommunityOnlyAppearsInEnvironmentAndIsClearedAfterCheck()
        {
            var factory = new ProfileProcessFactory(new ProfileSession(true));
            var provider = new ProfileEnvironmentProvider();
            var secret = Encoding.UTF8.GetBytes("profile-check-fixture-secret");
            using (var checker = new DesktopEngineProfileCheck("fixture-engine.exe", factory, provider))
            {
                var report = await checker.CheckAsync(IPAddress.Parse("192.0.2.1"), SnmpVersion.V2C, secret, Guid.NewGuid());
                Assert.AreEqual(6, report.Items.Count);
                Assert.IsTrue(report.Items.All(item => item.Status == SnmpProfileCheckStatus.Ok));
                Assert.AreEqual("profile-check-fixture-secret", factory.Request.EnvironmentVariables["NETLOOM_SNMP_COMMUNITY"]);
                Assert.IsFalse(factory.Request.Arguments.Contains("profile-check-fixture-secret"));
                Assert.IsFalse(factory.Request.Arguments.Contains("COMMUNITY"));
                Assert.IsTrue(factory.Request.Arguments.StartsWith("check-profile ", StringComparison.Ordinal));
                var parsed = EngineCommandLine.Parse(DesktopEngineProfileCheck.BuildTokens(IPAddress.Parse("192.0.2.1"), SnmpVersion.V2C).ToArray());
                Assert.AreEqual(IPAddress.Parse("192.0.2.1"), parsed.Address);
                Assert.AreEqual(0, provider.Calls);
                Assert.IsTrue(secret.All(value => value == 0));
            }
        }

        [TestMethod]
        public async Task BlankCommunityUsesSavedProfileEnvironment()
        {
            var factory = new ProfileProcessFactory(new ProfileSession(true));
            var provider = new ProfileEnvironmentProvider();
            var profileId = Guid.NewGuid();
            using (var checker = new DesktopEngineProfileCheck("fixture-engine.exe", factory, provider))
            {
                await checker.CheckAsync(IPAddress.Parse("192.0.2.1"), SnmpVersion.V1, null, profileId);
                Assert.AreEqual(1, provider.Calls);
                Assert.AreEqual(profileId, provider.ProfileId);
                Assert.AreEqual(SnmpVersion.V1, provider.Version);
                Assert.AreEqual("saved-fixture-secret", factory.Request.EnvironmentVariables["NETLOOM_SNMP_COMMUNITY"]);
                Assert.IsFalse(factory.Request.Arguments.Contains("saved-fixture-secret"));
            }
        }

        [TestMethod]
        public async Task TimeoutTerminatesHungProcessPreservesReceivedItemsAndFailsMissingItems()
        {
            var session = new ProfileSession(false);
            var secret = Encoding.UTF8.GetBytes("profile-check-fixture-secret");
            using (var checker = new DesktopEngineProfileCheck("fixture-engine.exe", new ProfileProcessFactory(session),
                new ProfileEnvironmentProvider(), TimeSpan.FromMilliseconds(10)))
            {
                var report = await checker.CheckAsync(IPAddress.Parse("192.0.2.1"), SnmpVersion.V2C, secret, null);
                Assert.IsTrue(session.Terminated);
                Assert.IsTrue(session.Disposed);
                Assert.AreEqual(SnmpProfileCheckStatus.Ok, report.Items[0].Status);
                Assert.IsTrue(report.Items.Skip(1).All(item => item.Status == SnmpProfileCheckStatus.Failed
                    && item.Failure == SnmpTransportFailure.Timeout));
                Assert.IsTrue(secret.All(value => value == 0));
            }
        }

        [TestMethod]
        public async Task SavedCommunityCanCheckChangedVersionWithoutSavingProfile()
        {
            var factory = new ProfileProcessFactory(new ProfileSession(true));
            var provider = new ProfileEnvironmentProvider { StoredVersion = SnmpVersion.V2C };
            using (var checker = new DesktopEngineProfileCheck("fixture-engine.exe", factory, provider))
            {
                var report = await checker.CheckAsync(IPAddress.Parse("192.0.2.1"), SnmpVersion.V1, null, Guid.NewGuid());
                Assert.IsTrue(report.Items.All(item => item.Status == SnmpProfileCheckStatus.Ok));
                Assert.AreEqual(2, provider.Calls);
                Assert.AreEqual("saved-fixture-secret", factory.Request.EnvironmentVariables["NETLOOM_SNMP_COMMUNITY"]);
                Assert.IsTrue(factory.Request.Arguments.Contains("--version V1"));
            }
        }

        [TestMethod]
        public async Task MalformedProcessMarkersFailReportInsteadOfAcceptingLaterValidItems()
        {
            var session = new ProfileSession(true) { Malformed = true };
            using (var checker = new DesktopEngineProfileCheck("fixture-engine.exe", new ProfileProcessFactory(session),
                new ProfileEnvironmentProvider()))
            {
                var report = await checker.CheckAsync(IPAddress.Parse("192.0.2.1"), SnmpVersion.V2C,
                    Encoding.UTF8.GetBytes("profile-check-fixture-secret"), null);
                Assert.IsTrue(report.Items.All(item => item.Status == SnmpProfileCheckStatus.Failed
                    && item.Failure == SnmpTransportFailure.Protocol));
            }
        }

        private sealed class ProfileEnvironmentProvider : IDiscoveryProcessEnvironmentProvider
        {
            public int Calls;
            public Guid ProfileId;
            public SnmpVersion Version;
            public SnmpVersion? StoredVersion;
            public IReadOnlyDictionary<string, string> CreateEnvironment(Guid profileId, SnmpVersion version)
            {
                Calls++;
                if (StoredVersion.HasValue && StoredVersion.Value != version)
                    throw new InvalidOperationException("DISCOVERY_ACCESS_PROFILE_VERSION_MISMATCH");
                ProfileId = profileId;
                Version = version;
                return new Dictionary<string, string> { { "NETLOOM_SNMP_COMMUNITY", "saved-fixture-secret" } };
            }
        }

        private sealed class ProfileProcessFactory : IEngineProcessFactory
        {
            private readonly ProfileSession _session;
            public EngineProcessStartRequest Request;
            public ProfileProcessFactory(ProfileSession session) { _session = session; }
            public IEngineProcessSession Start(EngineProcessStartRequest request) { Request = request; return _session; }
            public void Dispose() { }
        }

        private sealed class ProfileSession : IEngineProcessSession
        {
            private readonly bool _complete;
            private readonly TaskCompletionSource<int> _completion = new TaskCompletionSource<int>();
            public bool Terminated;
            public bool Malformed;
            public bool Disposed;
            public ProfileSession(bool complete) { _complete = complete; }
            public event Action<string> OutputLineReceived;
            public event Action<string> ErrorLineReceived;
            public Task<int> Completion => _completion.Task;
            public void BeginRead()
            {
                OutputLineReceived?.Invoke("NETLOOM_PROFILE_CHECK item=availability status=ok count=none ms=14 failure=none");
                if (!_complete) return;
                if (Malformed) OutputLineReceived?.Invoke("NETLOOM_PROFILE_CHECK item=system status=invalid count=none ms=none failure=none");
                foreach (var kind in new[] { "system", "if-mib", "lldp-mib", "bridge-mib", "q-bridge-mib" })
                    OutputLineReceived?.Invoke("NETLOOM_PROFILE_CHECK item=" + kind + " status=ok count=none ms=none failure=none");
                OutputLineReceived?.Invoke("profile-check-fixture-secret");
                ErrorLineReceived?.Invoke("profile-check-fixture-secret");
                OutputLineReceived?.Invoke("NETLOOM_PROFILE_CHECK state=completed");
                _completion.SetResult(0);
            }
            public void WriteLine(string line) { throw new AssertFailedException("Profile check has no stdin commands."); }
            public void Terminate() { Terminated = true; }
            public void Dispose() { Disposed = true; }
        }
    }
}
