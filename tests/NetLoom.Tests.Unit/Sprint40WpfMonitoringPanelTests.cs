using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint40WpfMonitoringPanelTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                19,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            SelectedDevicePrefillsObservedAddressAndStartUsesManualSessionOverride()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.NewGuid();

                    var provider =
                        new FixedRefreshProvider(
                            Snapshot(
                                Device(
                                    deviceId,
                                    "Switch A",
                                    "10.20.30.40",
                                    120.0,
                                    160.0)));

                    var control =
                        new RecordingMonitoringControl();

                    var window =
                        new MainWindow(
                            provider,
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

                        var address =
                            (TextBox)window.FindName(
                                "MonitoringTargetAddressTextBox");

                        Assert.AreEqual(
                            "10.20.30.40",
                            address.Text,
                            "Selecting a device must prefill the session target from observed management_address.");

                        address.Text =
                            "10.20.30.41";

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        WaitForCondition(
                            () =>
                                control.StartTarget !=
                                null);

                        Assert.AreEqual(
                            deviceId,
                            control.StartTarget.DeviceId,
                            "Monitoring must use the selected stable DeviceId rather than the entered IP as identity.");

                        Assert.AreEqual(
                            "10.20.30.41",
                            control.StartTarget
                                .TargetAddress
                                .ToString(),
                            "The editable target address must be a session launch override.");

                        Assert.IsNotNull(
                            control.StartPolicy);

                        Assert.AreEqual(
                            60.0,
                            control.StartPolicy
                                .Interval
                                .TotalSeconds);

                        Assert.AreEqual(
                            7,
                            control.StartPolicy
                                .Kinds
                                .Count,
                            "The initial panel policy must expose all existing Engine poll kinds.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            DeviceWithoutObservedAddressAcceptsManualSessionTarget()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.NewGuid();

                    var provider =
                        new FixedRefreshProvider(
                            Snapshot(
                                Device(
                                    deviceId,
                                    "New switch",
                                    null,
                                    140.0,
                                    180.0)));

                    var control =
                        new RecordingMonitoringControl();

                    var window =
                        new MainWindow(
                            provider,
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

                        var address =
                            (TextBox)window.FindName(
                                "MonitoringTargetAddressTextBox");

                        Assert.AreEqual(
                            string.Empty,
                            address.Text,
                            "A device without observed management_address must not invent a target address.");

                        Assert.IsTrue(
                            address.IsEnabled,
                            "The operator must be able to enter the first session target manually.");

                        address.Text =
                            "192.0.2.10";

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        WaitForCondition(
                            () =>
                                control.StartTarget !=
                                null);

                        Assert.AreEqual(
                            deviceId,
                            control.StartTarget.DeviceId,
                            "Manual first-poll addressing must keep the selected stable DeviceId.");

                        Assert.AreEqual(
                            "192.0.2.10",
                            control.StartTarget
                                .TargetAddress
                                .ToString(),
                            "The manually entered address must be used only as the session poll target.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            RunningPollNowKeepsActiveTargetAfterMapSelectionChanges()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.NewGuid();
                    var secondId =
                        Guid.NewGuid();

                    var provider =
                        new FixedRefreshProvider(
                            Snapshot(
                                Device(
                                    firstId,
                                    "Switch A",
                                    "10.0.0.11",
                                    100.0,
                                    100.0),
                                Device(
                                    secondId,
                                    "Switch B",
                                    "10.0.0.12",
                                    420.0,
                                    100.0)));

                    var control =
                        new RecordingMonitoringControl();

                    var window =
                        new MainWindow(
                            provider,
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
                                    null &&
                                DeviceBorder(
                                    window,
                                    secondId) !=
                                    null);

                        SelectDevice(
                            window,
                            firstId);

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        WaitForCondition(
                            () =>
                                control.Current.State ==
                                MonitoringControlState.Running);

                        SelectDevice(
                            window,
                            secondId);

                        Assert.AreEqual(
                            "10.0.0.12",
                            ((TextBox)window.FindName(
                                "MonitoringTargetAddressTextBox"))
                            .Text,
                            "The panel may prepare the newly selected device for the next session.");

                        Click(
                            (Button)window.FindName(
                                "MonitoringPollNowButton"));

                        WaitForCondition(
                            () =>
                                control.PollNowTarget !=
                                null);

                        Assert.AreEqual(
                            firstId,
                            control.PollNowTarget.DeviceId,
                            "Poll now during a running schedule must stay on the active target instead of silently switching with map selection.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            RefreshTopologyButtonUsesTheExistingRefreshProviderPath()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.NewGuid();

                    var provider =
                        new FixedRefreshProvider(
                            Snapshot(
                                Device(
                                    deviceId,
                                    "Switch A",
                                    "10.0.0.21",
                                    100.0,
                                    100.0)));

                    var window =
                        new MainWindow(
                            provider,
                            new EmptyLookupReader(),
                            new RecordingMonitoringControl());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                provider.ReadCount >= 1);

                        var before =
                            provider.ReadCount;

                        Click(
                            (Button)window.FindName(
                                "MonitoringRefreshTopologyButton"));

                        WaitForCondition(
                            () =>
                                provider.ReadCount >
                                before);

                        var lastRefresh =
                            (TextBlock)window.FindName(
                                "MonitoringLastRefreshValueText");

                        Assert.IsFalse(
                            string.IsNullOrWhiteSpace(
                                lastRefresh.Text),
                            "A successful coherent refresh must surface its timestamp in the monitoring panel.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
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

            var args =
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonDownEvent,
                    Source = border
                };

            border.RaiseEvent(
                args);

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

        private static TopologyRefreshSnapshot Snapshot(
            params DeviceFixture[] devices)
        {
            var nodes =
                devices
                    .Select(
                        item =>
                            new MapNode(
                                item.DeviceId
                                    .ToString("D"),
                                item.Name,
                                null,
                                item.X,
                                item.Y,
                                null,
                                MapNodeOrigin.Automatic,
                                MapMonitoringCapability.Unknown,
                                MapNodeCategory.Unknown,
                                item.DeviceId,
                                item.ManagementAddress))
                    .ToArray();

            var diagnostics =
                devices
                    .Select(
                        item =>
                            new DeviceDiagnostic(
                                item.DeviceId,
                                item.Name,
                                null,
                                null,
                                Now,
                                Now,
                                new InterfaceDiagnostic[0],
                                item.ManagementAddress))
                    .ToArray();

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

        private static DeviceFixture Device(
            Guid deviceId,
            string name,
            string managementAddress,
            double x,
            double y)
        {
            return new DeviceFixture(
                deviceId,
                name,
                managementAddress,
                x,
                y);
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

            private int _readCount;

            public int ReadCount =>
                Volatile.Read(
                    ref _readCount);

            public TopologyRefreshSnapshot GetSnapshot(
                string stpInstanceId)
            {
                Interlocked.Increment(
                    ref _readCount);
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

        private sealed class RecordingMonitoringControl :
            IMonitoringControl
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

            public MonitoringTarget StartTarget { get; private set; }

            public MonitoringSessionPolicy StartPolicy { get; private set; }

            public MonitoringTarget PollNowTarget { get; private set; }

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged;

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StartTarget = target;
                StartPolicy = policy;

                Publish(
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Running,
                        target,
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
                PollNowTarget =
                    targetWhenStopped;
                return Task.CompletedTask;
            }

            private void Publish(
                MonitoringControlSnapshot snapshot)
            {
                _current = snapshot;

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
                double x,
                double y)
            {
                DeviceId = deviceId;
                Name = name;
                ManagementAddress =
                    managementAddress;
                X = x;
                Y = y;
            }

            public Guid DeviceId { get; }
            public string Name { get; }
            public string ManagementAddress { get; }
            public double X { get; }
            public double Y { get; }
        }
    }
}
