using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Inventory;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint42EngineDiscoveryHostingTests
    {
        [TestMethod]
        public void DiscoverCommandUsesOneExplicitProfileAndSafeDefaults()
        {
            var profileId =
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555");

            var parsed =
                EngineCommandLine.Parse(
                    new[]
                    {
                        "discover",
                        "--cidr",
                        "192.0.2.0/30",
                        "--access-profile-id",
                        profileId.ToString("D")
                    });

            Assert.AreEqual(
                "discover",
                parsed.Command);
            Assert.AreEqual(
                "192.0.2.0/30",
                parsed.DiscoveryCidr);
            Assert.AreEqual(
                profileId,
                parsed.AccessProfileId);
            Assert.AreEqual(
                161,
                parsed.Port);
            Assert.AreEqual(
                SnmpVersion.V2C,
                parsed.Version);
            Assert.AreEqual(
                750,
                parsed.TimeoutMilliseconds);
            Assert.AreEqual(
                0,
                parsed.RetryCount);
            Assert.AreEqual(
                10,
                parsed.MaxRepetitions);
            Assert.AreEqual(
                500,
                parsed.DiscoveryIcmpTimeoutMilliseconds);
            Assert.AreEqual(
                500,
                parsed.DiscoveryTcpTimeoutMilliseconds);
            CollectionAssert.AreEqual(
                new[] { 22, 80, 443 },
                new List<int>(
                    parsed.DiscoveryTcpPorts));
            Assert.AreEqual(
                50,
                parsed.DiscoveryInterAddressDelayMilliseconds);
            Assert.AreEqual(
                4096,
                parsed.DiscoveryMaxAddresses);
            Assert.IsTrue(
                parsed.ControlStdin);
        }

        [TestMethod]
        public void DiscoverCommandRejectsMissingProfileAndMonitoringOnlyOptions()
        {
            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "discover",
                        "--cidr",
                        "192.0.2.0/30"
                    });

                Assert.Fail(
                    "Discovery must require one explicit access profile id.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "MISSING_ACCESS-PROFILE-ID",
                    exception.Message);
            }

            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "discover",
                        "--cidr",
                        "192.0.2.0/30",
                        "--access-profile-id",
                        Guid.NewGuid().ToString("D"),
                        "--device-id",
                        Guid.NewGuid().ToString("D")
                    });

                Assert.Fail(
                    "Discovery must reject monitoring-only options.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "UNKNOWN_OPTION_device-id",
                    exception.Message);
            }
        }

        [TestMethod]
        public void DiscoveryStopAndEndOfInputCancelOwnedRun()
        {
            using (var stopCancellation =
                new CancellationTokenSource())
            {
                var output =
                    new RecordingWriter();

                var control =
                    new EngineDiscoveryStdinControl(
                        stopCancellation,
                        new StringReader(string.Empty),
                        output);

                control.AcceptLine(
                    "STOP");

                Assert.IsTrue(
                    stopCancellation.IsCancellationRequested);

                StringAssert.Contains(
                    output.ToString(),
                    "NETLOOM_DISCOVERY_CONTROL command=STOP result=accepted");
            }

            using (var eofCancellation =
                new CancellationTokenSource())
            {
                var output =
                    new RecordingWriter();

                var control =
                    new EngineDiscoveryStdinControl(
                        eofCancellation,
                        new StringReader(string.Empty),
                        output);

                control.StartReading();

                Assert.IsTrue(
                    SpinWait.SpinUntil(
                        () => eofCancellation
                            .IsCancellationRequested,
                        TimeSpan.FromSeconds(2)));

                StringAssert.Contains(
                    output.ToString(),
                    "NETLOOM_DISCOVERY_CONTROL event=EOF action=stop");
            }
        }

        [TestMethod]
        public void DiscoveryRunnerStreamsProgressCandidateAndCompletion()
        {
            var profileId =
                Guid.Parse(
                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

            var request =
                CreateRequest(
                    profileId,
                    new[]
                    {
                        IPAddress.Parse("192.0.2.10"),
                        IPAddress.Parse("192.0.2.11")
                    });

            var network =
                new RecordingNetworkProbe();

            var engine =
                new DiscoveryEngine(
                    network,
                    new SuccessfulInventoryCollector());

            var output =
                new RecordingWriter();

            var completed =
                EngineDiscoveryRunner.Run(
                    engine,
                    request,
                    CancellationToken.None,
                    output);

            Assert.IsTrue(completed);

            var text =
                output.ToString();

            StringAssert.Contains(
                text,
                "NETLOOM_DISCOVERY state=started accessProfileId=" +
                profileId.ToString("D") +
                " total=2 delayMs=0");
            StringAssert.Contains(
                text,
                "NETLOOM_DISCOVERY state=progress processed=1 total=2 found=1 address=192.0.2.10 candidate=true");
            StringAssert.Contains(
                text,
                "NETLOOM_DISCOVERY_CANDIDATE address=192.0.2.10 accessProfileId=" +
                profileId.ToString("D") +
                " icmp=false snmp=true tcpPorts=none");
            StringAssert.Contains(
                text,
                "NETLOOM_DISCOVERY state=completed processed=2 total=2 found=2");

            Assert.AreEqual(
                2,
                network.ProbedAddresses.Count);
            Assert.IsTrue(
                output.FlushCount >= 6);

            foreach (var character in text)
            {
                Assert.IsTrue(
                    character <= 127,
                    "Discovery machine markers must remain ASCII.");
            }
        }

        [TestMethod]
        public void DiscoveryRunnerReportsStoppedBeforeNextAddress()
        {
            var profileId =
                Guid.Parse(
                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

            var request =
                CreateRequest(
                    profileId,
                    new[]
                    {
                        IPAddress.Parse("192.0.2.20"),
                        IPAddress.Parse("192.0.2.21")
                    });

            var network =
                new RecordingNetworkProbe();

            var engine =
                new DiscoveryEngine(
                    network,
                    new SuccessfulInventoryCollector());

            using (var cancellation =
                new CancellationTokenSource())
            {
                var output =
                    new CancelAfterFirstProgressWriter(
                        cancellation);

                var completed =
                    EngineDiscoveryRunner.Run(
                        engine,
                        request,
                        cancellation.Token,
                        output);

                Assert.IsFalse(completed);

                StringAssert.Contains(
                    output.ToString(),
                    "NETLOOM_DISCOVERY state=stopped processed=1 total=2 found=1");

                CollectionAssert.AreEqual(
                    new[] { "192.0.2.20" },
                    network.ProbedAddresses);
            }
        }

        [TestMethod]
        public void DiscoveryMachineMarkersRemainInvariantUnderNonEnglishCulture()
        {
            var previousCulture =
                CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture =
                    CultureInfo.GetCultureInfo(
                        "fr-FR");

                var output =
                    new RecordingWriter();

                EngineMachineOutput
                    .WriteDiscoveryControlReady(
                        output);

                EngineMachineOutput
                    .WriteDiscoveryCandidate(
                        output,
                        Candidate(
                            Guid.Parse(
                                "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));

                var text =
                    output.ToString();

                StringAssert.Contains(
                    text,
                    "NETLOOM_DISCOVERY_CONTROL state=ready");
                StringAssert.Contains(
                    text,
                    "sysName64=" +
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            "Узел")));

                foreach (var character in text)
                {
                    Assert.IsTrue(
                        character <= 127,
                        "Discovery machine markers must remain ASCII.");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture =
                    previousCulture;
            }
        }

        private static DiscoveryRequest CreateRequest(
            Guid profileId,
            IReadOnlyList<IPAddress> addresses)
        {
            return new DiscoveryRequest(
                addresses,
                new IPAddress[0],
                new[] { 22, 80, 443 },
                new DiscoverySnmpProfile(
                    profileId,
                    161,
                    SnmpVersion.V2C,
                    new SnmpCommunityCredentials(
                        Encoding.ASCII.GetBytes(
                            "synthetic-canary")),
                    100,
                    0,
                    10),
                100,
                100,
                0);
        }

        private static DiscoveryCandidate Candidate(
            Guid profileId)
        {
            return new DiscoveryCandidate(
                IPAddress.Parse("192.0.2.30"),
                true,
                new[] { 443 },
                new InventorySnapshot(
                    IPAddress.Parse("192.0.2.30"),
                    "Узел",
                    "Описание",
                    "1.3.6.1.4.1.99999",
                    null,
                    "Цех",
                    "12345",
                    new InventoryInterface[0]),
                profileId);
        }

        private sealed class RecordingNetworkProbe
            : INetworkDiscoveryProbe
        {
            public RecordingNetworkProbe()
            {
                ProbedAddresses =
                    new List<string>();
            }

            public List<string> ProbedAddresses { get; }

            public bool IsIcmpReachable(
                IPAddress address,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ProbedAddresses.Add(
                    address.ToString());

                return false;
            }

            public IReadOnlyList<int> FindOpenTcpPorts(
                IPAddress address,
                IReadOnlyList<int> ports,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new int[0];
            }
        }

        private sealed class SuccessfulInventoryCollector
            : IInventoryCollector
        {
            public InventorySnapshot Collect(
                InventoryCollectionRequest request)
            {
                return new InventorySnapshot(
                    request.Address,
                    "Узел",
                    "Описание",
                    "1.3.6.1.4.1.99999",
                    null,
                    "Цех",
                    "12345",
                    new InventoryInterface[0]);
            }
        }

        private class RecordingWriter
            : StringWriter
        {
            public int FlushCount { get; private set; }

            public override void Flush()
            {
                FlushCount++;
                base.Flush();
            }
        }

        private sealed class CancelAfterFirstProgressWriter
            : RecordingWriter
        {
            private readonly CancellationTokenSource
                _cancellation;

            private bool _cancelled;

            public CancelAfterFirstProgressWriter(
                CancellationTokenSource cancellation)
            {
                _cancellation =
                    cancellation ??
                        throw new ArgumentNullException(
                            nameof(cancellation));
            }

            public override void WriteLine(
                string value)
            {
                base.WriteLine(value);

                if (!_cancelled &&
                    value != null &&
                    value.IndexOf(
                        "NETLOOM_DISCOVERY state=progress processed=1 ",
                        StringComparison.Ordinal) >= 0)
                {
                    _cancelled = true;
                    _cancellation.Cancel();
                }
            }
        }
    }
}
