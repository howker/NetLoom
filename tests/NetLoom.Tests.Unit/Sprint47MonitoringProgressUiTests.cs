using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.MonitoringControl;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    // Sprint 47: прогресс цикла опроса в верхней строке. Окно открывается на копии стенда аудита,
    // Набор устройств — настоящий (кнопка «Запустить»), прогресс считает настоящий MonitoringCycleTracker,
    // А снимки состояния публикует тестовый мониторинг — так, как это делает Desktop по строкам Engine.
    public sealed partial class Sprint46LiveUiAuditTests
    {
        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void MonitoringCycleProgressIsVisibleInTheHeaderAndExplainedInDetails()
        {
            var repositoryRoot =
                FindRepositoryRoot();

            var sourceDatabase =
                Path.Combine(
                    repositoryRoot,
                    "artifacts",
                    "realistic-stand",
                    "operator-s46-pass3-visual.db");

            if (!File.Exists(
                    sourceDatabase))
            {
                Assert.Inconclusive(
                    "Live UI audit database is not available: " +
                    sourceDatabase);
            }

            var output =
                Path.Combine(
                    repositoryRoot,
                    "artifacts",
                    "ui-audit-s47");

            Directory.CreateDirectory(
                output);

            var workDatabase =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s47-progress-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            File.Copy(
                sourceDatabase,
                workDatabase);

            try
            {
                RunOnSta(
                    () =>
                    {
                        var monitoring =
                            new ScriptedMonitoringControl();

                        var window =
                            CreateLiveWindow(
                                workDatabase,
                                false,
                                monitoring);

                        try
                        {
                            PrepareWindow(
                                window,
                                1440,
                                900);

                            RaiseClick(
                                (ButtonBase)window.FindName(
                                    "ShellMonitoringStartButton"));
                            Settle(300);

                            Assert.IsNotNull(
                                monitoring.StartTargets,
                                "The header Start button must launch the device set.");

                            var targets =
                                monitoring.StartTargets;

                            Assert.IsTrue(
                                targets.Count >= 3,
                                "The audit stand must monitor at least three devices.");

                            var tracker =
                                new MonitoringCycleTracker(
                                    targets);

                            var t0 =
                                new DateTime(
                                    2026, 10, 7, 9, 0, 0,
                                    DateTimeKind.Utc);

                            tracker.TargetStarted(targets[0].DeviceId, t0);
                            tracker.TargetCompleted(targets[0].DeviceId, t0.AddSeconds(1), true);
                            tracker.TargetStarted(targets[1].DeviceId, t0.AddSeconds(2));
                            tracker.TargetCompleted(targets[1].DeviceId, t0.AddSeconds(3), false);
                            tracker.TargetStarted(targets[2].DeviceId, t0.AddSeconds(4));

                            monitoring.Publish(
                                MonitoringControlState.Polling,
                                tracker);
                            Settle(200);

                            var header =
                                (TextBlock)window.FindName(
                                    "ShellMonitoringHeaderText");
                            var bar =
                                (ProgressBar)window.FindName(
                                    "ShellMonitoringProgressBar");
                            var detailsButton =
                                (Button)window.FindName(
                                    "ShellMonitoringDetailsButton");

                            Assert.AreEqual(
                                "Опрос: 2 / " + targets.Count,
                                header.Text,
                                "The header shows N / total while a cycle runs.");
                            Assert.AreEqual(Visibility.Visible, bar.Visibility);
                            Assert.AreEqual(2.0, bar.Value);
                            Assert.AreEqual(targets.Count, (int)bar.Maximum);
                            Assert.AreEqual(
                                header.Text,
                                AutomationProperties.GetName(detailsButton),
                                "The visible label is the accessible name (§8).");

                            SaveCapture(
                                window,
                                Path.Combine(output, "s47-header-progress.png"));

                            RaiseClick(
                                detailsButton);
                            Settle(300);

                            var popup =
                                (Popup)window.FindName(
                                    "ShellMonitoringDetailsPopup");

                            Assert.IsTrue(popup.IsOpen, "Clicking the state opens cycle details.");

                            var fields =
                                DetailFields(
                                    window);

                            var current =
                                ((TextBlock)window.FindName(
                                    "ShellMonitoringCurrentValueText")).Text;

                            Assert.IsTrue(
                                current.EndsWith(" · " + targets[2].TargetAddress, StringComparison.Ordinal),
                                "Details name the device being polled with its address: " + current);
                            Assert.AreEqual("1", fields["Успешно"], Describe(fields));
                            Assert.AreEqual("1", fields["С ошибкой"], Describe(fields));
                            Assert.AreEqual((targets.Count - 2).ToString(), fields["Осталось"], Describe(fields));
                            Assert.IsTrue(
                                fields.ContainsKey("Не ответили"),
                                "Details name the devices that did not answer: " + Describe(fields));

                            var action =
                                (Button)window.FindName(
                                    "ShellMonitoringCycleActionButton");

                            Assert.AreEqual("Запустить цикл сейчас", action.Content);

                            SaveElementCapture(
                                (FrameworkElement)popup.Child,
                                Path.Combine(output, "s47-details-running.png"));

                            RaiseClick(
                                action);
                            Settle(200);

                            Assert.AreEqual(
                                1,
                                monitoring.PollNowCount,
                                "«Запустить цикл сейчас» sends POLL_NOW for the whole set.");

                            ((FrameworkElement)popup.Child).RaiseEvent(
                                new KeyEventArgs(
                                    Keyboard.PrimaryDevice,
                                    PresentationSource.FromVisual(window),
                                    0,
                                    Key.Escape)
                                {
                                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                                });
                            Settle(100);

                            Assert.IsFalse(popup.IsOpen, "Esc closes the details (§8).");

                            // Остальные устройства отвечают — цикл завершён, в ленте событие цикла.
                            tracker.TargetCompleted(targets[2].DeviceId, t0.AddSeconds(5), true);

                            foreach (var target in targets.Skip(3))
                            {
                                tracker.TargetStarted(target.DeviceId, t0.AddSeconds(6));
                                tracker.TargetCompleted(target.DeviceId, t0.AddSeconds(7), true);
                            }

                            monitoring.Publish(
                                MonitoringControlState.Running,
                                tracker);
                            Settle(200);

                            Assert.IsTrue(
                                header.Text.StartsWith("Мониторинг:", StringComparison.Ordinal),
                                "Between cycles the header returns to the monitoring state: " + header.Text);
                            Assert.AreEqual(Visibility.Collapsed, bar.Visibility);

                            var events =
                                ((ItemsControl)window.FindName(
                                    "ShellEventList"))
                                .Items
                                .Cast<object>()
                                .Select(
                                    item =>
                                        item.GetType().GetProperty("Title").GetValue(item) +
                                        " · " +
                                        item.GetType().GetProperty("Scope").GetValue(item))
                                .ToArray();

                            CollectionAssert.Contains(
                                events,
                                "Цикл опроса завершён · Успешно: " +
                                (targets.Count - 1) +
                                " · С ошибкой: 1",
                                "The finished cycle is an event in the bottom strip: " +
                                string.Join(" | ", events));

                            AssertEventStripNotClipped(
                                window);

                            SaveCapture(
                                window,
                                Path.Combine(output, "s47-cycle-event.png"));
                        }
                        finally
                        {
                            window.Close();
                            Settle();
                        }
                    });
            }
            finally
            {
                TryDelete(
                    workDatabase);
            }
        }

        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void DeviceThatStopsAnsweringRaisesOneWarningEverywhereAndResolvesWhenItAnswers()
        {
            var repositoryRoot =
                FindRepositoryRoot();

            var sourceDatabase =
                Path.Combine(
                    repositoryRoot,
                    "artifacts",
                    "realistic-stand",
                    "operator-s46-pass3-visual.db");

            if (!File.Exists(
                    sourceDatabase))
            {
                Assert.Inconclusive(
                    "Live UI audit database is not available: " +
                    sourceDatabase);
            }

            var output =
                Path.Combine(
                    repositoryRoot,
                    "artifacts",
                    "ui-audit-s47");

            Directory.CreateDirectory(
                output);

            var workDatabase =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s47-unreachable-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            File.Copy(
                sourceDatabase,
                workDatabase);

            try
            {
                RunOnSta(
                    () =>
                    {
                        var monitoring =
                            new ScriptedMonitoringControl();

                        var window =
                            CreateLiveWindow(
                                workDatabase,
                                false,
                                monitoring);

                        try
                        {
                            PrepareWindow(
                                window,
                                1440,
                                900);

                            RaiseClick(
                                (ButtonBase)window.FindName(
                                    "ShellMonitoringStartButton"));
                            Settle(300);

                            var targets =
                                monitoring.StartTargets;

                            Assert.IsNotNull(targets);

                            // Устройство на карте и без собственных предупреждений схемы — иначе в инспекторе
                            // Главной окажется более серьёзная проблема его связей.
                            var target =
                                targets.First(
                                    item =>
                                        NodeVisual(window, item.DeviceId) != null &&
                                        InvokePrivate(window, "InspectorAlertForDevice", item.DeviceId) == null);

                            var name =
                                (string)InvokePrivate(
                                    window,
                                    "MonitoringDeviceName",
                                    target.DeviceId,
                                    target.TargetAddress.ToString());

                            var alertsBefore =
                                ((ItemsControl)window.FindName("AlertList")).Items.Count;

                            var tracker =
                                new MonitoringCycleTracker(
                                    targets);

                            var t0 =
                                DateTime.UtcNow.AddMinutes(-3);

                            tracker.TargetCompleted(target.DeviceId, t0, false);
                            monitoring.Publish(MonitoringControlState.Running, tracker);
                            Settle(200);

                            Assert.AreEqual(
                                alertsBefore,
                                ((ItemsControl)window.FindName("AlertList")).Items.Count,
                                "One missed answer is not an alert.");

                            // Второй опрос: SNMP молчит, но устройство отвечает на ICMP и держит открытым SSH.
                            tracker.TargetCompleted(
                                target.DeviceId,
                                t0.AddMinutes(1),
                                false,
                                new NetLoom.Application.Monitoring.MonitoringAvailability(
                                    true,
                                    new[] { 22, 80, 443 },
                                    new[] { 22 }));
                            monitoring.Publish(MonitoringControlState.Running, tracker);
                            Settle(300);

                            var cards =
                                ((ItemsControl)window.FindName("AlertList")).Items
                                    .Cast<object>()
                                    .Select(
                                        item =>
                                            item.GetType().GetProperty("Title").GetValue(item) + " | " +
                                            item.GetType().GetProperty("Scope").GetValue(item) + " | " +
                                            item.GetType().GetProperty("Reason").GetValue(item))
                                    .ToArray();

                            Assert.AreEqual(alertsBefore + 1, cards.Length, string.Join(" || ", cards));

                            var card =
                                cards.Single(
                                    item => item.StartsWith("Устройство не отвечает | ", StringComparison.Ordinal));

                            StringAssert.Contains(card, name, "The card names the device.");
                            StringAssert.Contains(card, "Опросов подряд без ответа: 2", "The card explains why.");
                            StringAssert.Contains(
                                card,
                                "По ICMP устройство отвечает",
                                "The card says where to look: the device is alive, SNMP is silent.");

                            Assert.AreEqual(
                                "Предупреждения: " + (alertsBefore + 1),
                                AutomationProperties.GetName(
                                    (DependencyObject)window.FindName("ShellAlertsButton")),
                                "The rail badge counts the new warning.");

                            var nodeVisual =
                                NodeVisual(window, target.DeviceId);

                            Assert.AreEqual(
                                Visibility.Visible,
                                ((UIElement)nodeVisual.GetType().GetProperty("StatusIcon").GetValue(nodeVisual)).Visibility,
                                "The map card shows the warning icon.");

                            Assert.IsTrue(
                                AllEventTitles(window).Contains("Устройство не отвечает"),
                                "The new warning is an event: " + string.Join(" | ", AllEventTitles(window)));

                            SelectEquipmentRow(window, name);

                            StringAssert.StartsWith(
                                Text(window, "InspectorProblemText"),
                                "! Предупреждение — Устройство не отвечает",
                                "The inspector explains the same problem.");

                            // Словарь состояний ТЗ §10: SNMP молчит, ICMP отвечает — «Частично доступен».
                            StringAssert.StartsWith(
                                Text(window, "InspectorOperationalStatusText"),
                                "Частично доступен",
                                "The state line uses the availability vocabulary.");

                            var fields =
                                ((ItemsControl)window.FindName("DiagnosticFieldsList")).Items
                                    .Cast<object>()
                                    .ToDictionary(
                                        item => (string)item.GetType().GetProperty("Label").GetValue(item),
                                        item => (string)item.GetType().GetProperty("Value").GetValue(item));

                            Assert.AreEqual("Отвечает", fields["ICMP"], Describe(fields));
                            Assert.AreEqual("Не отвечает", fields["SNMP"], Describe(fields));
                            Assert.AreEqual("Открыты: 22 · Закрыты: 80, 443", fields["TCP-порты"], Describe(fields));

                            SaveCapture(
                                window,
                                Path.Combine(output, "s47-device-unreachable.png"));

                            // Устройство ответило — предупреждение снято, в ленте «Устранено».
                            tracker.TargetCompleted(target.DeviceId, t0.AddMinutes(2), true);
                            monitoring.Publish(MonitoringControlState.Running, tracker);
                            Settle(300);

                            Assert.AreEqual(
                                alertsBefore,
                                ((ItemsControl)window.FindName("AlertList")).Items.Count,
                                "The warning resolves when the device answers.");
                            Assert.IsTrue(
                                AllEventTitles(window).Contains("Устранено: Устройство не отвечает"),
                                "Resolution is an event: " + string.Join(" | ", AllEventTitles(window)));
                        }
                        finally
                        {
                            window.Close();
                            Settle();
                        }
                    });
            }
            finally
            {
                TryDelete(
                    workDatabase);
            }
        }

        private static object InvokePrivate(
            MainWindow window,
            string method,
            params object[] arguments)
        {
            return typeof(MainWindow)
                .GetMethod(
                    method,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(
                    window,
                    arguments);
        }

        private static object NodeVisual(
            MainWindow window,
            Guid deviceId)
        {
            var visuals =
                (System.Collections.IDictionary)typeof(MainWindow)
                    .GetField(
                        "_nodeVisualsByIdentity",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    .GetValue(window);

            return visuals.Values
                .Cast<object>()
                .FirstOrDefault(
                    visual =>
                        (Guid?)visual.GetType().GetProperty("DeviceId").GetValue(visual) ==
                        deviceId);
        }

        // Все события, а не только три видимых в ленте.
        private static string[] AllEventTitles(
            MainWindow window)
        {
            var rows =
                (System.Collections.IEnumerable)typeof(MainWindow)
                    .GetField(
                        "_shellEventRows",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    .GetValue(window);

            return rows
                .Cast<object>()
                .Select(
                    item => (string)item.GetType().GetProperty("Title").GetValue(item))
                .ToArray();
        }

        private static void SaveElementCapture(
            FrameworkElement element,
            string path)
        {
            element.UpdateLayout();

            var bitmap =
                new System.Windows.Media.Imaging.RenderTargetBitmap(
                    Math.Max(1, (int)Math.Ceiling(element.ActualWidth)),
                    Math.Max(1, (int)Math.Ceiling(element.ActualHeight)),
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Pbgra32);

            bitmap.Render(element);

            var encoder =
                new System.Windows.Media.Imaging.PngBitmapEncoder();

            encoder.Frames.Add(
                System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));

            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        // §10: видимое событие ленты целиком помещается и не заходит под ссылку «Все события».
        private static void AssertEventStripNotClipped(
            Window window)
        {
            var list =
                (ItemsControl)window.FindName(
                    "ShellEventList");
            var allEvents =
                (FrameworkElement)window.FindName(
                    "ShellAllEventsButton");

            window.UpdateLayout();

            var linkLeft =
                allEvents.TransformToAncestor(window)
                    .Transform(new Point(0, 0))
                    .X;

            var shown = 0;

            foreach (var item in list.Items)
            {
                var container =
                    (FrameworkElement)list.ItemContainerGenerator
                        .ContainerFromItem(item);

                if (container == null ||
                    container.ActualWidth <= 0.0 ||
                    container.RenderSize.Width <= 0.0)
                {
                    continue;
                }

                var bounds =
                    container.TransformToAncestor(window)
                        .TransformBounds(
                            new Rect(container.RenderSize));

                if (bounds.Width <= 0.5)
                {
                    continue;
                }

                shown++;

                Assert.IsTrue(
                    bounds.Right <= linkLeft + 0.5,
                    "Event «" + item.GetType().GetProperty("Title").GetValue(item) +
                    "» runs under «Все события»: right " + bounds.Right + " > " + linkLeft);
            }

            Assert.IsTrue(
                shown > 0,
                "At least the most important event stays visible.");
        }

        private static Dictionary<string, string> DetailFields(
            Window window)
        {
            var list =
                (ItemsControl)window.FindName(
                    "ShellMonitoringDetailsFieldsList");

            return list.Items
                .Cast<object>()
                .ToDictionary(
                    item => (string)item.GetType().GetProperty("Label").GetValue(item),
                    item => (string)item.GetType().GetProperty("Value").GetValue(item));
        }

        private static string Describe(
            Dictionary<string, string> fields)
        {
            return string.Join(
                " | ",
                fields.Select(
                    pair => pair.Key + " = " + pair.Value));
        }

        // Мониторинг под управлением теста: запоминает набор и команды, публикует снимки с прогрессом цикла.
        private sealed class ScriptedMonitoringControl :
            IMultiTargetMonitoringControl
        {
            private MonitoringControlSnapshot _current =
                new MonitoringControlSnapshot(
                    MonitoringControlState.Stopped,
                    null,
                    null,
                    null);

            public MonitoringControlSnapshot Current =>
                _current;

            public IReadOnlyList<MonitoringTarget> StartTargets { get; private set; }

            public int PollNowCount { get; private set; }

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged;

            public Task StartSetAsync(
                IReadOnlyList<MonitoringTarget> targets,
                MonitoringSessionPolicy policy,
                MonitoringTargetSetPolicy targetSetPolicy,
                CancellationToken cancellationToken)
            {
                StartTargets =
                    targets;

                Publish(
                    MonitoringControlState.Running,
                    new MonitoringCycleTracker(
                        targets));

                return Task.CompletedTask;
            }

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException(
                    "The shell starts the device set.");
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                _current =
                    new MonitoringControlSnapshot(
                        MonitoringControlState.Stopped,
                        null,
                        null,
                        null);
                Raise();
                return Task.CompletedTask;
            }

            public Task PollNowAsync(
                MonitoringTarget targetWhenStopped,
                MonitoringSessionPolicy policyWhenStopped,
                CancellationToken cancellationToken)
            {
                PollNowCount++;
                return Task.CompletedTask;
            }

            public void Publish(
                MonitoringControlState state,
                MonitoringCycleTracker tracker)
            {
                _current =
                    new MonitoringControlSnapshot(
                        state,
                        null,
                        null,
                        null,
                        tracker.Current,
                        tracker.LastCompleted,
                        tracker.Outcomes);
                Raise();
            }

            private void Raise()
            {
                SnapshotChanged?.Invoke(
                    this,
                    new MonitoringControlSnapshotChangedEventArgs(
                        _current));
            }
        }
    }
}
