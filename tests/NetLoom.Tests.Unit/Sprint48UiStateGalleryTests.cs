using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Snmp;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Discovery;

namespace NetLoom.Tests.Unit
{
    // Sprint 48, галерея: итог запуска обнаружения в блоке «Последний запуск» (UI_DESIGN_RULES §9).
    // Оси: состояние запуска (завершён, остановлен), длина значений (много адресов),
    // Ширина (минимальная и обычная), обе темы.
    public sealed partial class Sprint46UiStateGalleryTests
    {
        [TestMethod]
        public void DiscoveryRunSummaryGalleryCoversFinishedRuns()
        {
            RunOnSta(
                () =>
                {
                    var outputDirectory =
                        Path.Combine(
                            Path.GetDirectoryName(
                                ResolveOutputDirectory()),
                            "sprint48-discovery");

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

                    var profile =
                        new AccessProfile(
                            Guid.Parse(
                                "48484848-0001-0001-0001-484848484848"),
                            "Площадка А",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var scenarios =
                        new[]
                        {
                            Tuple.Create(
                                "30-discovery-run-completed",
                                DiscoveryControlState.Completed,
                                254,
                                37,
                                31,
                                new TimeSpan(0, 4, 37)),
                            Tuple.Create(
                                "31-discovery-run-stopped",
                                DiscoveryControlState.Stopped,
                                120,
                                12,
                                9,
                                new TimeSpan(0, 2, 8))
                        };

                    foreach (var item in scenarios)
                    {
                        var scenario =
                            item.Item1;

                        foreach (var dark in new[] { false, true })
                        {
                            var theme =
                                dark ? "dark" : "light";

                            var bitmaps =
                                new List<BitmapSource>();

                            foreach (var width in
                                new[] { NarrowWidth, NormalWidth })
                            {
                                var discoveryControl =
                                    new GalleryDiscoveryControl();

                                var materializer =
                                    new GalleryCandidateMaterializer();

                                var window =
                                    new MainWindow(
                                        new FixedRefreshProvider(
                                            EmptySnapshot()),
                                        new EmptyLookupReader(),
                                        new GalleryMonitoringControl(),
                                        discoveryControl,
                                        new[] { profile },
                                        materializer);

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
                                            "ShellDiscoveryButton"));

                                    // Диапазон в форме согласован с адресами найденных устройств.
                                    ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "10.48.228.1";
                                    ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text = "10.48.228.254";

                                    var runClock =
                                        new DateTime(2026, 10, 7, 9, 12, 5, DateTimeKind.Utc);
                                    window.DiscoveryRunClock =
                                        () => runClock;

                                    ((ComboBox)window.FindName("DiscoveryProfileComboBox")).SelectedIndex = 0;
                                    Click((Button)window.FindName("DiscoveryStartButton"));

                                    discoveryControl.PublishState(
                                        DiscoveryControlState.Running,
                                        0,
                                        254,
                                        0);

                                    for (var i = 1; i <= item.Item4; i++)
                                    {
                                        discoveryControl.EmitCandidate(
                                            new DiscoveryCandidateSnapshot(
                                                IPAddress.Parse(
                                                    "10.48.228." + (100 + i)),
                                                profile.Id,
                                                true,
                                                i <= item.Item5,
                                                new[] { 22 },
                                                "sw-" + i,
                                                "Synthetic device",
                                                null,
                                                null,
                                                8));
                                    }

                                    PumpDispatcher();

                                    runClock =
                                        runClock + item.Item6;

                                    discoveryControl.PublishState(
                                        item.Item2,
                                        item.Item3,
                                        254,
                                        item.Item4);

                                    PumpDispatcher();
                                    window.UpdateLayout();

                                    var frameScenario =
                                        scenario + "/" + theme + "/" + width;

                                    var panel =
                                        (StackPanel)window.FindName(
                                            "DiscoveryRunSummaryPanel");

                                    if (panel.Visibility != Visibility.Visible)
                                    {
                                        findings.Add(
                                            frameScenario + ": блок «Последний запуск» не виден после завершения запуска (§9).");
                                    }

                                    Assert.AreEqual(
                                        Visibility.Visible,
                                        panel.Visibility,
                                        frameScenario + ": the last discovery run summary must be visible.");

                                    var expectedDuration =
                                        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
                                            ? (item.Item2 == DiscoveryControlState.Completed
                                                ? "4 мин 37 с"
                                                : "2 мин 8 с")
                                            : (item.Item2 == DiscoveryControlState.Completed
                                                ? "4 min 37 s"
                                                : "2 min 8 s");
                                    var durationText =
                                        (TextBlock)window.FindName(
                                            "DiscoveryRunDurationValueText");

                                    if (durationText.Text != expectedDuration)
                                    {
                                        findings.Add(
                                            frameScenario + ": длительность запуска не совпадает с ожидаемой (§9): «" +
                                            durationText.Text + "» вместо «" + expectedDuration + "».");
                                    }

                                    CollectTextClipping(
                                        window.Content as DependencyObject,
                                        frameScenario,
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
                                Path.Combine(
                                    outputDirectory,
                                    scenario + "-" + theme + ".png"));
                        }
                    }

                    File.WriteAllLines(
                        Path.Combine(
                            outputDirectory,
                            "findings.txt"),
                        findings.Count == 0
                            ? new[] { "Находок нет." }
                            : findings.ToArray());

                    // Два сценария в светлой и тёмной темах; узкий и обычный варианты рядом.
                    Assert.AreEqual(
                        4,
                        Directory.GetFiles(
                            outputDirectory,
                            "*.png").Length,
                        "Every Sprint 48 gallery frame must be produced.");
                });
        }

        [TestMethod]
        public void DiscoveryRunErrorsGalleryShowsReasons()
        {
            RunOnSta(
                () =>
                {
                    var outputDirectory =
                        Path.Combine(
                            Path.GetDirectoryName(
                                ResolveOutputDirectory()),
                            "sprint48-errors");

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

                    var profile =
                        new AccessProfile(
                            Guid.Parse(
                                "48484848-0001-0001-0001-484848484848"),
                            "Площадка А",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var scenarios =
                        new[]
                        {
                            Tuple.Create(
                                "34-discovery-run-errors",
                                DiscoveryControlState.Completed,
                                254,
                                37,
                                31,
                                new TimeSpan(0, 4, 37))
                        };

                    foreach (var item in scenarios)
                    {
                        var scenario =
                            item.Item1;

                        foreach (var dark in new[] { false, true })
                        {
                            var theme =
                                dark ? "dark" : "light";

                            var bitmaps =
                                new List<BitmapSource>();

                            foreach (var width in
                                new[] { NarrowWidth, NormalWidth })
                            {
                                var discoveryControl =
                                    new GalleryDiscoveryControl();

                                var materializer =
                                    new GalleryCandidateMaterializer();

                                var window =
                                    new MainWindow(
                                        new FixedRefreshProvider(
                                            EmptySnapshot()),
                                        new EmptyLookupReader(),
                                        new GalleryMonitoringControl(),
                                        discoveryControl,
                                        new[] { profile },
                                        materializer);

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
                                            "ShellDiscoveryButton"));

                                    // Диапазон в форме согласован с адресами найденных устройств.
                                    ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "10.48.228.1";
                                    ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text = "10.48.228.254";

                                    var runClock =
                                        new DateTime(2026, 10, 7, 9, 12, 5, DateTimeKind.Utc);
                                    window.DiscoveryRunClock =
                                        () => runClock;

                                    ((ComboBox)window.FindName("DiscoveryProfileComboBox")).SelectedIndex = 0;
                                    Click((Button)window.FindName("DiscoveryStartButton"));

                                    discoveryControl.PublishState(
                                        DiscoveryControlState.Running,
                                        0,
                                        254,
                                        0,
                                        accessProfileId: profile.Id);

                                    for (var i = 1; i <= item.Item5; i++)
                                    {
                                        discoveryControl.EmitCandidate(
                                            new DiscoveryCandidateSnapshot(
                                                IPAddress.Parse(
                                                    "10.48.228." + (100 + i)),
                                                profile.Id,
                                                true,
                                                i <= item.Item5,
                                                new[] { 22 },
                                                "sw-" + i,
                                                "Synthetic device",
                                                null,
                                                null,
                                                8));
                                    }

                                    PumpDispatcher();

                                    runClock =
                                        runClock + item.Item6;

                                    var failures = new[]
                                    {
                                        SnmpTransportFailure.Authentication,
                                        SnmpTransportFailure.Authentication,
                                        SnmpTransportFailure.Authentication,
                                        SnmpTransportFailure.Timeout,
                                        SnmpTransportFailure.Timeout,
                                        SnmpTransportFailure.Protocol
                                    };

                                    for (var i = 0; i < failures.Length; i++)
                                    {
                                        discoveryControl.EmitCandidate(
                                            new DiscoveryCandidateSnapshot(
                                                IPAddress.Parse("10.48.228." + (10 + i)),
                                                null,
                                                true,
                                                false,
                                                new int[0],
                                                null,
                                                null,
                                                null,
                                                null,
                                                0,
                                                failures[i]));
                                    }

                                    PumpDispatcher();

                                    discoveryControl.PublishState(
                                        item.Item2,
                                        item.Item3,
                                        254,
                                        item.Item4);

                                    PumpDispatcher();
                                    window.UpdateLayout();

                                    var frameScenario =
                                        scenario + "/" + theme + "/" + width;

                                    var panel =
                                        (StackPanel)window.FindName(
                                            "DiscoveryRunSummaryPanel");

                                    if (panel.Visibility != Visibility.Visible)
                                    {
                                        findings.Add(
                                            frameScenario + ": блок «Последний запуск» не виден после завершения запуска (§9).");
                                    }

                                    Assert.AreEqual(
                                        Visibility.Visible,
                                        panel.Visibility,
                                        frameScenario + ": the last discovery run summary must be visible.");

                                    var errorsText =
                                        (TextBlock)window.FindName("DiscoveryRunErrorsValueText");

                                    if (errorsText.Text != "6")
                                    {
                                        findings.Add(
                                            frameScenario + ": число ошибок не равно 6 (§9): «" +
                                            errorsText.Text + "».");
                                    }

                                    var rows = ((ItemsControl)window.FindName("DiscoveryInboxGroupsList"))
                                        .Items.Cast<DiscoveryInboxGroup>().SelectMany(group => group.Rows).ToArray();

                                    for (var i = 0; i < failures.Length; i++)
                                    {
                                        var address = "10.48.228." + (10 + i);
                                        var row = rows.Single(candidate =>
                                            candidate.Address == address);
                                        var errorKey = failures[i] == SnmpTransportFailure.Authentication
                                            ? "DiscoveryErrorSnmpAuthentication"
                                            : failures[i] == SnmpTransportFailure.Timeout
                                                ? "DiscoveryErrorSnmpTimeout"
                                                : "DiscoveryErrorSnmpProtocol";
                                        var expectedSummary = UiText.Format(
                                            "DiscoveryCandidateErrorSummary",
                                            UiText.Get("DiscoveryUnnamedCandidate"),
                                            UiText.Get(errorKey),
                                            profile.Name,
                                            runClock.ToLocalTime().ToString("t", CultureInfo.CurrentCulture));
                                        var actualSummary = row.Reason;

                                        if (actualSummary != expectedSummary)
                                        {
                                            findings.Add(
                                                frameScenario + ": причина ошибки адреса " + address +
                                                " не совпадает с ожидаемой (§9): «" + actualSummary + "».");
                                        }
                                    }

                                    var expectedDuration =
                                        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
                                            ? (item.Item2 == DiscoveryControlState.Completed
                                                ? "4 мин 37 с"
                                                : "2 мин 8 с")
                                            : (item.Item2 == DiscoveryControlState.Completed
                                                ? "4 min 37 s"
                                                : "2 min 8 s");
                                    var durationText =
                                        (TextBlock)window.FindName(
                                            "DiscoveryRunDurationValueText");

                                    if (durationText.Text != expectedDuration)
                                    {
                                        findings.Add(
                                            frameScenario + ": длительность запуска не совпадает с ожидаемой (§9): «" +
                                            durationText.Text + "» вместо «" + expectedDuration + "».");
                                    }

                                    CollectTextClipping(
                                        window.Content as DependencyObject,
                                        frameScenario,
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
                                Path.Combine(
                                    outputDirectory,
                                    scenario + "-" + theme + ".png"));
                        }
                    }

                    File.WriteAllLines(
                        Path.Combine(
                            outputDirectory,
                            "findings.txt"),
                        findings.Count == 0
                            ? new[] { "Находок нет." }
                            : findings.ToArray());

                    // Один сценарий ошибок в светлой и тёмной темах; узкий и обычный варианты рядом.
                    Assert.AreEqual(
                        2,
                        Directory.GetFiles(
                            outputDirectory,
                            "*.png").Length,
                        "Every discovery error gallery frame must be produced.");
                });
        }

        [TestMethod]
        public void DiscoveryRunningGalleryShowsCurrentPhase()
        {
            RunOnSta(
                () =>
                {
                    var outputDirectory =
                        Path.Combine(
                            Path.GetDirectoryName(
                                ResolveOutputDirectory()),
                            "sprint48-phases");

                    Directory.CreateDirectory(
                        outputDirectory);

                    foreach (var file in
                        Directory.GetFiles(
                            outputDirectory,
                            "*.png"))
                    {
                        File.Delete(file);
                    }

                    var findings = new List<string>();
                    var profile = new AccessProfile(
                        Guid.Parse(
                            "48484848-0001-0001-0001-484848484848"),
                        "Площадка А",
                        true,
                        SnmpVersion.V2C,
                        null);
                    var scenario = "33-discovery-running-snmp";

                    foreach (var dark in new[] { false, true })
                    {
                        var theme = dark ? "dark" : "light";
                        var bitmaps = new List<BitmapSource>();

                        foreach (var width in
                            new[] { NarrowWidth, NormalWidth })
                        {
                            var discoveryControl = new GalleryDiscoveryControl();
                            var window = new MainWindow(
                                new FixedRefreshProvider(
                                    EmptySnapshot()),
                                new EmptyLookupReader(),
                                new GalleryMonitoringControl(),
                                discoveryControl,
                                new[] { profile },
                                new GalleryCandidateMaterializer());

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
                                        "ShellDiscoveryButton"));

                                // Диапазон и найденные адреса согласованы с текущим адресом кадра.
                                ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "10.48.228.1";
                                ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text = "10.48.228.254";

                                discoveryControl.PublishState(
                                    DiscoveryControlState.Running,
                                    0,
                                    254,
                                    0);

                                for (var i = 1; i <= 23; i++)
                                {
                                    discoveryControl.EmitCandidate(
                                        new DiscoveryCandidateSnapshot(
                                            IPAddress.Parse(
                                                "10.48.228." + (i * 5)),
                                            profile.Id,
                                            true,
                                            true,
                                            new[] { 22 },
                                            "sw-" + i,
                                            "Synthetic device",
                                            null,
                                            null,
                                            8));
                                }

                                PumpDispatcher();

                                discoveryControl.PublishState(
                                    DiscoveryControlState.Running,
                                    117,
                                    254,
                                    23,
                                    IPAddress.Parse("10.48.228.118"),
                                    DiscoveryPhase.Snmp,
                                    3,
                                    3);

                                PumpDispatcher();
                                window.UpdateLayout();

                                var frameScenario =
                                    scenario + "/" + theme + "/" + width;
                                var phaseText =
                                    (TextBlock)window.FindName(
                                        "DiscoveryPhaseValueText");
                                var expectedPhase =
                                    UiText.Format(
                                        "DiscoveryPhaseValue",
                                        "SNMP",
                                        3,
                                        3);

                                if (phaseText.Text != expectedPhase)
                                {
                                    findings.Add(
                                        frameScenario + ": этап обнаружения не совпадает с ожидаемым (§9): «" +
                                        phaseText.Text + "» вместо «" + expectedPhase + "».");
                                }

                                var addressText =
                                    (TextBlock)window.FindName(
                                        "DiscoveryCurrentAddressValueText");

                                if (addressText.Text != "10.48.228.118")
                                {
                                    findings.Add(
                                        frameScenario + ": текущий адрес не совпадает с ожидаемым (§9): «" +
                                        addressText.Text + "» вместо «10.48.228.118».");
                                }

                                CollectTextClipping(
                                    window.Content as DependencyObject,
                                    frameScenario,
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
                            Path.Combine(
                                outputDirectory,
                                scenario + "-" + theme + ".png"));
                    }

                    File.WriteAllLines(
                        Path.Combine(
                            outputDirectory,
                            "findings.txt"),
                        findings.Count == 0
                            ? new[] { "Находок нет." }
                            : findings.ToArray());

                    // Один сценарий в двух темах; узкий и обычный варианты рядом.
                    Assert.AreEqual(
                        2,
                        Directory.GetFiles(
                            outputDirectory,
                            "*.png").Length,
                        "Every discovery phase gallery frame must be produced.");
                });
        }

        [TestMethod]
        public void OneSidedLldpLinkGalleryShowsEvidenceGap()
        {
            RunOnSta(
                () =>
                {
                    var outputDirectory = Path.Combine(
                        Path.GetDirectoryName(ResolveOutputDirectory()),
                        "sprint48-g4");
                    Directory.CreateDirectory(outputDirectory);

                    foreach (var file in Directory.GetFiles(outputDirectory, "*.png"))
                    {
                        File.Delete(file);
                    }

                    var findings = new List<string>();
                    var snapshot = Sprint48OneSidedLldpTests.OneSidedSnapshot(
                        "kb-sw-03", "kb-sw-04");
                    var linkId = snapshot.DiagnosticSnapshot.Links.Single().PhysicalLinkId;

                    RenderShellScenario(
                        outputDirectory,
                        "32-link-one-sided-lldp",
                        () => snapshot,
                        window =>
                        {
                            WaitForCondition(() => FindLink(window, linkId) != null);
                            SelectLink(window, linkId);
                            ((TabControl)window.FindName("InspectorTabControl")).SelectedItem =
                                window.FindName("InspectorEvidenceTab");
                            PumpDispatcher();
                        },
                        findings);

                    File.WriteAllLines(
                        Path.Combine(outputDirectory, "findings.txt"),
                        findings.Count == 0
                            ? new[] { "Находок нет." }
                            : findings.ToArray());

                    // Один сценарий в двух темах; узкий и обычный варианты рядом.
                    Assert.AreEqual(
                        2,
                        Directory.GetFiles(outputDirectory, "*.png").Length,
                        "Every one-sided LLDP gallery frame must be produced.");
                });
        }

        [TestMethod]
        public void UnconfirmedDeviceGalleryShowsMarkOnMapAndEquipment()
        {
            RunOnSta(() =>
            {
                var outputDirectory = Path.Combine(
                    Path.GetDirectoryName(ResolveOutputDirectory()), "sprint48-unconfirmed");
                Directory.CreateDirectory(outputDirectory);
                foreach (var file in Directory.GetFiles(outputDirectory, "*.png"))
                {
                    File.Delete(file);
                }

                var findings = new List<string>();
                var snapshot = Sprint48DeviceConfirmationFixture.Snapshot();
                var deviceId = snapshot.MapSnapshot.Nodes.Single(node => node.IsUnconfirmed).DeviceId.Value;

                RenderShellScenario(
                    outputDirectory,
                    "35-unconfirmed-device-map",
                    () => snapshot,
                    window =>
                    {
                        WaitForCondition(() => DeviceBorder(window, deviceId) != null);
                        SelectDevice(window, deviceId);
                        PumpDispatcher();
                        var card = DeviceBorder(window, deviceId);
                        Assert.IsTrue(VisualDescendants(card).OfType<TextBlock>().Any(
                            text => text.IsVisible && text.Text == UiText.Get("DeviceUnconfirmedMark")));
                        var fields = (ItemsControl)window.FindName("DiagnosticFieldsList");
                        Assert.IsTrue(fields.Items.Cast<object>().Any(item =>
                            (string)item.GetType().GetProperty("Label").GetValue(item) ==
                                UiText.Get("DiagnosticFieldConfirmation")));
                    },
                    findings);

                RenderShellScenario(
                    outputDirectory,
                    "36-unconfirmed-device-equipment",
                    () => snapshot,
                    window =>
                    {
                        Click((Button)window.FindName("ShellEquipmentButton"));
                        PumpDispatcher();
                        var rows = (ItemsControl)window.FindName("EquipmentList");
                        Assert.AreEqual(4, rows.Items.Count);
                        Assert.IsTrue(rows.Items.Cast<object>().Any(item =>
                            (string)item.GetType().GetProperty("UnconfirmedText").GetValue(item) ==
                                UiText.Get("DeviceUnconfirmedMark")));
                    },
                    findings);

                File.WriteAllLines(Path.Combine(outputDirectory, "findings.txt"),
                    findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray());
                // Два сценария в двух темах; узкий и обычный варианты рядом.
                Assert.AreEqual(4, Directory.GetFiles(outputDirectory, "*.png").Length,
                    "Every unconfirmed device gallery frame must be produced.");
            });
        }

        [TestMethod]
        public void DiscoveryInboxGalleryCoversEveryGroup()
        {
            RunOnSta(() =>
            {
                var outputDirectory = Path.Combine(Path.GetDirectoryName(ResolveOutputDirectory()), "sprint48-inbox");
                Directory.CreateDirectory(outputDirectory);
                foreach (var file in Directory.GetFiles(outputDirectory, "*.png")) File.Delete(file);
                var findings = new List<string>();
                // Оси: все группы и причины, полнота, число строк, прокрутка, ширины и темы.
                // Наведение и выделение не меняют содержание строк; здесь отдельно не рисуются.
                foreach (var scenario in new[] { "37-inbox-all-groups", "38-inbox-scrolled-errors" })
                foreach (var dark in new[] { false, true })
                {
                    var theme = dark ? "dark" : "light";
                    var bitmaps = new List<BitmapSource>();
                    foreach (var width in new[] { NarrowWidth, NormalWidth })
                    {
                        var data = new Sprint48DiscoveryInboxFixture();
                        var journal = new DiscoveryRunJournal(data.Repository(), new EmptyDiscoveryTopologyReader(),
                            new GalleryCandidateMaterializer(), new EmptyDiscoveryExclusionSource());
                        var window = new MainWindow(new FixedRefreshProvider(EmptySnapshot()),
                            new EmptyLookupReader(), new GalleryMonitoringControl(),
                            new GalleryDiscoveryControl(), new[] { data.Profile }, journal);
                        window.DiscoveryRunClock = () => data.Now;
                        try
                        {
                            PrepareWindow(window, width, GalleryHeight);
                            if (dark) Click((Button)window.FindName("ShellThemeButton"));
                            Click((Button)window.FindName("ShellDiscoveryButton"));
                            ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "10.48.228.1";
                            ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text = "10.48.228.254";
                            PumpDispatcher();
                            window.UpdateLayout();
                            if (scenario == "38-inbox-scrolled-errors")
                            {
                                ((ScrollViewer)window.FindName("DiscoveryResultsPanel")).ScrollToEnd();
                                PumpDispatcher();
                                window.UpdateLayout();
                            }
                            var frame = scenario + "/" + theme + "/" + width;
                            var groups = ((ItemsControl)window.FindName("DiscoveryInboxGroupsList"))
                                .Items.Cast<DiscoveryInboxGroup>().Select(group => group.Group).ToArray();
                            if (!groups.SequenceEqual(Sprint48DiscoveryInboxFixture.Order))
                                findings.Add(frame + ": во входящих нет шести групп в нужном порядке (§9).");
                            CollectionAssert.AreEqual(Sprint48DiscoveryInboxFixture.Order, groups, frame);
                            CollectTextClipping(window.Content as DependencyObject, frame, findings);
                            bitmaps.Add(Capture(window.Content as FrameworkElement));
                        }
                        finally
                        {
                            window.Close();
                            PumpDispatcher();
                        }
                    }
                    SaveSideBySide(bitmaps[0], bitmaps[1],
                        Path.Combine(outputDirectory, scenario + "-" + theme + ".png"));
                }
                File.WriteAllLines(Path.Combine(outputDirectory, "findings.txt"),
                    findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray());
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(4, Directory.GetFiles(outputDirectory, "*.png").Length);
            });
        }

        [TestMethod]
        public void DiscoveryInboxBulkActionsGallery()
        {
            RunOnSta(() =>
            {
                // Обработчик действия делает await; тест нажимает кнопки из своего потока, поэтому ставит контекст диспетчера.
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext());
                var outputDirectory = Path.Combine(Path.GetDirectoryName(ResolveOutputDirectory()), "sprint48-inbox-actions");
                Directory.CreateDirectory(outputDirectory);
                foreach (var file in Directory.GetFiles(outputDirectory, "*.png")) File.Delete(file);
                var findings = new List<string>();
                // Оси: массовый выбор, результат принятия, диалог размещения, обе ширины и темы.
                // Ошибки запуска и наведение покрыты другими кадрами; здесь используются данные кадра 37.
                foreach (var scenario in new[] { "39-inbox-selection", "40-inbox-after-accept", "41-placement-dialog" })
                foreach (var dark in new[] { false, true })
                {
                    var theme = dark ? "dark" : "light";
                    var bitmaps = new List<BitmapSource>();
                    foreach (var width in new[] { NarrowWidth, NormalWidth })
                    {
                        var data = new Sprint48DiscoveryInboxFixture();
                        var repository = Sprint48InboxActionFixture.Repository(data);
                        var journal = new DiscoveryRunJournal(repository, new EmptyDiscoveryTopologyReader(),
                            new GalleryCandidateMaterializer(), new EmptyDiscoveryExclusionSource());
                        var window = new MainWindow(new FixedRefreshProvider(EmptySnapshot()), new EmptyLookupReader(),
                            new GalleryMonitoringControl(), new GalleryDiscoveryControl(), new[] { data.Profile }, journal);
                        var now = new DateTime(2026, 10, 7, 9, 21, 0, DateTimeKind.Local).ToUniversalTime();
                        window.DiscoveryRunClock = () => now;
                        window.DiscoveryInboxActions = new Sprint48RecordingInboxActions(repository);
                        Window dialog = null;
                        try
                        {
                            PrepareWindow(window, width, GalleryHeight);
                            if (dark) Click((Button)window.FindName("ShellThemeButton"));
                            Click((Button)window.FindName("ShellDiscoveryButton"));
                            ((TextBox)window.FindName("DiscoveryStartAddressTextBox")).Text = "10.48.228.1";
                            ((TextBox)window.FindName("DiscoveryEndAddressTextBox")).Text = "10.48.228.254";
                            PumpDispatcher();
                            var groups = ((ItemsControl)window.FindName("DiscoveryInboxGroupsList")).Items.Cast<DiscoveryInboxGroup>().ToArray();
                            var selected = groups.Single(group => group.Group == DiscoveryResultGroup.New).Rows.Take(3)
                                .Concat(groups.Single(group => group.Group == DiscoveryResultGroup.Changed).Rows.Take(1)).ToArray();
                            foreach (var row in selected) row.IsSelected = true;
                            ((Expander)window.FindName("DiscoveryExpander")).IsExpanded = false;
                            Assert.AreEqual(UiText.Format("DiscoveryInboxSelectedCount", 4),
                                ((TextBlock)window.FindName("DiscoveryInboxSelectedText")).Text);
                            var frame = scenario + "/" + theme + "/" + width;
                            if (scenario == "40-inbox-after-accept")
                            {
                                Click((Button)window.FindName("DiscoveryInboxAcceptButton"));
                                WaitForCondition(() => ((TextBlock)window.FindName("DiscoveryMessageText")).Text ==
                                    UiText.Format("DiscoveryInboxAppliedAccept", 4));
                                var rows = ((ItemsControl)window.FindName("DiscoveryInboxGroupsList")).Items
                                    .Cast<DiscoveryInboxGroup>().SelectMany(group => group.Rows).ToArray();
                                Assert.IsTrue(rows.Where(row => selected.Any(item => item.Address == row.Address))
                                    .All(row => row.ResolutionText == UiText.Format("DiscoveryInboxResolutionAccepted", "09:21")));
                            }
                            if (scenario == "41-placement-dialog")
                            {
                                dialog = window.CreateDiscoveryInboxPlacementDialog(new Sprint48InboxLocations().GetSnapshot());
                                dialog.Show();
                                PumpDispatcher();
                                dialog.UpdateLayout();
                                var placements = VisualDescendants(dialog.Content as DependencyObject).OfType<ListBox>().Single();
                                Assert.AreEqual(3, placements.Items.Count);
                                CollectTextClipping(dialog.Content as DependencyObject, frame, findings);
                                bitmaps.Add(Capture(dialog.Content as FrameworkElement));
                            }
                            else
                            {
                                ((ScrollViewer)window.FindName("DiscoveryResultsPanel")).ScrollToTop();
                                PumpDispatcher();
                                window.UpdateLayout();
                                CollectTextClipping(window.Content as DependencyObject, frame, findings);
                                bitmaps.Add(Capture(window.Content as FrameworkElement));
                            }
                        }
                        finally
                        {
                            dialog?.Close();
                            window.Close();
                            PumpDispatcher();
                        }
                    }
                    SaveSideBySide(bitmaps[0], bitmaps[1], Path.Combine(outputDirectory, scenario + "-" + theme + ".png"));
                }
                File.WriteAllLines(Path.Combine(outputDirectory, "findings.txt"),
                    findings.Count == 0 ? new[] { "Находок нет." } : findings.ToArray());
                Assert.AreEqual(0, findings.Count, string.Join(Environment.NewLine, findings));
                Assert.AreEqual(6, Directory.GetFiles(outputDirectory, "*.png").Length);
            });
        }

        private sealed class GalleryDiscoveryControl :
            IDiscoveryControl
        {
            private DiscoveryControlSnapshot _current =
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

            public void PublishState(
                DiscoveryControlState state,
                int processed,
                int total,
                int found,
                IPAddress currentAddress = null,
                DiscoveryPhase? phase = null,
                int step = 0,
                int steps = 0,
                Guid? accessProfileId = null)
            {
                _current =
                    new DiscoveryControlSnapshot(
                        state,
                        null,
                        accessProfileId,
                        processed,
                        total,
                        found,
                        currentAddress,
                        null,
                        phase,
                        step,
                        steps);

                SnapshotChanged?.Invoke(
                    this,
                    new DiscoveryControlSnapshotChangedEventArgs(
                        _current));
            }

            public void EmitCandidate(
                DiscoveryCandidateSnapshot candidate)
            {
                CandidateDiscovered?.Invoke(
                    this,
                    new DiscoveryCandidateDiscoveredEventArgs(
                        candidate));
            }
        }

        private sealed class GalleryCandidateMaterializer :
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
