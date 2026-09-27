using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Lookup;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint46ShellFoundationTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                27,
                10,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void
            NavigationChangesContextSidebarWithoutReplacingMapCanvas()
        {
            RunOnSta(
                () =>
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var map =
                            (ScrollViewer)window.FindName(
                                "MapScrollViewer");

                        Assert.AreEqual(
                            Visibility.Visible,
                            map.Visibility);

                        Click(
                            (Button)window.FindName(
                                "ShellDiscoveryButton"));

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "ShellDiscoverySidebarPanel"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "ShellMapSidebarPanel"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Visible,
                            map.Visibility,
                            "Section navigation must preserve the map surface.");

                        Click(
                            (Button)window.FindName(
                                "ShellSearchButton"));

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "ShellSearchSidebarPanel"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Visible,
                            map.Visibility);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            RestoredGlobalProfileStartsMonitoringWithoutVisitingDiscovery()
        {
            RunOnSta(
                () =>
                {
                    var profileId =
                        Guid.Parse(
                            "46464646-1111-2222-3333-464646464646");

                    var profile =
                        new AccessProfile(
                            profileId,
                            "Field profile",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var deviceId =
                        Guid.Parse(
                            "46464646-aaaa-bbbb-cccc-464646464646");

                    var monitoring =
                        new RecordingMonitoringControl();

                    var stateStore =
                        new MemoryShellStateStore(
                            new UiShellState(
                                profileId,
                                UiShellTheme.Light));

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    deviceId,
                                    "Switch A",
                                    "192.0.2.46")),
                            new EmptyLookupReader(),
                            monitoring,
                            new EmptyDiscoveryControl(),
                            new[]
                            {
                                profile
                            },
                            new NoopCandidateMaterializer(),
                            stateStore);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) !=
                                null);

                        var profiles =
                            (ComboBox)window.FindName(
                                "DiscoveryProfileComboBox");

                        Assert.AreEqual(
                            0,
                            profiles.SelectedIndex,
                            "The persisted global profile must be restored into the always-visible header.");

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "ShellDiscoverySidebarPanel"))
                            .Visibility,
                            "Monitoring setup must not require visiting Discovery.");

                        SelectDevice(
                            window,
                            deviceId);

                        Click(
                            (Button)window.FindName(
                                "ShellEquipmentButton"));

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        WaitForCondition(
                            () =>
                                monitoring.StartPolicy !=
                                null);

                        Assert.AreEqual(
                            profileId,
                            monitoring.StartPolicy
                                .AccessProfileId);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            MissingGlobalProfileBlocksMonitoringBeforeControlInvocation()
        {
            RunOnSta(
                () =>
                {
                    var profile =
                        new AccessProfile(
                            Guid.Parse(
                                "46464646-4444-5555-6666-464646464646"),
                            "Available profile",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var deviceId =
                        Guid.Parse(
                            "46464646-dddd-eeee-ffff-464646464646");

                    var monitoring =
                        new RecordingMonitoringControl();

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    deviceId,
                                    "Switch B",
                                    "192.0.2.47")),
                            new EmptyLookupReader(),
                            monitoring,
                            new EmptyDiscoveryControl(),
                            new[]
                            {
                                profile
                            },
                            new NoopCandidateMaterializer(),
                            new MemoryShellStateStore(
                                UiShellState.Default));

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) !=
                                null);

                        Assert.AreEqual(
                            -1,
                            ((ComboBox)window.FindName(
                                "DiscoveryProfileComboBox"))
                            .SelectedIndex);

                        SelectDevice(
                            window,
                            deviceId);

                        Click(
                            (Button)window.FindName(
                                "MonitoringStartButton"));

                        PumpDispatcher();

                        Assert.IsNull(
                            monitoring.StartPolicy,
                            "Monitoring control must not be invoked without the global SNMP profile.");

                        Assert.IsFalse(
                            string.IsNullOrWhiteSpace(
                                ((TextBlock)window.FindName(
                                    "MonitoringMessageText"))
                                .Text));

                        Assert.IsFalse(
                            string.IsNullOrWhiteSpace(
                                ((TextBlock)window.FindName(
                                    "ShellProfileStatusText"))
                                .Text));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            TopologyEditorsRenderInsideShellInsteadOfOwnedWindows()
        {
            RunOnSta(
                () =>
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var host =
                            (ContentControl)window.FindName(
                                "ShellWorkspaceEditorHost");

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            host.Visibility);

                        Click(
                            (Button)window.FindName(
                                "ManualTopologyButton"));

                        WaitForCondition(
                            () =>
                                host.Visibility ==
                                    Visibility.Visible &&
                                host.Content != null);

                        Assert.IsInstanceOfType(
                            host.Content,
                            typeof(UserControl));
                        Assert.AreEqual(
                            "ManualTopologyEditorControl",
                            host.Content.GetType().Name);
                        Assert.AreSame(
                            window,
                            Window.GetWindow(
                                (DependencyObject)host.Content),
                            "The manual editor must live in the MainWindow visual tree.");
                        Assert.AreEqual(
                            0,
                            window.OwnedWindows.Count,
                            "Opening the manual editor must not create an owned working window.");

                        Click(
                            (Button)window.FindName(
                                "LocationsButton"));

                        WaitForCondition(
                            () =>
                                host.Content != null &&
                                string.Equals(
                                    "LocationTopologyEditorControl",
                                    host.Content.GetType().Name,
                                    StringComparison.Ordinal));

                        Assert.IsInstanceOfType(
                            host.Content,
                            typeof(UserControl));
                        Assert.AreSame(
                            window,
                            Window.GetWindow(
                                (DependencyObject)host.Content),
                            "The location editor must live in the MainWindow visual tree.");
                        Assert.AreEqual(
                            0,
                            window.OwnedWindows.Count,
                            "Opening the location editor must not create an owned working window.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            ThemeToggleChangesSemanticPaletteAndPersistsSelection()
        {
            RunOnSta(
                () =>
                {
                    var stateStore =
                        new MemoryShellStateStore(
                            UiShellState.Default);

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new RecordingMonitoringControl(),
                            new EmptyDiscoveryControl(),
                            new AccessProfile[0],
                            new NoopCandidateMaterializer(),
                            stateStore);

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var before =
                            ((SolidColorBrush)window.Background)
                                .Color;

                        Click(
                            (Button)window.FindName(
                                "ShellThemeButton"));

                        var after =
                            ((SolidColorBrush)window.Background)
                                .Color;

                        Assert.AreNotEqual(
                            before,
                            after,
                            "Switching theme must replace the semantic palette used by the live window.");

                        Assert.IsNotNull(
                            stateStore.LastSaved);
                        Assert.AreEqual(
                            UiShellTheme.Dark,
                            stateStore.LastSaved.Theme);

                        Assert.AreEqual(
                            ((Button)window.FindName(
                                "ShellThemeButton"))
                            .Content,
                            ((Button)window.FindName(
                                "ShellSettingsThemeButton"))
                            .Content);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            FileShellStateStoreRoundTripsOnlyProfileIdAndTheme()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s46-shell-" +
                    Guid.NewGuid()
                        .ToString("N"));

            var path =
                Path.Combine(
                    directory,
                    "ui-shell-state.txt");

            try
            {
                var profileId =
                    Guid.Parse(
                        "46464646-7777-8888-9999-464646464646");

                var store =
                    new FileUiShellStateStore(
                        path);

                store.Save(
                    new UiShellState(
                        profileId,
                        UiShellTheme.Dark));

                var restored =
                    store.Load();

                Assert.AreEqual(
                    profileId,
                    restored.AccessProfileId);
                Assert.AreEqual(
                    UiShellTheme.Dark,
                    restored.Theme);

                var persisted =
                    File.ReadAllText(
                        path);

                StringAssert.Contains(
                    persisted,
                    profileId.ToString("D"));
                StringAssert.Contains(
                    persisted,
                    "theme=Dark");
                Assert.IsFalse(
                    persisted.Contains(
                        "community"),
                    "Shell state must never persist SNMP secrets.");
            }
            finally
            {
                if (Directory.Exists(
                        directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        private static TopologyRefreshSnapshot
            EmptySnapshot()
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

        private static TopologyRefreshSnapshot Snapshot(
            Guid deviceId,
            string name,
            string managementAddress)
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
                            managementAddress)
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
                            managementAddress)
                    },
                    new PhysicalLinkDiagnostic[0]));
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
                    Source =
                        border
                });

            border.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonUpEvent,
                    Source =
                        border
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
                            failure =
                                error;
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

        private sealed class MemoryShellStateStore :
            IUiShellStateStore
        {
            private UiShellState _state;

            public MemoryShellStateStore(
                UiShellState state)
            {
                _state =
                    state ??
                    UiShellState.Default;
            }

            public UiShellState LastSaved { get; private set; }

            public UiShellState Load()
            {
                return _state;
            }

            public void Save(
                UiShellState state)
            {
                LastSaved =
                    state;
                _state =
                    state;
            }
        }

        private sealed class FixedRefreshProvider :
            ITopologyRefreshSnapshotProvider
        {
            private readonly TopologyRefreshSnapshot
                _snapshot;

            public FixedRefreshProvider(
                TopologyRefreshSnapshot snapshot)
            {
                _snapshot =
                    snapshot;
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

            public MonitoringSessionPolicy StartPolicy { get; private set; }

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged;

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StartPolicy =
                    policy;

                _current =
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Running,
                        target,
                        null,
                        null);

                SnapshotChanged?.Invoke(
                    this,
                    new MonitoringControlSnapshotChangedEventArgs(
                        _current));

                return Task.CompletedTask;
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _current =
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Stopped,
                        null,
                        null,
                        null);

                SnapshotChanged?.Invoke(
                    this,
                    new MonitoringControlSnapshotChangedEventArgs(
                        _current));

                return Task.CompletedTask;
            }

            public Task PollNowAsync(
                MonitoringTarget targetWhenStopped,
                MonitoringSessionPolicy policyWhenStopped,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class EmptyDiscoveryControl :
            IDiscoveryControl
        {
            private readonly DiscoveryControlSnapshot
                _current =
                    new DiscoveryControlSnapshot(
                        DiscoveryControlState.Idle,
                        null,
                        null,
                        0,
                        0,
                        0,
                        null,
                        null);

            public DiscoveryControlSnapshot Current =>
                _current;

            public event EventHandler<DiscoveryControlSnapshotChangedEventArgs>
                SnapshotChanged;

            public event EventHandler<DiscoveryCandidateDiscoveredEventArgs>
                CandidateDiscovered;

            public Task StartAsync(
                DiscoveryControlRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class NoopCandidateMaterializer :
            IDiscoveryCandidateMaterializer
        {
            public Guid Materialize(
                DiscoveryCandidateSnapshot candidate,
                DateTime observedUtc)
            {
                return Guid.NewGuid();
            }
        }
    }
}
