using Path = System.Windows.Shapes.Path;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;

using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint46UiStateGalleryTests
    {
        private const int NarrowWidth = 1100;
        private const int NormalWidth = 1400;
        private const int GalleryHeight = 830;

        [TestMethod]
        public void Pass3StateGalleryRendersProductionSurfacesAndReportsVisualBreakage()
        {
            RunOnSta(
                () =>
                {
                    var outputDirectory =
                        ResolveOutputDirectory();

                    Directory.CreateDirectory(
                        outputDirectory);

                    foreach (var file in
                        Directory.GetFiles(
                            outputDirectory,
                            "*.png"))
                    {
                        File.Delete(file);
                    }

                    var findings =
                        new List<string>();

                    RenderShellScenario(
                        outputDirectory,
                        "01-shell-empty",
                        () => EmptySnapshot(),
                        null,
                        findings);

                    var longDeviceId =
                        Guid.Parse(
                            "46464646-9001-9001-9001-464646464646");

                    RenderShellScenario(
                        outputDirectory,
                        "02-device-long-name",
                        () => SingleDeviceSnapshot(
                            longDeviceId,
                            "Очень длинное имя промышленного коммутатора ЙЁ 012345678901234567890123456789012345678901234567890123456789",
                            "192.0.2.201"),
                        window =>
                        {
                            WaitForCondition(
                                () =>
                                    DeviceBorder(
                                        window,
                                        longDeviceId) != null);

                            SelectDevice(
                                window,
                                longDeviceId);
                        },
                        findings);

                    Guid firstDeviceId;
                    Guid secondDeviceId;
                    Guid firstInterfaceId;
                    Guid secondInterfaceId;
                    Guid physicalLinkId;

                    var staleCriticalSnapshot =
                        Pass2InspectorSnapshot(
                            out firstDeviceId,
                            out secondDeviceId,
                            out firstInterfaceId,
                            out secondInterfaceId,
                            out physicalLinkId);

                    RenderShellScenario(
                        outputDirectory,
                        "03-link-critical-stale",
                        () => staleCriticalSnapshot,
                        window =>
                        {
                            WaitForCondition(
                                () =>
                                    FindLink(
                                        window,
                                        physicalLinkId) !=
                                    null);

                            SelectLink(
                                window,
                                physicalLinkId);
                        },
                        findings);

                    var siteId =
                        Guid.Parse(
                            "46464646-9100-9100-9100-464646464646");
                    var rackId =
                        Guid.Parse(
                            "46464646-9200-9200-9200-464646464646");
                    var locationDeviceId =
                        Guid.Parse(
                            "46464646-9300-9300-9300-464646464646");
                    var locationInterfaceId =
                        Guid.Parse(
                            "46464646-9400-9400-9400-464646464646");

                    RenderShellScenario(
                        outputDirectory,
                        "04-location-inspector",
                        () => InspectorSnapshot(
                            siteId,
                            rackId,
                            locationDeviceId,
                            locationInterfaceId),
                        window =>
                        {
                            WaitForCondition(
                                () =>
                                    LocationBorder(
                                        window,
                                        rackId) !=
                                    null);

                            SelectLocation(
                                window,
                                rackId);
                        },
                        findings);

                    RenderShellScenario(
                        outputDirectory,
                        "05-alerts-and-events",
                        () => CriticalLinkSnapshot(
                            Guid.Parse(
                                "46464646-9500-9500-9500-464646464646"),
                            Guid.Parse(
                                "46464646-9600-9600-9600-464646464646"),
                            Guid.Parse(
                                "46464646-9700-9700-9700-464646464646")),
                        window =>
                        {
                            Click(
                                (Button)window.FindName(
                                    "ShellAlertsButton"));
                        },
                        findings);

                    RenderShellScenario(
                        outputDirectory,
                        "06-manual-topology-editor",
                        () => EmptySnapshot(),
                        window =>
                        {
                            Click(
                                (Button)window.FindName(
                                    "MapEditModeButton"));

                            Click(
                                (Button)window.FindName(
                                    "ManualTopologyButton"));

                            WaitForCondition(
                                () =>
                                    ((ContentControl)window.FindName(
                                        "ShellWorkspaceEditorHost"))
                                    .Visibility ==
                                    Visibility.Visible);
                        },
                        findings);

                    RenderShellScenario(
                        outputDirectory,
                        "07-location-editor",
                        () => EmptySnapshot(),
                        window =>
                        {
                            Click(
                                (Button)window.FindName(
                                    "MapEditModeButton"));

                            Click(
                                (Button)window.FindName(
                                    "LocationsButton"));

                            WaitForCondition(
                                () =>
                                    ((ContentControl)window.FindName(
                                        "ShellWorkspaceEditorHost"))
                                    .Visibility ==
                                    Visibility.Visible);
                        },
                        findings);

                    RenderProfileSettingsScenario(
                        outputDirectory,
                        findings);

                    RenderProfileResolutionScenarios(
                        outputDirectory,
                        findings);

                    RenderPopupScenario(
                        outputDirectory,
                        findings);

                    RenderSection9ShellMatrix(
                        outputDirectory,
                        findings);

                    RenderControlStateMatrix(
                        outputDirectory);

                    var reportPath =
                        System.IO.Path.Combine(
                            outputDirectory,
                            "gallery-findings.txt");

                    var coveragePath =
                        System.IO.Path.Combine(
                            outputDirectory,
                            "gallery-coverage.txt");

                    File.WriteAllLines(
                        reportPath,
                        findings.Count == 0
                            ? new[]
                            {
                                "OK: automatic clipping/layout scan found no breakage."
                            }
                            : findings
                                .OrderBy(
                                    item => item,
                                    StringComparer.Ordinal)
                                .ToArray());

                    File.WriteAllLines(
                        coveragePath,
                        new[]
                        {
                            "Sprint 46 Pass 3 — UI_DESIGN_RULES §9 state gallery.",
                            "Rendered from production WPF controls; gallery defines no product styles.",
                            "Each shell PNG contains minimum-width 1100 px and normal-width 1400 px side by side.",
                            "Themes: Light and Dark.",
                            "Covered shell surfaces: empty state; device names (one word, IP-only, long/unbroken, Й/Ё); selected device inspector; stale Critical link; Location inspector; Equipment; Monitoring error; Alerts/event strip; edit-mode controls; Manual topology editor; Location editor; SNMP profile settings; access-profile resolution states (0 profiles, 1 auto-selected, 2+ unresolved for Monitoring and Discovery).",
                            "Covered component states from production resource dictionaries: normal, hover, keyboard focus, disabled, selected/checked/expanded for Button, secondary Button, ToggleButton, ListBoxItem, TabItem, ComboBox, CheckBox, Expander and TextBox; count badge 0/1/many/99+; status/severity labels.",
                            "Separate-HWND identity + WPF visual-root raster proof: production ComboBox popup, ContextMenu (normal and disabled item), submenu and ToolTip are located through their live production popup HwndSource and rasterized from that source's live WPF RootVisual in both themes; the test rejects owner-window substitution, mostly transparent captures and visually flat/black captures.",
                            "Text axes represented: empty, one word, ordinary, multiline, unbroken 60+ characters, Й/Ё. Width axis: minimum and normal shell widths side by side. Themes: Light and Dark.",
                            "Automatic findings are informational only in Step 2. The gallery test fails only when a required production state cannot be reached or a PNG cannot be produced; visual defects are reviewed from the archive.",
                            "Discarded static axes where they do not apply: ToolTip has no disabled/selected state; submenu has no separate focus state beyond MenuItem focus; count scenarios apply only to count-bearing controls. Behavior-only axes not represented by PNG: Windows 150–200% DPI behavior, full keyboard-only journey, UI Automation tree semantics, system animation setting and copy/select behavior. These belong to Step 3 or later acceptance, not this Step 2 gallery."
                        });
                });
        }

        private static void RenderShellScenario(
            string outputDirectory,
            string scenario,
            Func<TopologyRefreshSnapshot> snapshotFactory,
            Action<MainWindow> configure,
            IList<string> findings)
        {
            foreach (var dark in new[] { false, true })
            {
                var narrow =
                    RenderWindow(
                        snapshotFactory(),
                        dark,
                        NarrowWidth,
                        GalleryHeight,
                        configure,
                        scenario,
                        findings);

                var normal =
                    RenderWindow(
                        snapshotFactory(),
                        dark,
                        NormalWidth,
                        GalleryHeight,
                        configure,
                        scenario,
                        findings);

                var path =
                    System.IO.Path.Combine(
                        outputDirectory,
                        scenario +
                        "-" +
                        (dark ? "dark" : "light") +
                        ".png");

                SaveSideBySide(
                    narrow,
                    normal,
                    path);
            }
        }

        private static BitmapSource RenderWindow(
            TopologyRefreshSnapshot snapshot,
            bool dark,
            int width,
            int height,
            Action<MainWindow> configure,
            string scenario,
            IList<string> findings)
        {
            var window =
                new MainWindow(
                    new FixedRefreshProvider(snapshot),
                    new EmptyLookupReader());

            try
            {
                PrepareWindow(
                    window,
                    width,
                    height);

                if (dark)
                {
                    Click(
                        (Button)window.FindName(
                            "ShellThemeButton"));
                }

                configure?.Invoke(window);

                PumpDispatcher();
                window.UpdateLayout();

                CollectTextClipping(
                    window.Content as DependencyObject,
                    scenario +
                    "/" +
                    (dark ? "dark" : "light") +
                    "/" +
                    width,
                    findings);

                return Capture(
                    window.Content as FrameworkElement);
            }
            finally
            {
                window.Close();
                PumpDispatcher();
            }
        }

        private static void RenderProfileSettingsScenario(
            string outputDirectory,
            IList<string> findings)
        {
            var first =
                new AccessProfile(
                    Guid.Parse(
                        "46464646-9800-9800-9800-464646464646"),
                    "Основной профиль SNMP для очень длинного имени площадки ЙЁ",
                    true,
                    SnmpVersion.V2C,
                    null);

            var second =
                new AccessProfile(
                    Guid.Parse(
                        "46464646-9801-9801-9801-464646464646"),
                    "Резервный профиль",
                    true,
                    SnmpVersion.V1,
                    null);

            foreach (var dark in new[] { false, true })
            {
                var bitmaps =
                    new List<BitmapSource>();

                foreach (var width in
                    new[] { NarrowWidth, NormalWidth })
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new GalleryMonitoringControl(),
                            new[]
                            {
                                first,
                                second
                            });

                    try
                    {
                        PrepareWindow(
                            window,
                            width,
                            GalleryHeight);

                        if (dark)
                        {
                            Click(
                                (Button)window.FindName(
                                    "ShellThemeButton"));
                        }

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));

                        var list =
                            (ListBox)window.FindName(
                                "ShellProfileSettingsList");

                        list.SelectedIndex = 1;

                        PumpDispatcher();
                        window.UpdateLayout();

                        CollectTextClipping(
                            window.Content as DependencyObject,
                            "08-profile-settings/" +
                            (dark ? "dark" : "light") +
                            "/" +
                            width,
                            findings);

                        CollectUnexpectedHorizontalScrollBars(
                            list,
                            "08-profile-settings/" +
                            (dark ? "dark" : "light") +
                            "/" +
                            width,
                            findings);

                        bitmaps.Add(
                            Capture(
                                window.Content as FrameworkElement));
                    }
                    finally
                    {
                        window.Close();
                        PumpDispatcher();
                    }
                }

                SaveSideBySide(
                    bitmaps[0],
                    bitmaps[1],
                    System.IO.Path.Combine(
                        outputDirectory,
                        "08-profile-settings-" +
                        (dark ? "dark" : "light") +
                        ".png"));
            }
        }

        private static void RenderProfileResolutionScenarios(
            string outputDirectory,
            IList<string> findings)
        {
            var singleProfile =
                new AccessProfile(
                    Guid.Parse(
                        "46464646-9810-9810-9810-464646464646"),
                    "Единственный профиль",
                    true,
                    SnmpVersion.V2C,
                    null);

            var firstProfile =
                new AccessProfile(
                    Guid.Parse(
                        "46464646-9820-9820-9820-464646464646"),
                    "Профиль A",
                    true,
                    SnmpVersion.V2C,
                    null);

            var secondProfile =
                new AccessProfile(
                    Guid.Parse(
                        "46464646-9821-9821-9821-464646464646"),
                    "Профиль B",
                    true,
                    SnmpVersion.V1,
                    null);

            RenderProfileResolutionScenario(
                outputDirectory,
                "08a-profile-zero",
                new AccessProfile[0],
                () => EmptySnapshot(),
                window =>
                {
                    Assert.AreEqual(
                        Visibility.Collapsed,
                        ((ComboBox)window.FindName(
                            "DiscoveryProfileComboBox"))
                        .Visibility);

                    Assert.AreEqual(
                        Visibility.Visible,
                        ((Button)window.FindName(
                            "ShellProfileAddButton"))
                        .Visibility);
                },
                findings);

            RenderProfileResolutionScenario(
                outputDirectory,
                "08b-profile-single-auto-selected",
                new[]
                {
                    singleProfile
                },
                () => EmptySnapshot(),
                window =>
                {
                    var profiles =
                        (ComboBox)window.FindName(
                            "DiscoveryProfileComboBox");

                    Assert.AreEqual(
                        Visibility.Visible,
                        profiles.Visibility);

                    Assert.AreEqual(
                        0,
                        profiles.SelectedIndex,
                        "The §9 single-profile state must render the automatically resolved selection.");

                    Assert.AreEqual(
                        Visibility.Collapsed,
                        ((Button)window.FindName(
                            "ShellProfileAddButton"))
                        .Visibility);
                },
                findings);

            var monitoringDeviceId =
                Guid.Parse(
                    "46464646-9830-9830-9830-464646464646");

            RenderProfileResolutionScenario(
                outputDirectory,
                "08c-profile-unresolved-monitoring",
                new[]
                {
                    firstProfile,
                    secondProfile
                },
                () => SingleDeviceSnapshot(
                    monitoringDeviceId,
                    "SW-PROFILE-STATE",
                    "192.0.2.214"),
                window =>
                {
                    WaitForCondition(
                        () =>
                            DeviceBorder(
                                window,
                                monitoringDeviceId) !=
                            null);

                    SelectDevice(
                        window,
                        monitoringDeviceId);

                    Click(
                        (Button)window.FindName(
                            "ShellMonitoringButton"));

                    PumpDispatcher();

                    AssertUnresolvedProfilePlaceholder(
                        window);

                    Assert.IsFalse(
                        ((Button)window.FindName(
                            "MonitoringStartButton"))
                        .IsEnabled,
                        "Monitoring start must be visibly disabled while the global profile is unresolved.");

                    Assert.IsFalse(
                        ((Button)window.FindName(
                            "ShellMonitoringStartButton"))
                        .IsEnabled);
                },
                findings);

            RenderProfileResolutionScenario(
                outputDirectory,
                "08d-profile-unresolved-discovery",
                new[]
                {
                    firstProfile,
                    secondProfile
                },
                () => EmptySnapshot(),
                window =>
                {
                    Click(
                        (Button)window.FindName(
                            "ShellDiscoveryButton"));

                    PumpDispatcher();

                    AssertUnresolvedProfilePlaceholder(
                        window);

                    Assert.IsFalse(
                        ((Button)window.FindName(
                            "DiscoveryStartButton"))
                        .IsEnabled,
                        "Discovery start must be visibly disabled while the global profile is unresolved.");

                    Assert.AreEqual(
                        UiText.Get(
                            "DiscoveryValidationProfileRequired"),
                        ((TextBlock)window.FindName(
                            "DiscoveryProfileHintText"))
                        .Text,
                        "Discovery must render an operator-visible explanation for the unresolved profile.");
                },
                findings);
        }

        private static void RenderProfileResolutionScenario(
            string outputDirectory,
            string scenario,
            IReadOnlyList<AccessProfile> profiles,
            Func<TopologyRefreshSnapshot> snapshotFactory,
            Action<MainWindow> configure,
            IList<string> findings)
        {
            foreach (var dark in new[] { false, true })
            {
                var bitmaps =
                    new List<BitmapSource>();

                foreach (var width in
                    new[] { NarrowWidth, NormalWidth })
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                snapshotFactory()),
                            new EmptyLookupReader(),
                            new GalleryMonitoringControl(),
                            profiles);

                    try
                    {
                        PrepareWindow(
                            window,
                            width,
                            GalleryHeight);

                        if (dark)
                        {
                            Click(
                                (Button)window.FindName(
                                    "ShellThemeButton"));
                        }

                        configure?.Invoke(window);

                        PumpDispatcher();
                        window.UpdateLayout();

                        CollectTextClipping(
                            window.Content as DependencyObject,
                            scenario +
                            "/" +
                            (dark
                                ? "dark"
                                : "light") +
                            "/" +
                            width,
                            findings);

                        bitmaps.Add(
                            Capture(
                                window.Content
                                as FrameworkElement));
                    }
                    finally
                    {
                        window.Close();
                        PumpDispatcher();
                    }
                }

                SaveSideBySide(
                    bitmaps[0],
                    bitmaps[1],
                    System.IO.Path.Combine(
                        outputDirectory,
                        scenario +
                        "-" +
                        (dark
                            ? "dark"
                            : "light") +
                        ".png"));
            }
        }

        private static void AssertUnresolvedProfilePlaceholder(
            MainWindow window)
        {
            var profiles =
                (ComboBox)window.FindName(
                    "DiscoveryProfileComboBox");

            Assert.AreEqual(
                -1,
                profiles.SelectedIndex,
                "The §9 unresolved state must preserve SelectedIndex = -1.");

            profiles.ApplyTemplate();
            PumpDispatcher();

            var placeholder =
                profiles.Template.FindName(
                    "PlaceholderPresenter",
                    profiles)
                as TextBlock;

            Assert.IsNotNull(
                placeholder,
                "The shared ComboBox style must render the unresolved placeholder.");

            Assert.AreEqual(
                Visibility.Visible,
                placeholder.Visibility);

            Assert.AreEqual(
                UiText.Get(
                    "ShellProfilePlaceholder"),
                placeholder.Text);
        }

        private static void RenderPopupScenario(
            string outputDirectory,
            IList<string> findings)
        {
            var firstProfile =
                new AccessProfile(
                    Guid.Parse(
                        "46464646-9900-9900-9900-464646464646"),
                    "Профиль для раскрытого списка ЙЁ",
                    true,
                    SnmpVersion.V2C,
                    null);

            var secondProfile =
                new AccessProfile(
                    Guid.Parse(
                        "46464646-9901-9901-9901-464646464646"),
                    "Резервный профиль для проверки второй строки",
                    true,
                    SnmpVersion.V1,
                    null);

            foreach (var dark in new[] { false, true })
            {
                var comboBitmaps = new List<BitmapSource>();
                var menuBitmaps = new List<BitmapSource>();
                var disabledMenuBitmaps = new List<BitmapSource>();
                var submenuBitmaps = new List<BitmapSource>();
                var tooltipBitmaps = new List<BitmapSource>();

                foreach (var width in new[] { NarrowWidth, NormalWidth })
                {
                    var window =
                        new MainWindow(
                            new FixedRefreshProvider(
                                EmptySnapshot()),
                            new EmptyLookupReader(),
                            new GalleryMonitoringControl(),
                            new[]
                            {
                                firstProfile,
                                secondProfile
                            });

                    try
                    {
                        PrepareWindowForInteractiveCapture(
                            window,
                            width,
                            GalleryHeight);

                        var ownerHandle =
                            new WindowInteropHelper(window)
                                .Handle;

                        Assert.AreNotEqual(
                            IntPtr.Zero,
                            ownerHandle,
                            "Gallery owner window handle is unavailable.");

                        if (dark)
                        {
                            Click(
                                (Button)window.FindName(
                                    "ShellThemeButton"));
                        }

                        var combo =
                            (ComboBox)window.FindName(
                                "DiscoveryProfileComboBox");

                        combo.SelectedIndex = 0;
                        combo.ApplyTemplate();
                        combo.IsDropDownOpen = true;

                        WaitForCondition(
                            () =>
                                combo.IsDropDownOpen);

                        var popup =
                            (Popup)combo.Template.FindName(
                                "PART_Popup",
                                combo);

                        Assert.IsNotNull(
                            popup,
                            "Production ComboBox popup is unavailable.");

                        Assert.IsTrue(
                            popup.IsOpen,
                            "Production ComboBox popup did not open.");

                        Assert.IsNotNull(
                            popup.Child,
                            "Production ComboBox popup child is unavailable.");

                        PumpDispatcher();

                        comboBitmaps.Add(
                            CapturePresentationHwnd(
                                popup.Child,
                                "ComboBox popup",
                                ownerHandle));

                        combo.IsDropDownOpen = false;
                        PumpDispatcher();

                        Click(
                            (Button)window.FindName(
                                "ShellSettingsButton"));

                        var settingsSidebar =
                            (ScrollViewer)window.FindName(
                                "ShellSettingsSidebarPanel");

                        WaitForCondition(
                            () =>
                                settingsSidebar.Visibility ==
                                Visibility.Visible);

                        var settingsButton =
                            (Button)window.FindName(
                                "MapSettingsButton");

                        var menu =
                            settingsButton.ContextMenu;

                        Assert.IsNotNull(
                            menu,
                            "Production map ContextMenu is unavailable.");

                        Click(settingsButton);

                        WaitForCondition(
                            () =>
                                menu.IsOpen);

                        menu.UpdateLayout();
                        PumpDispatcher();

                        var firstMenuItem =
                            menu.ItemContainerGenerator
                                .ContainerFromIndex(0)
                            as MenuItem;

                        Assert.IsNotNull(
                            firstMenuItem,
                            "Production ContextMenu first item is unavailable.");

                        menuBitmaps.Add(
                            CapturePresentationHwnd(
                                firstMenuItem,
                                "map ContextMenu",
                                ownerHandle));

                        firstMenuItem.IsEnabled = false;
                        PumpDispatcher();

                        disabledMenuBitmaps.Add(
                            CapturePresentationHwnd(
                                firstMenuItem,
                                "disabled map ContextMenu",
                                ownerHandle));

                        firstMenuItem.IsEnabled = true;
                        PumpDispatcher();

                        var submenuParent =
                            Enumerable.Range(
                                    0,
                                    menu.Items.Count)
                                .Select(
                                    index =>
                                        menu.ItemContainerGenerator
                                            .ContainerFromIndex(index)
                                        as MenuItem)
                                .FirstOrDefault(
                                    item =>
                                        item != null &&
                                        item.Items.Count > 0);

                        Assert.IsNotNull(
                            submenuParent,
                            "Production map ContextMenu has no submenu for gallery coverage.");

                        submenuParent.IsSubmenuOpen = true;
                        PumpDispatcher();

                        var firstSubmenuItem =
                            submenuParent.ItemContainerGenerator
                                .ContainerFromIndex(0)
                            as MenuItem;

                        Assert.IsNotNull(
                            firstSubmenuItem,
                            "Production ContextMenu submenu item is unavailable.");

                        submenuBitmaps.Add(
                            CapturePresentationHwnd(
                                firstSubmenuItem,
                                "map ContextMenu submenu",
                                ownerHandle));

                        submenuParent.IsSubmenuOpen = false;
                        menu.IsOpen = false;
                        PumpDispatcher();

                        Assert.IsNotNull(
                            settingsButton.ToolTip,
                            "Production map settings ToolTip is unavailable.");

                        var toolTip =
                            new ToolTip
                            {
                                Content = settingsButton.ToolTip,
                                PlacementTarget = settingsButton,
                                Placement = PlacementMode.Left
                            };

                        var toolTipStyle =
                            window.TryFindResource(
                                typeof(ToolTip))
                            as Style;

                        if (toolTipStyle != null)
                        {
                            toolTip.Style = toolTipStyle;
                        }

                        toolTip.IsOpen = true;

                        WaitForCondition(
                            () =>
                                toolTip.IsOpen);

                        PumpDispatcher();

                        tooltipBitmaps.Add(
                            CapturePresentationHwnd(
                                toolTip,
                                "ToolTip",
                                ownerHandle));

                        toolTip.IsOpen = false;
                        PumpDispatcher();
                    }
                    finally
                    {
                        window.Close();
                        PumpDispatcher();
                    }
                }

                SaveSideBySide(
                    comboBitmaps[0],
                    comboBitmaps[1],
                    System.IO.Path.Combine(
                        outputDirectory,
                        "09-popup-combo-" +
                        (dark ? "dark" : "light") +
                        ".png"));

                SaveSideBySide(
                    menuBitmaps[0],
                    menuBitmaps[1],
                    System.IO.Path.Combine(
                        outputDirectory,
                        "10-map-context-menu-" +
                        (dark ? "dark" : "light") +
                        ".png"));

                SaveSideBySide(
                    disabledMenuBitmaps[0],
                    disabledMenuBitmaps[1],
                    System.IO.Path.Combine(
                        outputDirectory,
                        "10a-map-context-menu-disabled-" +
                        (dark ? "dark" : "light") +
                        ".png"));

                SaveSideBySide(
                    submenuBitmaps[0],
                    submenuBitmaps[1],
                    System.IO.Path.Combine(
                        outputDirectory,
                        "11-map-context-submenu-" +
                        (dark ? "dark" : "light") +
                        ".png"));

                SaveSideBySide(
                    tooltipBitmaps[0],
                    tooltipBitmaps[1],
                    System.IO.Path.Combine(
                        outputDirectory,
                        "12-tooltip-" +
                        (dark ? "dark" : "light") +
                        ".png"));
            }
        }

        private static void RenderSection9ShellMatrix(
            string outputDirectory,
            IList<string> findings)
        {
            var oneWordId =
                Guid.Parse(
                    "46464646-a001-a001-a001-464646464646");

            RenderShellScenario(
                outputDirectory,
                "12-device-one-word",
                () => SingleDeviceSnapshot(
                    oneWordId,
                    "Коммутатор",
                    "192.0.2.210"),
                null,
                findings);

            var ipOnlyId =
                Guid.Parse(
                    "46464646-a002-a002-a002-464646464646");

            RenderShellScenario(
                outputDirectory,
                "13-device-ip-only",
                () => SingleDeviceSnapshot(
                    ipOnlyId,
                    "192.0.2.211",
                    "192.0.2.211"),
                window =>
                {
                    WaitForCondition(
                        () =>
                            DeviceBorder(
                                window,
                                ipOnlyId) != null);

                    SelectDevice(
                        window,
                        ipOnlyId);
                },
                findings);

            var normalDeviceId =
                Guid.Parse(
                    "46464646-a003-a003-a003-464646464646");

            RenderShellScenario(
                outputDirectory,
                "14-device-inspector-normal",
                () => SingleDeviceSnapshot(
                    normalDeviceId,
                    "SW-CORE-01",
                    "192.0.2.212"),
                window =>
                {
                    WaitForCondition(
                        () =>
                            DeviceBorder(
                                window,
                                normalDeviceId) != null);

                    SelectDevice(
                        window,
                        normalDeviceId);
                },
                findings);

            var equipmentDeviceId =
                Guid.Parse(
                    "46464646-a004-a004-a004-464646464646");

            RenderShellScenario(
                outputDirectory,
                "15-equipment-list",
                () => SingleDeviceSnapshot(
                    equipmentDeviceId,
                    "Очень длинное имя оборудования ЙЁ 012345678901234567890123456789012345678901234567890123456789",
                    "192.0.2.213"),
                window =>
                {
                    WaitForCondition(
                        () =>
                            DeviceBorder(
                                window,
                                equipmentDeviceId) != null);

                    Click(
                        (Button)window.FindName(
                            "ShellEquipmentButton"));
                },
                findings);

            RenderShellScenario(
                outputDirectory,
                "16-monitoring-error",
                () => EmptySnapshot(),
                window =>
                {
                    Click(
                        (Button)window.FindName(
                            "ShellMonitoringButton"));

                    var start =
                        (Button)window.FindName(
                            "MonitoringStartButton");

                    if (start.IsEnabled)
                    {
                        Click(start);
                    }
                },
                findings);

            RenderShellScenario(
                outputDirectory,
                "17-edit-mode-controls",
                () => EmptySnapshot(),
                window =>
                {
                    Click(
                        (Button)window.FindName(
                            "MapEditModeButton"));
                },
                findings);
        }

        private static void RenderControlStateMatrix(
            string outputDirectory)
        {
            foreach (var dark in new[] { false, true })
            {
                RenderControlBoardState(
                    outputDirectory,
                    dark,
                    "18-controls-normal",
                    null,
                    null);

                RenderControlBoardState(
                    outputDirectory,
                    dark,
                    "19-controls-disabled",
                    board => board.SetAllEnabled(false),
                    null);

                RenderControlBoardState(
                    outputDirectory,
                    dark,
                    "20-controls-selected-expanded",
                    board => board.SetSelectedExpanded(),
                    null);

                foreach (var key in
                    new[]
                    {
                        "primary-button",
                        "secondary-button",
                        "toggle-button",
                        "list-item",
                        "tab-item",
                        "combo-box",
                        "check-box",
                        "expander",
                        "text-box"
                    })
                {
                    RenderControlBoardState(
                        outputDirectory,
                        dark,
                        "21-focus-" + key,
                        null,
                        board =>
                        {
                            var element =
                                board.FocusTargets[key];

                            element.Focus();
                            Keyboard.Focus(element);
                            PumpDispatcher();
                        });

                    RenderControlBoardState(
                        outputDirectory,
                        dark,
                        "22-hover-" + key,
                        null,
                        board =>
                        {
                            var element =
                                board.HoverTargets[key];

                            var point =
                                element.PointToScreen(
                                    new Point(
                                        Math.Max(1.0, element.ActualWidth / 2.0),
                                        Math.Max(1.0, element.ActualHeight / 2.0)));

                            SetCursorPos(
                                (int)Math.Round(point.X),
                                (int)Math.Round(point.Y));

                            PumpDispatcher();
                        });
                }
            }
        }

        private static void RenderControlBoardState(
            string outputDirectory,
            bool dark,
            string scenario,
            Action<ControlGalleryBoard> configureBeforeLayout,
            Action<ControlGalleryBoard> configureAfterLayout)
        {
            var narrow =
                RenderControlBoard(
                    dark,
                    NarrowWidth,
                    configureBeforeLayout,
                    configureAfterLayout);

            var normal =
                RenderControlBoard(
                    dark,
                    NormalWidth,
                    configureBeforeLayout,
                    configureAfterLayout);

            SaveSideBySide(
                narrow,
                normal,
                System.IO.Path.Combine(
                    outputDirectory,
                    scenario +
                    "-" +
                    (dark ? "dark" : "light") +
                    ".png"));
        }

        private static BitmapSource RenderControlBoard(
            bool dark,
            int width,
            Action<ControlGalleryBoard> configureBeforeLayout,
            Action<ControlGalleryBoard> configureAfterLayout)
        {
            var window =
                new MainWindow(
                    new FixedRefreshProvider(
                        EmptySnapshot()),
                    new EmptyLookupReader());

            try
            {
                PrepareWindowForInteractiveCapture(
                    window,
                    width,
                    GalleryHeight);

                if (dark)
                {
                    Click(
                        (Button)window.FindName(
                            "ShellThemeButton"));
                }

                var host =
                    (ContentControl)window.FindName(
                        "ShellWorkspaceEditorHost");

                var board =
                    CreateControlGalleryBoard(
                        window);

                host.Content = board.Root;
                host.Visibility = Visibility.Visible;

                configureBeforeLayout?.Invoke(board);

                PumpDispatcher();
                window.UpdateLayout();

                board.BindGeneratedContainers();

                configureAfterLayout?.Invoke(board);

                PumpDispatcher();
                window.UpdateLayout();

                return Capture(
                    board.Root);
            }
            finally
            {
                window.Close();
                PumpDispatcher();
            }
        }

        private static ControlGalleryBoard CreateControlGalleryBoard(
            MainWindow window)
        {
            var root =
                new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };

            var stack =
                new StackPanel
                {
                    Margin = new Thickness(24)
                };

            root.Content = stack;

            stack.Children.Add(
                new TextBlock
                {
                    Text = "UI_DESIGN_RULES §9 — production control states",
                    Style = (Style)window.FindResource("NetLoom.Style.SectionTitle"),
                    Margin = new Thickness(0, 0, 0, 16)
                });

            var focusTargets =
                new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);

            var hoverTargets =
                new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);

            var enabledTargets =
                new List<FrameworkElement>();

            var primary = new Button
            {
                Content = "Сохранить очень длинное изменение конфигурации ЙЁ"
            };
            AddControlRow(stack, "Button / primary", primary);
            focusTargets["primary-button"] = primary;
            hoverTargets["primary-button"] = primary;
            enabledTargets.Add(primary);

            var secondary = new Button
            {
                Content = "Показать расположение выбранного устройства"
            };
            secondary.Style =
                (Style)window.FindResource("NetLoom.Style.SecondaryButton");
            AddControlRow(stack, "Button / secondary", secondary);
            focusTargets["secondary-button"] = secondary;
            hoverTargets["secondary-button"] = secondary;
            enabledTargets.Add(secondary);

            var toggle = new ToggleButton
            {
                Content = "Закрепить выбранный узел"
            };
            AddControlRow(stack, "ToggleButton", toggle);
            focusTargets["toggle-button"] = toggle;
            hoverTargets["toggle-button"] = toggle;
            enabledTargets.Add(toggle);

            var list = new ListBox
            {
                MinHeight = 92
            };
            list.Items.Add("Обычная строка");
            list.Items.Add("Очень длинная строка списка ЙЁ 012345678901234567890123456789012345678901234567890123456789");
            list.Items.Add("Третья строка");
            AddControlRow(stack, "ListBox / ListBoxItem", list);
            enabledTargets.Add(list);

            var tabs = new TabControl
            {
                Style = (Style)window.FindResource("NetLoom.Style.InspectorTabs")
            };
            var firstTab = new TabItem
            {
                Header = "Обзор",
                Content = "Обычное содержимое",
                Style = (Style)window.FindResource("NetLoom.Style.InspectorTab")
            };
            var secondTab = new TabItem
            {
                Header = "Технические детали и основания",
                Content = "Вторая вкладка",
                Style = (Style)window.FindResource("NetLoom.Style.InspectorTab")
            };
            tabs.Items.Add(firstTab);
            tabs.Items.Add(secondTab);
            tabs.SelectedIndex = 0;
            AddControlRow(stack, "TabControl / TabItem", tabs);
            focusTargets["tab-item"] = firstTab;
            hoverTargets["tab-item"] = firstTab;
            enabledTargets.Add(tabs);

            var combo = new ComboBox
            {
                MinWidth = 310
            };
            combo.Items.Add("Профиль SNMP ЙЁ");
            combo.Items.Add("Профиль с очень длинным названием 012345678901234567890123456789012345678901234567890123456789");
            combo.SelectedIndex = 0;
            AddControlRow(stack, "ComboBox", combo);
            focusTargets["combo-box"] = combo;
            hoverTargets["combo-box"] = combo;
            enabledTargets.Add(combo);

            var check = new CheckBox
            {
                Content = "Показывать только подтверждённые физические связи"
            };
            AddControlRow(stack, "CheckBox", check);
            focusTargets["check-box"] = check;
            hoverTargets["check-box"] = check;
            enabledTargets.Add(check);

            var expander = new Expander
            {
                Header = "Технические детали устройства ЙЁ",
                Content = new TextBlock
                {
                    Text = "46464646-0000-0000-0000-464646464646"
                }
            };
            AddControlRow(stack, "Expander", expander);
            focusTargets["expander"] = expander;
            hoverTargets["expander"] = expander;
            enabledTargets.Add(expander);

            var textBox = new TextBox
            {
                Text = "012345678901234567890123456789012345678901234567890123456789ЙЁ",
                MinWidth = 310
            };
            AddControlRow(stack, "TextBox", textBox);
            focusTargets["text-box"] = textBox;
            hoverTargets["text-box"] = textBox;
            enabledTargets.Add(textBox);

            var badgeRow = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            foreach (var value in new[] { "0", "1", "12", "99+" })
            {
                var badge = new Border
                {
                    Style = (Style)window.FindResource("NetLoom.Style.NavigationBadge"),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock
                    {
                        Text = value,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                badgeRow.Children.Add(badge);
            }
            AddControlRow(stack, "Count badge 0 / 1 / many / 99+", badgeRow);

            var statuses = new WrapPanel();
            foreach (var status in new[]
            {
                "Доступен",
                "Частично доступен",
                "Наблюдается косвенно",
                "Недоступен",
                "Нет данных",
                "Мониторинг отключён",
                "Не контролируется",
                "Мониторинг остановлен",
                "× Критическое",
                "! Предупреждение"
            })
            {
                statuses.Children.Add(
                    new TextBlock
                    {
                        Text = status,
                        Style = (Style)window.FindResource("NetLoom.Style.StatusEmphasis"),
                        Margin = new Thickness(0, 0, 16, 8)
                    });
            }
            AddControlRow(stack, "Statuses and severities", statuses);

            return new ControlGalleryBoard(
                root,
                focusTargets,
                hoverTargets,
                enabledTargets,
                toggle,
                list,
                tabs,
                combo,
                check,
                expander);
        }

        private static void AddControlRow(
            Panel parent,
            string label,
            FrameworkElement control)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 16)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(220)
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };

            Grid.SetColumn(labelBlock, 0);
            Grid.SetColumn(control, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(control);
            parent.Children.Add(grid);
        }

        private static BitmapSource CapturePresentationHwnd(
            Visual visual,
            string description,
            IntPtr ownerHandle)
        {
            var source =
                PresentationSource.FromVisual(visual)
                as HwndSource;

            Assert.IsNotNull(
                source,
                description +
                " has no HwndSource.");

            Assert.AreNotEqual(
                IntPtr.Zero,
                source.Handle,
                description +
                " has no native window handle.");

            Assert.AreNotEqual(
                ownerHandle,
                source.Handle,
                description +
                " resolved to the owner window instead of its popup HWND.");

            var root =
                source.RootVisual
                as FrameworkElement;

            Assert.IsNotNull(
                root,
                description +
                " popup HwndSource has no FrameworkElement RootVisual.");

            PumpDispatcher();

            WaitForCondition(
                () =>
                    root.ActualWidth > 2 &&
                    root.ActualHeight > 2);

            BitmapSource bitmap = null;
            AssertFailedException lastRasterFailure = null;
            var rasterDeadline =
                DateTime.UtcNow +
                TimeSpan.FromSeconds(2);

            do
            {
                PumpDispatcher();
                root.UpdateLayout();

                bitmap =
                    Capture(root);

                try
                {
                    AssertPopupRaster(
                        bitmap,
                        description);

                    return bitmap;
                }
                catch (AssertFailedException ex)
                {
                    lastRasterFailure = ex;
                }

                Thread.Sleep(50);
            }
            while (DateTime.UtcNow < rasterDeadline);

            if (lastRasterFailure != null)
            {
                throw lastRasterFailure;
            }

            Assert.Fail(
                description +
                " popup raster was not captured.");

            return bitmap;
        }

        private static void AssertPopupRaster(
            BitmapSource bitmap,
            string description)
        {
            var converted =
                new FormatConvertedBitmap(
                    bitmap,
                    PixelFormats.Bgra32,
                    null,
                    0);

            converted.Freeze();

            var stride =
                converted.PixelWidth * 4;

            var pixels =
                new byte[
                    stride *
                    converted.PixelHeight];

            converted.CopyPixels(
                pixels,
                stride,
                0);

            var opaquePixels = 0;
            var minimumLuminance = int.MaxValue;
            var maximumLuminance = int.MinValue;

            for (var index = 0;
                 index + 3 < pixels.Length;
                 index += 4)
            {
                var blue = pixels[index];
                var green = pixels[index + 1];
                var red = pixels[index + 2];
                var alpha = pixels[index + 3];

                if (alpha < 32)
                {
                    continue;
                }

                opaquePixels++;

                var luminance =
                    red +
                    green +
                    blue;

                minimumLuminance =
                    Math.Min(
                        minimumLuminance,
                        luminance);

                maximumLuminance =
                    Math.Max(
                        maximumLuminance,
                        luminance);
            }

            Assert.IsTrue(
                opaquePixels >
                    Math.Max(
                        16,
                        converted.PixelWidth *
                        converted.PixelHeight /
                        20),
                description +
                " raster is mostly transparent.");

            Assert.IsTrue(
                minimumLuminance != int.MaxValue &&
                maximumLuminance != int.MinValue &&
                maximumLuminance - minimumLuminance >= 48,
                description +
                " raster is visually flat and does not contain the popup surface/text.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int SourceCopy =
            0x00CC0020;

        private const int CaptureBlt =
            0x40000000;

        private const uint PrintWindowFullContent =
            0x00000002;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(
            IntPtr hWnd,
            out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(
            IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(
            IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(
            IntPtr hWnd,
            IntPtr hdcBlt,
            uint flags);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(
            IntPtr hWnd,
            IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(
            IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(
            IntPtr hDC,
            int width,
            int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(
            IntPtr hDC,
            IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(
            IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(
            IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr destinationDc,
            int x,
            int y,
            int width,
            int height,
            IntPtr sourceDc,
            int sourceX,
            int sourceY,
            int operation);

        private static void PrepareWindowForInteractiveCapture(
            MainWindow window,
            double width,
            double height)
        {
            window.Width = width;
            window.Height = height;
            window.Left = 24;
            window.Top = 24;
            window.ShowInTaskbar = false;
            window.Topmost = true;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Show();
            window.Activate();

            WaitForCondition(
                () =>
                    window.ActualWidth > 0 &&
                    window.ActualHeight > 0);

            PumpDispatcher();
            window.UpdateLayout();
        }

        private sealed class ControlGalleryBoard
        {
            private readonly IList<FrameworkElement> _enabledTargets;
            private readonly ToggleButton _toggle;
            private readonly ListBox _list;
            private readonly TabControl _tabs;
            private readonly ComboBox _combo;
            private readonly CheckBox _check;
            private readonly Expander _expander;

            public ControlGalleryBoard(
                FrameworkElement root,
                IDictionary<string, FrameworkElement> focusTargets,
                IDictionary<string, FrameworkElement> hoverTargets,
                IList<FrameworkElement> enabledTargets,
                ToggleButton toggle,
                ListBox list,
                TabControl tabs,
                ComboBox combo,
                CheckBox check,
                Expander expander)
            {
                Root = root;
                FocusTargets = focusTargets;
                HoverTargets = hoverTargets;
                _enabledTargets = enabledTargets;
                _toggle = toggle;
                _list = list;
                _tabs = tabs;
                _combo = combo;
                _check = check;
                _expander = expander;
            }

            public FrameworkElement Root { get; }
            public IDictionary<string, FrameworkElement> FocusTargets { get; }
            public IDictionary<string, FrameworkElement> HoverTargets { get; }

            public void BindGeneratedContainers()
            {
                var item =
                    _list.ItemContainerGenerator.ContainerFromIndex(0)
                    as ListBoxItem;

                Assert.IsNotNull(
                    item,
                    "Gallery ListBoxItem container is unavailable.");

                FocusTargets["list-item"] = item;
                HoverTargets["list-item"] = item;
            }

            public void SetAllEnabled(bool enabled)
            {
                foreach (var element in _enabledTargets)
                {
                    element.IsEnabled = enabled;
                }
            }

            public void SetSelectedExpanded()
            {
                _toggle.IsChecked = true;
                _list.SelectedIndex = 1;
                _tabs.SelectedIndex = 1;
                _combo.SelectedIndex = 1;
                _check.IsChecked = true;
                _expander.IsExpanded = true;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(
            int x,
            int y);

        private static void PrepareWindow(
            MainWindow window,
            double width,
            double height)
        {
            window.Width = width;
            window.Height = height;
            window.Left = -30000;
            window.Top = -30000;
            window.ShowInTaskbar = false;
            window.WindowStartupLocation =
                WindowStartupLocation.Manual;

            window.Show();

            WaitForCondition(
                () =>
                    window.ActualWidth > 0 &&
                    window.ActualHeight > 0);

            PumpDispatcher();
            window.UpdateLayout();
        }

        private static BitmapSource Capture(
            FrameworkElement element)
        {
            Assert.IsNotNull(
                element,
                "Gallery visual is unavailable.");

            element.UpdateLayout();

            var width =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        element.ActualWidth));

            var height =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        element.ActualHeight));

            var bitmap =
                new RenderTargetBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Pbgra32);

            bitmap.Render(element);
            bitmap.Freeze();

            return bitmap;
        }

        private static void SaveSideBySide(
            BitmapSource left,
            BitmapSource right,
            string path)
        {
            const int gap = 16;

            var width =
                left.PixelWidth +
                gap +
                right.PixelWidth;

            var height =
                Math.Max(
                    left.PixelHeight,
                    right.PixelHeight);

            var visual =
                new DrawingVisual();

            using (var drawing =
                visual.RenderOpen())
            {
                drawing.DrawRectangle(
                    Brushes.Transparent,
                    null,
                    new Rect(
                        0,
                        0,
                        width,
                        height));

                drawing.DrawImage(
                    left,
                    new Rect(
                        0,
                        0,
                        left.PixelWidth,
                        left.PixelHeight));

                drawing.DrawImage(
                    right,
                    new Rect(
                        left.PixelWidth + gap,
                        0,
                        right.PixelWidth,
                        right.PixelHeight));
            }

            var combined =
                new RenderTargetBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Pbgra32);

            combined.Render(visual);
            combined.Freeze();

            SaveBitmap(
                combined,
                path);
        }

        private static void SaveBitmap(
            BitmapSource bitmap,
            string path)
        {
            var encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(
                    bitmap));

            using (var stream =
                File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private static void CollectTextClipping(
            DependencyObject root,
            string scenario,
            IList<string> findings)
        {
            foreach (var element in
                VisualDescendants(root))
            {
                var text =
                    element as TextBlock;

                if (text != null &&
                    text.IsVisible &&
                    text.ActualWidth > 1 &&
                    text.TextWrapping ==
                        TextWrapping.NoWrap &&
                    !string.IsNullOrWhiteSpace(
                        text.Text) &&
                    NaturalTextWidth(text) >
                        text.ActualWidth + 2.0 &&
                    text.ToolTip == null)
                {
                    findings.Add(
                        scenario +
                        " — clipped TextBlock without full-value tooltip: \"" +
                        TrimForReport(
                            text.Text) +
                        "\" — UI_DESIGN_RULES §4");
                }

                var accessText =
                    element as AccessText;

                if (accessText != null &&
                    accessText.IsVisible &&
                    accessText.ActualWidth > 1 &&
                    !string.IsNullOrWhiteSpace(
                        accessText.Text) &&
                    NaturalTextWidth(accessText) >
                        accessText.ActualWidth + 2.0 &&
                    accessText.ToolTip == null)
                {
                    findings.Add(
                        scenario +
                        " — clipped control text without full-value tooltip: \"" +
                        TrimForReport(
                            accessText.Text) +
                        "\" — UI_DESIGN_RULES §4");
                }
            }
        }

        private static double NaturalTextWidth(
            TextBlock source)
        {
            var probe =
                new TextBlock
                {
                    Text =
                        source.Text,
                    FontFamily =
                        source.FontFamily,
                    FontStyle =
                        source.FontStyle,
                    FontWeight =
                        source.FontWeight,
                    FontStretch =
                        source.FontStretch,
                    FontSize =
                        source.FontSize,
                    FlowDirection =
                        source.FlowDirection,
                    TextWrapping =
                        TextWrapping.NoWrap,
                    TextTrimming =
                        TextTrimming.None
                };

            TextOptions.SetTextFormattingMode(
                probe,
                TextOptions.GetTextFormattingMode(
                    source));

            TextOptions.SetTextRenderingMode(
                probe,
                TextOptions.GetTextRenderingMode(
                    source));

            probe.Measure(
                new Size(
                    double.PositiveInfinity,
                    double.PositiveInfinity));

            return probe.DesiredSize.Width;
        }

        private static double NaturalTextWidth(
            AccessText source)
        {
            var probe =
                new AccessText
                {
                    Text =
                        source.Text,
                    FontFamily =
                        source.FontFamily,
                    FontStyle =
                        source.FontStyle,
                    FontWeight =
                        source.FontWeight,
                    FontStretch =
                        source.FontStretch,
                    FontSize =
                        source.FontSize,
                    FlowDirection =
                        source.FlowDirection
                };

            TextOptions.SetTextFormattingMode(
                probe,
                TextOptions.GetTextFormattingMode(
                    source));

            TextOptions.SetTextRenderingMode(
                probe,
                TextOptions.GetTextRenderingMode(
                    source));

            probe.Measure(
                new Size(
                    double.PositiveInfinity,
                    double.PositiveInfinity));

            return probe.DesiredSize.Width;
        }

        private static void CollectUnexpectedHorizontalScrollBars(
            DependencyObject root,
            string scenario,
            IList<string> findings)
        {
            foreach (var element in
                VisualDescendants(root))
            {
                var scrollBar =
                    element as ScrollBar;

                if (scrollBar == null ||
                    scrollBar.Orientation !=
                        Orientation.Horizontal ||
                    !scrollBar.IsVisible ||
                    scrollBar.ActualWidth <= 1.0 ||
                    scrollBar.ActualHeight <= 1.0)
                {
                    continue;
                }

                findings.Add(
                    scenario +
                    " — SNMP profile list shows a horizontal scrollbar although profile names must wrap — UI_DESIGN_RULES §5");
            }
        }

        private static IEnumerable<DependencyObject>
            VisualDescendants(
                DependencyObject root)
        {
            if (root == null)
            {
                yield break;
            }

            yield return root;

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

                foreach (var nested in
                    VisualDescendants(child))
                {
                    yield return nested;
                }
            }
        }

        private static string TrimForReport(
            string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var normalized =
                value.Replace(
                    Environment.NewLine,
                    " ");

            return normalized.Length <= 80
                ? normalized
                : normalized.Substring(
                    0,
                    77) +
                  "...";
        }

        private static TopologyRefreshSnapshot
            EmptySnapshot()
        {
            return InvokeFixtureSnapshot(
                "EmptySnapshot");
        }

        private static TopologyRefreshSnapshot
            SingleDeviceSnapshot(
                Guid deviceId,
                string name,
                string managementAddress)
        {
            return InvokeFixtureSnapshot(
                "Snapshot",
                deviceId,
                name,
                managementAddress);
        }

        private static TopologyRefreshSnapshot
            InspectorSnapshot(
                Guid siteId,
                Guid rackId,
                Guid deviceId,
                Guid interfaceId)
        {
            return InvokeFixtureSnapshot(
                "InspectorSnapshot",
                siteId,
                rackId,
                deviceId,
                interfaceId);
        }

        private static TopologyRefreshSnapshot
            CriticalLinkSnapshot(
                Guid firstId,
                Guid secondId,
                Guid physicalLinkId)
        {
            return InvokeFixtureSnapshot(
                "CriticalLinkSnapshot",
                firstId,
                secondId,
                physicalLinkId);
        }

        private static TopologyRefreshSnapshot
            Pass2InspectorSnapshot(
                out Guid firstId,
                out Guid secondId,
                out Guid firstInterfaceId,
                out Guid secondInterfaceId,
                out Guid physicalLinkId)
        {
            var method =
                FixtureMethod(
                    "Pass2InspectorSnapshot");

            var arguments =
                new object[]
                {
                    Guid.Empty,
                    Guid.Empty,
                    Guid.Empty,
                    Guid.Empty,
                    Guid.Empty
                };

            var snapshot =
                (TopologyRefreshSnapshot)method.Invoke(
                    null,
                    arguments);

            firstId =
                (Guid)arguments[0];

            secondId =
                (Guid)arguments[1];

            firstInterfaceId =
                (Guid)arguments[2];

            secondInterfaceId =
                (Guid)arguments[3];

            physicalLinkId =
                (Guid)arguments[4];

            return snapshot;
        }

        private static TopologyRefreshSnapshot
            InvokeFixtureSnapshot(
                string name,
                params object[] arguments)
        {
            return
                (TopologyRefreshSnapshot)
                FixtureMethod(name)
                    .Invoke(
                        null,
                        arguments);
        }

        private static MethodInfo FixtureMethod(
            string name)
        {
            var method =
                typeof(Sprint46ShellFoundationTests)
                    .GetMethod(
                        name,
                        BindingFlags.NonPublic |
                        BindingFlags.Static);

            Assert.IsNotNull(
                method,
                "Existing Sprint 46 production fixture method is unavailable: " +
                name);

            return method;
        }

        private static void SelectDevice(
            MainWindow window,
            Guid deviceId)
        {
            var border =
                DeviceBorder(
                    window,
                    deviceId);

            Assert.IsNotNull(border);

            RaisePrimaryClick(border);
        }

        private static Border DeviceBorder(
            MainWindow window,
            Guid deviceId)
        {
            var canvas =
                (Canvas)window.FindName(
                    "MapCanvas");

            return canvas.Children
                .OfType<Border>()
                .FirstOrDefault(
                    item =>
                        item.Tag is Guid &&
                        (Guid)item.Tag ==
                        deviceId);
        }

        private static Line FindLink(
            MainWindow window,
            Guid physicalLinkId)
        {
            var canvas =
                (Canvas)window.FindName(
                    "MapCanvas");

            return canvas.Children
                .OfType<Line>()
                .FirstOrDefault(
                    item =>
                        item.Tag is Guid &&
                        (Guid)item.Tag ==
                        physicalLinkId);
        }

        private static void SelectLink(
            MainWindow window,
            Guid physicalLinkId)
        {
            var link =
                FindLink(
                    window,
                    physicalLinkId);

            Assert.IsNotNull(link);

            RaisePrimaryClick(link);
        }

        private static Border LocationBorder(
            MainWindow window,
            Guid locationId)
        {
            var field =
                typeof(MainWindow)
                    .GetField(
                        "_locationVisualsById",
                        BindingFlags.NonPublic |
                        BindingFlags.Instance);

            Assert.IsNotNull(
                field,
                "MainWindow location visual collection is unavailable.");

            var dictionary =
                field.GetValue(window)
                    as System.Collections.IDictionary;

            Assert.IsNotNull(
                dictionary,
                "MainWindow location visual collection is unavailable.");

            var visual =
                dictionary[locationId];

            if (visual == null)
            {
                return null;
            }

            var property =
                visual.GetType()
                    .GetProperty(
                        "Border",
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Instance);

            Assert.IsNotNull(
                property,
                "MapLocationVisual.Border is unavailable.");

            return property.GetValue(
                    visual,
                    null)
                as Border;
        }

        private static void SelectLocation(
            MainWindow window,
            Guid locationId)
        {
            var border =
                LocationBorder(
                    window,
                    locationId);

            Assert.IsNotNull(border);

            RaisePrimaryClick(border);
        }

        private static void RaisePrimaryClick(
            UIElement element)
        {
            element.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonDownEvent,
                    Source =
                        element
                });

            element.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent =
                        UIElement.MouseLeftButtonUpEvent,
                    Source =
                        element
                });

            PumpDispatcher();
        }

        private static void Click(
            Button button)
        {
            Assert.IsNotNull(button);

            Assert.IsTrue(
                button.IsEnabled,
                "Gallery interaction requires the production button to be enabled.");

            button.RaiseEvent(
                new RoutedEventArgs(
                    Button.ClickEvent));

            PumpDispatcher();
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
                        "The expected WPF gallery state was not reached.");
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

            Dispatcher.CurrentDispatcher
                .BeginInvoke(
                    DispatcherPriority.Background,
                    new DispatcherOperationCallback(
                        value =>
                        {
                            ((DispatcherFrame)value)
                                .Continue =
                                false;

                            return null;
                        }),
                    frame);

            Dispatcher.PushFrame(frame);
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
                    TimeSpan.FromSeconds(240)))
            {
                throw new AssertFailedException(
                    "STA state-gallery test did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        private static string ResolveOutputDirectory()
        {
            var overridePath =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_UI_GALLERY_DIR");

            if (!string.IsNullOrWhiteSpace(
                    overridePath))
            {
                return overridePath;
            }

            var directory =
                new DirectoryInfo(
                    AppDomain.CurrentDomain
                        .BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(
                    System.IO.Path.Combine(
                        directory.FullName,
                        "NetLoom.sln")))
                {
                    return System.IO.Path.Combine(
                        directory.FullName,
                        "artifacts",
                        "ui-state-gallery",
                        "sprint46-pass3");
                }

                directory =
                    directory.Parent;
            }

            return System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "NetLoom-S46-Pass3-StateGallery");
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
                    snapshot ??
                    throw new ArgumentNullException(
                        nameof(snapshot));
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

        private sealed class GalleryMonitoringControl :
            IMonitoringControl
        {
            private readonly MonitoringControlSnapshot
                _current =
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Stopped,
                        null,
                        null,
                        null);

            public MonitoringControlSnapshot Current =>
                _current;

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged;

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
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

            public Task PollNowAsync(
                MonitoringTarget targetWhenStopped,
                MonitoringSessionPolicy policyWhenStopped,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }
    }
}
