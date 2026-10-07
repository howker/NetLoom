using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MonitoringControl;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    // Sprint 47, галерея состояний (UI_DESIGN_RULES §9): прогресс цикла в верхней строке и подробности цикла.
    // Оси: количество (0, 1, много, 99+), длина текста (длинное имя с «ЙЁ», «и ещё N»), состояние мониторинга
    // (запуск, цикл, между циклами, остановлен с итогом и без, ошибка), ширина (минимальная и обычная), обе темы.
    // Отброшены: наведение и нажатие кнопки состояния — общий FlatButton, уже в кадрах 18/19.
    public sealed partial class Sprint46UiStateGalleryTests
    {
        private static readonly DateTime GalleryCycleStartUtc =
            new DateTime(
                2026, 10, 7, 9, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void MonitoringProgressGalleryCoversCycleStates()
        {
            RunOnSta(
                () =>
                {
                    var outputDirectory =
                        Path.Combine(
                            Path.GetDirectoryName(
                                ResolveOutputDirectory()),
                            "sprint47-progress");

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

                    var longDeviceId =
                        Guid.Parse(
                            "47474747-0001-0001-0001-474747474747");

                    const string longName =
                        "Очень длинное имя промышленного коммутатора ЙЁ 012345678901234567890123456789";

                    RenderShellScenario(
                        outputDirectory,
                        "20-monitoring-cycle-progress",
                        () => EmptySnapshot(),
                        null,
                        findings,
                        () => new GalleryMonitoringControl(
                            CycleSnapshot(
                                MonitoringControlState.Polling,
                                55,
                                7,
                                1,
                                false)));

                    RenderShellScenario(
                        outputDirectory,
                        "21-monitoring-cycle-many",
                        () => EmptySnapshot(),
                        null,
                        findings,
                        () => new GalleryMonitoringControl(
                            CycleSnapshot(
                                MonitoringControlState.Polling,
                                120,
                                118,
                                0,
                                false)));

                    RenderShellScenario(
                        outputDirectory,
                        "22-monitoring-cycle-finished-event",
                        () => EmptySnapshot(),
                        null,
                        findings,
                        () => new GalleryMonitoringControl(
                            CycleSnapshot(
                                MonitoringControlState.Running,
                                55,
                                55,
                                2,
                                true)));

                    var details =
                        new[]
                        {
                            Tuple.Create(
                                "23-details-running-long-name",
                                (Func<MonitoringControlSnapshot>)(() =>
                                    CycleSnapshot(
                                        MonitoringControlState.Polling,
                                        55,
                                        7,
                                        1,
                                        false,
                                        longDeviceId,
                                        4))),
                            Tuple.Create(
                                "24-details-waiting-many-failed",
                                (Func<MonitoringControlSnapshot>)(() =>
                                    CycleSnapshot(
                                        MonitoringControlState.Running,
                                        55,
                                        55,
                                        5,
                                        true))),
                            Tuple.Create(
                                "25-details-stopped-last-cycle",
                                (Func<MonitoringControlSnapshot>)(() =>
                                    CycleSnapshot(
                                        MonitoringControlState.Stopped,
                                        1,
                                        1,
                                        0,
                                        true))),
                            Tuple.Create(
                                "26-details-stopped-empty",
                                (Func<MonitoringControlSnapshot>)(() =>
                                    new MonitoringControlSnapshot(
                                        MonitoringControlState.Stopped,
                                        null,
                                        null,
                                        null))),
                            Tuple.Create(
                                "27-details-faulted",
                                (Func<MonitoringControlSnapshot>)(() =>
                                    new MonitoringControlSnapshot(
                                        MonitoringControlState.Faulted,
                                        null,
                                        null,
                                        "Процесс опроса завершился с кодом 3. Проверьте журнал Engine и запустите мониторинг снова."))),
                            Tuple.Create(
                                "28-details-starting",
                                (Func<MonitoringControlSnapshot>)(() =>
                                    new MonitoringControlSnapshot(
                                        MonitoringControlState.Starting,
                                        null,
                                        null,
                                        null)))
                        };

                    foreach (var item in details)
                    {
                        var bitmaps =
                            new List<BitmapSource>();

                        foreach (var dark in new[] { false, true })
                        {
                            bitmaps.Add(
                                RenderMonitoringDetails(
                                    () => SingleDeviceSnapshot(
                                        longDeviceId,
                                        longName,
                                        "192.0.2.201"),
                                    item.Item2(),
                                    dark,
                                    item.Item1,
                                    findings));
                        }

                        SaveSideBySide(
                            bitmaps[0],
                            bitmaps[1],
                            Path.Combine(
                                outputDirectory,
                                item.Item1 + ".png"));
                    }

                    File.WriteAllLines(
                        Path.Combine(
                            outputDirectory,
                            "findings.txt"),
                        findings.Count == 0
                            ? new[] { "Находок нет." }
                            : findings.ToArray());

                    // Три кадра окна (светлая и тёмная темы отдельными файлами) и шесть кадров подробностей.
                    Assert.AreEqual(
                        12,
                        Directory.GetFiles(
                            outputDirectory,
                            "*.png").Length,
                        "Every Sprint 47 gallery frame must be produced.");
                });
        }

        // Снимок мониторинга с прогрессом цикла: total устройств, done опрошено, failed из них без ответа.
        // Если finished — цикл завершён и следующий не начат; иначе идёт цикл и опрашиваются inProgress устройств.
        private static MonitoringControlSnapshot CycleSnapshot(
            MonitoringControlState state,
            int total,
            int done,
            int failed,
            bool finished,
            Guid? firstDeviceId = null,
            int inProgress = 1)
        {
            var targets =
                Enumerable.Range(0, total)
                    .Select(
                        index =>
                            new MonitoringTarget(
                                index == 0 && firstDeviceId.HasValue
                                    ? firstDeviceId.Value
                                    : new Guid(index + 1, 47, 47, new byte[8]),
                                IPAddress.Parse(
                                    "192.0.2." + (index % 250 + 1))))
                    .ToArray();

            var tracker =
                new MonitoringCycleTracker(
                    targets);

            // Сначала заняты «текущие» устройства, затем ответы остальных; failed — первые из ответивших.
            var answered =
                targets
                    .Skip(
                        finished
                            ? 0
                            : inProgress)
                    .Take(done)
                    .ToArray();

            for (var index = 0; index < answered.Length; index++)
            {
                var at =
                    GalleryCycleStartUtc.AddSeconds(index);

                tracker.TargetStarted(answered[index].DeviceId, at);
                tracker.TargetCompleted(answered[index].DeviceId, at.AddMilliseconds(500), index >= failed);
            }

            if (!finished)
            {
                foreach (var target in targets.Take(inProgress))
                {
                    tracker.TargetStarted(
                        target.DeviceId,
                        GalleryCycleStartUtc.AddSeconds(done));
                }
            }

            return new MonitoringControlSnapshot(
                state,
                null,
                GalleryCycleStartUtc.AddSeconds(done),
                null,
                tracker.Current,
                tracker.LastCompleted,
                tracker.Outcomes);
        }

        private static BitmapSource RenderMonitoringDetails(
            Func<NetLoom.Application.TopologyRefresh.TopologyRefreshSnapshot> snapshotFactory,
            MonitoringControlSnapshot monitoring,
            bool dark,
            string scenario,
            IList<string> findings)
        {
            var window =
                new MainWindow(
                    new FixedRefreshProvider(snapshotFactory()),
                    new EmptyLookupReader(),
                    new GalleryMonitoringControl(monitoring),
                    new NetLoom.Domain.Access.AccessProfile[0]);

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

                PumpDispatcher();

                Click(
                    (Button)window.FindName(
                        "ShellMonitoringDetailsButton"));

                PumpDispatcher();

                var popup =
                    (Popup)window.FindName(
                        "ShellMonitoringDetailsPopup");

                if (!popup.IsOpen)
                {
                    findings.Add(
                        scenario + ": подробности цикла не открылись по нажатию на состояние (§8).");
                }

                var panel =
                    (FrameworkElement)popup.Child;

                panel.UpdateLayout();

                CollectTextClipping(
                    panel,
                    scenario + "/" + (dark ? "dark" : "light"),
                    findings);

                return Capture(
                    panel);
            }
            finally
            {
                window.Close();
                PumpDispatcher();
            }
        }
    }
}
