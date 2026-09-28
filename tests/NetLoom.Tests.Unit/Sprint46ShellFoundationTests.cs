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
using NetLoom.Application.MapLayout;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.Topology;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
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

                        Assert.AreEqual(
                            Visibility.Visible,
                            ((Button)window.FindName(
                                "ShellProfileAddButton"))
                            .Visibility);

                        Assert.AreEqual(
                            UiText.Get(
                                "ShellProfilesEmpty"),
                            ((TextBlock)window.FindName(
                                "ShellProfileStatusText"))
                            .Text);
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

                        SelectDevice(
                            window,
                            firstId);

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

                        var eventText =
                            ((TextBlock)window.FindName(
                                "AlertTransitionText"))
                            .Text;

                        StringAssert.Contains(
                            eventText,
                            "Switch critical A");
                        Assert.IsFalse(
                            eventText.Contains(
                                physicalLinkId.ToString(
                                    "D")));
                        Assert.IsFalse(
                            eventText.Contains(
                                "cist"));

                        SelectDevice(
                            window,
                            firstId);

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
                        Assert.IsFalse(
                            ((Expander)window.FindName(
                                "MonitoringExpander"))
                            .IsExpanded,
                            "Detailed polling parameters must stay collapsed during normal operation.");

                        var sidebarWidth =
                            (GridLength)window.FindResource(
                                "NetLoom.Shell.SidebarWidth");

                        Assert.AreEqual(
                            380.0,
                            sidebarWidth.Value,
                            0.01);
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

                        var expectedDarkSelectedTab =
                            (SolidColorBrush)window.FindResource(
                                "NetLoom.Brush.SurfaceHover");

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

        [TestMethod]
        public void
            RailUsesVectorIconsBadgeAndReadableMapTokens()
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
                            window.FindResource(
                                "NetLoom.Icon.ShellMap"));
                        Assert.IsNotNull(
                            window.FindResource(
                                "NetLoom.Icon.ShellAlerts"));

                        var railWidth =
                            (GridLength)window.FindResource(
                                "NetLoom.Shell.RailWidth");

                        Assert.AreEqual(
                            160.0,
                            railWidth.Value,
                            0.01);

                        Assert.AreEqual(
                            Visibility.Collapsed,
                            ((FrameworkElement)window.FindName(
                                "ShellAlertsBadge"))
                            .Visibility);

                        Assert.AreEqual(
                            0.75,
                            (double)window.FindResource(
                                "NetLoom.Map.ReadableZoomMin"),
                            0.001);
                        Assert.AreEqual(
                            0.95,
                            (double)window.FindResource(
                                "NetLoom.Map.LinkLabelMinZoom"),
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
