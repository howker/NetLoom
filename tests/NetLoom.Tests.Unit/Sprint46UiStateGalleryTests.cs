using Path = System.Windows.Shapes.Path;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

                    RenderPopupScenario(
                        outputDirectory,
                        findings);

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
                            "Covered now: empty shell/inspector, device card + device inspector, stale Critical link inspector, Location inspector, Alerts/event strip, Manual topology editor, Location editor and SNMP profile settings.",
                            "Text risks: long Russian device name, unbroken 60+ character tail, Й/Ё.",
                            "Automatic scan: no-wrap WPF text whose natural measured width exceeds its arranged width without a tooltip (§4), plus actual visible horizontal-scrollbar layout in the SNMP profile list (§5).",
                            "Production interaction exercised without raster acceptance: ComboBox popup and map ContextMenu are opened through their real controls in both themes.",
                            "Operator acceptance required: ComboBox popup, ContextMenu and ToolTip visual theming in both themes. Two separate-HWND raster attempts produced transparent/partial PNG evidence, so they are explicitly not accepted as automated visual coverage (§10).",
                            "Also not automated by this PNG gallery: hover timing, Windows 150–200% scaling, full keyboard-only journey, UI Automation tree inspection, 24x24 hit-target proof, system 'Show animations' setting, theme-transition animation suppression and selectable/copyable IP/MAC. These remain explicit Pass 3 verification items under §§8–10."
                        });

                    if (findings.Count > 0)
                    {
                        Assert.Fail(
                            "State gallery found " +
                            findings.Count +
                            " visual breakage item(s). Review: " +
                            reportPath);
                    }
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
                    PrepareWindow(
                        window,
                        NormalWidth,
                        GalleryHeight);

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

                    Assert.IsTrue(
                        popup.Child.IsVisible,
                        "Production ComboBox popup child is not visible.");

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

                    Assert.AreEqual(
                        Visibility.Visible,
                        settingsButton.Visibility,
                        "Production map settings button is not visible.");

                    var menu =
                        settingsButton.ContextMenu;

                    Assert.IsNotNull(
                        menu,
                        "Production map ContextMenu is unavailable.");

                    Assert.IsTrue(
                        menu.Items.Count > 0,
                        "Production map ContextMenu has no items.");

                    Click(settingsButton);

                    WaitForCondition(
                        () =>
                            menu.IsOpen);

                    Assert.IsTrue(
                        menu.IsOpen,
                        "Production map ContextMenu did not open.");

                    menu.IsOpen = false;
                    PumpDispatcher();

                    Assert.IsNotNull(
                        settingsButton.ToolTip,
                        "Production map settings ToolTip is unavailable.");
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            }
        }

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
                    TimeSpan.FromSeconds(90)))
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
