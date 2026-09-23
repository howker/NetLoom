using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint43WpfMultiTargetMonitoringTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                21,
                16,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            MonitoringScopeShowsAvailableAndActiveCountsAtTheSameTime()
        {
            RunOnSta(
                () =>
                {
                    var previousCulture =
                        CultureInfo.CurrentUICulture;

                    try
                    {
                        CultureInfo.CurrentUICulture =
                            CultureInfo.GetCultureInfo(
                                "ru-RU");

                        var window =
                            new MainWindow(
                                new FixedRefreshProvider(
                                    Snapshot(
                                        Device(
                                            Guid.NewGuid(),
                                            "Switch A",
                                            "192.0.2.61",
                                            MapMonitoringCapability.Unknown),
                                        Device(
                                            Guid.NewGuid(),
                                            "Switch B",
                                            "192.0.2.62",
                                            MapMonitoringCapability.Unknown))),
                                new EmptyLookupReader(),
                                new RecordingMultiTargetMonitoringControl());

                        try
                        {
                            window.Show();

                            WaitForCondition(
                                () =>
                                    ((Button)window.FindName(
                                        "MonitoringStartButton"))
                                    .IsEnabled);

                            var values =
                                MonitoringTextValues(
                                    window);

                            CollectionAssert.Contains(
                                values.ToArray(),
                                "Доступно для мониторинга: 2");

                            CollectionAssert.Contains(
                                values.ToArray(),
                                "Устройств в мониторинге: 0");
                        }
                        finally
                        {
                            window.Close();
                        }
                    }
                    finally
                    {
                        CultureInfo.CurrentUICulture =
                            previousCulture;
                    }
                });
        }

        [TestMethod]
        public void
            PollNowUsesSelectedMapAddressWhenDiagnosticAddressIsMissing()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.NewGuid();

                    var control =
                        new RecordingMultiTargetMonitoringControl();

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                SnapshotWithDiagnosticAddress(
                                    deviceId,
                                    "Switch A",
                                    "192.0.2.71",
                                    null)),
                            new EmptyLookupReader(),
                            control);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) !=
                                null);

                        SelectDevice(
                            window,
                            deviceId);

                        Click(
                            (Button)window.FindName(
                                "MonitoringPollNowButton"));

                        Assert.AreEqual(
                            1,
                            control.PollNowCallCount);

                        Assert.IsNotNull(
                            control.PollNowTarget);

                        Assert.AreEqual(
                            deviceId,
                            control.PollNowTarget.DeviceId);

                        Assert.AreEqual(
                            "192.0.2.71",
                            control.PollNowTarget
                                .TargetAddress
                                .ToString());
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            ContinuousMonitoringStartsAllPollableDevicesWithoutSelection()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.NewGuid();

                    var secondId =
                        Guid.NewGuid();

                    var unmanagedId =
                        Guid.NewGuid();

                    var missingAddressId =
                        Guid.NewGuid();

                    var control =
                        new RecordingMultiTargetMonitoringControl();

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    Device(
                                        firstId,
                                        "Switch A",
                                        "192.0.2.11",
                                        MapMonitoringCapability.Unknown),
                                    Device(
                                        secondId,
                                        "Switch B",
                                        "192.0.2.12",
                                        MapMonitoringCapability.Unknown),
                                    Device(
                                        unmanagedId,
                                        "Manual unmanaged",
                                        "192.0.2.13",
                                        MapMonitoringCapability.None),
                                    Device(
                                        missingAddressId,
                                        "No address",
                                        null,
                                        MapMonitoringCapability.Unknown))),
                            new EmptyLookupReader(),
                            control);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                ((Button)window.FindName(
                                    "MonitoringStartButton"))
                                .IsEnabled);

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        WaitForCondition(
                            () =>
                                control.StartSetTargets !=
                                null);

                        Assert.AreEqual(
                            0,
                            control.SingleStartCount,
                            "Production multi-target control must not fall back to one selected target.");

                        CollectionAssert.AreEquivalent(
                            new[]
                            {
                                firstId,
                                secondId
                            },
                            control.StartSetTargets
                                .Select(
                                    item => item.DeviceId)
                                .ToArray());

                        Assert.AreEqual(
                            1,
                            control.StartSetPolicy
                                .MaxConcurrentPolls,
                            "The first WPF policy is intentionally sequential until real-network Sprint 43 acceptance measures a safe higher value.");

                        Assert.AreEqual(
                            TimeSpan.Zero,
                            control.StartSetPolicy
                                .StartupJitter);

                        Assert.AreEqual(
                            MonitoringControlState.Running,
                            control.Current.State);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            SelectedAddressOverrideChangesOnlyThatDeviceInsideTargetSet()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.NewGuid();

                    var secondId =
                        Guid.NewGuid();

                    var control =
                        new RecordingMultiTargetMonitoringControl();

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    Device(
                                        firstId,
                                        "Switch A",
                                        "192.0.2.21",
                                        MapMonitoringCapability.Unknown),
                                    Device(
                                        secondId,
                                        "Switch B",
                                        "192.0.2.22",
                                        MapMonitoringCapability.Unknown))),
                            new EmptyLookupReader(),
                            control);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    firstId) !=
                                null);

                        SelectDevice(
                            window,
                            firstId);

                        ((TextBox)window.FindName(
                            "MonitoringTargetAddressTextBox"))
                            .Text =
                                "192.0.2.99";

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        WaitForCondition(
                            () =>
                                control.StartSetTargets !=
                                null);

                        var byId =
                            control.StartSetTargets
                                .ToDictionary(
                                    item => item.DeviceId);

                        Assert.AreEqual(
                            "192.0.2.99",
                            byId[firstId]
                                .TargetAddress
                                .ToString());

                        Assert.AreEqual(
                            "192.0.2.22",
                            byId[secondId]
                                .TargetAddress
                                .ToString());
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            PollNowDuringTargetSetSessionWakesTheSetWithoutSingleTarget()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.NewGuid();

                    var secondId =
                        Guid.NewGuid();

                    var control =
                        new RecordingMultiTargetMonitoringControl();

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    Device(
                                        firstId,
                                        "Switch A",
                                        "192.0.2.31",
                                        MapMonitoringCapability.Unknown),
                                    Device(
                                        secondId,
                                        "Switch B",
                                        "192.0.2.32",
                                        MapMonitoringCapability.Unknown))),
                            new EmptyLookupReader(),
                            control);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                ((Button)window.FindName(
                                    "MonitoringStartButton"))
                                .IsEnabled);

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        WaitForCondition(
                            () =>
                                control.Current.State ==
                                MonitoringControlState.Running);

                        Click(
                            (Button)window.FindName(
                                "MonitoringPollNowButton"));

                        WaitForCondition(
                            () =>
                                control.PollNowCallCount ==
                                1);

                        Assert.IsNull(
                            control.PollNowTarget,
                            "A running target-set session must wake the owned Engine set instead of inventing one active target.");

                        Assert.IsNull(
                            control.PollNowPolicy);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            NoPollableManagementAddressKeepsContinuousStartDisabled()
        {
            RunOnSta(
                () =>
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    Device(
                                        Guid.NewGuid(),
                                        "Unmanaged",
                                        "192.0.2.41",
                                        MapMonitoringCapability.None),
                                    Device(
                                        Guid.NewGuid(),
                                        "No address",
                                        null,
                                        MapMonitoringCapability.Unknown))),
                            new EmptyLookupReader(),
                            new RecordingMultiTargetMonitoringControl());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                ((Button)window.FindName(
                                    "MonitoringRefreshTopologyButton"))
                                .IsEnabled);

                        Assert.IsFalse(
                            ((Button)window.FindName(
                                "MonitoringStartButton"))
                            .IsEnabled);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static TopologyRefreshSnapshot Snapshot(
            params DeviceFixture[] devices)
        {
            var nodes =
                new List<MapNode>();

            var diagnostics =
                new List<DeviceDiagnostic>();

            var x = 100.0;

            foreach (var item in devices)
            {
                nodes.Add(
                    new MapNode(
                        item.DeviceId.ToString("D"),
                        item.Name,
                        null,
                        x,
                        100.0,
                        null,
                        MapNodeOrigin.Automatic,
                        item.MonitoringCapability,
                        MapNodeCategory.Unknown,
                        item.DeviceId,
                        item.ManagementAddress));

                diagnostics.Add(
                    new DeviceDiagnostic(
                        item.DeviceId,
                        item.Name,
                        null,
                        null,
                        Now,
                        Now,
                        new InterfaceDiagnostic[0],
                        item.ManagementAddress));

                x += 240.0;
            }

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    Now,
                    nodes,
                    new MapLink[0]),
                new TopologyAlertSnapshot(
                    Now,
                    "cist",
                    new TopologyAlert[0]),
                new NetworkDiagnosticSnapshot(
                    Now,
                    diagnostics,
                    new PhysicalLinkDiagnostic[0]));
        }

        private static TopologyRefreshSnapshot
            SnapshotWithDiagnosticAddress(
                Guid deviceId,
                string name,
                string mapManagementAddress,
                string diagnosticManagementAddress)
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
                            mapManagementAddress)
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
                            diagnosticManagementAddress)
                    },
                    new PhysicalLinkDiagnostic[0]));
        }

        private static DeviceFixture Device(
            Guid deviceId,
            string name,
            string managementAddress,
            MapMonitoringCapability monitoringCapability)
        {
            return new DeviceFixture(
                deviceId,
                name,
                managementAddress,
                monitoringCapability);
        }

        private static IReadOnlyList<string>
            MonitoringTextValues(
                MainWindow window)
        {
            var expander =
                (Expander)window.FindName(
                    "MonitoringExpander");

            var result =
                new List<string>();

            CollectTextValues(
                expander,
                result);

            return result;
        }

        private static void CollectTextValues(
            DependencyObject root,
            ICollection<string> result)
        {
            var text =
                root as TextBlock;

            if (text != null &&
                !string.IsNullOrWhiteSpace(
                    text.Text))
            {
                result.Add(
                    text.Text);
            }

            foreach (var child in
                LogicalTreeHelper.GetChildren(
                    root))
            {
                var dependencyObject =
                    child as DependencyObject;

                if (dependencyObject != null)
                {
                    CollectTextValues(
                        dependencyObject,
                        result);
                }
            }
        }

        private static void SelectDevice(
            MainWindow window,
            Guid deviceId)
        {
            var border =
                DeviceBorder(
                    window,
                    deviceId);

            Assert.IsNotNull(
                border);

            border.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonDownEvent,
                    Source = border
                });

            border.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonUpEvent,
                    Source = border
                });

            PumpDispatcher();
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
            Exception failure =
                null;

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
                TimeSpan.FromSeconds(5);

            while (!condition())
            {
                if (DateTime.UtcNow >=
                    deadline)
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

        private sealed class FixedRefreshProvider :
            ITopologyRefreshSnapshotProvider
        {
            private readonly TopologyRefreshSnapshot
                _snapshot;

            public FixedRefreshProvider(
                TopologyRefreshSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public TopologyRefreshSnapshot GetSnapshot(
                string stpInstanceId)
            {
                return _snapshot;
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

        private sealed class RecordingMultiTargetMonitoringControl :
            IMultiTargetMonitoringControl
        {
            private MonitoringControlSnapshot
                _current =
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Stopped,
                        null,
                        null,
                        null);

            public MonitoringControlSnapshot Current =>
                _current;

            public int SingleStartCount { get; private set; }

            public IReadOnlyList<MonitoringTarget>
                StartSetTargets { get; private set; }

            public MonitoringSessionPolicy
                StartSetSessionPolicy { get; private set; }

            public MonitoringTargetSetPolicy
                StartSetPolicy { get; private set; }

            public int PollNowCallCount { get; private set; }

            public MonitoringTarget PollNowTarget { get; private set; }

            public MonitoringSessionPolicy PollNowPolicy { get; private set; }

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged;

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SingleStartCount++;

                Publish(
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Running,
                        target,
                        null,
                        null));

                return Task.CompletedTask;
            }

            public Task StartSetAsync(
                IReadOnlyList<MonitoringTarget> targets,
                MonitoringSessionPolicy policy,
                MonitoringTargetSetPolicy targetSetPolicy,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StartSetTargets =
                    targets.ToArray();

                StartSetSessionPolicy =
                    policy;

                StartSetPolicy =
                    targetSetPolicy;

                Publish(
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Running,
                        null,
                        null,
                        null));

                return Task.CompletedTask;
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Publish(
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Stopped,
                        null,
                        _current.LastSuccessfulPollUtc,
                        null));

                return Task.CompletedTask;
            }

            public Task PollNowAsync(
                MonitoringTarget targetWhenStopped,
                MonitoringSessionPolicy policyWhenStopped,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PollNowCallCount++;
                PollNowTarget =
                    targetWhenStopped;
                PollNowPolicy =
                    policyWhenStopped;

                return Task.CompletedTask;
            }

            private void Publish(
                MonitoringControlSnapshot snapshot)
            {
                _current =
                    snapshot;

                SnapshotChanged?.Invoke(
                    this,
                    new MonitoringControlSnapshotChangedEventArgs(
                        snapshot));
            }
        }

        private sealed class DeviceFixture
        {
            public DeviceFixture(
                Guid deviceId,
                string name,
                string managementAddress,
                MapMonitoringCapability monitoringCapability)
            {
                DeviceId = deviceId;
                Name = name;
                ManagementAddress =
                    managementAddress;
                MonitoringCapability =
                    monitoringCapability;
            }

            public Guid DeviceId { get; }

            public string Name { get; }

            public string ManagementAddress { get; }

            public MapMonitoringCapability MonitoringCapability { get; }
        }
    }
}
