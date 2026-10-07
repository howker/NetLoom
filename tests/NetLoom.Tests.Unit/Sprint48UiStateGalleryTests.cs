using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Wpf;

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

                                    var runClock =
                                        new DateTime(2026, 10, 7, 9, 12, 5, DateTimeKind.Utc);
                                    window.DiscoveryRunClock =
                                        () => runClock;

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
                                                    "198.51.100." + i),
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
                int found)
            {
                _current =
                    new DiscoveryControlSnapshot(
                        state,
                        null,
                        null,
                        processed,
                        total,
                        found,
                        null,
                        null);

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
