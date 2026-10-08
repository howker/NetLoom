using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Inventory;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class DiscoveryEngineTests
    {
        [TestMethod]
        public void FailedIcmpDoesNotBlockSnmpDiscovery()
        {
            var profileId = Guid.NewGuid();

            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new int[0]),
                new SuccessfulInventoryCollector());

            var results = engine.Discover(
                CreateRequest(
                    profileId,
                    new[]
                    {
                        IPAddress.Parse("192.0.2.10")
                    }));

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IcmpReachable);
            Assert.IsTrue(results[0].SnmpResponded);
            Assert.AreEqual(
                profileId,
                results[0].AccessProfileId);
        }

        [TestMethod]
        public void OpenTcpPortCreatesCandidateWithoutSnmp()
        {
            var inventory =
                new CountingFailingInventoryCollector();

            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new[] { 443 }),
                inventory);

            var results = engine.Discover(
                CreateRequest(
                    Guid.NewGuid(),
                    new[]
                    {
                        IPAddress.Parse("192.0.2.20")
                    }));

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].SnmpResponded);
            Assert.AreEqual(
                443,
                results[0].OpenTcpPorts[0]);
            Assert.AreEqual(
                1,
                inventory.CollectionCount);
        }

        [TestMethod]
        public void ExcludedAddressIsNotProbed()
        {
            var address =
                IPAddress.Parse("192.0.2.30");

            var network =
                new CountingNetworkProbe();

            var inventory =
                new CountingFailingInventoryCollector();

            var request = new DiscoveryRequest(
                new[] { address },
                new[] { address },
                new[] { 22, 443 },
                CreateProfile(Guid.NewGuid()),
                100,
                100,
                0);

            var engine =
                new DiscoveryEngine(
                    network,
                    inventory);

            var results =
                engine.Discover(request);

            Assert.AreEqual(0, results.Count);
            Assert.AreEqual(0, network.ProbeCount);
            Assert.AreEqual(0, inventory.CollectionCount);
        }

        [TestMethod]
        public void StreamingProgressDoesNotProbeLaterAddressEarly()
        {
            var first =
                IPAddress.Parse("192.0.2.40");

            var second =
                IPAddress.Parse("192.0.2.41");

            var network =
                new AddressRecordingNetworkProbe();

            var engine = new DiscoveryEngine(
                network,
                new SuccessfulInventoryCollector());

            using (var enumerator =
                engine.Discover(
                        CreateRequest(
                            Guid.NewGuid(),
                            new[] { first, second }),
                        CancellationToken.None)
                    .GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());

                Assert.AreEqual(
                    first,
                    enumerator.Current.Address);
                Assert.AreEqual(
                    1,
                    enumerator.Current.ProcessedAddresses);
                Assert.AreEqual(
                    2,
                    enumerator.Current.TotalAddresses);
                Assert.IsTrue(
                    enumerator.Current.CandidateFound);
                Assert.IsFalse(
                    network.ProbedAddresses.Contains(
                        second.ToString()));
            }
        }

        [TestMethod]
        public void CancellationStopsBeforeNextAddress()
        {
            var first =
                IPAddress.Parse("192.0.2.50");

            var second =
                IPAddress.Parse("192.0.2.51");

            var network =
                new AddressRecordingNetworkProbe();

            var engine = new DiscoveryEngine(
                network,
                new SuccessfulInventoryCollector());

            using (var cancellation =
                new CancellationTokenSource())
            using (var enumerator =
                engine.Discover(
                        CreateRequest(
                            Guid.NewGuid(),
                            new[] { first, second }),
                        cancellation.Token)
                    .GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());

                cancellation.Cancel();

                try
                {
                    enumerator.MoveNext();
                    Assert.Fail(
                        "Cancellation must stop discovery before the next address.");
                }
                catch (OperationCanceledException)
                {
                }

                Assert.IsFalse(
                    network.ProbedAddresses.Contains(
                        second.ToString()));
            }
        }

        [TestMethod]
        public void InterAddressDelayThrottlesSecondProbe()
        {
            var request = CreateRequest(
                Guid.NewGuid(),
                new[]
                {
                    IPAddress.Parse("192.0.2.60"),
                    IPAddress.Parse("192.0.2.61")
                },
                80);

            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new int[0]),
                new CountingFailingInventoryCollector());

            using (var enumerator =
                engine.Discover(
                        request,
                        CancellationToken.None)
                    .GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());

                var stopwatch =
                    Stopwatch.StartNew();

                Assert.IsTrue(enumerator.MoveNext());

                stopwatch.Stop();

                Assert.IsTrue(
                    stopwatch.ElapsedMilliseconds >= 60,
                    "Discovery must apply the configured delay between address starts.");
            }
        }

        [TestMethod]
        public void OneAddressAttemptsExactlyOneSnmpProfile()
        {
            var inventory =
                new CountingFailingInventoryCollector();

            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new int[0]),
                inventory);

            engine.Discover(
                CreateRequest(
                    Guid.NewGuid(),
                    new[]
                    {
                        IPAddress.Parse("192.0.2.70")
                    }));

            Assert.AreEqual(
                1,
                inventory.CollectionCount);
        }

        [TestMethod]
        public void PhasesAreReportedInProbeOrderForEachAddress()
        {
            var first = IPAddress.Parse("192.0.2.80");
            var second = IPAddress.Parse("192.0.2.81");
            var calls = new List<string>();
            var phases = new List<DiscoveryPhaseUpdate>();
            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new int[0],
                    calls),
                new SuccessfulInventoryCollector(
                    calls));

            var results = engine.Discover(
                    CreateRequest(
                        Guid.NewGuid(),
                        new[] { first, second }),
                    CancellationToken.None,
                    update =>
                    {
                        phases.Add(update);
                        calls.Add(
                            "phase:" + update.Phase + ":" + update.Address);
                    })
                .ToArray();

            Assert.AreEqual(
                2,
                results.Length);
            Assert.AreEqual(
                6,
                phases.Count);

            var expectedPhases = new[]
            {
                DiscoveryPhase.Icmp,
                DiscoveryPhase.Tcp,
                DiscoveryPhase.Snmp
            };

            for (var index = 0; index < phases.Count; index++)
            {
                Assert.AreEqual(
                    index < 3 ? first : second,
                    phases[index].Address);
                Assert.AreEqual(
                    index / 3 + 1,
                    phases[index].AddressIndex);
                Assert.AreEqual(
                    2,
                    phases[index].TotalAddresses);
                Assert.AreEqual(
                    expectedPhases[index % 3],
                    phases[index].Phase);
                Assert.AreEqual(
                    index % 3 + 1,
                    phases[index].Step);
                Assert.AreEqual(
                    3,
                    phases[index].StepCount);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "phase:Icmp:192.0.2.80",
                    "probe:icmp:192.0.2.80",
                    "phase:Tcp:192.0.2.80",
                    "probe:tcp:192.0.2.80",
                    "phase:Snmp:192.0.2.80",
                    "probe:snmp:192.0.2.80",
                    "phase:Icmp:192.0.2.81",
                    "probe:icmp:192.0.2.81",
                    "phase:Tcp:192.0.2.81",
                    "probe:tcp:192.0.2.81",
                    "phase:Snmp:192.0.2.81",
                    "probe:snmp:192.0.2.81"
                },
                calls);
        }

        [TestMethod]
        public void TcpPhaseIsSkippedWhenNoPortsAreConfigured()
        {
            var address = IPAddress.Parse("192.0.2.82");
            var calls = new List<string>();
            var phases = new List<DiscoveryPhaseUpdate>();
            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(
                    false,
                    new int[0],
                    calls),
                new SuccessfulInventoryCollector(
                    calls));

            var request = new DiscoveryRequest(
                new[] { address },
                new IPAddress[0],
                new int[0],
                CreateProfile(Guid.NewGuid()),
                100,
                100,
                0);

            var results = engine.Discover(
                    request,
                    CancellationToken.None,
                    update =>
                    {
                        phases.Add(update);
                        calls.Add(
                            "phase:" + update.Phase + ":" + update.Address);
                    })
                .ToArray();

            Assert.AreEqual(
                1,
                results.Length);
            Assert.AreEqual(
                0,
                results[0].Candidate.OpenTcpPorts.Count);
            Assert.AreEqual(
                2,
                phases.Count);
            Assert.AreEqual(
                DiscoveryPhase.Icmp,
                phases[0].Phase);
            Assert.AreEqual(
                1,
                phases[0].Step);
            Assert.AreEqual(
                DiscoveryPhase.Snmp,
                phases[1].Phase);
            Assert.AreEqual(
                2,
                phases[1].Step);

            foreach (var phase in phases)
            {
                Assert.AreEqual(
                    address,
                    phase.Address);
                Assert.AreEqual(
                    1,
                    phase.AddressIndex);
                Assert.AreEqual(
                    1,
                    phase.TotalAddresses);
                Assert.AreEqual(
                    2,
                    phase.StepCount);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "phase:Icmp:192.0.2.82",
                    "probe:icmp:192.0.2.82",
                    "phase:Snmp:192.0.2.82",
                    "probe:snmp:192.0.2.82"
                },
                calls);
        }

        [TestMethod]
        public void SilentAddressWithSnmpTimeoutIsNotAnError()
        {
            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(false, new int[0]),
                new CountingFailingInventoryCollector(SnmpTransportFailure.Timeout));

            var results = engine.Discover(CreateRequest(
                Guid.NewGuid(),
                new[] { IPAddress.Parse("192.0.2.7") }));

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void AddressAnsweringIcmpWithSnmpTimeoutReportsTimeout()
        {
            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(true, new int[0]),
                new CountingFailingInventoryCollector(SnmpTransportFailure.Timeout));

            var results = engine.Discover(CreateRequest(
                Guid.NewGuid(),
                new[] { IPAddress.Parse("192.0.2.7") }));

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(SnmpTransportFailure.Timeout, results[0].SnmpFailure);
        }

        [TestMethod]
        public void SilentAddressWithAuthenticationFailureIsReported()
        {
            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(false, new int[0]),
                new CountingFailingInventoryCollector(SnmpTransportFailure.Authentication));

            var results = engine.Discover(CreateRequest(
                Guid.NewGuid(),
                new[] { IPAddress.Parse("192.0.2.7") }));

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IcmpReachable);
            Assert.AreEqual(0, results[0].OpenTcpPorts.Count);
            Assert.AreEqual(SnmpTransportFailure.Authentication, results[0].SnmpFailure);
        }

        [TestMethod]
        public void SuccessfulSnmpHasNoFailure()
        {
            var engine = new DiscoveryEngine(
                new FakeNetworkProbe(false, new int[0]),
                new SuccessfulInventoryCollector());

            var results = engine.Discover(CreateRequest(
                Guid.NewGuid(),
                new[] { IPAddress.Parse("192.0.2.7") }));

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].SnmpResponded);
            Assert.IsNull(results[0].SnmpFailure);
        }

        private static DiscoveryRequest CreateRequest(
            Guid profileId,
            IReadOnlyList<IPAddress> addresses,
            int interAddressDelayMilliseconds = 0)
        {
            return new DiscoveryRequest(
                addresses,
                new IPAddress[0],
                new[] { 22, 443 },
                CreateProfile(profileId),
                100,
                100,
                interAddressDelayMilliseconds);
        }

        private static DiscoverySnmpProfile CreateProfile(
            Guid profileId)
        {
            return new DiscoverySnmpProfile(
                profileId,
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    Encoding.ASCII.GetBytes("public")),
                100,
                0,
                10);
        }

        private sealed class FakeNetworkProbe
            : INetworkDiscoveryProbe
        {
            private readonly bool _icmp;
            private readonly IReadOnlyList<int> _ports;
            private readonly IList<string> _calls;

            public FakeNetworkProbe(
                bool icmp,
                IReadOnlyList<int> ports,
                IList<string> calls = null)
            {
                _icmp = icmp;
                _ports = ports;
                _calls = calls;
            }

            public bool IsIcmpReachable(
                IPAddress address,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _calls?.Add("probe:icmp:" + address);
                return _icmp;
            }

            public IReadOnlyList<int> FindOpenTcpPorts(
                IPAddress address,
                IReadOnlyList<int> ports,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _calls?.Add("probe:tcp:" + address);
                return _ports;
            }
        }

        private sealed class CountingNetworkProbe
            : INetworkDiscoveryProbe
        {
            public int ProbeCount { get; private set; }

            public bool IsIcmpReachable(
                IPAddress address,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProbeCount++;
                return false;
            }

            public IReadOnlyList<int> FindOpenTcpPorts(
                IPAddress address,
                IReadOnlyList<int> ports,
                int timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProbeCount++;
                return new int[0];
            }
        }

        private sealed class AddressRecordingNetworkProbe
            : INetworkDiscoveryProbe
        {
            public AddressRecordingNetworkProbe()
            {
                ProbedAddresses =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
            }

            public ISet<string> ProbedAddresses { get; }

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
                ProbedAddresses.Add(
                    address.ToString());
                return new int[0];
            }
        }

        private sealed class SuccessfulInventoryCollector
            : IInventoryCollector
        {
            private readonly IList<string> _calls;

            public SuccessfulInventoryCollector(
                IList<string> calls = null)
            {
                _calls = calls;
            }

            public InventorySnapshot Collect(
                InventoryCollectionRequest request)
            {
                _calls?.Add("probe:snmp:" + request.Address);
                return new InventorySnapshot(
                    request.Address,
                    "switch-01",
                    "Test switch",
                    "1.3.6.1.4.1.9999",
                    null,
                    null,
                    "100",
                    new InventoryInterface[0]);
            }
        }

        private sealed class CountingFailingInventoryCollector
            : IInventoryCollector
        {
            private readonly SnmpTransportFailure _failure;

            public CountingFailingInventoryCollector(
                SnmpTransportFailure failure = SnmpTransportFailure.Timeout)
            {
                _failure = failure;
            }

            public int CollectionCount { get; private set; }

            public InventorySnapshot Collect(
                InventoryCollectionRequest request)
            {
                CollectionCount++;

                throw new SnmpTransportException(
                    _failure,
                    "Synthetic SNMP failure",
                    null);
            }
        }
    }
}
