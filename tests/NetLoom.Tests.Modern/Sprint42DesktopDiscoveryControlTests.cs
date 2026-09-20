using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Desktop.Discovery;
using NetLoom.Desktop.Monitoring;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint42DesktopDiscoveryControlTests
    {
        private static readonly Guid ProfileId =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555");

        [TestMethod]
        public void CommandBuilderUsesOneExplicitProfileAndSafeDefaults()
        {
            var request =
                new DiscoveryControlRequest(
                    "192.0.2.0/30",
                    ProfileId,
                    SnmpVersion.V2C);

            var tokens =
                EngineDiscoveryCommandBuilder
                    .BuildTokens(
                        request);

            CollectionAssert.AreEqual(
                new[]
                {
                    "discover",
                    "--cidr",
                    "192.0.2.0/30",
                    "--access-profile-id",
                    ProfileId.ToString("D"),
                    "--port",
                    "161",
                    "--version",
                    "V2C",
                    "--timeout-ms",
                    "750",
                    "--retries",
                    "0",
                    "--max-repetitions",
                    "10",
                    "--icmp-timeout-ms",
                    "500",
                    "--tcp-timeout-ms",
                    "500",
                    "--tcp-ports",
                    "22,80,443",
                    "--inter-address-delay-ms",
                    "50",
                    "--max-addresses",
                    "4096",
                    "--control-stdin",
                    "true"
                },
                new List<string>(tokens));

            var arguments =
                EngineDiscoveryCommandBuilder
                    .FormatArguments(
                        tokens);

            Assert.AreEqual(
                -1,
                arguments.IndexOf(
                    "community",
                    StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(
                -1,
                arguments.IndexOf(
                    "password",
                    StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void MarkerParserDecodesDocumentedCandidateWithoutLocalizedParsing()
        {
            EngineDiscoveryMarker marker;

            Assert.IsFalse(
                EngineDiscoveryMarkerParser.TryParse(
                    "DISCOVERY: found device",
                    out marker));

            Assert.IsTrue(
                EngineDiscoveryMarkerParser.TryParse(
                    "NETLOOM_DISCOVERY state=started accessProfileId=" +
                    ProfileId.ToString("D") +
                    " total=4 delayMs=50",
                    out marker));

            Assert.AreEqual(
                EngineDiscoveryMarkerKind.Started,
                marker.Kind);
            Assert.AreEqual(
                ProfileId,
                marker.AccessProfileId);
            Assert.AreEqual(
                4,
                marker.TotalAddresses);

            var candidateLine =
                CandidateMarker(
                    "192.0.2.1",
                    ProfileId,
                    true,
                    true,
                    "22,443",
                    "Коммутатор",
                    "Описание устройства",
                    "1.3.6.1.4.1.9.1",
                    "Шкаф 1",
                    24);

            Assert.IsTrue(
                EngineDiscoveryMarkerParser.TryParse(
                    candidateLine,
                    out marker));

            Assert.AreEqual(
                EngineDiscoveryMarkerKind.Candidate,
                marker.Kind);
            Assert.AreEqual(
                IPAddress.Parse("192.0.2.1"),
                marker.Candidate.Address);
            Assert.AreEqual(
                "Коммутатор",
                marker.Candidate.SysName);
            Assert.AreEqual(
                "Описание устройства",
                marker.Candidate.SysDescription);
            Assert.AreEqual(
                "Шкаф 1",
                marker.Candidate.SysLocation);
            CollectionAssert.AreEqual(
                new[] { 22, 443 },
                new List<int>(
                    marker.Candidate.OpenTcpPorts));

            Assert.IsFalse(
                EngineDiscoveryMarkerParser.TryParse(
                    "NETLOOM_DISCOVERY_CANDIDATE address=192.0.2.1 accessProfileId=" +
                    ProfileId.ToString("D") +
                    " icmp=true snmp=true tcpPorts=22 sysName64=%%% sysDescription64=- sysObjectId64=- sysLocation64=- interfaces=1",
                    out marker));
        }

        [TestMethod]
        public async Task StartStreamsProgressCandidateAndTerminalCompletion()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineDiscoveryControl(
                    "NetLoom.Engine.exe",
                    factory))
            {
                DiscoveryCandidateSnapshot observed = null;

                control.CandidateDiscovered +=
                    (sender, eventArgs) =>
                    {
                        observed =
                            eventArgs.Candidate;
                    };

                var request =
                    DefaultRequest();

                var start =
                    control.StartAsync(
                        request,
                        CancellationToken.None);

                var process =
                    factory.LastSession;

                Assert.IsNotNull(process);
                Assert.IsTrue(process.ReadStarted);
                Assert.AreEqual(
                    DiscoveryControlState.Starting,
                    control.Current.State);

                process.EmitOutput(
                    "NETLOOM_DISCOVERY_CONTROL state=ready");
                process.EmitOutput(
                    StartedMarker(
                        4));

                await start;

                Assert.AreEqual(
                    DiscoveryControlState.Running,
                    control.Current.State);
                Assert.AreEqual(
                    4,
                    control.Current.TotalAddresses);

                process.EmitOutput(
                    "NETLOOM_DISCOVERY state=progress processed=1 total=4 found=1 address=192.0.2.1 candidate=true");
                process.EmitOutput(
                    CandidateMarker(
                        "192.0.2.1",
                        ProfileId,
                        true,
                        true,
                        "22,443",
                        "Коммутатор",
                        "Описание",
                        "1.3.6.1.4.1.9.1",
                        "Шкаф 1",
                        24));

                Assert.AreEqual(
                    1,
                    control.Current.ProcessedAddresses);
                Assert.AreEqual(
                    1,
                    control.Current.FoundCandidates);
                Assert.AreEqual(
                    IPAddress.Parse("192.0.2.1"),
                    control.Current.CurrentAddress);

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () => observed != null,
                        TimeSpan.FromSeconds(2)));

                Assert.AreEqual(
                    "Коммутатор",
                    observed.SysName);
                Assert.AreEqual(
                    24,
                    observed.InterfaceCount);

                process.EmitOutput(
                    "NETLOOM_DISCOVERY state=completed processed=4 total=4 found=1");
                process.Complete(
                    0);

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () =>
                            control.Current.State ==
                            DiscoveryControlState.Completed,
                        TimeSpan.FromSeconds(2)));

                Assert.AreEqual(
                    4,
                    control.Current.ProcessedAddresses);
                Assert.AreEqual(
                    1,
                    control.Current.FoundCandidates);
            }
        }

        [TestMethod]
        public async Task StopWritesCooperativeCommandAndPreservesStoppedSummary()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineDiscoveryControl(
                    "NetLoom.Engine.exe",
                    factory))
            {
                var start =
                    control.StartAsync(
                        DefaultRequest(),
                        CancellationToken.None);

                var process =
                    factory.LastSession;

                process.EmitOutput(
                    StartedMarker(
                        4));

                await start;

                process.EmitOutput(
                    "NETLOOM_DISCOVERY state=progress processed=1 total=4 found=0 address=192.0.2.1 candidate=false");

                var stop =
                    control.StopAsync(
                        CancellationToken.None);

                Assert.AreEqual(
                    DiscoveryControlState.Stopping,
                    control.Current.State);
                CollectionAssert.Contains(
                    new List<string>(process.WrittenLines),
                    "STOP");

                process.EmitOutput(
                    "NETLOOM_DISCOVERY state=stopped processed=1 total=4 found=0");
                process.Complete(
                    0);

                await stop;

                Assert.AreEqual(
                    DiscoveryControlState.Stopped,
                    control.Current.State);
                Assert.AreEqual(
                    1,
                    control.Current.ProcessedAddresses);
                Assert.AreEqual(
                    4,
                    control.Current.TotalAddresses);
            }
        }

        [TestMethod]
        public async Task UnexpectedExitWithoutTerminalMarkerBecomesFaulted()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineDiscoveryControl(
                    "NetLoom.Engine.exe",
                    factory))
            {
                var start =
                    control.StartAsync(
                        DefaultRequest(),
                        CancellationToken.None);

                factory.LastSession.EmitOutput(
                    StartedMarker(
                        4));

                await start;

                factory.LastSession.Complete(
                    9);

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () =>
                            control.Current.State ==
                            DiscoveryControlState.Faulted,
                        TimeSpan.FromSeconds(2)));

                Assert.AreEqual(
                    "ENGINE_PROCESS_EXITED_9",
                    control.Current.FaultMessage);
            }
        }

        [TestMethod]
        public async Task StartedMarkerFromAnotherProfileIsRejected()
        {
            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineDiscoveryControl(
                    "NetLoom.Engine.exe",
                    factory))
            {
                var start =
                    control.StartAsync(
                        DefaultRequest(),
                        CancellationToken.None);

                factory.LastSession.EmitOutput(
                    "NETLOOM_DISCOVERY state=started accessProfileId=" +
                    Guid.NewGuid().ToString("D") +
                    " total=4 delayMs=50");

                try
                {
                    await start;
                    Assert.Fail(
                        "A marker for another profile must not start the session.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.AreEqual(
                        "ENGINE_DISCOVERY_PROFILE_MISMATCH",
                        exception.Message);
                }

                Assert.AreEqual(
                    DiscoveryControlState.Faulted,
                    control.Current.State);
                Assert.IsTrue(
                    factory.LastSession.Terminated);
            }
        }

        private static DiscoveryControlRequest DefaultRequest()
        {
            return new DiscoveryControlRequest(
                "192.0.2.0/30",
                ProfileId,
                SnmpVersion.V2C);
        }

        private static string StartedMarker(
            int total)
        {
            return
                "NETLOOM_DISCOVERY state=started accessProfileId=" +
                ProfileId.ToString("D") +
                " total=" +
                total.ToString(
                    CultureInfo.InvariantCulture) +
                " delayMs=50";
        }

        private static string CandidateMarker(
            string address,
            Guid profileId,
            bool icmp,
            bool snmp,
            string tcpPorts,
            string sysName,
            string sysDescription,
            string sysObjectId,
            string sysLocation,
            int interfaces)
        {
            return
                "NETLOOM_DISCOVERY_CANDIDATE address=" +
                address +
                " accessProfileId=" +
                (snmp
                    ? profileId.ToString("D")
                    : "none") +
                " icmp=" +
                (icmp ? "true" : "false") +
                " snmp=" +
                (snmp ? "true" : "false") +
                " tcpPorts=" +
                tcpPorts +
                " sysName64=" +
                Encode(sysName) +
                " sysDescription64=" +
                Encode(sysDescription) +
                " sysObjectId64=" +
                Encode(sysObjectId) +
                " sysLocation64=" +
                Encode(sysLocation) +
                " interfaces=" +
                interfaces.ToString(
                    CultureInfo.InvariantCulture);
        }

        private static string Encode(
            string value)
        {
            return string.IsNullOrEmpty(value)
                ? "-"
                : Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        value));
        }

        private sealed class FakeEngineProcessFactory :
            IEngineProcessFactory
        {
            public EngineProcessStartRequest LastRequest { get; private set; }

            public FakeEngineProcessSession LastSession { get; private set; }

            public IEngineProcessSession Start(
                EngineProcessStartRequest request)
            {
                LastRequest = request;
                LastSession =
                    new FakeEngineProcessSession();
                return LastSession;
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeEngineProcessSession :
            IEngineProcessSession
        {
            private readonly TaskCompletionSource<int>
                _completion =
                    new TaskCompletionSource<int>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

            public event Action<string> OutputLineReceived;

            public event Action<string> ErrorLineReceived;

            public Task<int> Completion =>
                _completion.Task;

            public IList<string> WrittenLines { get; } =
                new List<string>();

            public bool ReadStarted { get; private set; }

            public bool Terminated { get; private set; }

            public void BeginRead()
            {
                ReadStarted = true;
            }

            public void WriteLine(
                string line)
            {
                WrittenLines.Add(
                    line);
            }

            public void Terminate()
            {
                Terminated = true;
                _completion.TrySetResult(
                    -1);
            }

            public void EmitOutput(
                string line)
            {
                OutputLineReceived?.Invoke(
                    line);
            }

            public void EmitError(
                string line)
            {
                ErrorLineReceived?.Invoke(
                    line);
            }

            public void Complete(
                int exitCode)
            {
                _completion.TrySetResult(
                    exitCode);
            }

            public void Dispose()
            {
            }
        }
    }
}
