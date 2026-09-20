using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Lookup;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint42WpfDiscoveryPanelTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                20,
                10,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void StartUsesExplicitSelectedProfileAndStopUsesDiscoveryControl()
        {
            RunOnSta(
                () =>
                {
                    var profileA =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Profile A",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var profileB =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Profile B",
                            true,
                            SnmpVersion.V1,
                            null);

                    var control =
                        new RecordingDiscoveryControl();

                    var window =
                        new MainWindow(
                            new MutableRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new NoopMonitoringControl(),
                            control,
                            new[]
                            {
                                profileA,
                                profileB
                            },
                            new RecordingCandidateMaterializer());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var startAddress =
                            (TextBox)window.FindName(
                                "DiscoveryStartAddressTextBox");

                        var endAddress =
                            (TextBox)window.FindName(
                                "DiscoveryEndAddressTextBox");

                        var subnetMask =
                            (TextBox)window.FindName(
                                "DiscoverySubnetMaskTextBox");

                        var profiles =
                            (ComboBox)window.FindName(
                                "DiscoveryProfileComboBox");

                        var warning =
                            (TextBlock)window.FindName(
                                "DiscoveryWarningText");

                        Assert.AreEqual(
                            2,
                            profiles.Items.Count,
                            "The panel must expose only the profile list supplied by Desktop composition.");

                        Assert.AreEqual(
                            -1,
                            profiles.SelectedIndex,
                            "Discovery must require an explicit profile selection instead of silently choosing one.");

                        Assert.IsFalse(
                            string.IsNullOrWhiteSpace(
                                warning.Text),
                            "The scanning warning must be visible before the operator starts discovery.");

                        startAddress.Text =
                            "192.0.2.1";
                        endAddress.Text =
                            "192.0.2.4";
                        subnetMask.Text =
                            "255.255.255.0";
                        profiles.SelectedIndex =
                            1;

                        Click(
                            (Button)window.FindName(
                                "DiscoveryStartButton"));

                        WaitForCondition(
                            () =>
                                control.StartRequest !=
                                null);

                        Assert.IsNull(
                            control.StartRequest.Cidr);
                        Assert.AreEqual(
                            "192.0.2.1",
                            control.StartRequest.StartAddress);
                        Assert.AreEqual(
                            "192.0.2.4",
                            control.StartRequest.EndAddress);
                        Assert.AreEqual(
                            "255.255.255.0",
                            control.StartRequest.SubnetMask);
                        Assert.AreEqual(
                            profileB.Id,
                            control.StartRequest.AccessProfileId);
                        Assert.AreEqual(
                            SnmpVersion.V1,
                            control.StartRequest.Version);

                        Click(
                            (Button)window.FindName(
                                "DiscoveryStopButton"));

                        WaitForCondition(
                            () =>
                                control.StopCalled);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void CandidateIsMaterializedThenAppearsThroughExistingTopologyRefreshPath()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.NewGuid();

                    var provider =
                        new MutableRefreshProvider(
                            EmptySnapshot());

                    var materializer =
                        new RecordingCandidateMaterializer(
                            candidate =>
                                provider.SetSnapshot(
                                    SnapshotWithDevice(
                                        deviceId,
                                        candidate.Address.ToString(),
                                        candidate.SysName)));

                    var control =
                        new RecordingDiscoveryControl();

                    var profile =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Discovery",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var window =
                        new MainWindow(
                            provider,
                            new EmptyLookupReader(),
                            new NoopMonitoringControl(),
                            control,
                            new[] { profile },
                            materializer);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                provider.ReadCount >= 1);

                        control.EmitCandidate(
                            new DiscoveryCandidateSnapshot(
                                IPAddress.Parse(
                                    "192.0.2.44"),
                                profile.Id,
                                true,
                                true,
                                new[] { 22, 443 },
                                "Found switch",
                                "Synthetic device",
                                "1.3.6.1.4.1.99999",
                                null,
                                12));

                        WaitForCondition(
                            () =>
                                materializer.LastCandidate !=
                                    null &&
                                DeviceBorder(
                                    window,
                                    deviceId) !=
                                    null);

                        Assert.AreEqual(
                            "192.0.2.44",
                            materializer.LastCandidate
                                .Address
                                .ToString());

                        var list =
                            (ListBox)window.FindName(
                                "DiscoveryCandidatesList");

                        Assert.AreEqual(
                            1,
                            list.Items.Count,
                            "A found candidate must be surfaced to the operator immediately.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void InvalidOversizedOrCrossSubnetRangeDoesNotStartDiscovery()
        {
            RunOnSta(
                () =>
                {
                    var profile =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Discovery",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var control =
                        new RecordingDiscoveryControl();

                    var window =
                        new MainWindow(
                            new MutableRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new NoopMonitoringControl(),
                            control,
                            new[] { profile },
                            new RecordingCandidateMaterializer());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var startAddress =
                            (TextBox)window.FindName(
                                "DiscoveryStartAddressTextBox");

                        var endAddress =
                            (TextBox)window.FindName(
                                "DiscoveryEndAddressTextBox");

                        var subnetMask =
                            (TextBox)window.FindName(
                                "DiscoverySubnetMaskTextBox");

                        var profiles =
                            (ComboBox)window.FindName(
                                "DiscoveryProfileComboBox");

                        profiles.SelectedIndex = 0;
                        startAddress.Text = "not-an-ip";
                        endAddress.Text = "192.0.2.10";
                        subnetMask.Text = "255.255.255.0";

                        Click(
                            (Button)window.FindName(
                                "DiscoveryStartButton"));

                        Assert.IsNull(
                            control.StartRequest);

                        startAddress.Text = "10.0.0.1";
                        endAddress.Text = "10.0.31.255";
                        subnetMask.Text = "255.255.0.0";

                        Click(
                            (Button)window.FindName(
                                "DiscoveryStartButton"));

                        Assert.IsNull(
                            control.StartRequest,
                            "The WPF boundary must reject a range above the Engine safety limit before launching a child process.");

                        startAddress.Text = "192.0.2.250";
                        endAddress.Text = "192.0.3.5";
                        subnetMask.Text = "255.255.255.0";

                        Click(
                            (Button)window.FindName(
                                "DiscoveryStartButton"));

                        Assert.IsNull(
                            control.StartRequest,
                            "The WPF boundary must reject start/end addresses that cross the operator-selected subnet mask.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static Border DeviceBorder(
            MainWindow window,
            Guid deviceId)
        {
            var canvas =
                window.FindName(
                    "MapCanvas") as Canvas;

            return canvas == null
                ? null
                : canvas.Children
                    .OfType<Border>()
                    .FirstOrDefault(
                        item =>
                            item.Tag is Guid &&
                            (Guid)item.Tag ==
                                deviceId);
        }

        private static TopologyRefreshSnapshot EmptySnapshot()
        {
            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    Now,
                    new MapNode[0],
                    new MapLink[0]),
                new TopologyAlertSnapshot(
                    Now,
                    "cist",
                    new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(
                    Now,
                    new DeviceDiagnostic[0],
                    new PhysicalLinkDiagnostic[0]));
        }

        private static TopologyRefreshSnapshot SnapshotWithDevice(
            Guid deviceId,
            string address,
            string name)
        {
            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    Now,
                    new[]
                    {
                        new MapNode(
                            deviceId.ToString("D"),
                            name,
                            null,
                            100.0,
                            100.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            deviceId,
                            address)
                    },
                    new MapLink[0]),
                new TopologyAlertSnapshot(
                    Now,
                    "cist",
                    new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(
                    Now,
                    new[]
                    {
                        new DeviceDiagnostic(
                            deviceId,
                            name,
                            null,
                            null,
                            Now,
                            Now,
                            new InterfaceDiagnostic[0],
                            address)
                    },
                    new PhysicalLinkDiagnostic[0]));
        }

        private static void Click(
            Button button)
        {
            Assert.IsNotNull(
                button);
            Assert.IsTrue(
                button.IsEnabled,
                "The production button must be enabled for this interaction.");

            button.RaiseEvent(
                new RoutedEventArgs(
                    Button.ClickEvent));

            PumpDispatcher();
        }

        private static void RunOnSta(
            Action action)
        {
            Exception failure = null;

            var thread =
                new Thread(
                    () =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception error)
                        {
                            failure = error;
                        }
                    });

            thread.SetApartmentState(
                ApartmentState.STA);
            thread.Start();

            if (!thread.Join(
                    TimeSpan.FromSeconds(20)))
            {
                throw new AssertFailedException(
                    "STA WPF test did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        private static void WaitForCondition(
            Func<bool> condition)
        {
            var deadline =
                DateTime.UtcNow +
                TimeSpan.FromSeconds(6);

            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail(
                        "The expected WPF state was not reached.");
                }

                PumpDispatcher();
                Thread.Sleep(10);
            }

            PumpDispatcher();
        }

        private static void PumpDispatcher()
        {
            var frame =
                new DispatcherFrame();

            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(
                    state =>
                    {
                        ((DispatcherFrame)state)
                            .Continue = false;
                        return null;
                    }),
                frame);

            Dispatcher.PushFrame(
                frame);
        }

        private sealed class MutableRefreshProvider :
            ITopologyRefreshSnapshotProvider
        {
            private readonly object _gate =
                new object();

            private TopologyRefreshSnapshot _snapshot;
            private int _readCount;

            public MutableRefreshProvider(
                TopologyRefreshSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public int ReadCount =>
                Volatile.Read(
                    ref _readCount);

            public void SetSnapshot(
                TopologyRefreshSnapshot snapshot)
            {
                lock (_gate)
                {
                    _snapshot = snapshot;
                }
            }

            public TopologyRefreshSnapshot GetSnapshot(
                string stpInstanceId)
            {
                Interlocked.Increment(
                    ref _readCount);

                lock (_gate)
                {
                    return _snapshot;
                }
            }
        }

        private sealed class EmptyLookupReader :
            IMacIpLookupReader
        {
            public MacIpLookupResult FindByMac(
                string macAddress,
                int maxCandidates)
            {
                return new MacIpLookupResult(
                    MacIpLookupKind.Mac,
                    macAddress,
                    new MacIpLookupCandidate[0]);
            }

            public MacIpLookupResult FindByIp(
                string ipAddress,
                int maxCandidates)
            {
                return new MacIpLookupResult(
                    MacIpLookupKind.Ip,
                    ipAddress,
                    new MacIpLookupCandidate[0]);
            }
        }

        private sealed class NoopMonitoringControl :
            IMonitoringControl
        {
            private readonly MonitoringControlSnapshot _current =
                new MonitoringControlSnapshot(
                    MonitoringControlState.Stopped,
                    null,
                    null,
                    null);

            public MonitoringControlSnapshot Current =>
                _current;

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged
            {
                add { }
                remove { }
            }

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException();
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task PollNowAsync(
                MonitoringTarget targetWhenStopped,
                MonitoringSessionPolicy policyWhenStopped,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException();
            }
        }

        private sealed class RecordingDiscoveryControl :
            IDiscoveryControl
        {
            private DiscoveryControlSnapshot _current =
                Snapshot(
                    DiscoveryControlState.Idle,
                    0,
                    0,
                    0,
                    null);

            public DiscoveryControlSnapshot Current =>
                _current;

            public DiscoveryControlRequest StartRequest { get; private set; }

            public bool StopCalled { get; private set; }

            public event EventHandler<DiscoveryControlSnapshotChangedEventArgs>
                SnapshotChanged;

            public event EventHandler<DiscoveryCandidateDiscoveredEventArgs>
                CandidateDiscovered;

            public Task StartAsync(
                DiscoveryControlRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StartRequest = request;

                Publish(
                    new DiscoveryControlSnapshot(
                        DiscoveryControlState.Running,
                        request.Cidr,
                        request.AccessProfileId,
                        0,
                        4,
                        0,
                        null,
                        null));

                return Task.CompletedTask;
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StopCalled = true;

                Publish(
                    Snapshot(
                        DiscoveryControlState.Stopped,
                        _current.ProcessedAddresses,
                        _current.TotalAddresses,
                        _current.FoundCandidates,
                        _current.CurrentAddress));

                return Task.CompletedTask;
            }

            public void EmitCandidate(
                DiscoveryCandidateSnapshot candidate)
            {
                CandidateDiscovered?.Invoke(
                    this,
                    new DiscoveryCandidateDiscoveredEventArgs(
                        candidate));
            }

            private void Publish(
                DiscoveryControlSnapshot snapshot)
            {
                _current = snapshot;
                SnapshotChanged?.Invoke(
                    this,
                    new DiscoveryControlSnapshotChangedEventArgs(
                        snapshot));
            }

            private static DiscoveryControlSnapshot Snapshot(
                DiscoveryControlState state,
                int processed,
                int total,
                int found,
                IPAddress current)
            {
                return new DiscoveryControlSnapshot(
                    state,
                    null,
                    null,
                    processed,
                    total,
                    found,
                    current,
                    null);
            }
        }

        private sealed class RecordingCandidateMaterializer :
            IDiscoveryCandidateMaterializer
        {
            private readonly Action<DiscoveryCandidateSnapshot>
                _onMaterialize;

            public RecordingCandidateMaterializer(
                Action<DiscoveryCandidateSnapshot> onMaterialize = null)
            {
                _onMaterialize = onMaterialize;
            }

            public DiscoveryCandidateSnapshot LastCandidate { get; private set; }

            public Guid Materialize(
                DiscoveryCandidateSnapshot candidate,
                DateTime observedUtc)
            {
                LastCandidate = candidate;
                _onMaterialize?.Invoke(candidate);
                return Guid.NewGuid();
            }
        }
    }
}
