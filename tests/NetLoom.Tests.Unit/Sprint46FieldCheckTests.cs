using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Wpf;
using NetLoom.Wpf.Export;

namespace NetLoom.Tests.Unit
{
    // Полевая проверка «как инженер на объекте» на полевом стенде (FieldStandBuilder → field-s46.db).
    // Настоящее окно; действия — те же, что делает инженер: разделы, фильтры, выбор, поиск, настройки,
    // Обнаружение, экспорт, перезапуск. Ожидания берутся из модели сети стенда, а не задаются числами.
    // Отчёт — artifacts/field-check/field-check-report.txt и снимки; тест не падает на находках:
    // Его результат — реестр наблюдений для docs/sprint46-field-check.md.
    // Запуск: dotnet test ... --filter "TestCategory=FieldCheck" (сначала TestCategory=StandBuilder).
    public sealed partial class Sprint46LiveUiAuditTests
    {
        private sealed class FieldReport
        {
            private readonly StringBuilder _text = new StringBuilder();

            public int Errors { get; private set; }

            public void Ok(string id, string message)
            {
                Add("ВЕРНО", id, message);
            }

            public void Error(string id, string message)
            {
                Errors++;
                Add("ОШИБКА", id, message);
            }

            public void Note(string id, string message)
            {
                Add("НАБЛЮДЕНИЕ", id, message);
            }

            public void NotChecked(string id, string message)
            {
                Add("НЕ ПРОВЕРЕНО", id, message);
            }

            private void Add(string kind, string id, string message)
            {
                _text.AppendLine("[" + kind + "] " + id + " — " + message);
            }

            public override string ToString()
            {
                return _text.ToString();
            }
        }

        [TestMethod]
        [TestCategory("FieldCheck")]
        public void FieldEngineerWalkthroughOnFieldStand()
        {
            var root =
                FindRepositoryRoot();

            var source =
                Path.Combine(root, "artifacts", "realistic-stand", "field-s46.db");

            if (!File.Exists(source))
            {
                Assert.Inconclusive("Field stand is not built: run TestCategory=StandBuilder first.");
            }

            var output =
                Path.Combine(root, "artifacts", "field-check");

            Directory.CreateDirectory(output);

            foreach (var file in Directory.GetFiles(output, "*.png").Concat(Directory.GetFiles(output, "*.txt")).Concat(Directory.GetFiles(output, "*.csv")))
            {
                File.Delete(file);
            }

            var database =
                Path.Combine(Path.GetTempPath(), "netloom-field-check-" + Guid.NewGuid().ToString("N") + ".db");

            File.Copy(source, database);

            var report =
                new FieldReport();

            var network =
                FieldStandBuilder.SimNetwork.Create();

            try
            {
                RunOnSta(
                    () =>
                    {
                        // Как в приложении: продолжения async-обработчиков возвращаются в поток окна.
                        System.Threading.SynchronizationContext.SetSynchronizationContext(
                            new System.Windows.Threading.DispatcherSynchronizationContext());

                        RunFieldScenarios(database, output, network, report);
                        RunRestartScenario(database, output, report);
                        RunRapidZoomScenario(database, output, report);
                    });
            }
            catch (Exception error)
            {
                report.Error("СБОЙ", "сценарий прерван исключением: " + error);
            }
            finally
            {
                File.WriteAllText(
                    Path.Combine(output, "field-check-report.txt"),
                    report.ToString(),
                    new UTF8Encoding(false));

                TryDelete(database);
            }
        }

        private static void Step(FieldReport report, string id, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                report.Error(id, "исключение в сценарии: " + error.GetType().Name + ": " + error.Message);
            }
        }

        private static void RunFieldScenarios(
            string database,
            string output,
            FieldStandBuilder.SimNetwork network,
            FieldReport report)
        {
            var window =
                CreateLiveWindow(database, false);

            try
            {
                PrepareWindow(window, 1440, 900);

                var managed = network.Devices.Count;
                var manual = network.ManualDevices.Count;

                // F01. Запуск: на карте все устройства объекта.
                Step(report, "F01 Запуск", () =>
                {
                    RaiseClick((ButtonBase)window.FindName("ShellMapButton"));
                    Settle(800);
                    SaveCapture(window, Path.Combine(output, "F01-map.png"));

                    // Узлы устройств карты (рамки размещений на холсте тоже имеют идентификатор — не считаем их).
                    var nodes =
                        ((System.Collections.ICollection)typeof(MainWindow)
                            .GetField("_nodeVisualsByIdentity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .GetValue(window)).Count;

                    if (nodes == managed + manual)
                    {
                        report.Ok("F01 Запуск", "на карте " + nodes + " устройств (" + managed + " опрашиваемых и " + manual + " ручных)");
                    }
                    else
                    {
                        report.Error("F01 Запуск", "на карте " + nodes + " устройств, ожидалось " + (managed + manual));
                    }
                });

                // F02. «Оборудование»: таблица и счётчики фильтров.
                Step(report, "F02 Оборудование", () =>
                {
                    RaiseClick((ButtonBase)window.FindName("ShellEquipmentButton"));
                    Settle(500);
                    SaveCapture(window, Path.Combine(output, "F02-equipment.png"));

                    var rows = ((ItemsControl)window.FindName("EquipmentList")).Items.Count;
                    var manualFilter = ButtonText(window, "EquipmentFilterManualButton");
                    var problems = ButtonText(window, "EquipmentFilterProblemsButton");
                    var noData = ButtonText(window, "EquipmentFilterNoDataButton");

                    if (rows == managed + manual)
                    {
                        report.Ok("F02 Оборудование", "в таблице " + rows + " строк");
                    }
                    else
                    {
                        report.Error("F02 Оборудование", "в таблице " + rows + " строк, ожидалось " + (managed + manual));
                    }

                    if (manualFilter.EndsWith(" " + manual, StringComparison.Ordinal))
                    {
                        report.Ok("F02 Фильтр «Ручные»", manualFilter);
                    }
                    else
                    {
                        report.Error("F02 Фильтр «Ручные»", "«" + manualFilter + "», ожидалось " + manual + " ручных устройств");
                    }

                    report.Note("F02 Счётчики", "«" + problems + "», «" + noData + "»");
                });

                // F03. Фильтр таблицы по имени, IP и модели.
                Step(report, "F03 Фильтр", () =>
                {
                    var filter = (TextBox)window.FindName("EquipmentFilterTextBox");
                    var list = (ItemsControl)window.FindName("EquipmentList");

                    var checks = new[]
                    {
                        Tuple.Create("kb-sw", network.Devices.Count(item => item.Name.StartsWith("kb-sw", StringComparison.Ordinal)), "по имени"),
                        Tuple.Create(network.Find("kb-sw-01").Address, 1, "по IP"),
                        // Г2 (Sprint 48): sysDescr из обнаружения сохраняется — фильтр находит модель.
                        Tuple.Create("EDS-518", network.Devices.Count(item => item.Managed && item.SysDescr.Contains("EDS-518")), "по модели")
                    };

                    foreach (var check in checks)
                    {
                        filter.Text = check.Item1;
                        Settle(300);

                        var count = list.Items.Count;

                        if (count == check.Item2)
                        {
                            report.Ok("F03 Фильтр " + check.Item3, "«" + check.Item1 + "» → " + count);
                        }
                        else
                        {
                            report.Error("F03 Фильтр " + check.Item3, "«" + check.Item1 + "» → " + count + " строк, ожидалось " + check.Item2);
                        }
                    }

                    filter.Text = string.Empty;
                    Settle(300);

                    // Г1 (Sprint 48): категория по возможностям LLDP — коммутатор и маршрутизатор, а не «Неизвестно».
                    var categories =
                        list.Items
                            .Cast<object>()
                            .ToDictionary(
                                item => (string)item.GetType().GetProperty("Name").GetValue(item),
                                item => (string)item.GetType().GetProperty("Category").GetValue(item));

                    foreach (var expected in new[]
                    {
                        Tuple.Create("core-sw-01", "Коммутатор"),
                        Tuple.Create("kb-sw-01", "Коммутатор"),
                        Tuple.Create("gw-01", "Маршрутизатор")
                    })
                    {
                        string category;
                        categories.TryGetValue(expected.Item1, out category);

                        if (category == expected.Item2)
                        {
                            report.Ok("F03 Категория " + expected.Item1, category);
                        }
                        else
                        {
                            report.Error("F03 Категория " + expected.Item1, "«" + category + "», ожидалось «" + expected.Item2 + "»");
                        }
                    }
                });

                // F04. Устройство кольца: инспектор, путь размещения, порты, связи, модель.
                Step(report, "F04 Инспектор устройства", () =>
                {
                    var name = "ps1-sw-03";
                    SelectEquipmentRow(window, name);
                    SaveCapture(window, Path.Combine(output, "F04-device-inspector.png"));

                    var title = Text(window, "DiagnosticElementTitleText");
                    var crumb = Text(window, "ShellBreadcrumbText");
                    var ports = ((ItemsControl)window.FindName("DiagnosticInterfaceList")).Items.Count;
                    var links = ((ItemsControl)window.FindName("DiagnosticLinkList")).Items.Count;
                    var inspectorText = string.Join(" | ", VisibleTexts((DependencyObject)window.FindName("ShellInspectorPanel")));

                    Check(report, "F04 Заголовок", title == name, "«" + title + "»");
                    Check(report, "F04 Путь размещения", crumb.Contains("ПС-1") && crumb.Contains("Щитовая"), "«" + crumb + "»");
                    Check(report, "F04 Порты", ports == network.Find(name).Ports.Count, ports + " из " + network.Find(name).Ports.Count);
                    Check(report, "F04 Связи", links == 2, links + " (ожидалось 2 соседа по кольцу)");
                    // ADR-083: «Описание» — модель, а при её отсутствии «—», но не MAC шасси (P4).
                    var modelShown = inspectorText.Contains("EDS-408A");
                    Check(report, "F04 Описание", modelShown || inspectorText.Contains("— | Описание"), modelShown ? "модель видна в инспекторе" : "модели нет, поле «Описание» — «—»");
                });

                // F05. Недоступное устройство с устаревшими данными.
                Step(report, "F05 Недоступное устройство", () =>
                {
                    SelectEquipmentRow(window, "kb-sw-06");
                    SaveCapture(window, Path.Combine(output, "F05-stale-device.png"));

                    var status = Text(window, "InspectorOperationalStatusText");
                    var row = EquipmentRowTexts(window, "kb-sw-06");

                    report.Note("F05 Недоступное устройство", "строка состояния: «" + status + "»; строка таблицы: " + string.Join(" · ", row));
                    Check(report, "F05 Свежесть", status.Contains("3 дня") || status.Contains("3 days"), "строка состояния называет давность данных");
                });

                // F06. Ручное устройство.
                Step(report, "F06 Ручное устройство", () =>
                {
                    SelectEquipmentRow(window, "МК-1 Серверная");
                    SaveCapture(window, Path.Combine(output, "F06-manual-device.png"));

                    var links = ((ItemsControl)window.FindName("DiagnosticLinkList")).Items.Count;
                    var row = EquipmentRowTexts(window, "МК-1 Серверная");

                    Check(report, "F06 Связи ручного медиаконвертера", links == 2, links + " (ожидалось 2: ядро и МК-2)");
                    report.Note("F06 Строка таблицы", string.Join(" · ", row));
                });

                var alertCardTexts = new List<string>();
                var alertsCaptured = false;

                // F07. Предупреждения по сценариям стенда.
                Step(report, "F07 Предупреждения", () =>
                {
                    RaiseClick((ButtonBase)window.FindName("ShellAlertsButton"));
                    Settle(1200);
                    SaveCapture(window, Path.Combine(output, "F07-alerts.png"));

                    var cards = ((ItemsControl)window.FindName("AlertList")).Items.Count;
                    var alertList = (ItemsControl)window.FindName("AlertList");
                    var texts = VisibleTexts(alertList).ToList();
                    alertCardTexts = alertList.Items.Cast<object>()
                        .Select(item => string.Join(" · ",
                            new[] { "Title", "Scope", "Reason", "TechnicalDetails" }
                                .Select(name => (string)item.GetType().GetProperty(name).GetValue(item))))
                        .ToList();
                    alertsCaptured = true;

                    report.Note("F07 Список", cards + " карточек: " + Shorten(string.Join(" | ", texts), 600));

                    Check(report, "F07 Обрыв простого кольца ПС-2", texts.Any(item => item.Contains("Кольцо без резерва")) && texts.Any(item => item.Contains("ps2-sw")), "«Кольцо без резерва» по ПС-2");
                    Check(report, "F07 Штатная блокировка STP не тревожит", !texts.Any(item => item.Contains("ps1-sw")) && !texts.Any(item => item.Contains("Цикл пересылки")), "кольцо ПС-1 и параллельные кабели ядра без предупреждений");
                });

                // F15. Односторонний LLDP объясняется в основаниях без предупреждения.
                Step(report, "F15 Односторонний LLDP", () =>
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var snapshot = (NetworkDiagnosticSnapshot)typeof(MainWindow)
                        .GetField("_lastDiagnosticSnapshot", flags).GetValue(window);
                    var link = snapshot.Links.Single(item =>
                        (item.DeviceAName == "kb-sw-03" && item.DeviceBName == "kb-sw-04") ||
                        (item.DeviceAName == "kb-sw-04" && item.DeviceBName == "kb-sw-03"));

                    RaiseClick((ButtonBase)window.FindName("ShellMapButton"));
                    typeof(MainWindow).GetMethod("ShowLinkDiagnostic", flags)
                        .Invoke(window, new object[] { link });
                    ((TabControl)window.FindName("InspectorTabControl")).SelectedItem =
                        window.FindName("InspectorEvidenceTab");
                    Settle(500);
                    SaveCapture(window, Path.Combine(output, "F15-one-sided-lldp.png"));

                    var evidence = (ItemsControl)window.FindName("DiagnosticTertiaryList");
                    var firstText = evidence.Items.Count == 0
                        ? string.Empty
                        : (string)evidence.Items[0].GetType().GetProperty("Text").GetValue(evidence.Items[0]);
                    Check(report, "F15 Пробел в основаниях",
                        firstText.StartsWith("Пробел в основаниях", StringComparison.Ordinal) ||
                        firstText.StartsWith("Evidence gap", StringComparison.Ordinal),
                        "первая строка подтверждающих данных: «" + firstText + "»");
                    Check(report, "F15 Без предупреждения",
                        alertsCaptured && !alertCardTexts.Any(text =>
                            text.Contains("kb-sw-04") && text.Contains("LLDP")),
                        "в списке F07 нет предупреждения об одностороннем LLDP для kb-sw-04");
                });

                // F08. Глобальный поиск: имя, IP и MAC оконечного устройства.
                Step(report, "F08 Поиск", () =>
                {
                    var camera = network.Endpoints[0];
                    var box = (TextBox)window.FindName("ShellGlobalSearchTextBox");
                    var list = (ItemsControl)window.FindName("ShellGlobalSearchResultsList");

                    var queries = new[]
                    {
                        Tuple.Create("ps2-sw-04", "имя устройства", "ps2-sw-04"),
                        Tuple.Create(camera.Ip, "IP камеры", "ps1-sw-01"),
                        Tuple.Create(camera.Mac, "MAC камеры", "ps1-sw-01"),
                        Tuple.Create(camera.Mac.Replace(":", "-").ToUpperInvariant(), "MAC камеры через дефис", "ps1-sw-01"),
                        Tuple.Create(camera.Mac.Substring(0, 8), "часть MAC", camera.Mac.Substring(0, 8).ToUpperInvariant())
                    };

                    foreach (var query in queries)
                    {
                        box.Text = string.Empty;
                        Settle(100);
                        box.Text = query.Item1;

                        var deadline = DateTime.UtcNow.AddSeconds(5);

                        do
                        {
                            Settle(150);
                        }
                        while (DateTime.UtcNow < deadline &&
                               !VisibleTexts(list).Any(item => item.Contains(query.Item3)));

                        var texts = VisibleTexts(list).ToList();
                        var status = Text(window, "ShellGlobalSearchStatusText");

                        if (texts.Any(item => item.Contains(query.Item3)))
                        {
                            report.Ok("F08 Поиск: " + query.Item2, "«" + query.Item1 + "» → " + Shorten(string.Join(" | ", texts), 200));
                        }
                        else
                        {
                            report.Error("F08 Поиск: " + query.Item2, "«" + query.Item1 + "» → нет «" + query.Item3 + "»; статус «" + status + "»; результаты: " + Shorten(string.Join(" | ", texts), 200));
                        }

                        if (query.Item2 == "MAC камеры")
                        {
                            SaveCapture(window, Path.Combine(output, "F08-search-mac.png"));
                        }
                    }

                    box.Text = string.Empty;
                    Settle(200);
                });

                // F09. Обнаружение: проверка диапазона.
                Step(report, "F09 Обнаружение", () =>
                {
                    RaiseClick((ButtonBase)window.FindName("ShellDiscoveryButton"));
                    Settle(400);

                    var start = (TextBox)window.FindName("DiscoveryStartAddressTextBox");
                    var end = (TextBox)window.FindName("DiscoveryEndAddressTextBox");
                    var button = (ButtonBase)window.FindName("DiscoveryStartButton");

                    var cases = new[]
                    {
                        Tuple.Create(string.Empty, string.Empty, "пустой диапазон"),
                        Tuple.Create("300.1.1.1", "300.1.1.5", "неверный адрес"),
                        Tuple.Create("10.0.0.0", "10.0.255.255", "слишком большой диапазон"),
                        Tuple.Create("198.51.100.20", "198.51.100.10", "конец раньше начала")
                    };

                    foreach (var item in cases)
                    {
                        start.Text = item.Item1;
                        end.Text = item.Item2;
                        RaiseClick(button);
                        Settle(300);

                        var message = Text(window, "DiscoveryMessageText");
                        var state = Text(window, "DiscoveryStateValueText");

                        Check(report, "F09 " + item.Item3, !string.IsNullOrWhiteSpace(message), "сообщение: «" + message + "», состояние: «" + state + "»");
                    }

                    SaveCapture(window, Path.Combine(output, "F09-discovery-validation.png"));
                    report.NotChecked("F09 Запуск обнаружения", "настоящий запуск требует Engine и сети; в тестовом окне — заглушка");
                });

                // F10. Настройки: профиль, проверка параметров опроса.
                Step(report, "F10 Настройки", () =>
                {
                    RaiseClick((ButtonBase)window.FindName("ShellSettingsButton"));
                    Settle(400);

                    var profiles = VisibleTexts((DependencyObject)window.FindName("ShellProfileSettingsList")).ToList();
                    Check(report, "F10 Профиль в списке", profiles.Any(item => item.Contains("Площадка А")), Shorten(string.Join(" | ", profiles)));

                    var interval = (TextBox)window.FindName("SettingsMonitoringIntervalTextBox");
                    var save = (ButtonBase)window.FindName("SettingsMonitoringSaveButton");
                    var original = interval.Text;

                    foreach (var value in new[] { "0", "abc", "-5" })
                    {
                        interval.Text = value;
                        RaiseClick(save);
                        Settle(300);

                        var status = Text(window, "SettingsMonitoringStatusText");
                        Check(report, "F10 Интервал «" + value + "»", !string.IsNullOrWhiteSpace(status) && interval.Text == value, "сообщение: «" + status + "»");
                    }

                    SaveCapture(window, Path.Combine(output, "F10-settings-validation.png"));

                    interval.Text = original;
                    RaiseClick(save);
                    Settle(300);
                    report.Note("F10 Сохранение корректного значения", "«" + Text(window, "SettingsMonitoringStatusText") + "»");
                });

                // F11. Экспорт схемы (PNG + CSV) тем же сервисом, что и кнопка «Экспорт».
                Step(report, "F11 Экспорт", () =>
                {
                    var provider =
                        typeof(MainWindow)
                            .GetField("_topologyExportSnapshotProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .GetValue(window);

                    var service =
                        (TopologySiteExportService)Activator.CreateInstance(
                            typeof(TopologySiteExportService),
                            provider);

                    var result =
                        service.Export(Path.Combine(output, "F11-site-export.png"));

                    var png = new FileInfo(result.PngPath);
                    var csv = File.ReadAllLines(result.CsvPath);

                    Check(report, "F11 PNG", png.Exists && png.Length > 10000, png.Exists ? png.Length + " байт" : "файла нет");
                    Check(report, "F11 CSV", csv.Length > managed, csv.Length + " строк; заголовок: " + (csv.Length > 0 ? csv[0] : "—"));
                    report.NotChecked("F11 Диалог сохранения", "системное окно сохранения файла автоматически не проходится — проверить вручную");
                });

                // F12. Мониторинг.
                report.NotChecked("F12 Мониторинг", "запуск и остановка опроса требуют Engine; в тестовом окне — заглушка «Остановлен»");
            }
            finally
            {
                window.Close();
                Settle();
            }
        }

        // F13. Перезапуск: вид карты сохраняется.
        private static void RunRestartScenario(
            string database,
            string output,
            FieldReport report)
        {
            Step(report, "F13 Перезапуск", () =>
            {
                string zoomBefore;

                var first = CreateLiveWindow(database, false);

                try
                {
                    PrepareWindow(first, 1440, 900);
                    RaiseClick((ButtonBase)first.FindName("MapZoomInButton"));
                    Settle(400);
                    RaiseClick((ButtonBase)first.FindName("MapZoomInButton"));
                    Settle(600);
                    zoomBefore = Text(first, "MapZoomValueText");
                }
                finally
                {
                    first.Close();
                    Settle(500);
                }

                var second = CreateLiveWindow(database, false);

                try
                {
                    PrepareWindow(second, 1440, 900);
                    Settle(800);
                    SaveCapture(second, Path.Combine(output, "F13-after-restart.png"));

                    var zoomAfter = Text(second, "MapZoomValueText");
                    Check(report, "F13 Масштаб после перезапуска", zoomAfter == zoomBefore, "до «" + zoomBefore + "», после «" + zoomAfter + "»");
                }
                finally
                {
                    second.Close();
                    Settle();
                }
            });
        }

        // F14. Быстрые нажатия «+» подряд без выбранного объекта (как сразу после запуска): вид остаётся на объектах.
        private static void RunRapidZoomScenario(
            string database,
            string output,
            FieldReport report)
        {
            Step(report, "F14 Быстрое масштабирование", () =>
            {
                var window = CreateLiveWindow(database, false);

                try
                {
                    PrepareWindow(window, 1440, 900);

                    var before = VisibleDeviceBorders(window).Count();

                    RaiseClick((ButtonBase)window.FindName("MapZoomInButton"));
                    RaiseClick((ButtonBase)window.FindName("MapZoomInButton"));
                    Settle(800);

                    var after = VisibleDeviceBorders(window).Count();
                    SaveCapture(window, Path.Combine(output, "F14-rapid-zoom.png"));

                    Check(report, "F14 Быстрое масштабирование", after > 0, "видимых рамок до " + before + ", после двух быстрых «+» — " + after + " (масштаб «" + Text(window, "MapZoomValueText") + "»)");
                }
                finally
                {
                    window.Close();
                    Settle();
                }
            });
        }

        // ---------- помощники ----------

        private static void Check(FieldReport report, string id, bool ok, string details)
        {
            if (ok)
            {
                report.Ok(id, details);
            }
            else
            {
                report.Error(id, details);
            }
        }

        private static string ButtonText(Window window, string name)
        {
            var button = window.FindName(name) as ContentControl;
            return button == null
                ? string.Empty
                : (button.Content as string ?? string.Join(" ", VisibleTexts(button)));
        }

        private static string Text(Window window, string name)
        {
            return (window.FindName(name) as TextBlock)?.Text ?? string.Empty;
        }

        private static IEnumerable<string> VisibleTexts(DependencyObject root)
        {
            if (root == null)
            {
                return Enumerable.Empty<string>();
            }

            return FindVisualDescendants<TextBlock>(root)
                .Where(item => item.IsVisible && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.Text.Trim());
        }

        private static Button EquipmentRowButton(Window window, string deviceName)
        {
            return FindVisualDescendants<Button>((DependencyObject)window.FindName("EquipmentList"))
                .FirstOrDefault(
                    item => FindVisualDescendants<TextBlock>(item).Any(text => text.Text == deviceName));
        }

        private static IEnumerable<string> EquipmentRowTexts(Window window, string deviceName)
        {
            RaiseClick((ButtonBase)window.FindName("ShellEquipmentButton"));
            Settle(300);
            return VisibleTexts(EquipmentRowButton(window, deviceName));
        }

        private static void SelectEquipmentRow(Window window, string deviceName)
        {
            RaiseClick((ButtonBase)window.FindName("ShellEquipmentButton"));
            Settle(300);

            var filter = (TextBox)window.FindName("EquipmentFilterTextBox");
            filter.Text = deviceName;
            Settle(300);

            var row = EquipmentRowButton(window, deviceName);

            if (row == null)
            {
                throw new InvalidOperationException("строка «" + deviceName + "» не найдена в таблице");
            }

            RaiseClick(row);
            Settle(500);
            filter.Text = string.Empty;
            Settle(300);
        }

        private static string Shorten(string text, int length = 300)
        {
            return text.Length <= length ? text : text.Substring(0, length) + "…";
        }
    }
}
