using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Lookup;
using NetLoom.Application.MapLayout;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed partial class Sprint46ShellFoundationTests
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
                                "ShellMonitoringButton"));

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
            SingleAvailableProfileWithoutSavedSelectionAutoSelectsPersistsAndRestores()
        {
            RunOnSta(
                () =>
                {
                    var profile =
                        new AccessProfile(
                            Guid.Parse(
                                "46464646-3333-4444-5555-464646464646"),
                            "Only profile",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var stateStore =
                        new MemoryShellStateStore(
                            UiShellState.Default);

                    var firstWindow =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new RecordingMonitoringControl(),
                            new EmptyDiscoveryControl(),
                            new[]
                            {
                                profile
                            },
                            new NoopCandidateMaterializer(),
                            stateStore);

                    try
                    {
                        firstWindow.Show();
                        PumpDispatcher();

                        Assert.AreEqual(
                            0,
                            ((ComboBox)firstWindow.FindName(
                                "DiscoveryProfileComboBox"))
                            .SelectedIndex,
                            "A single available profile must be selected automatically when no valid saved selection exists.");

                        Assert.IsNotNull(
                            stateStore.LastSaved,
                            "Automatic profile resolution must use the same persisted UiShellState path as a manual selection.");

                        Assert.AreEqual(
                            profile.Id,
                            stateStore.LastSaved
                                .AccessProfileId);
                    }
                    finally
                    {
                        firstWindow.Close();
                    }

                    var secondWindow =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new RecordingMonitoringControl(),
                            new EmptyDiscoveryControl(),
                            new[]
                            {
                                profile
                            },
                            new NoopCandidateMaterializer(),
                            stateStore);

                    try
                    {
                        secondWindow.Show();
                        PumpDispatcher();

                        Assert.AreEqual(
                            0,
                            ((ComboBox)secondWindow.FindName(
                                "DiscoveryProfileComboBox"))
                            .SelectedIndex,
                            "The automatically selected profile must restore from UiShellState after restart.");
                    }
                    finally
                    {
                        secondWindow.Close();
                    }
                });
        }

        [TestMethod]
        public void
            UnresolvedGlobalProfileShowsPlaceholderAndDisablesMonitoringAndDiscoveryStarts()
        {
            RunOnSta(
                () =>
                {
                    var firstProfile =
                        new AccessProfile(
                            Guid.Parse(
                                "46464646-4444-5555-6666-464646464646"),
                            "Available profile A",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var secondProfile =
                        new AccessProfile(
                            Guid.Parse(
                                "46464646-4444-5555-7777-464646464646"),
                            "Available profile B",
                            true,
                            SnmpVersion.V1,
                            null);

                    var staleProfileId =
                        Guid.Parse(
                            "46464646-4444-5555-8888-464646464646");

                    var deviceId =
                        Guid.Parse(
                            "46464646-dddd-eeee-ffff-464646464646");

                    var monitoring =
                        new RecordingMonitoringControl();

                    var stateStore =
                        new MemoryShellStateStore(
                            new UiShellState(
                                staleProfileId,
                                UiShellTheme.Light));

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
                                firstProfile,
                                secondProfile
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
                            -1,
                            profiles.SelectedIndex,
                            "A missing saved profile must stay unresolved when two or more valid profiles remain.");

                        profiles.ApplyTemplate();
                        PumpDispatcher();

                        var placeholder =
                            (TextBlock)profiles.Template.FindName(
                                "PlaceholderPresenter",
                                profiles);

                        Assert.IsNotNull(
                            placeholder,
                            "The shared ComboBox style must provide the unresolved-state placeholder.");

                        Assert.AreEqual(
                            Visibility.Visible,
                            placeholder.Visibility);

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellProfilePlaceholder"),
                            placeholder.Text);

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.TextSecondary"),
                            placeholder.Foreground);

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellNoProfile"),
                            ((TextBlock)window.FindName(
                                "ShellProfileStatusText"))
                            .Text);

                        SelectDevice(
                            window,
                            deviceId);

                        PumpDispatcher();

                        var monitoringStart =
                            (Button)window.FindName(
                                "MonitoringStartButton");
                        var shellMonitoringStart =
                            (Button)window.FindName(
                                "ShellMonitoringStartButton");

                        Assert.IsFalse(
                            monitoringStart.IsEnabled,
                            "Monitoring start must be disabled until the unresolved profile is selected.");

                        Assert.IsFalse(
                            shellMonitoringStart.IsEnabled);

                        Assert.AreEqual(
                            UiText.Get(
                                "MonitoringValidationProfileRequired"),
                            monitoringStart.ToolTip);

                        Assert.AreEqual(
                            UiText.Get(
                                "MonitoringValidationProfileRequired"),
                            shellMonitoringStart.ToolTip);

                        Assert.IsNull(
                            monitoring.StartPolicy,
                            "Disabled monitoring start must not invoke the monitoring control.");

                        Click(
                            (Button)window.FindName(
                                "ShellDiscoveryButton"));

                        PumpDispatcher();

                        var discoveryStart =
                            (Button)window.FindName(
                                "DiscoveryStartButton");

                        Assert.IsFalse(
                            discoveryStart.IsEnabled,
                            "Discovery start must be disabled until the unresolved profile is selected.");

                        Assert.AreEqual(
                            UiText.Get(
                                "DiscoveryValidationProfileRequired"),
                            discoveryStart.ToolTip);

                        profiles.SelectedIndex =
                            0;

                        PumpDispatcher();

                        Assert.AreEqual(
                            firstProfile.Id,
                            stateStore.LastSaved
                                .AccessProfileId,
                            "Explicit profile selection must persist through UiShellState.");

                        Assert.IsTrue(
                            monitoringStart.IsEnabled,
                            "Selecting a profile must immediately re-enable monitoring start when the remaining monitoring inputs are valid.");

                        Assert.IsTrue(
                            discoveryStart.IsEnabled,
                            "Selecting a profile must immediately re-enable discovery start.");

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            placeholder.Visibility);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            MapStartsInViewModeAndRequiresExplicitEditModeForMutations()
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

                        var viewButton =
                            (Button)window.FindName(
                                "MapViewModeButton");
                        var editButton =
                            (Button)window.FindName(
                                "MapEditModeButton");
                        var editIndicator =
                            (FrameworkElement)window.FindName(
                                "MapEditModeIndicator");
                        var manualTopology =
                            (Button)window.FindName(
                                "ManualTopologyButton");
                        var locations =
                            (Button)window.FindName(
                                "LocationsButton");
                        var lockSelected =
                            (CheckBox)window.FindName(
                                "MapLockSelectedCheckBox");

                        Assert.IsFalse(
                            manualTopology.IsEnabled);

                        Assert.IsFalse(
                            locations.IsEnabled);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            lockSelected.Visibility);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            editIndicator.Visibility);

                        Click(
                            editButton);

                        Assert.IsTrue(
                            manualTopology.IsEnabled);

                        Assert.IsTrue(
                            locations.IsEnabled);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            lockSelected.Visibility,
                            "ADR-083 removes the legacy lock-selected control from the map toolbar; edit mode remains explicit through its dedicated actions and indicator.");

                        Assert.AreEqual(
                            Visibility.Visible,
                            editIndicator.Visibility,
                            "Edit mode must be unmistakable to the operator.");

                        Click(
                            viewButton);

                        Assert.IsFalse(
                            manualTopology.IsEnabled);

                        Assert.IsFalse(
                            locations.IsEnabled);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            lockSelected.Visibility);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            editIndicator.Visibility);
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
                                "MapEditModeButton"));

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
            SectionLabelsAreUppercaseByStyleWhileResourcesKeepSentenceCase()
        {
            // G5 (sprint46-mockup-gap), UI_DESIGN_RULES §3: подписи разделов заглавными задаются стилем.
            // Ресурсы хранят текст в обычном регистре.
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

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));
                        PumpDispatcher();

                        var resource =
                            UiText.Get(
                                "SettingsPollingSectionTitle");
                        var culture =
                            System.Globalization.CultureInfo.CurrentUICulture;

                        Assert.AreNotEqual(
                            resource.ToUpper(
                                culture),
                            resource,
                            "The resource keeps sentence case; uppercase is presentation.");

                        var label =
                            (TextBlock)window.FindName(
                                "SettingsPollingTitleText");

                        Assert.AreEqual(
                            resource.ToUpper(
                                culture),
                            label.Text);

                        Assert.AreEqual(
                            UiText.Get(
                                "EquipmentColumnName")
                                .ToUpper(
                                    culture),
                            ((TextBlock)window.FindName(
                                "EquipmentNameHeaderText"))
                            .Text,
                            "Equipment table column headers are uppercase as in the mockup.");

                        // Повторное назначение текста кодом тоже показывается заглавными.
                        label.Text =
                            resource;
                        PumpDispatcher();

                        Assert.AreEqual(
                            resource.ToUpper(
                                culture),
                            label.Text);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            EquipmentAgeSpellsDaysInFullWithRussianPluralForms()
        {
            // E7 (sprint46-mockup-gap), UI_DESIGN_RULES §3 «Числа»: сокращения вида «12 дн» запрещены.
            var previous =
                System.Threading.Thread.CurrentThread.CurrentUICulture;

            try
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture =
                    new System.Globalization.CultureInfo(
                        "ru-RU");

                var now =
                    new DateTime(
                        2026,
                        10,
                        7,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc);

                var expected =
                    new[]
                    {
                        Tuple.Create(1, "1 день"),
                        Tuple.Create(2, "2 дня"),
                        Tuple.Create(5, "5 дней"),
                        Tuple.Create(11, "11 дней"),
                        Tuple.Create(18, "18 дней"),
                        Tuple.Create(21, "21 день"),
                        Tuple.Create(22, "22 дня")
                    };

                foreach (var item in expected)
                {
                    Assert.AreEqual(
                        item.Item2,
                        MainWindow.CompactAgeText(
                            now.AddDays(
                                -item.Item1),
                            now));
                }

                Assert.AreEqual(
                    "5 мин",
                    MainWindow.CompactAgeText(
                        now.AddMinutes(
                            -5),
                        now),
                    "Minutes keep the standard unit symbol.");
                Assert.AreEqual(
                    "3 ч",
                    MainWindow.CompactAgeText(
                        now.AddHours(
                            -3),
                        now),
                    "Hours keep the standard unit symbol.");
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture =
                    previous;
            }
        }

        [TestMethod]
        public void
            EmptyInspectorPromptNamesWhereToSelectInTheOpenSection()
        {
            // E6 (sprint46-mockup-gap): на «Оборудовании» устройство выбирают в таблице, а не на карте.
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

                        var prompt =
                            (TextBlock)window.FindName(
                                "DiagnosticStatusText");

                        Assert.AreEqual(
                            UiText.Get(
                                "DiagnosticNothingSelected"),
                            prompt.Text);

                        Click(
                            (Button)window.FindName(
                                "ShellEquipmentButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Get(
                                "DiagnosticNothingSelectedEquipment"),
                            prompt.Text);

                        Click(
                            (Button)window.FindName(
                                "ShellMapButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Get(
                                "DiagnosticNothingSelected"),
                            prompt.Text);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            EventStripKeepsTextInsetAndHitAreaWhileStayingCompact()
        {
            // G8 (sprint46-mockup-gap): лента событий ниже, но текст события не ближе 12 к краям ленты (§5).
            // Кнопка события при этом не меньше минимальной площади нажатия (§8).
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6700-6700-6700-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6701-6701-6701-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6702-6702-6702-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        var events =
                            (ItemsControl)window.FindName(
                                "ShellEventList");

                        WaitForCondition(
                            () =>
                                FindVisualDescendant<Button>(
                                    events) != null);
                        PumpDispatcher();

                        var eventButton =
                            FindVisualDescendant<Button>(
                                events);

                        var strip =
                            (FrameworkElement)VisualTreeHelper.GetParent(
                                (DependencyObject)VisualTreeHelper.GetParent(
                                    (DependencyObject)window.FindName(
                                        "ShellEventTitleText")));

                        Assert.IsInstanceOfType(
                            strip,
                            typeof(Border),
                            "The event strip is the bordered panel around the title and the list.");

                        var title =
                            FindVisualDescendant<TextBlock>(
                                eventButton);
                        var titleTop =
                            title.TransformToAncestor(
                                    strip)
                                .Transform(
                                    new Point(
                                        0.0,
                                        0.0))
                                .Y;
                        var titleBottom =
                            strip.ActualHeight -
                            (titleTop +
                             title.ActualHeight);

                        Assert.IsTrue(
                            titleTop >= 12.0 &&
                            titleBottom >= 12.0,
                            "Event text must keep at least 12 px from the strip edges (§5): top " +
                            titleTop +
                            ", bottom " +
                            titleBottom);

                        Assert.IsTrue(
                            eventButton.ActualHeight >=
                            (double)window.FindResource(
                                "NetLoom.Control.MinHitSize"),
                            "Event buttons keep the minimum hit area (§8).");

                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            BreadcrumbNamesOpenSectionWithoutSelectionAndKeepsPlacementPathAcrossSections()
        {
            // G2 (sprint46-mockup-gap): без выбора крошки называли «Карта» в любом разделе.
            RunOnSta(
                () =>
                {
                    var siteId =
                        Guid.Parse(
                            "46464646-1100-1100-1100-464646464646");

                    var rackId =
                        Guid.Parse(
                            "46464646-2100-2100-2100-464646464646");

                    var deviceId =
                        Guid.Parse(
                            "46464646-3100-3100-3100-464646464646");

                    var interfaceId =
                        Guid.Parse(
                            "46464646-4100-4100-4100-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                InspectorSnapshot(
                                    siteId,
                                    rackId,
                                    deviceId,
                                    interfaceId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) !=
                                null);

                        var breadcrumb =
                            (TextBlock)window.FindName(
                                "ShellBreadcrumbText");

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellMapSection"),
                            breadcrumb.Text);

                        Click(
                            (Button)window.FindName(
                                "ShellEquipmentButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellEquipmentSection"),
                            breadcrumb.Text,
                            "Without a selection the breadcrumb names the open section.");

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellSettingsSection"),
                            breadcrumb.Text);

                        Click(
                            (Button)window.FindName(
                                "ShellMapButton"));
                        PumpDispatcher();

                        SelectDevice(
                            window,
                            deviceId);

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));
                        PumpDispatcher();

                        StringAssert.Contains(
                            breadcrumb.Text,
                            "Site A");

                        StringAssert.Contains(
                            breadcrumb.Text,
                            "Rack A",
                            "The selection survives a section switch, so the placement path stays.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            InspectorKeepsMapContextAndSupportsStableInterfaceSelection()
        {
            RunOnSta(
                () =>
                {
                    var siteId =
                        Guid.Parse(
                            "46464646-1000-1000-1000-464646464646");

                    var rackId =
                        Guid.Parse(
                            "46464646-2000-2000-2000-464646464646");

                    var deviceId =
                        Guid.Parse(
                            "46464646-3000-3000-3000-464646464646");

                    var interfaceId =
                        Guid.Parse(
                            "46464646-4000-4000-4000-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                InspectorSnapshot(
                                    siteId,
                                    rackId,
                                    deviceId,
                                    interfaceId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) !=
                                null);

                        Click(
                            (Button)window.FindName(
                                "MapFitAllButton"));

                        PumpDispatcher();
                        PumpDispatcher();

                        var scroll =
                            (ScrollViewer)window.FindName(
                                "MapScrollViewer");

                        var zoomText =
                            ((TextBlock)window.FindName(
                                "MapZoomValueText"))
                            .Text;

                        var horizontal =
                            scroll.HorizontalOffset;

                        var vertical =
                            scroll.VerticalOffset;

                        SelectDevice(
                            window,
                            deviceId);

                        Assert.AreEqual(
                            horizontal,
                            scroll.HorizontalOffset,
                            0.5,
                            "Selecting a device must not pan the map.");

                        Assert.AreEqual(
                            vertical,
                            scroll.VerticalOffset,
                            0.5,
                            "Selecting a device must not pan the map.");

                        Assert.AreEqual(
                            zoomText,
                            ((TextBlock)window.FindName(
                                "MapZoomValueText"))
                            .Text,
                            "Selecting a device must not change zoom.");

                        var breadcrumb =
                            ((TextBlock)window.FindName(
                                "ShellBreadcrumbText"))
                            .Text;

                        StringAssert.Contains(
                            breadcrumb,
                            "Site A");

                        StringAssert.Contains(
                            breadcrumb,
                            "Rack A");

                        StringAssert.Contains(
                            ((TextBlock)window.FindName(
                                "InspectorEntityIdText"))
                            .Text,
                            deviceId.ToString("D"));

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((TabItem)window.FindName(
                                "InspectorInterfacesTab"))
                            .Visibility);

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((TabItem)window.FindName(
                                "InspectorLinksTab"))
                            .Visibility);

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((TabItem)window.FindName(
                                "InspectorEvidenceTab"))
                            .Visibility);

                        var tabs =
                            (TabControl)window.FindName(
                                "InspectorTabControl");

                        tabs.SelectedItem =
                            (TabItem)window.FindName(
                                "InspectorInterfacesTab");

                        PumpDispatcher();

                        var interfaceList =
                            (ItemsControl)window.FindName(
                                "DiagnosticInterfaceList");

                        Assert.AreEqual(
                            1,
                            interfaceList.Items.Count);

                        var interfaceButton =
                            FindVisualDescendant<Button>(
                                interfaceList);

                        Assert.IsNotNull(
                            interfaceButton);

                        Click(
                            interfaceButton);

                        StringAssert.Contains(
                            ((TextBlock)window.FindName(
                                "InspectorEntityIdText"))
                            .Text,
                            interfaceId.ToString("D"));

                        StringAssert.Contains(
                            ((TextBlock)window.FindName(
                                "DiagnosticElementTitleText"))
                            .Text,
                            "Gi0/1");

                        StringAssert.Contains(
                            ((TextBlock)window.FindName(
                                "ShellBreadcrumbText"))
                            .Text,
                            "Rack A");

                        Assert.AreEqual(
                            horizontal,
                            scroll.HorizontalOffset,
                            0.5,
                            "Selecting an interface in the inspector must preserve pan.");

                        Assert.AreEqual(
                            vertical,
                            scroll.VerticalOffset,
                            0.5,
                            "Selecting an interface in the inspector must preserve pan.");

                        Assert.AreEqual(
                            zoomText,
                            ((TextBlock)window.FindName(
                                "MapZoomValueText"))
                            .Text,
                            "Selecting an interface in the inspector must preserve zoom.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            MeaningfulPersistedViewportSurvivesStartupShellMeasurement()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.Parse(
                            "46464646-5000-5000-5000-464646464646");

                    var layoutStore =
                        new FixedViewportLayoutStore(
                            new MapLayoutSnapshot(
                                MapLayoutScope
                                    .PhysicalTopologyMapId,
                                new MapViewportLayout(
                                    2.0,
                                    0.0,
                                    0.0),
                                new MapDeviceLayout[0],
                                new MapLocationLayout[0]));

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    deviceId,
                                    "Switch viewport",
                                    "192.0.2.146")),
                            new EmptyLookupReader(),
                            layoutStore);

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) !=
                                null &&
                                ((ScrollViewer)window.FindName(
                                    "MapScrollViewer"))
                                .ViewportWidth >
                                0.0);

                        for (var index = 0;
                             index < 6;
                             index++)
                        {
                            PumpDispatcher();
                            Thread.Sleep(10);
                        }

                        Assert.AreEqual(
                            "200%",
                            ((TextBlock)window.FindName(
                                "MapZoomValueText"))
                            .Text,
                            "A meaningful persisted viewport must not be replaced by startup Fit all.");

                        Assert.AreEqual(
                            0,
                            layoutStore.SaveViewportCount,
                            "Preserving a meaningful startup viewport must not rewrite it before operator interaction.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            EquipmentAndMonitoringUseDistinctNavigationPanels()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.Parse(
                            "46464646-6000-6000-6000-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    deviceId,
                                    "Switch equipment",
                                    "192.0.2.160")),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) != null);

                        Click(
                            (Button)window.FindName(
                                "ShellEquipmentButton"));

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "ShellEquipmentSidebarPanel"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "ShellMonitoringSidebarPanel"))
                            .Visibility,
                            "Equipment must no longer open the monitoring form.");

                        var equipmentList =
                            (ItemsControl)window.FindName(
                                "EquipmentList");

                        Assert.AreEqual(
                            1,
                            equipmentList.Items.Count);

                        var equipmentDeviceButton =
                            FindVisualDescendant<Button>(
                                equipmentList);

                        Assert.IsNotNull(
                            equipmentDeviceButton);

                        Click(
                            equipmentDeviceButton);

                        WaitForCondition(
                            () =>
                                ((TextBlock)window.FindName(
                                    "MapZoomValueText"))
                                .Text ==
                                "100%");

                        var selectedBorder =
                            DeviceBorder(
                                window,
                                deviceId);

                        Assert.IsNotNull(
                            selectedBorder);

                        var selectionBrush =
                            (SolidColorBrush)window.FindResource(
                                "NetLoom.Brush.Selection");

                        Assert.AreEqual(
                            selectionBrush.Color,
                            ((SolidColorBrush)selectedBorder.BorderBrush)
                                .Color,
                            "Choosing a device from Equipment must select it on the map before the focus pulse.");

                        Click(
                            (Button)window.FindName(
                                "ShellMonitoringButton"));

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "ShellEquipmentSidebarPanel"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "ShellMonitoringSidebarPanel"))
                            .Visibility);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            OperatorTextTabsDisabledChromeAndEmptyInspectorStayReadable()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.Parse(
                            "46464646-6500-6500-6500-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    deviceId,
                                    "Longest operator device name",
                                    "192.0.2.180")),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) != null);

                        var startButton =
                            (Button)window.FindName(
                                "ShellMonitoringStartButton");

                        startButton.Measure(
                            new Size(
                                double.PositiveInfinity,
                                double.PositiveInfinity));

                        Assert.IsTrue(
                            double.IsNaN(
                                startButton.Width),
                            "The always-visible monitoring action must size from content instead of using a fixed Width.");

                        Assert.IsTrue(
                            startButton.ActualWidth +
                                startButton.Margin.Left +
                                startButton.Margin.Right +
                                0.5 >=
                            startButton.DesiredSize.Width,
                            "The always-visible monitoring action must fit instead of losing Russian letters.");

                        var monitoringHeader =
                            (TextBlock)window.FindName(
                                "ShellMonitoringHeaderText");

                        Assert.IsTrue(
                            double.IsNaN(
                                monitoringHeader.Width),
                            "ADR-083 moves monitoring state to the always-visible header instead of a fixed-width section panel.");
                        Assert.AreEqual(
                            TextTrimming.None,
                            monitoringHeader.TextTrimming);

                        SelectDevice(
                            window,
                            deviceId);

                        PumpDispatcher();

                        var inspector =
                            (TabControl)window.FindName(
                                "InspectorTabControl");

                        var tabs =
                            new[]
                            {
                                (TabItem)window.FindName(
                                    "InspectorOverviewTab"),
                                (TabItem)window.FindName(
                                    "InspectorInterfacesTab"),
                                (TabItem)window.FindName(
                                    "InspectorLinksTab"),
                                (TabItem)window.FindName(
                                    "InspectorEvidenceTab")
                            };

                        var visibleTabs =
                            tabs
                                .Where(
                                    item =>
                                        item.Visibility ==
                                        Visibility.Visible)
                                .ToArray();

                        Assert.IsTrue(
                            visibleTabs.Length >= 2);

                        var firstTop =
                            visibleTabs[0]
                                .TransformToAncestor(
                                    inspector)
                                .Transform(
                                    new Point(
                                        0.0,
                                        0.0))
                                .Y;

                        foreach (var tab in
                            visibleTabs.Skip(1))
                        {
                            var top =
                                tab
                                    .TransformToAncestor(
                                        inspector)
                                    .Transform(
                                        new Point(
                                            0.0,
                                            0.0))
                                    .Y;

                            Assert.AreEqual(
                                firstTop,
                                top,
                                0.5,
                                "Inspector tabs must stay in one row.");
                        }

                        var themeButton =
                            (Button)window.FindName(
                                "ShellThemeButton");

                        Click(
                            themeButton);

                        Click(
                            (Button)window.FindName(
                                "ShellDiscoveryButton"));

                        var discoveryStart =
                            (Button)window.FindName(
                                "DiscoveryStartButton");

                        Assert.IsFalse(
                            discoveryStart.IsEnabled);

                        discoveryStart.ApplyTemplate();
                        PumpDispatcher();

                        var disabledChrome =
                            FindVisualDescendant<Border>(
                                discoveryStart);

                        Assert.IsNotNull(
                            disabledChrome);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.SurfaceMuted"),
                            disabledChrome.Background,
                            "Disabled buttons in dark theme must use semantic dark chrome.");

                        Assert.AreEqual(
                            UiText.Get(
                                "DiscoveryStateIdle"),
                            ((TextBlock)window.FindName(
                                "DiscoveryStateValueText"))
                            .Text);

                        Assert.AreEqual(
                            UiText.Get(
                                "OperatorStatusGlyphIdle"),
                            ((TextBlock)window.FindName(
                                "DiscoveryStateGlyphText"))
                            .Text,
                            "Discovery before first run must be neutral, not a green success.");

                        Click(
                            (Button)window.FindName(
                                "ShellEquipmentButton"));

                        var equipmentList =
                            (ItemsControl)window.FindName(
                                "EquipmentList");

                        WaitForCondition(
                            () =>
                                equipmentList.Items.Count > 0 &&
                                FindVisualDescendant<Button>(
                                    equipmentList) != null);

                        var equipmentButton =
                            FindVisualDescendant<Button>(
                                equipmentList);

                        Assert.IsNotNull(
                            equipmentButton);

                        Click(
                            equipmentButton);
                        PumpDispatcher();

                        WaitForCondition(
                            () =>
                                FindVisualDescendant<Button>(
                                    equipmentList) != null);

                        equipmentButton =
                            FindVisualDescendant<Button>(
                                equipmentList);

                        Assert.IsNotNull(
                            equipmentButton);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.AccentSoft"),
                            equipmentButton.Background,
                            "The selected equipment row must remain visibly selected in dark theme.");
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Accent"),
                            equipmentButton.BorderBrush);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            EmptyInspectorShowsPromptWithoutEmptyTabs()
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

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((TabControl)window.FindName(
                                "InspectorTabControl"))
                            .Visibility,
                            "An empty inspector must show only its prompt, without an empty tab frame.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            OperatorContainersKeepSharedMinimumInsets()
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

                        var minimum =
                            12.0;

                        var railSummary =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.ShellRailSummary");
                        var eventPadding =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.ShellEventPadding");
                        var mapControls =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.ShellMapControlsPadding");
                        var expanderContent =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.ExpanderContent");
                        var inspectorContent =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.InspectorContentPadding");
                        var cardPadding =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.PanelPadding");

                        foreach (var inset in
                            new[]
                            {
                                railSummary,
                                eventPadding,
                                mapControls,
                                expanderContent
                            })
                        {
                            Assert.IsTrue(
                                inset.Left >= minimum &&
                                inset.Right >= minimum,
                                "Operator containers must keep at least 12 px horizontal breathing room from their frame or window edge.");
                        }

                        Assert.IsTrue(
                            cardPadding.Left >= 16.0 &&
                            cardPadding.Right >= 16.0,
                            "Operator cards must keep at least 16 px horizontal content padding.");

                        var inspector =
                            (TabControl)window.FindName(
                                "InspectorTabControl");

                        Assert.AreEqual(
                            inspectorContent,
                            inspector.Padding,
                            "Inspector content padding must come from the shared token.");

                        // I1 (sprint46-mockup-gap): содержимое вкладок инспектора без рамки, как в макетах ADR-083.
                        // Правило §5 о поле не меньше 12 от рамки к нему больше не относится: рамки нет.
                        Assert.AreEqual(
                            new Thickness(0.0),
                            inspector.BorderThickness,
                            "Inspector tab content has no frame; groups are separated by spacing (§5).");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            SingleLineControlsUseGlyphMetricOpticalCentering()
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

                        var family =
                            (System.Windows.Media.FontFamily)
                            window.FindResource(
                                "NetLoom.FontFamily.Ui");
                        var fontSize =
                            Convert.ToDouble(
                                window.FindResource(
                                    "NetLoom.FontSize.Body"));

                        var typeface =
                            new System.Windows.Media.Typeface(
                                family,
                                FontStyles.Normal,
                                FontWeights.Normal,
                                FontStretches.Normal);

                        System.Windows.Media.GlyphTypeface
                            glyphTypeface;

                        Assert.IsTrue(
                            typeface.TryGetGlyphTypeface(
                                out glyphTypeface),
                            "The UI font must expose glyph metrics for optical centering.");

                        var expectedOffset =
                            (
                                glyphTypeface.Baseline -
                                glyphTypeface.CapsHeight / 2.0 -
                                glyphTypeface.Height / 2.0
                            ) *
                            fontSize;

                        var actualOffset =
                            Convert.ToDouble(
                                window.FindResource(
                                    "NetLoom.Type.OpticalOffsetY"));

                        Assert.AreEqual(
                            expectedOffset,
                            actualOffset,
                            0.001,
                            "Optical vertical shift must come from Baseline, CapsHeight and Height instead of a hand-tuned per-control margin.");

                        var fitAllButton =
                            (Button)window.FindName(
                                "MapFitAllButton");

                        fitAllButton.ApplyTemplate();

                        var buttonPresenter =
                            (ContentPresenter)
                            fitAllButton.Template.FindName(
                                "ButtonContentPresenter",
                                fitAllButton);

                        Assert.IsNotNull(
                            buttonPresenter);

                        var buttonTransform =
                            buttonPresenter.RenderTransform
                                as System.Windows.Media.TranslateTransform;

                        Assert.IsNotNull(
                            buttonTransform);
                        Assert.AreEqual(
                            actualOffset,
                            buttonTransform.Y,
                            0.001);

                        var overviewTab =
                            (TabItem)window.FindName(
                                "InspectorOverviewTab");

                        overviewTab.ApplyTemplate();

                        var tabPresenter =
                            (ContentPresenter)
                            overviewTab.Template.FindName(
                                "InspectorTabHeaderPresenter",
                                overviewTab);

                        Assert.IsNotNull(
                            tabPresenter);

                        var tabTransform =
                            tabPresenter.RenderTransform
                                as System.Windows.Media.TranslateTransform;

                        Assert.IsNotNull(
                            tabTransform);
                        Assert.AreEqual(
                            actualOffset,
                            tabTransform.Y,
                            0.001);

                        var textBox =
                            (TextBox)window.FindName(
                                "MonitoringTargetAddressTextBox");

                        var basePadding =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.ControlPadding");
                        var opticalPadding =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.ControlPaddingOptical");

                        Assert.AreEqual(
                            basePadding.Left,
                            opticalPadding.Left,
                            0.001);
                        Assert.AreEqual(
                            basePadding.Right,
                            opticalPadding.Right,
                            0.001);
                        Assert.AreEqual(
                            Math.Max(
                                0.0,
                                basePadding.Top +
                                actualOffset),
                            opticalPadding.Top,
                            0.001);
                        Assert.AreEqual(
                            Math.Max(
                                0.0,
                                basePadding.Bottom -
                                actualOffset),
                            opticalPadding.Bottom,
                            0.001);
                        Assert.AreEqual(
                            opticalPadding,
                            textBox.Padding);

                        Assert.AreEqual(
                            LineStackingStrategy.BlockLineHeight,
                            fitAllButton.GetValue(
                                TextBlock.LineStackingStrategyProperty));
                        Assert.AreEqual(
                            fontSize,
                            Convert.ToDouble(
                                fitAllButton.GetValue(
                                    TextBlock.LineHeightProperty)),
                            0.001);

                        var badgeFontSize =
                            Convert.ToDouble(
                                window.FindResource(
                                    "NetLoom.Navigation.BadgeFontSize"));
                        var semiBoldTypeface =
                            new Typeface(
                                family,
                                FontStyles.Normal,
                                FontWeights.SemiBold,
                                FontStretches.Normal);
                        GlyphTypeface badgeGlyphTypeface;

                        Assert.IsTrue(
                            semiBoldTypeface.TryGetGlyphTypeface(
                                out badgeGlyphTypeface));

                        var expectedBadgeOffset =
                            (
                                badgeGlyphTypeface.Baseline -
                                badgeGlyphTypeface.CapsHeight / 2.0 -
                                badgeGlyphTypeface.Height / 2.0
                            ) *
                            badgeFontSize;

                        Assert.AreEqual(
                            expectedBadgeOffset,
                            Convert.ToDouble(
                                window.FindResource(
                                    "NetLoom.Type.BadgeOpticalOffsetY")),
                            0.001,
                            "Navigation badge digits must use the same glyph-metric optical-centering rule as buttons and tabs.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            ComboBoxUsesSemanticDarkChromeWhenNormalDisabledAndOpen()
        {
            RunOnSta(
                () =>
                {
                    var profileId =
                        Guid.Parse(
                            "46464646-6600-6600-6600-464646464646");

                    var profile =
                        new AccessProfile(
                            profileId,
                            "Field profile A",
                            true,
                            SnmpVersion.V2C,
                            null);
                    var secondProfileId =
                        Guid.Parse(
                            "46464646-6601-6601-6601-464646464646");
                    var secondProfile =
                        new AccessProfile(
                            secondProfileId,
                            "Field profile B",
                            true,
                            SnmpVersion.V1,
                            null);
                    var shellStateStore =
                        new MemoryShellStateStore(
                            new UiShellState(
                                profileId,
                                UiShellTheme.Dark));

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new RecordingMonitoringControl(),
                            new EmptyDiscoveryControl(),
                            new[]
                            {
                                profile,
                                secondProfile
                            },
                            new NoopCandidateMaterializer(),
                            shellStateStore);

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var combo =
                            (ComboBox)window.FindName(
                                "DiscoveryProfileComboBox");

                        combo.ApplyTemplate();
                        PumpDispatcher();

                        var chrome =
                            (Border)combo.Template.FindName(
                                "ComboChrome",
                                combo);

                        Assert.IsNotNull(
                            chrome);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Surface"),
                            chrome.Background,
                            "Enabled ComboBox must use the semantic dark surface.");

                        var selectionPresenter =
                            (ContentPresenter)combo.Template.FindName(
                                "SelectionPresenter",
                                combo);

                        Assert.IsNotNull(
                            selectionPresenter);
                        Assert.IsFalse(
                            selectionPresenter.IsHitTestVisible,
                            "Selected ComboBox content must not intercept clicks intended for the drop-down toggle.");
                        Assert.IsNotNull(
                            combo.SelectedItem,
                            "The seeded profile must be selected before ComboBox rendering is verified.");
                        Assert.IsNotNull(
                            selectionPresenter.Content,
                            "The selected ComboBox value must be present in the semantic template.");

                        PumpDispatcher();

                        var selectedText =
                            FindVisualDescendant<TextBlock>(
                                selectionPresenter);

                        Assert.IsNotNull(
                            selectedText,
                            "The selected ComboBox value must render as visible text.");
                        Assert.AreEqual(
                            UiText.Format(
                                "DiscoveryProfileDisplay",
                                profile.Name,
                                "v2c"),
                            selectedText.Text,
                            "The custom ComboBox template must render the selected discovery profile display text instead of dropping the selected value.");

                        combo.IsEnabled =
                            false;
                        PumpDispatcher();

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.SurfaceMuted"),
                            chrome.Background,
                            "Disabled ComboBox must use the semantic muted dark surface.");

                        combo.IsEnabled =
                            true;

                        var toggle =
                            (System.Windows.Controls.Primitives.ToggleButton)combo.Template.FindName(
                                "DropDownToggle",
                                combo);

                        Assert.IsNotNull(
                            toggle);

                        toggle.IsChecked =
                            true;
                        PumpDispatcher();

                        Assert.IsTrue(
                            combo.IsDropDownOpen,
                            "The shared profile ComboBox template must wire its full-field toggle to IsDropDownOpen.");

                        var popup =
                            (System.Windows.Controls.Primitives.Popup)
                            combo.Template.FindName(
                                "PART_Popup",
                                combo);

                        Assert.IsNotNull(
                            popup);
                        Assert.IsTrue(
                            popup.IsOpen);

                        var popupChrome =
                            popup.Child as Border;

                        Assert.IsNotNull(
                            popupChrome);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Surface"),
                            popupChrome.Background,
                            "Open ComboBox popup must use the semantic dark surface instead of Windows white chrome.");
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Border"),
                            popupChrome.BorderBrush);

                        Assert.AreEqual(
                            UiText.Format(
                                "MonitoringVersionFromProfile",
                                "v2c"),
                            ((TextBlock)window.FindName(
                                "MonitoringVersionValueText"))
                            .Text,
                            "Monitoring SNMP version is inherited from the active profile and must be presented as read-only text, not as a dead drop-down.");
                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "MonitoringVersionReadOnlyBorder"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "MonitoringVersionNoProfileText"))
                            .Visibility);
                        Assert.IsNull(
                            window.FindName(
                                "MonitoringVersionComboBox"));

                        combo.IsDropDownOpen =
                            false;
                        combo.SelectedIndex =
                            1;
                        PumpDispatcher();

                        Assert.AreEqual(
                            secondProfileId,
                            shellStateStore.LastSaved.AccessProfileId,
                            "Changing the shared profile selector must persist the newly active profile.");
                        Assert.AreEqual(
                            UiText.Format(
                                "MonitoringVersionFromProfile",
                                "v1"),
                            ((TextBlock)window.FindName(
                                "MonitoringVersionValueText"))
                            .Text,
                            "Changing the active SNMP profile must update the read-only monitoring version.");
                        Assert.AreEqual(
                            UiText.Get(
                                "DiscoveryProfileSettingsHint"),
                            ((TextBlock)window.FindName(
                                "ShellProfileSettingsSummaryText"))
                            .Text);
                        Assert.AreEqual(
                            2,
                            ((ListBox)window.FindName(
                                "ShellProfileSettingsList"))
                            .Items.Count);
                        Assert.IsTrue(
                            ((Button)window.FindName(
                                "ShellProfileEditButton"))
                            .IsEnabled);
                        Assert.IsTrue(
                            ((Button)window.FindName(
                                "ShellProfileDeleteButton"))
                            .IsEnabled);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            ProfileSettingsSelectionDoesNotChangeActiveProfile()
        {
            RunOnSta(
                () =>
                {
                    var first =
                        new AccessProfile(
                            Guid.Parse(
                                "46464646-7100-7100-7100-464646464646"),
                            "Alpha",
                            true,
                            SnmpVersion.V2C,
                            null);
                    var second =
                        new AccessProfile(
                            Guid.Parse(
                                "46464646-7200-7200-7200-464646464646"),
                            "Beta",
                            true,
                            SnmpVersion.V1,
                            null);
                    var stateStore =
                        new MemoryShellStateStore(
                            new UiShellState(
                                first.Id,
                                UiShellTheme.Dark));
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new RecordingMonitoringControl(),
                            new EmptyDiscoveryControl(),
                            new[]
                            {
                                first,
                                second
                            },
                            new NoopCandidateMaterializer(),
                            stateStore);

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));
                        PumpDispatcher();

                        var activeCombo =
                            (ComboBox)window.FindName(
                                "DiscoveryProfileComboBox");
                        var settingsList =
                            (ListBox)window.FindName(
                                "ShellProfileSettingsList");

                        Assert.AreEqual(0, activeCombo.SelectedIndex);
                        Assert.AreEqual(2, settingsList.Items.Count);

                        settingsList.SelectedIndex = 1;
                        PumpDispatcher();

                        Assert.AreEqual(
                            0,
                            activeCombo.SelectedIndex,
                            "Choosing an inactive profile for maintenance must not activate it.");
                        Assert.AreEqual(
                            first.Id,
                            stateStore.Load().AccessProfileId,
                            "Maintenance selection must not persist a different active profile.");
                        Assert.IsTrue(
                            ((Button)window.FindName(
                                "ShellProfileEditButton"))
                            .IsEnabled);
                        Assert.IsTrue(
                            ((Button)window.FindName(
                                "ShellProfileDeleteButton"))
                            .IsEnabled);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            ExplicitShowAllCanGoBelowStartupReadableZoomFloor()
        {
            RunOnSta(
                () =>
                {
                    var firstDeviceId =
                        Guid.Parse(
                            "46464646-6100-6100-6100-464646464646");
                    var secondDeviceId =
                        Guid.Parse(
                            "46464646-6200-6200-6200-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                WideSnapshot(
                                    firstDeviceId,
                                    secondDeviceId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    firstDeviceId) != null &&
                                DeviceBorder(
                                    window,
                                    secondDeviceId) != null);

                        Click(
                            (Button)window.FindName(
                                "MapFitAllButton"));

                        WaitForCondition(
                            () =>
                            {
                                var canvas =
                                    (Canvas)window.FindName(
                                        "MapCanvas");
                                var scale =
                                    canvas.LayoutTransform
                                        as ScaleTransform;

                                return scale != null &&
                                    scale.ScaleX < 0.75 &&
                                    scale.ScaleY < 0.75;
                            });

                        var mapCanvas =
                            (Canvas)window.FindName(
                                "MapCanvas");
                        var finalScale =
                            (ScaleTransform)mapCanvas.LayoutTransform;

                        Assert.IsTrue(
                            finalScale.ScaleX < 0.75,
                            "Explicit Show all must fit the whole site even when that requires a zoom below the automatic readability floor.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            OperatorPanelsUseSharedGridAndTypographyTokens()
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

                        var panelPadding =
                            (Thickness)window.FindResource(
                                "NetLoom.Thickness.PanelPadding");

                        Assert.AreEqual(
                            new Thickness(20),
                            panelPadding,
                            "Operator panels must share the 20 px panel inset.");

                        var mapScroller =
                            (ScrollViewer)window.FindName(
                                "MapScrollViewer");
                        var mapScrollStyle =
                            mapScroller.Resources[
                                typeof(System.Windows.Controls.Primitives.ScrollBar)]
                            as Style;

                        Assert.IsNotNull(
                            mapScrollStyle,
                            "The map must override system scrollbars with theme-aware chrome.");
                        Assert.IsNotNull(
                            mapScrollStyle.BasedOn,
                            "The map scrollbar override must inherit the shared semantic map scrollbar style.");

                        mapScroller.ApplyTemplate();

                        var scrollCorner =
                            mapScroller.Template.FindName(
                                "MapScrollBarCorner",
                                mapScroller) as Border;

                        Assert.IsNotNull(
                            scrollCorner,
                            "The map ScrollViewer must own the bottom-right scrollbar corner instead of exposing the system control brush.");
                        Assert.AreSame(
                            mapScroller.Background,
                            scrollCorner.Background,
                            "The scrollbar corner must use the same theme-aware canvas brush as the map viewport.");

                        Assert.AreEqual(
                            13d,
                            (double)window.FindResource(
                                "NetLoom.FontSize.Body"),
                            0.001d,
                            "Operator body copy must use the shared readable type scale.");

                        Assert.AreEqual(
                            "NetLoom",
                            ((TextBlock)window.FindName(
                                "ShellBrandText"))
                            .Text,
                            "The application header must not duplicate the full window title.");

                        Click(
                            (Button)window.FindName(
                                "ShellSearchButton"));
                        PumpDispatcher();

                        var searchScroller =
                            (ScrollViewer)window.FindName(
                                "ShellSearchSidebarPanel");
                        var searchRoot =
                            searchScroller.Content as FrameworkElement;

                        Assert.IsNotNull(
                            searchRoot);
                        Assert.AreEqual(
                            new Thickness(20),
                            searchRoot.Margin,
                            "Search content must align to the common sidebar inset.");

                        Assert.AreEqual(
                            HorizontalAlignment.Stretch,
                            ((Button)window.FindName(
                                "LookupSearchButton"))
                            .HorizontalAlignment,
                            "Primary sidebar actions must fill the shared content width.");

                        Assert.AreEqual(
                            window.FontFamily.Source,
                            ((TextBlock)window.FindName(
                                "MonitoringActiveTargetValueText"))
                            .FontFamily.Source,
                            "Operator status text must not leak monospace styling.");

                        Click(
                            (Button)window.FindName(
                                "ShellDiscoveryButton"));

                        ((Expander)window.FindName(
                            "DiscoveryExpander"))
                            .IsExpanded =
                            true;

                        PumpDispatcher();

                        var start =
                            (TextBox)window.FindName(
                                "DiscoveryStartAddressTextBox");
                        var end =
                            (TextBox)window.FindName(
                                "DiscoveryEndAddressTextBox");

                        Assert.IsTrue(
                            start.ActualWidth > 0d);
                        Assert.AreEqual(
                            start.ActualWidth,
                            end.ActualWidth,
                            1d,
                            "Discovery range fields must use equal columns.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            EmptyProfileSetDoesNotExposeBlankComboPopup()
        {
            RunOnSta(
                () =>
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new RecordingMonitoringControl(),
                            new EmptyDiscoveryControl(),
                            new AccessProfile[0],
                            new NoopCandidateMaterializer(),
                            new MemoryShellStateStore(
                                UiShellState.Default));

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((ComboBox)window.FindName(
                                "DiscoveryProfileComboBox"))
                            .Visibility,
                            "An empty profile list must not open as a blank white popup.");

                        var manageButton =
                            (Button)window.FindName(
                                "ShellProfileAddButton");

                        Assert.AreEqual(
                            Visibility.Visible,
                            manageButton.Visibility);
                        Assert.AreEqual(
                            UiText.Get(
                                "DiscoveryProfileManageAction"),
                            manageButton.Content);

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellProfilesEmpty"),
                            ((TextBlock)window.FindName(
                                "ShellProfileStatusText"))
                            .Text);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "MonitoringVersionReadOnlyBorder"))
                            .Visibility);

                        var noProfile =
                            (TextBlock)window.FindName(
                                "MonitoringVersionNoProfileText");

                        Assert.AreEqual(
                            Visibility.Visible,
                            noProfile.Visibility);
                        Assert.AreEqual(
                            UiText.Get(
                                "MonitoringVersionNoProfile"),
                            noProfile.Text);
                        Assert.AreEqual(
                            TextWrapping.Wrap,
                            noProfile.TextWrapping,
                            "Without an active profile the monitoring version must be plain wrapping guidance, not a clipped field-like control.");
                        Assert.AreEqual(
                            UiText.Get(
                                "DiscoveryProfileSettingsEmpty"),
                            ((TextBlock)window.FindName(
                                "ShellProfileSettingsSummaryText"))
                            .Text);
                        Assert.AreEqual(
                            0,
                            ((ListBox)window.FindName(
                                "ShellProfileSettingsList"))
                            .Items.Count);

                        Click(
                            manageButton);
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "ShellSettingsSidebarPanel"))
                            .Visibility,
                            "No-profile actions must lead to the single profile-management surface instead of opening creation in place.");

                        Assert.IsFalse(
                            ((Button)window.FindName(
                                "ShellProfileEditButton"))
                            .IsEnabled);
                        Assert.IsFalse(
                            ((Button)window.FindName(
                                "ShellProfileDeleteButton"))
                            .IsEnabled);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            DeviceEvidenceEmptyStateUsesDeviceWording()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.Parse(
                            "46464646-6100-6100-6100-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                Snapshot(
                                    deviceId,
                                    "Switch evidence",
                                    "192.0.2.161")),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) != null);

                        SelectDevice(
                            window,
                            deviceId);

                        var rows =
                            (ItemsControl)window.FindName(
                                "DiagnosticTertiaryList");

                        Assert.AreEqual(
                            1,
                            rows.Items.Count);

                        var row =
                            rows.Items[0];

                        var textProperty =
                            row.GetType()
                                .GetProperty(
                                    "Text");

                        Assert.IsNotNull(
                            textProperty);
                        Assert.AreEqual(
                            UiText.Get(
                                "DiagnosticNoEvidenceDevice"),
                            textProperty.GetValue(
                                row,
                                null));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            CriticalConnectedLinkDrivesDeviceStateStripeWithoutSelectionOverride()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6200-6200-6200-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6201-6201-6201-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6202-6202-6202-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    firstId) != null);

                        var first =
                            DeviceBorder(
                                window,
                                firstId);

                        var stripe =
                            FindVisualDescendantByTag<Border>(
                                first,
                                "NodeStateStripe");

                        Assert.IsNotNull(
                            stripe);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Critical"),
                            stripe.Background);

                        var statusIcon =
                            FindVisualDescendantByTag<System.Windows.Shapes.Path>(
                                first,
                                "NodeStatusIcon");

                        Assert.IsNotNull(
                            statusIcon);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Icon.StatusCritical"),
                            statusIcon.Data,
                            "Critical map status must use the canonical non-color vector symbol.");
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Critical"),
                            statusIcon.Stroke,
                            "Map status must expose the same critical color plus a non-color vector symbol.");

                        SelectDevice(
                            window,
                            firstId);

                        var inspectorStatus =
                            ((TextBlock)window.FindName(
                                "InspectorOperationalStatusText"))
                            .Text;

                        Assert.IsFalse(
                            inspectorStatus.Contains(
                                UiText.Get(
                                    "OperatorStatusCritical")),
                            "Inspector device availability must not reuse alert severity as the device state.");

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((TextBlock)window.FindName(
                                "InspectorProblemText"))
                            .Visibility,
                            "Inspector must expose the active problem separately from availability.");

                        Assert.AreEqual(
                            UiText.Get(
                                "InspectorShowLoopAction"),
                            ((Button)window.FindName(
                                "InspectorPrimaryActionButton"))
                            .Content,
                            "Forwarding-cycle problems must expose one clear primary action.");

                        var alertRow =
                            ((ItemsControl)window.FindName(
                                "AlertList"))
                            .Items[0];

                        var severityProperty =
                            alertRow.GetType()
                                .GetProperty(
                                    "SeverityText");

                        Assert.IsNotNull(
                            severityProperty);

                        Assert.AreEqual(
                            UiText.Get(
                                "OperatorStatusGlyphCritical") +
                            " " +
                            UiText.Get(
                                "OperatorStatusCritical"),
                            (string)severityProperty.GetValue(
                                alertRow,
                                null),
                            "Alerts must use the same critical label and glyph as map and inspector.");

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Critical"),
                            stripe.Background,
                            "Selection outline must not erase the current state stripe.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            SelectedCriticalLinkKeepsCriticalStateColor()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6510-6510-6510-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6511-6511-6511-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6512-6512-6512-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    firstId) != null);

                        var canvas =
                            (Canvas)window.FindName(
                                "MapCanvas");

                        var link =
                            canvas.Children
                                .OfType<System.Windows.Shapes.Line>()
                                .Single(
                                    item =>
                                        item.Tag is Guid &&
                                        (Guid)item.Tag ==
                                            physicalLinkId);

                        var criticalBrush =
                            window.FindResource(
                                "NetLoom.Brush.Critical");

                        Assert.AreSame(
                            criticalBrush,
                            link.Stroke);

                        var beforeThickness =
                            link.StrokeThickness;

                        link.RaiseEvent(
                            new MouseButtonEventArgs(
                                Mouse.PrimaryDevice,
                                Environment.TickCount,
                                MouseButton.Left)
                            {
                                RoutedEvent =
                                    UIElement.MouseLeftButtonDownEvent,
                                Source =
                                    link
                            });

                        PumpDispatcher();

                        link =
                            canvas.Children
                                .OfType<System.Windows.Shapes.Line>()
                                .Single(
                                    item =>
                                        item.Tag is Guid &&
                                        (Guid)item.Tag ==
                                            physicalLinkId);

                        Assert.AreSame(
                            criticalBrush,
                            link.Stroke,
                            "Selection must not replace a critical state color with blue.");

                        Assert.IsTrue(
                            link.StrokeThickness >
                            beforeThickness,
                            "Selection may strengthen the status line without replacing its status color.");

                        var halo =
                            canvas.Children
                                .OfType<System.Windows.Shapes.Path>()
                                .Single(
                                    item =>
                                        item.Tag is Guid &&
                                        (Guid)item.Tag ==
                                            physicalLinkId);

                        Assert.AreEqual(
                            Visibility.Visible,
                            halo.Visibility,
                            "A selected link must expose an additional visible selection halo.");

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Selection"),
                            halo.Stroke);

                        Assert.IsTrue(
                            halo.StrokeThickness >=
                            link.StrokeThickness * 3.0,
                            "The selection halo must be roughly three times wider than the selected status line.");

                        Assert.IsTrue(
                            halo.Opacity >= 0.40 &&
                            halo.Opacity < 0.55,
                            "The halo must be visibly accent-colored while remaining translucent enough to preserve the operational status line.");

                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.TextSecondary"),
                            ((TextBlock)window.FindName(
                                "InspectorEntityTypeText"))
                            .Foreground,
                            "The inspector entity type is descriptive text, not a link or status accent.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            OperatorSurfacesKeepInternalIdentifiersBehindTechnicalDetails()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6300-6300-6300-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6301-6301-6301-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6302-6302-6302-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                ((ItemsControl)window.FindName(
                                    "AlertList"))
                                .Items.Count == 1);

                        var alertList =
                            (ItemsControl)window.FindName(
                                "AlertList");

                        var row =
                            alertList.Items[0];

                        var scopeProperty =
                            row.GetType()
                                .GetProperty(
                                    "Scope");
                        var technicalProperty =
                            row.GetType()
                                .GetProperty(
                                    "TechnicalDetails");

                        Assert.IsNotNull(
                            scopeProperty);
                        Assert.IsNotNull(
                            technicalProperty);

                        var scope =
                            (string)scopeProperty.GetValue(
                                row,
                                null);
                        var technical =
                            (string)technicalProperty.GetValue(
                                row,
                                null);

                        StringAssert.Contains(
                            scope,
                            "Switch critical A");
                        StringAssert.Contains(
                            scope,
                            "Switch critical B");
                        Assert.IsFalse(
                            scope.Contains(
                                physicalLinkId.ToString(
                                    "D")));

                        StringAssert.Contains(
                            technical,
                            physicalLinkId.ToString(
                                "D"));
                        StringAssert.Contains(
                            technical,
                            "cist");

                        var shellEvents =
                            (ItemsControl)window.FindName(
                                "ShellEventList");

                        Assert.AreEqual(
                            1,
                            shellEvents.Items.Count);

                        var shellEvent =
                            shellEvents.Items[0];
                        var shellEventTitle =
                            (string)shellEvent.GetType()
                                .GetProperty(
                                    "Title")
                                .GetValue(
                                    shellEvent,
                                    null);
                        var shellEventScope =
                            (string)shellEvent.GetType()
                                .GetProperty(
                                    "Scope")
                                .GetValue(
                                    shellEvent,
                                    null);

                        StringAssert.Contains(
                            shellEventScope,
                            "Switch critical A");
                        Assert.IsFalse(
                            shellEventTitle.Contains(
                                physicalLinkId.ToString(
                                    "D")));
                        Assert.IsFalse(
                            shellEventScope.Contains(
                                "cist"));

                        SelectDevice(
                            window,
                            firstId);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "DiagnosticStatusText"))
                            .Visibility,
                            "Selected entities must not repeat the redundant current-snapshot sentence.");

                        var inspectorTechnical =
                            (Expander)window.FindName(
                                "InspectorTechnicalDetailsExpander");

                        Assert.AreEqual(
                            Visibility.Visible,
                            inspectorTechnical.Visibility);
                        Assert.IsFalse(
                            inspectorTechnical.IsExpanded);
                        StringAssert.Contains(
                            ((TextBlock)window.FindName(
                                "InspectorEntityIdText"))
                            .Text,
                            firstId.ToString(
                                "D"));

                        Assert.AreEqual(
                            "Switch critical A",
                            ((TextBlock)window.FindName(
                                "MonitoringSelectedDeviceValueText"))
                            .Text);

                        var monitoringHint =
                            ((TextBlock)window.FindName(
                                "MonitoringHintText"))
                            .Text;

                        Assert.IsFalse(
                            monitoringHint.Contains(
                                "NETLOOM_SNMP"));
                        Assert.IsFalse(
                            monitoringHint.Contains(
                                "DeviceId"));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AlertTechnicalDetailsExpansionSurvivesUnchangedRefresh()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6390-6390-6390-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6391-6391-6391-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6392-6392-6392-464646464646");
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        var list =
                            (ItemsControl)window.FindName(
                                "AlertList");

                        WaitForCondition(
                            () =>
                                list.Items.Count == 1);

                        Click(
                            (Button)window.FindName(
                                "ShellAlertsButton"));
                        PumpDispatcher();

                        var expander =
                            FindVisualDescendant<Expander>(
                                list);

                        Assert.IsNotNull(
                            expander);

                        expander.IsExpanded =
                            true;
                        PumpDispatcher();

                        var alertSnapshot =
                            new TopologyAlertSnapshot(
                                Now.AddSeconds(5),
                                "cist",
                                new[]
                                {
                                    new TopologyAlert(
                                        "critical-link",
                                        TopologyAlertKind.ForwardingCycle,
                                        TopologyAlertSeverity.Critical,
                                        "cist",
                                        new string[0],
                                        new[]
                                        {
                                            physicalLinkId
                                        },
                                        new[]
                                        {
                                            TopologyAlertReason
                                                .ConfirmedForwardingCycle
                                        })
                                });

                        var showAlerts =
                            typeof(MainWindow).GetMethod(
                                "ShowAlerts",
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic);

                        Assert.IsNotNull(
                            showAlerts);

                        var transitionType =
                            showAlerts.GetParameters()[1]
                                .ParameterType;

                        showAlerts.Invoke(
                            window,
                            new[]
                            {
                                (object)alertSnapshot,
                                Activator.CreateInstance(
                                    transitionType)
                            });

                        PumpDispatcher();

                        var refreshedExpander =
                            FindVisualDescendant<Expander>(
                                list);

                        Assert.IsNotNull(
                            refreshedExpander);
                        Assert.IsTrue(
                            refreshedExpander.IsExpanded,
                            "Refreshing an unchanged alert must preserve the operator's expanded technical-details state.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            InspectorTechnicalDetailsExpansionSurvivesUnchangedRefresh()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6450-6450-6450-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6451-6451-6451-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6452-6452-6452-464646464646");
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    firstId) != null);

                        SelectDevice(
                            window,
                            firstId);

                        var expander =
                            (Expander)window.FindName(
                                "InspectorTechnicalDetailsExpander");

                        expander.IsExpanded =
                            true;

                        var showSelected =
                            typeof(MainWindow).GetMethod(
                                "ShowSelectedDiagnostic",
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic);

                        Assert.IsNotNull(
                            showSelected);

                        showSelected.Invoke(
                            window,
                            null);

                        PumpDispatcher();

                        Assert.IsTrue(
                            expander.IsExpanded,
                            "Refreshing the same inspector entity must preserve the operator's expanded technical-details state.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AlertEventTimeDoesNotDriftOnUnchangedRefresh()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6460-6460-6460-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6461-6461-6461-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6462-6462-6462-464646464646");
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        var shellEvents =
                            (ItemsControl)window.FindName(
                                "ShellEventList");

                        WaitForCondition(
                            () =>
                                shellEvents.Items.Count == 1);

                        var firstEvent =
                            shellEvents.Items[0];
                        var timeProperty =
                            firstEvent.GetType()
                                .GetProperty(
                                    "TimeText");

                        Assert.IsNotNull(
                            timeProperty);

                        var originalTime =
                            (string)timeProperty.GetValue(
                                firstEvent,
                                null);

                        var laterSnapshot =
                            new TopologyAlertSnapshot(
                                Now.AddMinutes(5),
                                "cist",
                                new[]
                                {
                                    new TopologyAlert(
                                        "critical-link",
                                        TopologyAlertKind.ForwardingCycle,
                                        TopologyAlertSeverity.Critical,
                                        "cist",
                                        new string[0],
                                        new[]
                                        {
                                            physicalLinkId
                                        },
                                        new[]
                                        {
                                            TopologyAlertReason
                                                .ConfirmedForwardingCycle
                                        })
                                });

                        var showAlerts =
                            typeof(MainWindow).GetMethod(
                                "ShowAlerts",
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic);

                        Assert.IsNotNull(
                            showAlerts);

                        var transitionType =
                            showAlerts.GetParameters()[1]
                                .ParameterType;

                        showAlerts.Invoke(
                            window,
                            new[]
                            {
                                (object)laterSnapshot,
                                Activator.CreateInstance(
                                    transitionType)
                            });

                        PumpDispatcher();

                        Assert.AreEqual(
                            1,
                            shellEvents.Items.Count,
                            "An unchanged alert must not create another shell event.");

                        var refreshedTime =
                            (string)timeProperty.GetValue(
                                shellEvents.Items[0],
                                null);

                        Assert.AreEqual(
                            originalTime,
                            refreshedTime,
                            "Event time must represent the transition, not the latest refresh.");

                        var alertRow =
                            ((ItemsControl)window.FindName(
                                "AlertList"))
                            .Items[0];
                        var alertTime =
                            (string)alertRow.GetType()
                                .GetProperty(
                                    "TimeText")
                                .GetValue(
                                    alertRow,
                                    null);

                        Assert.AreEqual(
                            originalTime,
                            alertTime,
                            "The current alert card must keep its first-seen time across unchanged refreshes.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            InspectorSeparatesAvailabilitySeverityAndExplanation()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6470-6470-6470-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6471-6471-6471-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6472-6472-6472-464646464646");
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    firstId) != null);

                        SelectDevice(
                            window,
                            firstId);

                        var availability =
                            ((TextBlock)window.FindName(
                                "InspectorOperationalStatusText"))
                            .Text;
                        var availabilityPrefix =
                            UiText.Get(
                                    "InspectorAvailabilityMonitoringStopped")
                                .Split('{')[0]
                                .Trim();

                        StringAssert.StartsWith(
                            availability,
                            availabilityPrefix,
                            "Global monitoring stop must be named as stopped monitoring and include data age.");
                        Assert.IsFalse(
                            availability.Contains(
                                UiText.Get(
                                    "OperatorStatusCritical")),
                            "Device availability must remain separate from alert severity.");

                        var problem =
                            (TextBlock)window.FindName(
                                "InspectorProblemText");
                        var expectedSeverity =
                            UiText.Get(
                                "OperatorStatusGlyphCritical") +
                            " " +
                            UiText.Get(
                                "OperatorStatusCritical");
                        var deviceProblemPrefix =
                            UiText.Get(
                                    "InspectorProblemForwardingCycleDeviceTitle")
                                .Split('{')[0]
                                .Trim();

                        StringAssert.StartsWith(
                            problem.Text,
                            expectedSeverity + " — ",
                            "Problem importance must be explicit text plus glyph, not color-only.");
                        StringAssert.Contains(
                            problem.Text,
                            deviceProblemPrefix,
                            "Device loop wording must name the device rather than a generic object.");

                        var explanation =
                            ((TextBlock)window.FindName(
                                "InspectorProblemExplanationText"))
                            .Text;
                        StringAssert.Contains(
                            explanation,
                            "Switch critical A");
                        StringAssert.Contains(
                            explanation,
                            "Switch critical B");
                        StringAssert.Contains(
                            explanation,
                            UiText.Get(
                                "InspectorProblemForwardingCycleExplanation"),
                            "Loop explanation must remain a separate neutral text line.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            InspectorDeviceDetailsShowDescriptionPlacementAndPortTable()
        {
            RunOnSta(
                () =>
                {
                    Guid firstId;
                    Guid secondId;
                    Guid firstInterfaceId;
                    Guid secondInterfaceId;
                    Guid linkId;
                    var snapshot =
                        Pass2InspectorSnapshot(
                            out firstId,
                            out secondId,
                            out firstInterfaceId,
                            out secondInterfaceId,
                            out linkId);
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                snapshot),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    firstId) != null);

                        SelectDevice(
                            window,
                            firstId);

                        // G5: заголовок группы инспектора — подпись раздела, заглавными стилем.
                        Assert.AreEqual(
                            UiText.Get(
                                "DiagnosticStateTitle")
                                .ToUpper(
                                    System.Globalization.CultureInfo.CurrentUICulture),
                            ((TextBlock)window.FindName(
                                "DiagnosticPrimaryTitleText"))
                            .Text);

                        var fields =
                            (ItemsControl)window.FindName(
                                "DiagnosticFieldsList");
                        var description =
                            FindDiagnosticFieldValue(
                                fields,
                                UiText.Get(
                                    "DiagnosticFieldDescription"));
                        var location =
                            FindDiagnosticFieldValue(
                                fields,
                                UiText.Get(
                                    "DiagnosticFieldLocation"));

                        Assert.AreEqual(
                            "NetLoom Sprint39 Acceptance",
                            description,
                            "Description must be a labeled detail row rather than an unlabeled subtitle.");
                        StringAssert.Contains(
                            location,
                            "Rack A",
                            "Assigned device location must be visible in details.");

                        Assert.AreEqual(
                            string.Empty,
                            ((TextBlock)window.FindName(
                                "DiagnosticElementSubtitleText"))
                            .Text,
                            "Device description must not remain as an unlabeled subtitle.");

                        var portsScrollViewer =
                            (ScrollViewer)window.FindName(
                                "InspectorInterfacesScrollViewer");
                        Assert.AreEqual(
                            ScrollBarVisibility.Disabled,
                            portsScrollViewer.HorizontalScrollBarVisibility,
                            "Narrow Inspector port rows must not require horizontal scrolling.");

                        var ports =
                            (ItemsControl)window.FindName(
                                "DiagnosticInterfaceList");
                        Assert.AreEqual(
                            1,
                            ports.Items.Count);

                        var row =
                            ports.Items[0];
                        var portName =
                            DiagnosticRowString(
                                row,
                                "PortName");
                        var portMeta =
                            DiagnosticRowString(
                                row,
                                "PortMeta");
                        var portStatus =
                            DiagnosticRowString(
                                row,
                                "PortStatus");
                        var portStp =
                            DiagnosticRowString(
                                row,
                                "PortStp");
                        var portNeighbor =
                            DiagnosticRowString(
                                row,
                                "PortNeighbor");
                        var portStatusGlyph =
                            DiagnosticRowString(
                                row,
                                "PortStatusGlyph");
                        var portSummary =
                            DiagnosticRowString(
                                row,
                                "PortSummary");
                        var portNeighborLine =
                            DiagnosticRowString(
                                row,
                                "PortNeighborLine");

                        Assert.AreEqual(
                            "Gi0/1",
                            portName,
                            "Inspector port title must not duplicate ifIndex.");
                        Assert.AreEqual(
                            1,
                            CountOccurrences(
                                portMeta,
                                "ifIndex 1"),
                            "ifIndex must appear exactly once in port metadata.");
                        StringAssert.Contains(
                            portMeta,
                            "MAC 00:11:22:33:44:55");
                        StringAssert.Contains(
                            portMeta,
                            UiText.FormatCount(
                                "InspectorRelativeDays",
                                12),
                            "Port observation age must use the natural localized relative-time form.");
                        Assert.IsFalse(
                            portMeta.Contains(
                                "IF-MIB"),
                            "Port table must not expose IF-MIB implementation wording.");
                        StringAssert.StartsWith(
                            portStatus,
                            UiText.Get(
                                "OperatorStatusGlyphNormal"),
                            "Port state must include a non-color status glyph.");
                        Assert.AreEqual(
                            UiText.Get(
                                "DiagnosticStpForwarding"),
                            portStp);
                        StringAssert.Contains(
                            portNeighbor,
                            "Switch B");
                        Assert.IsTrue(
                            DiagnosticRowBool(
                                row,
                                "IsRiskyStp"),
                            "Forwarding STP on the active forwarding-cycle link must be marked risky.");
                        Assert.AreEqual(
                            UiText.Get(
                                "OperatorStatusGlyphNormal"),
                            portStatusGlyph);
                        StringAssert.Contains(
                            portSummary,
                            UiText.Get(
                                "DiagnosticStpForwarding"));
                        StringAssert.Contains(
                            portSummary,
                            UiText.Format(
                                "DiagnosticSpeedGbps",
                                1.0));
                        StringAssert.StartsWith(
                            portNeighborLine,
                            "→ ");
                        StringAssert.Contains(
                            portNeighborLine,
                            "Switch B");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            InspectorLinkUsesCurrentFreshnessAndEndpointRows()
        {
            RunOnSta(
                () =>
                {
                    Guid firstId;
                    Guid secondId;
                    Guid firstInterfaceId;
                    Guid secondInterfaceId;
                    Guid linkId;
                    var snapshot =
                        Pass2InspectorSnapshot(
                            out firstId,
                            out secondId,
                            out firstInterfaceId,
                            out secondInterfaceId,
                            out linkId);
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                snapshot),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var showLink =
                            typeof(MainWindow).GetMethod(
                                "ShowLinkDiagnostic",
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic);

                        Assert.IsNotNull(
                            showLink);

                        showLink.Invoke(
                            window,
                            new object[]
                            {
                                snapshot.DiagnosticSnapshot.Links[0]
                            });
                        PumpDispatcher();

                        var fields =
                            (ItemsControl)window.FindName(
                                "DiagnosticFieldsList");

                        var freshness =
                            FindDiagnosticFieldValue(
                                fields,
                                UiText.Get(
                                    "DiagnosticFieldFreshness"));
                        Assert.AreEqual(
                            UiText.Get(
                                "FreshnessStale"),
                            freshness,
                            "A link last seen 12 days ago must be stale even if the cached map snapshot still says Fresh.");

                        Assert.AreEqual(
                            2,
                            CountDiagnosticSections(
                                fields),
                            "Link endpoint details must be grouped into two named device sections.");

                        Assert.IsTrue(
                            DiagnosticSectionExists(
                                fields,
                                "Switch A"),
                            "First endpoint section must use the device name, not side A/B.");
                        Assert.IsTrue(
                            DiagnosticSectionExists(
                                fields,
                                "Switch B"),
                            "Second endpoint section must use the device name, not side A/B.");

                        StringAssert.Contains(
                            FindDiagnosticFieldValueAfterSection(
                                fields,
                                "Switch A",
                                UiText.Get(
                                    "DiagnosticFieldPort")),
                            "Gi0/1");
                        StringAssert.Contains(
                            FindDiagnosticFieldValueAfterSection(
                                fields,
                                "Switch A",
                                UiText.Get(
                                    "DiagnosticFieldLocation")),
                            "Rack A");
                        Assert.AreEqual(
                            UiText.Get(
                                "DiagnosticLocationNotAssigned"),
                            FindDiagnosticFieldValueAfterSection(
                                fields,
                                "Switch B",
                                UiText.Get(
                                    "DiagnosticFieldLocation")));

                        Assert.AreEqual(
                            string.Empty,
                            ((TextBlock)window.FindName(
                                "DiagnosticElementSubtitleText"))
                            .Text,
                            "Endpoint ports belong in grouped details, not an unlabeled gray subtitle.");

                        var statusText =
                            ((TextBlock)window.FindName(
                                "InspectorOperationalStatusText"))
                            .Text;
                        StringAssert.Contains(
                            statusText,
                            UiText.Get(
                                    "InspectorLinkAvailabilityForwarding")
                                .Split('{')[0]
                                .Trim(),
                            "Link state must describe forwarding state rather than repeat alert severity.");
                        Assert.IsFalse(
                            statusText.Contains(
                                UiText.Get(
                                    "AlertSeverityCritical")),
                            "Link state line must not repeat problem importance.");

                        var problemText =
                            ((TextBlock)window.FindName(
                                "InspectorProblemText"))
                            .Text;
                        StringAssert.Contains(
                            problemText,
                            UiText.Get(
                                "InspectorProblemForwardingCycleLinkTitle"),
                            "Link loop wording must name the link rather than a generic object.");

                        var explanationText =
                            ((TextBlock)window.FindName(
                                "InspectorProblemExplanationText"))
                            .Text;
                        StringAssert.Contains(
                            explanationText,
                            "Switch A");
                        StringAssert.Contains(
                            explanationText,
                            "Switch B");
                        Assert.IsFalse(
                            problemText.Contains(
                                "Switch A"),
                            "Cycle member list must stay in the neutral explanation line rather than the severity-colored title.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            InspectorLinkStateUsesDisabledStpEndpointInsteadOfProblemSeverity()
        {
            RunOnSta(
                () =>
                {
                    Guid firstId;
                    Guid secondId;
                    Guid firstInterfaceId;
                    Guid secondInterfaceId;
                    Guid linkId;
                    var snapshot =
                        Pass2InspectorSnapshot(
                            out firstId,
                            out secondId,
                            out firstInterfaceId,
                            out secondInterfaceId,
                            out linkId);
                    var original =
                        snapshot.DiagnosticSnapshot.Links[0];
                    var disabledLink =
                        new PhysicalLinkDiagnostic(
                            original.PhysicalLinkId,
                            original.DeviceAId,
                            original.DeviceBId,
                            original.InterfaceAId,
                            original.InterfaceBId,
                            original.DeviceAName,
                            original.DeviceBName,
                            original.InterfaceAName,
                            original.InterfaceBName,
                            original.Strength,
                            original.Freshness,
                            original.MediaType,
                            original.SpeedBps,
                            original.SourceSummary,
                            original.LastSeenUtc,
                            original.LastConfirmedUtc,
                            StpTreePortState.Disabled,
                            StpTreePortState.Forwarding,
                            original.Evidence,
                            original.IsBridge,
                            original.SideADeviceCount,
                            original.SideBDeviceCount,
                            original.SeparatedDevicePairCount);
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                snapshot),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var showLink =
                            typeof(MainWindow).GetMethod(
                                "ShowLinkDiagnostic",
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic);
                        Assert.IsNotNull(
                            showLink);

                        showLink.Invoke(
                            window,
                            new object[]
                            {
                                disabledLink
                            });
                        PumpDispatcher();

                        var state =
                            ((TextBlock)window.FindName(
                                "InspectorOperationalStatusText"))
                            .Text;
                        StringAssert.Contains(
                            state,
                            "Switch A");
                        StringAssert.Contains(
                            state,
                            UiText.Get(
                                    "InspectorLinkAvailabilityStpDisabled")
                                .Split('{')[0]
                                .Trim());
                        Assert.IsFalse(
                            state.Contains(
                                UiText.Get(
                                    "AlertSeverityCritical")),
                            "Disabled STP state must not be replaced by critical alert severity.");

                        StringAssert.Contains(
                            ((TextBlock)window.FindName(
                                "InspectorProblemText"))
                            .Text,
                            UiText.Get(
                                "AlertSeverityCritical"));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            CriticalShellEventIsPresentedBeforeWarning()
        {
            RunOnSta(
                () =>
                {
                    var criticalLink =
                        Guid.Parse(
                            "46464646-6490-6490-6490-464646464646");
                    var warningLink =
                        Guid.Parse(
                            "46464646-6491-6491-6491-464646464646");
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var alertSnapshot =
                            new TopologyAlertSnapshot(
                                Now,
                                "cist",
                                new[]
                                {
                                    new TopologyAlert(
                                        "warning-first",
                                        TopologyAlertKind.RingProtectionDegraded,
                                        TopologyAlertSeverity.Warning,
                                        "cist",
                                        new[] { "region-warning" },
                                        new[] { warningLink },
                                        new[]
                                        {
                                            TopologyAlertReason.DisabledRingLink
                                        }),
                                    new TopologyAlert(
                                        "critical-second",
                                        TopologyAlertKind.ForwardingCycle,
                                        TopologyAlertSeverity.Critical,
                                        "cist",
                                        new string[0],
                                        new[] { criticalLink },
                                        new[]
                                        {
                                            TopologyAlertReason.ConfirmedForwardingCycle
                                        })
                                });

                        var showAlerts =
                            typeof(MainWindow).GetMethod(
                                "ShowAlerts",
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic);
                        Assert.IsNotNull(
                            showAlerts);

                        var transitionType =
                            showAlerts.GetParameters()[1]
                                .ParameterType;
                        showAlerts.Invoke(
                            window,
                            new[]
                            {
                                (object)alertSnapshot,
                                Activator.CreateInstance(
                                    transitionType)
                            });
                        PumpDispatcher();

                        var events =
                            (ItemsControl)window.FindName(
                                "ShellEventList");
                        Assert.AreEqual(
                            2,
                            events.Items.Count);
                        Assert.IsTrue(
                            DiagnosticRowBool(
                                events.Items[0],
                                "IsCritical"),
                            "Critical active event must be presented before warning events regardless of arrival order.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AlertShowOnMapCentersAtNativeZoom()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6400-6400-6400-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6401-6401-6401-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6402-6402-6402-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                ((ItemsControl)window.FindName(
                                    "AlertList"))
                                .Items.Count == 1);

                        Click(
                            (Button)window.FindName(
                                "ShellAlertsButton"));
                        PumpDispatcher();

                        var alertButton =
                            FindVisualDescendantByTag<Button>(
                                (DependencyObject)window.FindName(
                                    "AlertList"),
                                physicalLinkId);

                        Assert.IsNotNull(
                            alertButton);

                        Click(
                            alertButton);
                        PumpDispatcher();
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "ShellAlertsSidebarPanel"))
                            .Visibility,
                            "Show on map from Alerts must keep the Alerts rail section active so the operator can inspect alerts consecutively.");
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "ShellMapSidebarPanel"))
                            .Visibility);

                        Assert.AreEqual(
                            "100%",
                            ((TextBlock)window.FindName(
                                "MapZoomValueText"))
                            .Text,
                            "Alert navigation must center the selected map context at readable native zoom.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AlertsSectionSelectsFirstCardOnEntryAndShowOnMapReselectsIt()
        {
            // A2/A4 (sprint46-mockup-gap): выбранная карточка — рамка цвета выделения.
            // Выбрана — первая при входе в раздел или после «Показать на карте»; выбор другого объекта её снимает.
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6500-6500-6500-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6501-6501-6501-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6502-6502-6502-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        var alertList =
                            (ItemsControl)window.FindName(
                                "AlertList");

                        WaitForCondition(
                            () =>
                                alertList.Items.Count == 1);

                        var selection =
                            window.FindResource(
                                "NetLoom.Brush.Selection");

                        Func<Border> card =
                            () =>
                            {
                                var presenter =
                                    (ContentPresenter)alertList
                                        .ItemContainerGenerator
                                        .ContainerFromIndex(
                                            0);

                                return presenter == null
                                    ? null
                                    : (Border)presenter.ContentTemplate.FindName(
                                        "AlertCard",
                                        presenter);
                            };

                        Click(
                            (Button)window.FindName(
                                "ShellAlertsButton"));
                        PumpDispatcher();
                        PumpDispatcher();

                        Assert.AreSame(
                            selection,
                            card().BorderBrush,
                            "Entering Alerts selects the first card.");

                        Assert.IsFalse(
                            ((FrameworkElement)window.FindName(
                                "ShellInspectorPanel"))
                            .IsVisible,
                            "The automatic selection keeps the Alerts inspector collapsed (ADR-083 п. 4).");

                        SelectDevice(
                            window,
                            firstId);
                        PumpDispatcher();

                        Assert.AreNotSame(
                            selection,
                            card().BorderBrush,
                            "Selecting another object on the map clears the card selection.");

                        Click(
                            FindVisualDescendantByTag<Button>(
                                alertList,
                                physicalLinkId));
                        PumpDispatcher();
                        PumpDispatcher();

                        Assert.AreSame(
                            selection,
                            card().BorderBrush,
                            "Show on map selects its card again.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AutomaticAlertSelectionDoesNotChangeTheMapWorkingViewport()
        {
            // A4: карта у «Карты» и «Предупреждений» общая; автоматический выбор первой карточки
            // Вписывает её участников, но после выхода из раздела рабочий вид карты прежний.
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6600-6600-6600-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6601-6601-6601-464646464646");
                    var physicalLinkId =
                        Guid.Parse(
                            "46464646-6602-6602-6602-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    physicalLinkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                ((ItemsControl)window.FindName(
                                    "AlertList"))
                                .Items.Count == 1);
                        PumpDispatcher();

                        Click(
                            (Button)window.FindName(
                                "MapZoomOutButton"));
                        Click(
                            (Button)window.FindName(
                                "MapZoomOutButton"));
                        PumpDispatcher();

                        var zoomText =
                            (TextBlock)window.FindName(
                                "MapZoomValueText");
                        var scroll =
                            (ScrollViewer)window.FindName(
                                "MapScrollViewer");

                        var zoomBefore =
                            zoomText.Text;
                        var horizontalBefore =
                            scroll.HorizontalOffset;
                        var verticalBefore =
                            scroll.VerticalOffset;

                        Click(
                            (Button)window.FindName(
                                "ShellAlertsButton"));
                        PumpDispatcher();
                        PumpDispatcher();

                        Assert.AreNotEqual(
                            zoomBefore,
                            zoomText.Text,
                            "Entering Alerts fits the first card participants (test precondition).");

                        Click(
                            (Button)window.FindName(
                                "ShellMapButton"));
                        PumpDispatcher();
                        PumpDispatcher();

                        Assert.AreEqual(
                            zoomBefore,
                            zoomText.Text,
                            "Leaving Alerts restores the map zoom changed by the automatic selection.");
                        Assert.AreEqual(
                            horizontalBefore,
                            scroll.HorizontalOffset,
                            0.5);
                        Assert.AreEqual(
                            verticalBefore,
                            scroll.VerticalOffset,
                            0.5);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AlertShowOnMapFitsEveryDeviceInForwardingCycle()
        {
            RunOnSta(
                () =>
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                ForwardingCycleIncidentSnapshot()),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                ((ItemsControl)window.FindName(
                                    "AlertList"))
                                .Items.Count == 1);

                        Click(
                            (Button)window.FindName(
                                "ShellAlertsButton"));
                        PumpDispatcher();

                        var alertButton =
                            FindVisualDescendant<Button>(
                                (DependencyObject)window.FindName(
                                    "AlertList"));

                        Assert.IsNotNull(
                            alertButton);

                        Click(
                            alertButton);
                        PumpDispatcher();
                        PumpDispatcher();

                        var viewport =
                            (ScrollViewer)window.FindName(
                                "MapScrollViewer");
                        var canvas =
                            (Canvas)window.FindName(
                                "MapCanvas");
                        var focusHalos =
                            canvas.Children
                                .OfType<System.Windows.Shapes.Rectangle>()
                                .Where(
                                    item =>
                                        string.Equals(
                                            item.Tag as string,
                                            "NodeFocusHalo",
                                            StringComparison.Ordinal))
                                .ToArray();

                        Assert.IsTrue(
                            focusHalos.Length >= 3,
                            "Alert focus must provide a separate halo layer for every endpoint device instead of changing card opacity.");
                        WaitForCondition(
                            () =>
                                focusHalos.Any(
                                    item =>
                                        item.Visibility ==
                                        Visibility.Visible));

                        foreach (var deviceId in
                            new[]
                            {
                                Guid.Parse(
                                    "46464646-6700-6700-6700-464646464646"),
                                Guid.Parse(
                                    "46464646-6701-6701-6701-464646464646"),
                                Guid.Parse(
                                    "46464646-6702-6702-6702-464646464646")
                            })
                        {
                            var border =
                                DeviceBorder(
                                    window,
                                    deviceId);

                            Assert.IsNotNull(
                                border);
                            Assert.AreEqual(
                                1.0,
                                border.Opacity,
                                0.001,
                                "Alert focus must keep device cards fully opaque so link lines cannot bleed through them.");
                            Assert.IsTrue(
                                Panel.GetZIndex(
                                    border) >
                                focusHalos
                                    .Select(
                                        item =>
                                            Panel.GetZIndex(
                                                item))
                                    .Max(),
                                "Map layers must keep cards above their focus halos.");

                            var center =
                                border.TranslatePoint(
                                    new Point(
                                        border.ActualWidth / 2.0,
                                        border.ActualHeight / 2.0),
                                    viewport);

                            Assert.IsTrue(
                                center.X >= 0.0 &&
                                center.X <= viewport.ViewportWidth &&
                                center.Y >= 0.0 &&
                                center.Y <= viewport.ViewportHeight,
                                "Show on map must fit every device touched by the forwarding-cycle incident, not only the primary link.");
                        }
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            OperatorPanelsUseCardLayoutAndCollapsedPollingParameters()
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

                        Assert.IsNotNull(
                            window.FindName(
                                "MonitoringStatusCard"));
                        Assert.IsNotNull(
                            window.FindName(
                                "DiscoveryProfileCard"));

                        Assert.AreEqual(
                            UiText.Get(
                                "OperatorStatusGlyphStopped"),
                            ((TextBlock)window.FindName(
                                "MonitoringStateGlyphText"))
                            .Text);

                        Assert.AreEqual(
                            UiText.Get(
                                "OperatorStatusGlyphIdle"),
                            ((TextBlock)window.FindName(
                                "DiscoveryStateGlyphText"))
                            .Text,
                            "Discovery before its first run is neutral waiting state, not a successful completed state.");

                        Assert.IsFalse(
                            ((Expander)window.FindName(
                                "MonitoringExpander"))
                            .IsExpanded,
                            "Detailed polling parameters must stay collapsed during normal operation.");

                        var sidebarWidth =
                            (GridLength)window.FindResource(
                                "NetLoom.Shell.SidebarWidth");

                        Assert.AreEqual(
                            0.0,
                            sidebarWidth.Value,
                            0.01,
                            "ADR-083 removes the permanent section sidebar from the shell frame.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            Adr083FrameGivesMapSpaceAndUsesSectionInspectorDefaults()
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
                        window.Width =
                            1366;
                        window.Height =
                            820;
                        window.Show();
                        PumpDispatcher();

                        var workspace =
                            (Grid)window.FindName(
                                "ShellWorkspaceGrid");
                        var map =
                            (Grid)window.FindName(
                                "ShellMapSurface");
                        var inspector =
                            (Border)window.FindName(
                                "ShellInspectorPanel");
                        var editor =
                            (ContentControl)window.FindName(
                                "ShellWorkspaceEditorHost");

                        Assert.IsTrue(
                            map.ActualWidth + 0.5 >=
                            workspace.ActualWidth * 0.60,
                            "ADR-083 requires the map to keep at least 60% of the available window width at 1366 px.");
                        Assert.AreEqual(
                            Visibility.Visible,
                            inspector.Visibility);
                        Assert.AreEqual(
                            1,
                            Grid.GetColumn(
                                editor));
                        Assert.AreEqual(
                            2,
                            Grid.GetColumnSpan(
                                editor),
                            "Sprint 46 editors must occupy the center view and must not cover the inspector column.");

                        Click(
                            (Button)window.FindName(
                                "ShellAlertsButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((Border)window.FindName(
                                "ShellSectionSurface"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            inspector.Visibility,
                            "Warnings open with the inspector collapsed by ADR-083.");

                        Click(
                            (Button)window.FindName(
                                "ShellEquipmentButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Visible,
                            inspector.Visibility,
                            "Equipment opens with the shared inspector visible by ADR-083.");

                        Click(
                            (Button)window.FindName(
                                "ShellInspectorCollapseButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            inspector.Visibility);
                        Assert.AreEqual(
                            Visibility.Visible,
                            ((Button)window.FindName(
                                "ShellInspectorRevealButton"))
                            .Visibility);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            Adr083CentralViewsUseFullCenterAndAlertsShareMap()
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
                        window.Width =
                            1366;
                        window.Height =
                            820;
                        window.Show();
                        PumpDispatcher();

                        var surface =
                            (Border)window.FindName(
                                "ShellSectionSurface");
                        var map =
                            (Grid)window.FindName(
                                "ShellMapSurface");
                        var inspector =
                            (Border)window.FindName(
                                "ShellInspectorPanel");
                        var reveal =
                            (Button)window.FindName(
                                "ShellInspectorRevealButton");

                        Click(
                            (Button)window.FindName(
                                "ShellEquipmentButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "ShellEquipmentSidebarPanel"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            map.Visibility,
                            "Equipment is a center view, not a narrow panel beside the map.");
                        Assert.AreEqual(
                            Visibility.Visible,
                            inspector.Visibility);
                        Assert.IsNotNull(
                            window.FindName(
                                "EquipmentFilterTextBox"));
                        Assert.IsNotNull(
                            window.FindName(
                                "EquipmentExportCsvButton"));

                        Click(
                            (Button)window.FindName(
                                "ShellAlertsButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Visible,
                            map.Visibility,
                            "Alerts keep the map as the right-hand context surface.");
                        Assert.AreEqual(
                            HorizontalAlignment.Left,
                            surface.HorizontalAlignment);
                        Assert.IsTrue(
                            surface.Width >= 380.0 &&
                            surface.Width <= 460.0,
                            "ADR-083 keeps the warning-card column near 420 px without turning an approximate mock dimension into an exact contract.");
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "Adr083MapPrimaryToolbar"))
                            .Visibility,
                            "The alerts map keeps navigation controls but not the full map action toolbar.");
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            inspector.Visibility);
                        Assert.AreEqual(
                            Visibility.Visible,
                            reveal.Visibility);

                        Click(
                            (Button)window.FindName(
                                "ShellDiscoveryButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            HorizontalAlignment.Stretch,
                            surface.HorizontalAlignment);
                        Assert.IsTrue(
                            double.IsNaN(
                                surface.Width));
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            map.Visibility);
                        Assert.AreEqual(
                            Visibility.Visible,
                            ((FrameworkElement)window.FindName(
                                "DiscoveryResultsEmptyCard"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "DiscoveryResultsPanel"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            inspector.Visibility);

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            inspector.Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            reveal.Visibility,
                            "Settings hide the inspector instead of merely collapsing it.");

                        var settingsContent =
                            (FrameworkElement)window.FindName(
                                "ShellSettingsContent");

                        Assert.IsTrue(
                            settingsContent.MaxWidth <=
                            720.0 + 0.5,
                            "ADR-083 keeps the settings content column at 720 px or less.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            Adr083GlobalSearchGroupsDevicesAndPortsFromSavedSnapshot()
        {
            RunOnSta(
                () =>
                {
                    var siteId =
                        Guid.Parse(
                            "46464646-6900-6900-6900-464646464646");
                    var rackId =
                        Guid.Parse(
                            "46464646-6901-6901-6901-464646464646");
                    var deviceId =
                        Guid.Parse(
                            "46464646-6902-6902-6902-464646464646");
                    var interfaceId =
                        Guid.Parse(
                            "46464646-6903-6903-6903-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                InspectorSnapshot(
                                    siteId,
                                    rackId,
                                    deviceId,
                                    interfaceId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) != null);

                        var search =
                            (TextBox)window.FindName(
                                "ShellGlobalSearchTextBox");
                        var results =
                            (ListBox)window.FindName(
                                "ShellGlobalSearchResultsList");

                        search.Text =
                            "Switch A";
                        PumpDispatcher();

                        Assert.IsTrue(
                            results.Items.Count > 0);
                        Assert.IsTrue(
                            results.Items.Groups
                                .OfType<CollectionViewGroup>()
                                .Any(
                                    group =>
                                        string.Equals(
                                            group.Name as string,
                                            UiText.Get(
                                                "ShellGlobalSearchGroupDevices"),
                                            StringComparison.Ordinal)),
                            "ADR-083 global search must group saved devices instead of exposing the legacy flat lookup list.");

                        search.Text =
                            "Gi0/1";
                        PumpDispatcher();

                        Assert.IsTrue(
                            results.Items.Groups
                                .OfType<CollectionViewGroup>()
                                .Any(
                                    group =>
                                        string.Equals(
                                            group.Name as string,
                                            UiText.Get(
                                                "ShellGlobalSearchGroupPorts"),
                                            StringComparison.Ordinal)),
                            "ADR-083 global search must expose saved port identity without adding new collection or storage.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            Adr083NormalMapStateIsNeutralAndMonitoringStopUsesOneMapNotice()
        {
            RunOnSta(
                () =>
                {
                    var siteId =
                        Guid.Parse(
                            "46464646-6910-6910-6910-464646464646");
                    var rackId =
                        Guid.Parse(
                            "46464646-6911-6911-6911-464646464646");
                    var deviceId =
                        Guid.Parse(
                            "46464646-6912-6912-6912-464646464646");
                    var interfaceId =
                        Guid.Parse(
                            "46464646-6913-6913-6913-464646464646");

                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                InspectorSnapshot(
                                    siteId,
                                    rackId,
                                    deviceId,
                                    interfaceId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();

                        WaitForCondition(
                            () =>
                                DeviceBorder(
                                    window,
                                    deviceId) != null);

                        var device =
                            DeviceBorder(
                                window,
                                deviceId);
                        var stripe =
                            FindVisualDescendantByTag<Border>(
                                device,
                                "NodeStateStripe");
                        var statusIcon =
                            FindVisualDescendantByTag<System.Windows.Shapes.Path>(
                                device,
                                "NodeStatusIcon");

                        Assert.IsNotNull(stripe);
                        Assert.IsNotNull(statusIcon);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.BorderStrong"),
                            stripe.Background,
                            "Normal device state must use the neutral stripe from ADR-083.");
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            statusIcon.Visibility,
                            "Normal device state must not carry a status icon; color and icon are reserved for deviations.");

                        var notice =
                            (FrameworkElement)window.FindName(
                                "Adr083MapMonitoringNotice");
                        var noticeText =
                            (TextBlock)window.FindName(
                                "Adr083MapMonitoringNoticeText");

                        Assert.AreEqual(
                            Visibility.Visible,
                            notice.Visibility);
                        Assert.AreEqual(
                            UiText.Get(
                                "ShellMapMonitoringStoppedNotice"),
                            noticeText.Text,
                            "Stopped monitoring must be explained once on the map instead of coloring every no-data card.");
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

                        var themeButton =
                            (Button)window.FindName(
                                "ShellThemeButton");

                        Assert.AreEqual(
                            UiShellTheme.Dark,
                            themeButton.Tag,
                            "In light theme the button must advertise the action that switches to dark theme.");
                        Assert.AreEqual(
                            UiText.Get(
                                "ShellThemeDark"),
                            themeButton.Content);

                        var before =
                            ((SolidColorBrush)window.Background)
                                .Color;

                        Click(
                            themeButton);

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
                            UiShellTheme.Light,
                            themeButton.Tag,
                            "In dark theme the button must advertise the action that switches to light theme.");
                        Assert.AreEqual(
                            UiText.Get(
                                "ShellThemeLight"),
                            themeButton.Content);

                        var overviewTab =
                            (TabItem)window.FindName(
                                "InspectorOverviewTab");

                        overviewTab.ApplyTemplate();
                        PumpDispatcher();

                        var tabChrome =
                            FindVisualDescendant<Border>(
                                overviewTab);

                        Assert.IsNotNull(
                            tabChrome,
                            "Inspector tabs must use the NetLoom semantic template instead of system theme chrome.");

                        // I1: выбранная вкладка — выбранный сегмент группы, как переключатели G6 (AccentSoft).
                        var expectedDarkSelectedTab =
                            (SolidColorBrush)window.FindResource(
                                "NetLoom.Brush.AccentSoft");

                        Assert.AreEqual(
                            expectedDarkSelectedTab.Color,
                            ((SolidColorBrush)tabChrome.Background)
                                .Color,
                            "The selected inspector tab must stay on the dark semantic palette instead of turning into a white system tab.");

                        Assert.AreEqual(
                            themeButton.Content,
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

        // Элемент не обрезан, если WPF не наложил на него обрезку раскладки меньше его размера.
        private static void AssertNotLayoutClipped(
            FrameworkElement element,
            string message)
        {
            var clip =
                System.Windows.Controls.Primitives.LayoutInformation
                    .GetLayoutClip(
                        element);

            if (clip == null)
            {
                return;
            }

            Assert.IsTrue(
                clip.Bounds.Width + 0.5 >=
                    element.RenderSize.Width &&
                clip.Bounds.Height + 0.5 >=
                    element.RenderSize.Height,
                message +
                " Layout clip " +
                clip.Bounds +
                ", element " +
                element.RenderSize +
                ".");
        }

        [TestMethod]
        public void
            RailUsesAdr083IconOnlyGeometryAndAccessibleNames()
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

                        Assert.AreEqual(
                            56.0,
                            ((GridLength)window.FindResource(
                                "NetLoom.Shell.RailWidth"))
                            .Value,
                            0.01);

                        foreach (var name in
                            new[]
                            {
                                "ShellMapButton",
                                "ShellEquipmentButton",
                                "ShellAlertsButton",
                                "ShellDiscoveryButton",
                                "ShellSettingsButton"
                            })
                        {
                            var button =
                                (Button)window.FindName(
                                    name);

                            Assert.AreEqual(
                                44.0,
                                button.Width,
                                0.01);
                            Assert.AreEqual(
                                44.0,
                                button.Height,
                                0.01);
                            Assert.IsFalse(
                                string.IsNullOrWhiteSpace(
                                    System.Windows.Automation.AutomationProperties
                                        .GetName(
                                            button)),
                                name +
                                " must expose an AutomationProperties.Name in the icon-only rail.");

                            // Значок рейла должен помещаться в кнопку целиком (UI_DESIGN_RULES §5, §10).
                            var icon =
                                FindVisualDescendant<System.Windows.Shapes.Path>(
                                    button);

                            Assert.IsNotNull(
                                icon,
                                name +
                                " must render its rail icon.");
                            AssertNotLayoutClipped(
                                icon,
                                name +
                                " rail icon must not be clipped by the button.");
                        }

                        foreach (var name in
                            new[]
                            {
                                "ShellInspectorCollapseButton"
                            })
                        {
                            var button =
                                (Button)window.FindName(
                                    name);

                            button.ApplyTemplate();
                            button.UpdateLayout();

                            var glyph =
                                FindVisualDescendant<TextBlock>(
                                    button);

                            Assert.IsNotNull(
                                glyph,
                                name +
                                " must render its glyph.");
                            AssertNotLayoutClipped(
                                glyph,
                                name +
                                " glyph must not be clipped by the button padding.");
                        }

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((TextBlock)window.FindName(
                                "ShellAlertsButtonText"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((Button)window.FindName(
                                "ShellMonitoringButton"))
                            .Visibility);
                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((Button)window.FindName(
                                "ShellSearchButton"))
                            .Visibility);

                        Assert.IsNotNull(
                            window.FindResource(
                                "NetLoom.Icon.ShellMap"));
                        Assert.IsNotNull(
                            window.FindResource(
                                "NetLoom.Icon.ShellAlerts"));

                        Assert.AreEqual(
                            "−",
                            UiText.Get(
                                "MapZoomOutAction"));
                        Assert.AreEqual(
                            0.75,
                            (double)window.FindResource(
                                "NetLoom.Map.ReadableZoomMin"),
                            0.001);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AlertBadgeUsesCircularSingleDigitGeometryAndCriticalSeverity()
        {
            RunOnSta(
                () =>
                {
                    var firstId =
                        Guid.Parse(
                            "46464646-6800-6800-6800-464646464646");
                    var secondId =
                        Guid.Parse(
                            "46464646-6801-6801-6801-464646464646");
                    var linkId =
                        Guid.Parse(
                            "46464646-6802-6802-6802-464646464646");
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                CriticalLinkSnapshot(
                                    firstId,
                                    secondId,
                                    linkId)),
                            new EmptyLookupReader());

                    try
                    {
                        window.Show();
                        PumpDispatcher();

                        var badge =
                            (Border)window.FindName(
                                "ShellAlertsBadge");
                        var text =
                            (TextBlock)window.FindName(
                                "ShellAlertsBadgeText");
                        var expectedSize =
                            Convert.ToDouble(
                                window.FindResource(
                                    "NetLoom.Navigation.BadgeHeight"));

                        Assert.AreEqual(
                            Visibility.Visible,
                            badge.Visibility);
                        Assert.AreEqual(
                            expectedSize,
                            badge.Width,
                            0.001,
                            "A one-digit navigation badge must be a circle, not an ellipse.");
                        Assert.AreEqual(
                            expectedSize,
                            badge.Height,
                            0.001);
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Critical"),
                            badge.Background,
                            "The alert badge color must follow the most serious current alert.");
                        Assert.AreEqual(
                            "1",
                            text.Text);
                        Assert.AreEqual(
                            LineStackingStrategy.BlockLineHeight,
                            text.LineStackingStrategy);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            AlertBadgeUsesWarningPillAndOverflowPresentation()
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

                        var method =
                            typeof(MainWindow).GetMethod(
                                "ConfigureShellAlertBadge",
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic);

                        Assert.IsNotNull(
                            method);

                        method.Invoke(
                            window,
                            new object[]
                            {
                                12,
                                "NetLoom.Brush.Warning"
                            });
                        PumpDispatcher();

                        var badge =
                            (Border)window.FindName(
                                "ShellAlertsBadge");
                        var text =
                            (TextBlock)window.FindName(
                                "ShellAlertsBadgeText");

                        Assert.IsTrue(
                            double.IsNaN(
                                badge.Width),
                            "Two or more digits must use auto-width pill geometry.");
                        Assert.IsTrue(
                            badge.ActualWidth >
                            badge.ActualHeight,
                            "A multi-digit alert badge must be a pill, not an ellipse with arbitrary proportions.");
                        Assert.AreSame(
                            window.FindResource(
                                "NetLoom.Brush.Warning"),
                            badge.Background,
                            "Warnings-only badge presentation must use the warning semantic color.");
                        Assert.AreEqual(
                            "12",
                            text.Text);

                        method.Invoke(
                            window,
                            new object[]
                            {
                                100,
                                "NetLoom.Brush.Warning"
                            });
                        PumpDispatcher();

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellAlertBadgeOverflow"),
                            text.Text,
                            "Alert counts above 99 must use the compact 99+ representation.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            Adr083SettingsPageSavesPollingAndDirectMapPreferences()
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

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));
                        PumpDispatcher();

                        var interval =
                            (TextBox)window.FindName(
                                "SettingsMonitoringIntervalTextBox");
                        var timeout =
                            (TextBox)window.FindName(
                                "SettingsMonitoringTimeoutTextBox");
                        var retries =
                            (TextBox)window.FindName(
                                "SettingsMonitoringRetriesTextBox");
                        var maxRepetitions =
                            (TextBox)window.FindName(
                                "SettingsMonitoringMaxRepetitionsTextBox");

                        interval.Text = "75";
                        timeout.Text = "1750";
                        retries.Text = "2";
                        maxRepetitions.Text = "30";

                        Click(
                            (Button)window.FindName(
                                "SettingsMonitoringSaveButton"));
                        PumpDispatcher();

                        Assert.IsNotNull(
                            stateStore.LastSaved);
                        Assert.AreEqual(
                            75,
                            stateStore.LastSaved
                                .PollingSettings
                                .IntervalSeconds);
                        Assert.AreEqual(
                            1750,
                            stateStore.LastSaved
                                .PollingSettings
                                .TimeoutMilliseconds);
                        Assert.AreEqual(
                            2,
                            stateStore.LastSaved
                                .PollingSettings
                                .RetryCount);
                        Assert.AreEqual(
                            30,
                            stateStore.LastSaved
                                .PollingSettings
                                .MaxRepetitions);
                        Assert.AreEqual(
                            UiText.Get(
                                "SettingsPollingSaved"),
                            ((TextBlock)window.FindName(
                                "SettingsMonitoringStatusText"))
                            .Text);

                        Click(
                            (Button)window.FindName(
                                "SettingsMotionOffButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            MapMotionMode.Off,
                            stateStore.LastSaved.MotionMode);

                        Click(
                            (Button)window.FindName(
                                "SettingsThemeDarkButton"));
                        PumpDispatcher();

                        Assert.AreEqual(
                            UiShellTheme.Dark,
                            stateStore.LastSaved.Theme);

                        var lightButton =
                            (Button)window.FindName(
                                "SettingsThemeLightButton");
                        var darkButton =
                            (Button)window.FindName(
                                "SettingsThemeDarkButton");

                        Assert.AreNotEqual(
                            lightButton.Background,
                            darkButton.Background,
                            "The settings page must show which theme is selected instead of only exposing a toggle action.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            FileShellStateStoreRoundTripsProfileThemePollingAndMotion()
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
                        UiShellTheme.Dark,
                        new UiPollingSettings(
                            90,
                            1500,
                            2,
                            33,
                            true,
                            false,
                            true,
                            false,
                            true,
                            true,
                            false),
                        MapMotionMode.Reduced));

                var restored =
                    store.Load();

                Assert.AreEqual(
                    profileId,
                    restored.AccessProfileId);
                Assert.AreEqual(
                    UiShellTheme.Dark,
                    restored.Theme);
                Assert.AreEqual(
                    MapMotionMode.Reduced,
                    restored.MotionMode);
                Assert.AreEqual(
                    90,
                    restored.PollingSettings.IntervalSeconds);
                Assert.AreEqual(
                    1500,
                    restored.PollingSettings.TimeoutMilliseconds);
                Assert.AreEqual(
                    2,
                    restored.PollingSettings.RetryCount);
                Assert.AreEqual(
                    33,
                    restored.PollingSettings.MaxRepetitions);
                Assert.IsTrue(
                    restored.PollingSettings.Lldp);
                Assert.IsFalse(
                    restored.PollingSettings.Cdp);
                Assert.IsTrue(
                    restored.PollingSettings.Fdb);
                Assert.IsFalse(
                    restored.PollingSettings.Arp);
                Assert.IsTrue(
                    restored.PollingSettings.Health);
                Assert.IsTrue(
                    restored.PollingSettings.Interfaces);
                Assert.IsFalse(
                    restored.PollingSettings.Stp);

                var persisted =
                    File.ReadAllText(
                        path);

                StringAssert.Contains(
                    persisted,
                    profileId.ToString("D"));
                StringAssert.Contains(
                    persisted,
                    "theme=Dark");
                StringAssert.Contains(
                    persisted,
                    "motion=Reduced");
                StringAssert.Contains(
                    persisted,
                    "pollIntervalSeconds=90");
                StringAssert.Contains(
                    persisted,
                    "pollTimeoutMilliseconds=1500");
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

        private static TopologyRefreshSnapshot
            WideSnapshot(
                Guid firstDeviceId,
                Guid secondDeviceId)
        {
            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    Now,
                    new[]
                    {
                        new MapNode(
                            firstDeviceId.ToString("D"),
                            "Wide A",
                            null,
                            100.0,
                            100.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            firstDeviceId,
                            "192.0.2.171"),
                        new MapNode(
                            secondDeviceId.ToString("D"),
                            "Wide B",
                            null,
                            4200.0,
                            100.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            secondDeviceId,
                            "192.0.2.172")
                    },
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

        private static TopologyRefreshSnapshot
            InspectorSnapshot(
                Guid siteId,
                Guid rackId,
                Guid deviceId,
                Guid interfaceId)
        {
            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    Now,
                    new[]
                    {
                        new MapNode(
                            deviceId.ToString("D"),
                            "Switch A",
                            "192.0.2.146",
                            100.0,
                            100.0,
                            rackId,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            deviceId,
                            "192.0.2.146")
                    },
                    new MapLink[0],
                    new[]
                    {
                        new MapLocation(
                            siteId,
                            null,
                            "Site A",
                            null),
                        new MapLocation(
                            rackId,
                            siteId,
                            "Rack A",
                            null)
                    }),
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
                            "Switch A",
                            "192.0.2.146",
                            "Rack A",
                            Now,
                            Now,
                            new[]
                            {
                                new InterfaceDiagnostic(
                                    interfaceId,
                                    deviceId,
                                    1,
                                    "Gi0/1",
                                    "00:11:22:33:44:55",
                                    "up",
                                    "up",
                                    1000000000L,
                                    Now,
                                    NetLoom.Contracts.StpTree
                                        .StpTreePortState
                                        .Forwarding,
                                    DiagnosticDegradationStatus
                                        .Healthy,
                                    Now,
                                    new DiagnosticDegradationReason[0],
                                    "Gi0/1",
                                    "Uplink",
                                    6,
                                    "GigabitEthernet0/1")
                            },
                            "192.0.2.146")
                    },
                    new PhysicalLinkDiagnostic[0]));
        }

        private static TopologyRefreshSnapshot
            ForwardingCycleIncidentSnapshot()
        {
            var firstId =
                Guid.Parse(
                    "46464646-6700-6700-6700-464646464646");
            var secondId =
                Guid.Parse(
                    "46464646-6701-6701-6701-464646464646");
            var thirdId =
                Guid.Parse(
                    "46464646-6702-6702-6702-464646464646");
            var firstLinkId =
                Guid.Parse(
                    "46464646-6710-6710-6710-464646464646");
            var secondLinkId =
                Guid.Parse(
                    "46464646-6711-6711-6711-464646464646");
            var thirdLinkId =
                Guid.Parse(
                    "46464646-6712-6712-6712-464646464646");

            var firstKey = firstId.ToString("D");
            var secondKey = secondId.ToString("D");
            var thirdKey = thirdId.ToString("D");

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    Now,
                    new[]
                    {
                        new MapNode(
                            firstKey,
                            "Cycle A",
                            null,
                            100.0,
                            80.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            firstId,
                            "192.0.2.170"),
                        new MapNode(
                            secondKey,
                            "Cycle B",
                            null,
                            1800.0,
                            120.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            secondId,
                            "192.0.2.171"),
                        new MapNode(
                            thirdKey,
                            "Cycle C",
                            null,
                            950.0,
                            1200.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            thirdId,
                            "192.0.2.172")
                    },
                    new[]
                    {
                        new MapLink(
                            firstLinkId.ToString("D"),
                            firstKey,
                            secondKey,
                            null,
                            null,
                            MapConfidence.High,
                            MapFreshness.Fresh,
                            new MapEvidenceItem[0],
                            firstLinkId),
                        new MapLink(
                            secondLinkId.ToString("D"),
                            secondKey,
                            thirdKey,
                            null,
                            null,
                            MapConfidence.High,
                            MapFreshness.Fresh,
                            new MapEvidenceItem[0],
                            secondLinkId),
                        new MapLink(
                            thirdLinkId.ToString("D"),
                            thirdKey,
                            firstKey,
                            null,
                            null,
                            MapConfidence.High,
                            MapFreshness.Fresh,
                            new MapEvidenceItem[0],
                            thirdLinkId)
                    }),
                new TopologyAlertSnapshot(
                    Now,
                    "cist",
                    new[]
                    {
                        new TopologyAlert(
                            "forwarding-cycle",
                            TopologyAlertKind.ForwardingCycle,
                            TopologyAlertSeverity.Critical,
                            "cist",
                            new string[0],
                            new[]
                            {
                                firstLinkId,
                                secondLinkId,
                                thirdLinkId
                            },
                            new[]
                            {
                                TopologyAlertReason
                                    .ConfirmedForwardingCycle
                            })
                    }),
                new NetworkDiagnosticSnapshot(
                    Now,
                    new[]
                    {
                        new DeviceDiagnostic(
                            firstId,
                            "Cycle A",
                            null,
                            null,
                            Now,
                            Now,
                            new InterfaceDiagnostic[0],
                            "192.0.2.170"),
                        new DeviceDiagnostic(
                            secondId,
                            "Cycle B",
                            null,
                            null,
                            Now,
                            Now,
                            new InterfaceDiagnostic[0],
                            "192.0.2.171"),
                        new DeviceDiagnostic(
                            thirdId,
                            "Cycle C",
                            null,
                            null,
                            Now,
                            Now,
                            new InterfaceDiagnostic[0],
                            "192.0.2.172")
                    },
                    new PhysicalLinkDiagnostic[0]));
        }

        private static TopologyRefreshSnapshot
            Pass2InspectorSnapshot(
                out Guid firstId,
                out Guid secondId,
                out Guid firstInterfaceId,
                out Guid secondInterfaceId,
                out Guid physicalLinkId)
        {
            firstId =
                Guid.Parse(
                    "46464646-6480-6480-6480-464646464646");
            secondId =
                Guid.Parse(
                    "46464646-6481-6481-6481-464646464646");
            firstInterfaceId =
                Guid.Parse(
                    "46464646-6482-6482-6482-464646464646");
            secondInterfaceId =
                Guid.Parse(
                    "46464646-6483-6483-6483-464646464646");
            physicalLinkId =
                Guid.Parse(
                    "46464646-6484-6484-6484-464646464646");

            var siteId =
                Guid.Parse(
                    "46464646-6485-6485-6485-464646464646");
            var rackId =
                Guid.Parse(
                    "46464646-6486-6486-6486-464646464646");
            var observedUtc =
                DateTime.UtcNow.AddDays(
                    -12);
            var firstKey =
                firstId.ToString(
                    "D");
            var secondKey =
                secondId.ToString(
                    "D");

            var firstInterface =
                new InterfaceDiagnostic(
                    firstInterfaceId,
                    firstId,
                    1,
                    "Gi0/1",
                    "00:11:22:33:44:55",
                    "up",
                    "up",
                    1000000000L,
                    observedUtc,
                    NetLoom.Contracts.StpTree
                        .StpTreePortState
                        .Forwarding,
                    DiagnosticDegradationStatus.Healthy,
                    observedUtc,
                    new DiagnosticDegradationReason[0],
                    "Gi0/1",
                    "Uplink",
                    6,
                    "GigabitEthernet0/1");
            var secondInterface =
                new InterfaceDiagnostic(
                    secondInterfaceId,
                    secondId,
                    2,
                    "Gi0/2",
                    "00:11:22:33:44:66",
                    "up",
                    "up",
                    1000000000L,
                    observedUtc,
                    NetLoom.Contracts.StpTree
                        .StpTreePortState
                        .Forwarding,
                    DiagnosticDegradationStatus.Healthy,
                    observedUtc,
                    new DiagnosticDegradationReason[0],
                    "Gi0/2",
                    "Downlink",
                    6,
                    "GigabitEthernet0/2");

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    DateTime.UtcNow,
                    new[]
                    {
                        new MapNode(
                            firstKey,
                            "Switch A",
                            "NetLoom Sprint39 Acceptance",
                            100.0,
                            100.0,
                            rackId,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            firstId,
                            "192.0.2.180"),
                        new MapNode(
                            secondKey,
                            "Switch B",
                            null,
                            420.0,
                            100.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            secondId,
                            "192.0.2.181")
                    },
                    new[]
                    {
                        new MapLink(
                            physicalLinkId.ToString(
                                "D"),
                            firstKey,
                            secondKey,
                            null,
                            null,
                            MapConfidence.High,
                            MapFreshness.Fresh,
                            new MapEvidenceItem[0],
                            physicalLinkId)
                    },
                    new[]
                    {
                        new MapLocation(
                            siteId,
                            null,
                            "Site A",
                            null),
                        new MapLocation(
                            rackId,
                            siteId,
                            "Rack A",
                            null)
                    }),
                new TopologyAlertSnapshot(
                    DateTime.UtcNow,
                    "cist",
                    new[]
                    {
                        new TopologyAlert(
                            "pass2-critical-link",
                            TopologyAlertKind.ForwardingCycle,
                            TopologyAlertSeverity.Critical,
                            "cist",
                            new string[0],
                            new[]
                            {
                                physicalLinkId
                            },
                            new[]
                            {
                                TopologyAlertReason
                                    .ConfirmedForwardingCycle
                            })
                    }),
                new NetworkDiagnosticSnapshot(
                    DateTime.UtcNow,
                    new[]
                    {
                        new DeviceDiagnostic(
                            firstId,
                            "Switch A",
                            "NetLoom Sprint39 Acceptance",
                            "Rack A",
                            observedUtc,
                            observedUtc,
                            new[]
                            {
                                firstInterface
                            },
                            "192.0.2.180"),
                        new DeviceDiagnostic(
                            secondId,
                            "Switch B",
                            null,
                            null,
                            observedUtc,
                            observedUtc,
                            new[]
                            {
                                secondInterface
                            },
                            "192.0.2.181")
                    },
                    new[]
                    {
                        new PhysicalLinkDiagnostic(
                            physicalLinkId,
                            firstId,
                            secondId,
                            firstInterfaceId,
                            secondInterfaceId,
                            "Switch A",
                            "Switch B",
                            "Gi0/1",
                            "Gi0/2",
                            DiagnosticLinkStrength.Confirmed,
                            MapFreshness.Fresh,
                            "Ethernet",
                            1000000000L,
                            "LLDP",
                            observedUtc,
                            observedUtc,
                            NetLoom.Contracts.StpTree
                                .StpTreePortState
                                .Forwarding,
                            NetLoom.Contracts.StpTree
                                .StpTreePortState
                                .Forwarding,
                            new DiagnosticEvidenceItem[0],
                            false,
                            0,
                            0,
                            0L)
                    }));
        }

        private static TopologyRefreshSnapshot
            CriticalLinkSnapshot(
                Guid firstId,
                Guid secondId,
                Guid physicalLinkId)
        {
            var firstKey =
                firstId.ToString(
                    "D");
            var secondKey =
                secondId.ToString(
                    "D");

            return new TopologyRefreshSnapshot(
                new MapSnapshot(
                    Now,
                    new[]
                    {
                        new MapNode(
                            firstKey,
                            "Switch critical A",
                            null,
                            100.0,
                            100.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            firstId,
                            "192.0.2.162"),
                        new MapNode(
                            secondKey,
                            "Switch critical B",
                            null,
                            420.0,
                            100.0,
                            null,
                            MapNodeOrigin.Automatic,
                            MapMonitoringCapability.Unknown,
                            MapNodeCategory.Unknown,
                            secondId,
                            "192.0.2.163")
                    },
                    new[]
                    {
                        new MapLink(
                            physicalLinkId.ToString(
                                "D"),
                            firstKey,
                            secondKey,
                            null,
                            null,
                            MapConfidence.High,
                            MapFreshness.Fresh,
                            new MapEvidenceItem[0],
                            physicalLinkId)
                    }),
                new TopologyAlertSnapshot(
                    Now,
                    "cist",
                    new[]
                    {
                        new TopologyAlert(
                            "critical-link",
                            TopologyAlertKind.ForwardingCycle,
                            TopologyAlertSeverity.Critical,
                            "cist",
                            new string[0],
                            new[]
                            {
                                physicalLinkId
                            },
                            new[]
                            {
                                TopologyAlertReason
                                    .ConfirmedForwardingCycle
                            })
                    }),
                new NetworkDiagnosticSnapshot(
                    Now,
                    new[]
                    {
                        new DeviceDiagnostic(
                            firstId,
                            "Switch critical A",
                            null,
                            null,
                            Now,
                            Now,
                            new InterfaceDiagnostic[0],
                            "192.0.2.162"),
                        new DeviceDiagnostic(
                            secondId,
                            "Switch critical B",
                            null,
                            null,
                            Now,
                            Now,
                            new InterfaceDiagnostic[0],
                            "192.0.2.163")
                    },
                    new PhysicalLinkDiagnostic[0]));
        }

        private static T FindVisualDescendantByTag<T>(
            DependencyObject root,
            object tag)
            where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            var count =
                VisualTreeHelper.GetChildrenCount(
                    root);

            for (var index = 0;
                 index < count;
                 index++)
            {
                var child =
                    VisualTreeHelper.GetChild(
                        root,
                        index);

                var element =
                    child as T;

                if (element != null &&
                    Equals(
                        element.Tag,
                        tag))
                {
                    return element;
                }

                var nested =
                    FindVisualDescendantByTag<T>(
                        child,
                        tag);

                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static int CountOccurrences(
            string value,
            string token)
        {
            if (string.IsNullOrEmpty(
                    value) ||
                string.IsNullOrEmpty(
                    token))
            {
                return 0;
            }

            var count =
                0;
            var index =
                0;

            while ((index =
                    value.IndexOf(
                        token,
                        index,
                        StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static int CountDiagnosticSections(
            ItemsControl fields)
        {
            var count =
                0;

            foreach (var row in fields.Items)
            {
                if (DiagnosticRowBool(
                    row,
                    "IsSectionHeader"))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool DiagnosticSectionExists(
            ItemsControl fields,
            string title)
        {
            foreach (var row in fields.Items)
            {
                if (DiagnosticRowBool(
                        row,
                        "IsSectionHeader") &&
                    string.Equals(
                        DiagnosticRowString(
                            row,
                            "Label"),
                        title,
                        StringComparison.CurrentCulture))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FindDiagnosticFieldValueAfterSection(
            ItemsControl fields,
            string sectionTitle,
            string label)
        {
            var inSection =
                false;

            foreach (var row in fields.Items)
            {
                var isSection =
                    DiagnosticRowBool(
                        row,
                        "IsSectionHeader");

                if (isSection)
                {
                    inSection =
                        string.Equals(
                            DiagnosticRowString(
                                row,
                                "Label"),
                            sectionTitle,
                            StringComparison.CurrentCulture);
                    continue;
                }

                if (inSection &&
                    string.Equals(
                        DiagnosticRowString(
                            row,
                            "Label"),
                        label,
                        StringComparison.CurrentCulture))
                {
                    return DiagnosticRowString(
                        row,
                        "Value");
                }
            }

            Assert.Fail(
                "Diagnostic field not found in section: " +
                sectionTitle +
                " / " +
                label);
            return null;
        }

        private static string FindDiagnosticFieldValue(
            ItemsControl fields,
            string label)
        {
            foreach (var row in fields.Items)
            {
                if (string.Equals(
                    DiagnosticRowString(
                        row,
                        "Label"),
                    label,
                    StringComparison.CurrentCulture))
                {
                    return DiagnosticRowString(
                        row,
                        "Value");
                }
            }

            Assert.Fail(
                "Diagnostic field not found: " +
                label);
            return null;
        }

        private static string DiagnosticRowString(
            object row,
            string propertyName)
        {
            var property =
                row.GetType()
                    .GetProperty(
                        propertyName);

            Assert.IsNotNull(
                property,
                "Diagnostic row property not found: " +
                propertyName);

            return (string)property.GetValue(
                row,
                null);
        }

        private static bool DiagnosticRowBool(
            object row,
            string propertyName)
        {
            var property =
                row.GetType()
                    .GetProperty(
                        propertyName);

            Assert.IsNotNull(
                property,
                "Diagnostic row property not found: " +
                propertyName);

            return (bool)property.GetValue(
                row,
                null);
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

        private static T FindVisualDescendant<T>(
            DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }

            var count =
                VisualTreeHelper.GetChildrenCount(
                    root);

            for (var index = 0;
                 index < count;
                 index++)
            {
                var child =
                    VisualTreeHelper.GetChild(
                        root,
                        index);

                var typed =
                    child as T;

                if (typed != null)
                {
                    return typed;
                }

                var nested =
                    FindVisualDescendant<T>(
                        child);

                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
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

        private sealed class FixedViewportLayoutStore :
            IMapLayoutStore
        {
            private readonly MapLayoutSnapshot
                _snapshot;

            public FixedViewportLayoutStore(
                MapLayoutSnapshot snapshot)
            {
                _snapshot =
                    snapshot ??
                    throw new ArgumentNullException(
                        nameof(snapshot));
            }

            public int SaveViewportCount { get; private set; }

            public MapLayoutSnapshot Load(
                Guid mapId)
            {
                return _snapshot;
            }

            public void SaveViewport(
                Guid mapId,
                MapViewportLayout viewport)
            {
                SaveViewportCount++;
            }

            public void SaveDevice(
                Guid mapId,
                MapDeviceLayout deviceLayout)
            {
            }
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
