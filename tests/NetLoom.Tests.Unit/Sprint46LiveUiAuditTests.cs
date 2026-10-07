using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Locations;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.Topology;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Locations;
using NetLoom.Persistence.Sqlite.Lookup;
using NetLoom.Persistence.Sqlite.MapLayout;
using NetLoom.Persistence.Sqlite.Repositories;
using NetLoom.Persistence.Sqlite.Stp;
using NetLoom.Persistence.Sqlite.Topology;
using NetLoom.Topology.Alerts;
using NetLoom.Topology.Map;
using NetLoom.Topology.Refresh;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Shell;

namespace NetLoom.Tests.Unit
{
    // Автоматический аудит живого интерфейса на копии базы стенда (UI_DESIGN_RULES §5, §6, §8, §10).
    // Открывает настоящее окно с теми же провайдерами, что и Desktop, обходит разделы в обеих темах
    // и двух ширинах, выполняет типовые сценарии кликов и записывает находки и скриншоты.
    // База берётся из NETLOOM_UI_AUDIT_DB или artifacts/realistic-stand/operator-s46-pass3-visual.db;
    // без базы тест помечается как неподтверждённый (Inconclusive), исходная база не изменяется.
    [TestClass]
    public sealed partial class Sprint46LiveUiAuditTests
    {
        private const double LargeTextSize = 24.0;
        private const double LargeBoldTextSize = 18.66;
        private const double MinimumHitSize = 24.0;

        private static readonly (int Width, int Height)[] WindowSizes =
        {
            (1920, 1080),
            (1440, 900),
            (1366, 768)
        };

        // Эталонные макеты ADR-083 (docs/design), размер 1440×900.
        private static readonly Dictionary<string, string> Mockups =
            new Dictionary<string, string>
            {
                { "Map", "netloom-v2-1-karta" },
                { "Equipment", "netloom-v2-2-oborudovanie" },
                { "Alerts", "netloom-v2-4-preduprezhdeniya" },
                { "Discovery", "netloom-v2-5-obnaruzhenie" },
                { "Settings", "netloom-v2-6-nastroyki" }
            };

        private static readonly string[] Sections =
        {
            "ShellMapButton",
            "ShellEquipmentButton",
            "ShellAlertsButton",
            "ShellDiscoveryButton",
            "ShellSettingsButton"
        };

        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void LiveStandUiAuditReportsNoRuleViolations()
        {
            var repositoryRoot =
                FindRepositoryRoot();

            var sourceDatabase =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_UI_AUDIT_DB");

            if (string.IsNullOrWhiteSpace(
                    sourceDatabase))
            {
                sourceDatabase =
                    System.IO.Path.Combine(
                        repositoryRoot,
                        "artifacts",
                        "realistic-stand",
                        "operator-s46-pass3-visual.db");
            }

            if (!File.Exists(
                    sourceDatabase))
            {
                Assert.Inconclusive(
                    "Live UI audit database is not available: " +
                    sourceDatabase);
            }

            var outputDirectory =
                System.IO.Path.Combine(
                    repositoryRoot,
                    "artifacts",
                    "ui-audit");

            Directory.CreateDirectory(
                outputDirectory);

            foreach (var file in
                Directory.GetFiles(
                    outputDirectory))
            {
                File.Delete(file);
            }

            var workDatabase =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "netloom-ui-audit-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            File.Copy(
                sourceDatabase,
                workDatabase);

            var report =
                new AuditReport();

            try
            {
                RunOnSta(
                    () =>
                    {
                        foreach (var size in WindowSizes)
                        {
                            foreach (var dark in new[] { false, true })
                            {
                                AuditSections(
                                    workDatabase,
                                    size.Width,
                                    size.Height,
                                    dark,
                                    outputDirectory,
                                    report);
                            }
                        }

                        AuditInteractions(
                            workDatabase,
                            outputDirectory,
                            report);
                    });
            }
            finally
            {
                File.WriteAllText(
                    System.IO.Path.Combine(
                        outputDirectory,
                        "findings.txt"),
                    report.Render(),
                    new UTF8Encoding(false));

                TryDelete(workDatabase);
            }

            if (report.Count > 0)
            {
                Assert.Fail(
                    "Live UI audit found " +
                    report.Count +
                    " issue(s). See artifacts/ui-audit/findings.txt.");
            }
        }

        // ---------- обход разделов ----------

        private static void AuditSections(
            string databasePath,
            int width,
            int height,
            bool dark,
            string outputDirectory,
            AuditReport report)
        {
            var theme =
                dark ? "dark" : "light";

            var window =
                CreateLiveWindow(
                    databasePath,
                    dark);

            try
            {
                PrepareWindow(
                    window,
                    width,
                    height);

                foreach (var sectionButton in Sections)
                {
                    var button =
                        (Button)window.FindName(
                            sectionButton);

                    var section =
                        sectionButton
                            .Replace("Shell", string.Empty)
                            .Replace("Button", string.Empty);

                    var context =
                        section + " " + width + "x" + height + " " + theme;

                    if (button == null ||
                        !button.IsVisible)
                    {
                        report.Add(
                            "НАВИГАЦИЯ",
                            "ВЫСОКАЯ",
                            context,
                            "кнопка рейла " + sectionButton + " недоступна");
                        continue;
                    }

                    RaiseClick(button);
                    Settle();

                    var capturePath =
                        System.IO.Path.Combine(
                            outputDirectory,
                            Sanitize(context) + ".png");

                    SaveCapture(
                        window,
                        capturePath);

                    string mockup;

                    if (width == 1440 &&
                        Mockups.TryGetValue(
                            section,
                            out mockup))
                    {
                        SaveMockupComparison(
                            System.IO.Path.Combine(
                                FindRepositoryRoot(),
                                "docs",
                                "design",
                                mockup + (dark ? "-dark" : string.Empty) + ".png"),
                            capturePath,
                            System.IO.Path.Combine(
                                outputDirectory,
                                "compare-" + section + "-" + theme + ".png"));
                    }

                    AuditVisualTree(
                        window,
                        context,
                        report);

                    if (sectionButton == "ShellMapButton")
                    {
                        AuditStartupViewport(
                            window,
                            context,
                            report);
                    }
                }
            }
            finally
            {
                window.Close();
                Settle();
            }
        }

        // ---------- сценарии кликов ----------

        private static void AuditInteractions(
            string databasePath,
            string outputDirectory,
            AuditReport report)
        {
            var window =
                CreateLiveWindow(
                    databasePath,
                    false);

            try
            {
                PrepareWindow(
                    window,
                    1920,
                    1080);

                var mapButton =
                    (Button)window.FindName("ShellMapButton");
                var equipmentButton =
                    (Button)window.FindName("ShellEquipmentButton");
                var alertsButton =
                    (Button)window.FindName("ShellAlertsButton");
                var inspector =
                    (FrameworkElement)window.FindName("ShellInspectorPanel");
                var mapSurface =
                    (FrameworkElement)window.FindName("ShellMapSurface");
                var title =
                    (TextBlock)window.FindName("DiagnosticElementTitleText");

                // 1. Карта: щелчок по узлу открывает инспектор с именем устройства.
                RaiseClick(mapButton);
                Settle();

                var node =
                    VisibleDeviceBorders(window)
                        .FirstOrDefault();

                if (node == null)
                {
                    report.Add(
                        "ЛОГИКА",
                        "ВЫСОКАЯ",
                        "Карта 1920 light",
                        "на карте нет ни одного видимого узла после запуска");
                }
                else
                {
                    ClickNode(node);

                    if (!inspector.IsVisible ||
                        string.IsNullOrWhiteSpace(
                            title?.Text))
                    {
                        report.Add(
                            "ЛОГИКА",
                            "ВЫСОКАЯ",
                            "Карта: щелчок по узлу",
                            "инспектор не открылся или пуст после выбора узла");
                    }

                    SaveCapture(
                        window,
                        System.IO.Path.Combine(
                            outputDirectory,
                            "interaction-1-map-node.png"));
                }

                // 2. Предупреждения: щелчок по узлу на общей карте открывает инспектор (ADR-083 п. 4).
                RaiseClick(alertsButton);
                Settle();

                node =
                    VisibleDeviceBorders(window)
                        .FirstOrDefault();

                if (node != null)
                {
                    ClickNode(node);

                    if (!inspector.IsVisible)
                    {
                        report.Add(
                            "ЛОГИКА",
                            "ВЫСОКАЯ",
                            "Предупреждения: щелчок по узлу на карте",
                            "инспектор остаётся свёрнутым; выбор не показывает подробности (ADR-083 п. 4: инспектор одинаково работает для выбора в любом представлении)");
                    }

                    SaveCapture(
                        window,
                        System.IO.Path.Combine(
                            outputDirectory,
                            "interaction-2-alerts-node.png"));
                }

                // 3. Предупреждения: «Показать на карте» выделяет участников и держит их в видимой области.
                var showOnMap =
                    FindVisualDescendants<Button>(
                            (DependencyObject)window.FindName("AlertList"))
                        .FirstOrDefault(
                            item =>
                                item.IsVisible &&
                                string.Equals(
                                    item.Content as string,
                                    UiText.Get("AlertShowOnMapAction"),
                                    StringComparison.Ordinal));

                if (showOnMap == null)
                {
                    report.Add(
                        "ЛОГИКА",
                        "СРЕДНЯЯ",
                        "Предупреждения",
                        "в карточках нет видимой кнопки «Показать на карте»");
                }
                else
                {
                    RaiseClick(showOnMap);
                    Settle(600);

                    if (!mapSurface.IsVisible)
                    {
                        report.Add(
                            "ЛОГИКА",
                            "ВЫСОКАЯ",
                            "Предупреждения: «Показать на карте»",
                            "карта не видна после команды");
                    }

                    SaveCapture(
                        window,
                        System.IO.Path.Combine(
                            outputDirectory,
                            "interaction-3-alert-show-on-map.png"));
                }

                // 4. Оборудование: щелчок по строке показывает устройство и даёт путь на карту.
                RaiseClick(equipmentButton);
                Settle();

                // Переход из «Предупреждений» (раздел шириной 420) не должен оставлять скрытыми
                // столбцы, для которых на 1920 места достаточно (§5 «скрывать по приоритету при нехватке ширины»).
                var section =
                    (FrameworkElement)window.FindName(
                        "ShellSectionSurface");
                var categoryHeader =
                    (FrameworkElement)window.FindName(
                        "EquipmentCategoryHeaderText");

                if (section != null &&
                    categoryHeader != null &&
                    section.ActualWidth >= 1000 &&
                    categoryHeader.Visibility != Visibility.Visible)
                {
                    report.Add(
                        "РАСКЛАДКА",
                        "СРЕДНЯЯ",
                        "Предупреждения → Оборудование 1920",
                        "столбец «Категория» скрыт при ширине раздела " +
                        section.ActualWidth.ToString("0", CultureInfo.InvariantCulture) +
                        " px (§5)");
                }

                var row =
                    FindVisualDescendants<Button>(
                            (DependencyObject)window.FindName("EquipmentList"))
                        .FirstOrDefault(
                            item =>
                                item.IsVisible &&
                                item.IsEnabled &&
                                item.Tag is Guid);

                if (row == null)
                {
                    report.Add(
                        "ЛОГИКА",
                        "ВЫСОКАЯ",
                        "Оборудование",
                        "в таблице нет ни одной выбираемой строки");
                }
                else
                {
                    RaiseClick(row);
                    Settle(600);

                    if (!inspector.IsVisible ||
                        string.IsNullOrWhiteSpace(
                            title?.Text))
                    {
                        report.Add(
                            "ЛОГИКА",
                            "ВЫСОКАЯ",
                            "Оборудование: щелчок по строке",
                            "инспектор не показывает выбранное устройство");
                    }

                    SaveCapture(
                        window,
                        System.IO.Path.Combine(
                            outputDirectory,
                            "interaction-4-equipment-row.png"));

                    var primary =
                        (Button)window.FindName(
                            "InspectorPrimaryActionButton");

                    var hasShowOnMapPath =
                        mapSurface.IsVisible ||
                        (primary != null &&
                         primary.IsVisible &&
                         primary.IsEnabled);

                    if (!hasShowOnMapPath)
                    {
                        report.Add(
                            "ЛОГИКА",
                            "ВЫСОКАЯ",
                            "Оборудование: выбранное устройство",
                            "нет пути на карту: ни перехода, ни действия «Показать на карте» в инспекторе");
                    }
                    else if (primary != null &&
                             primary.IsVisible &&
                             primary.IsEnabled &&
                             !mapSurface.IsVisible)
                    {
                        RaiseClick(primary);
                        Settle(600);

                        if (!mapSurface.IsVisible)
                        {
                            report.Add(
                                "ЛОГИКА",
                                "ВЫСОКАЯ",
                                "Оборудование: «" +
                                (primary.Content as string) +
                                "» в инспекторе",
                                "команда не переводит на карту: карта остаётся скрытой");
                        }
                    }
                }

                // 4б. Хлебные крошки (ADR-083 п. 2): у устройства с размещением в шапке путь «… › …»,
                // полный путь — в подсказке.
                RaiseClick(equipmentButton);
                Settle();

                var placedRow =
                    FindVisualDescendants<Button>(
                            (DependencyObject)window.FindName("EquipmentList"))
                        .FirstOrDefault(
                            item =>
                                item.IsVisible &&
                                item.IsEnabled &&
                                item.Tag is Guid &&
                                FindVisualDescendants<TextBlock>(item)
                                    .Any(
                                        text =>
                                            text.Text != null &&
                                            text.Text.Contains(" / ")));

                var breadcrumb =
                    (TextBlock)window.FindName(
                        "ShellBreadcrumbText");

                if (placedRow != null &&
                    breadcrumb != null)
                {
                    RaiseClick(placedRow);
                    Settle(400);

                    if (breadcrumb.Text == null ||
                        !breadcrumb.Text.Contains("›") ||
                        breadcrumb.ToolTip == null)
                    {
                        report.Add(
                            "ЛОГИКА",
                            "СРЕДНЯЯ",
                            "Оборудование: устройство с размещением",
                            "хлебные крошки не показывают путь размещения: «" +
                            breadcrumb.Text +
                            "» (ADR-083 п. 2)");
                    }

                    SaveCapture(
                        window,
                        System.IO.Path.Combine(
                            outputDirectory,
                            "interaction-4b-breadcrumb.png"));
                }

                // 5. Глобальный поиск: ввод имени, Enter выбирает устройство и показывает его на карте.
                var search =
                    (TextBox)window.FindName(
                        "ShellGlobalSearchTextBox");
                var popup =
                    (Popup)window.FindName(
                        "ShellGlobalSearchPopup");
                var results =
                    (ListBox)window.FindName(
                        "ShellGlobalSearchResultsList");

                RaiseClick(mapButton);
                Settle();

                var deviceName =
                    VisibleDeviceBorders(window)
                        .Select(DeviceText)
                        .FirstOrDefault(
                            text =>
                                !string.IsNullOrWhiteSpace(text));

                if (search == null ||
                    deviceName == null)
                {
                    report.Add(
                        "ЛОГИКА",
                        "СРЕДНЯЯ",
                        "Поиск",
                        "поле поиска или имя устройства для проверки недоступны");
                }
                else
                {
                    search.Focus();
                    search.Text =
                        deviceName.Substring(
                            0,
                            Math.Min(
                                8,
                                deviceName.Length));
                    Settle(600);

                    if (popup == null ||
                        !popup.IsOpen ||
                        results == null ||
                        results.Items.Count == 0)
                    {
                        report.Add(
                            "ЛОГИКА",
                            "ВЫСОКАЯ",
                            "Поиск: ввод «" + search.Text + "»",
                            "выпадающая панель результатов не открылась или пуста");
                    }
                    else
                    {
                        RaiseKey(
                            search,
                            Key.Enter);
                        Settle(600);

                        if (popup.IsOpen)
                        {
                            report.Add(
                                "ЛОГИКА",
                                "СРЕДНЯЯ",
                                "Поиск: Enter",
                                "панель результатов не закрылась после выбора");
                        }

                        if (!inspector.IsVisible ||
                            string.IsNullOrWhiteSpace(
                                title?.Text))
                        {
                            report.Add(
                                "ЛОГИКА",
                                "ВЫСОКАЯ",
                                "Поиск: Enter",
                                "выбранный результат не открыл инспектор");
                        }
                    }

                    SaveCapture(
                        window,
                        System.IO.Path.Combine(
                            outputDirectory,
                            "interaction-5-search.png"));
                }

                // 6. Свернуть и раскрыть инспектор.
                var collapse =
                    (Button)window.FindName(
                        "ShellInspectorCollapseButton");
                var reveal =
                    (Button)window.FindName(
                        "ShellInspectorRevealButton");

                if (collapse != null &&
                    collapse.IsVisible)
                {
                    RaiseClick(collapse);
                    Settle();

                    if (inspector.IsVisible ||
                        reveal == null ||
                        !reveal.IsVisible)
                    {
                        report.Add(
                            "ЛОГИКА",
                            "СРЕДНЯЯ",
                            "Инспектор: свернуть",
                            "инспектор не свернулся или нет кнопки раскрытия");
                    }
                    else
                    {
                        RaiseClick(reveal);
                        Settle();

                        if (!inspector.IsVisible)
                        {
                            report.Add(
                                "ЛОГИКА",
                                "СРЕДНЯЯ",
                                "Инспектор: раскрыть",
                                "инспектор не раскрылся");
                        }
                    }
                }
            }
            finally
            {
                window.Close();
                Settle();
            }
        }

        // ---------- проверки визуального дерева ----------

        private static void AuditVisualTree(
            Window window,
            string context,
            AuditReport report)
        {
            foreach (var element in
                FindVisualDescendants<FrameworkElement>(window))
            {
                if (!element.IsVisible ||
                    element.ActualWidth <= 0.0 ||
                    element.ActualHeight <= 0.0 ||
                    IsInsideIgnoredContainer(element))
                {
                    continue;
                }

                AuditLayoutClip(
                    element,
                    context,
                    report);

                var text =
                    element as TextBlock;

                if (text != null)
                {
                    AuditTrimming(
                        text,
                        context,
                        report);
                    AuditTextContrast(
                        text,
                        context,
                        report);
                }

                var icon =
                    element as System.Windows.Shapes.Path;

                if (icon != null &&
                    FindAncestor<ButtonBase>(icon) != null)
                {
                    AuditIconContrast(
                        icon,
                        context,
                        report);
                }

                var control =
                    element as Control;

                if (control != null &&
                    control.IsEnabled &&
                    (control is ButtonBase ||
                     control is ComboBox ||
                     control is TextBox))
                {
                    AuditAutomationName(
                        control,
                        context,
                        report);
                    AuditHitSize(
                        control,
                        context,
                        report);
                }
            }
        }

        private static void AuditLayoutClip(
            FrameworkElement element,
            string context,
            AuditReport report)
        {
            var clip =
                LayoutInformation.GetLayoutClip(
                    element);

            if (clip == null)
            {
                return;
            }

            var bounds =
                clip.Bounds;

            var cutWidth =
                element.RenderSize.Width - bounds.Width;
            var cutHeight =
                element.RenderSize.Height - bounds.Height;

            if (cutWidth <= 1.0 &&
                cutHeight <= 1.0)
            {
                return;
            }

            // Полностью скрытая по приоритету колонка (ширина 0) — осознанное скрытие (§5), не обрезка.
            if (bounds.Width <= 0.5 ||
                bounds.Height <= 0.5)
            {
                return;
            }

            // Пустые контейнеры без видимого содержимого не считаются обрезанным содержимым.
            if (!(element is TextBlock) &&
                !(element is System.Windows.Shapes.Shape) &&
                !(element is Image) &&
                !(element is Control))
            {
                return;
            }

            report.Add(
                "ОБРЕЗКА",
                "ВЫСОКАЯ",
                context,
                Describe(element) +
                " обрезан раскладкой: видно " +
                Size(bounds.Width, bounds.Height) +
                " из " +
                Size(element.RenderSize.Width, element.RenderSize.Height) +
                " (§5, §10)");
        }

        private static void AuditTrimming(
            TextBlock text,
            string context,
            AuditReport report)
        {
            if (text.TextTrimming == TextTrimming.None ||
                text.TextWrapping != TextWrapping.NoWrap ||
                string.IsNullOrEmpty(text.Text))
            {
                return;
            }

            var formatted =
                new FormattedText(
                    text.Text,
                    CultureInfo.CurrentUICulture,
                    text.FlowDirection,
                    new Typeface(
                        text.FontFamily,
                        text.FontStyle,
                        text.FontWeight,
                        text.FontStretch),
                    text.FontSize,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(text).PixelsPerDip);

            var available =
                text.ActualWidth -
                text.Padding.Left -
                text.Padding.Right;

            if (formatted.WidthIncludingTrailingWhitespace <=
                available + 0.5)
            {
                return;
            }

            var hasTooltip =
                HasTooltip(text);

            // §5: в таблице при нехватке ширины допустимо обрезать с подсказкой — это сведение, не нарушение.
            report.Add(
                "ОБРЕЗКА",
                hasTooltip ? "ИНФО" : "СРЕДНЯЯ",
                context,
                Describe(text) +
                " обрезан многоточием" +
                (hasTooltip
                    ? " (полный текст в подсказке)"
                    : " без подсказки с полным текстом") +
                " (§5, §10)");
        }

        private static void AuditTextContrast(
            TextBlock text,
            string context,
            AuditReport report)
        {
            if (!text.IsEnabled ||
                string.IsNullOrWhiteSpace(text.Text))
            {
                return;
            }

            var foreground =
                text.Foreground as SolidColorBrush;

            if (foreground == null)
            {
                return;
            }

            Color background;

            if (!TryResolveBackground(
                    text,
                    out background))
            {
                return;
            }

            var color =
                Blend(
                    foreground.Color,
                    foreground.Opacity * EffectiveOpacity(text),
                    background);

            var ratio =
                ContrastRatio(
                    color,
                    background);

            var large =
                text.FontSize >= LargeTextSize ||
                (text.FontSize >= LargeBoldTextSize &&
                 text.FontWeight.ToOpenTypeWeight() >= 700);

            var required =
                large ? 3.0 : 4.5;

            if (ratio + 0.005 >= required)
            {
                return;
            }

            report.Add(
                "КОНТРАСТ",
                ratio < 3.0 ? "ВЫСОКАЯ" : "СРЕДНЯЯ",
                context,
                Describe(text) +
                ": " +
                ratio.ToString("0.00", CultureInfo.InvariantCulture) +
                ":1 при норме " +
                required.ToString("0.0", CultureInfo.InvariantCulture) +
                ":1 (текст " +
                Hex(color) +
                " на " +
                Hex(background) +
                ") (§6)");
        }

        private static void AuditIconContrast(
            System.Windows.Shapes.Path icon,
            string context,
            AuditReport report)
        {
            if (!icon.IsEnabled)
            {
                return;
            }

            var stroke =
                (icon.Stroke as SolidColorBrush) ??
                (icon.Fill as SolidColorBrush);

            if (stroke == null ||
                stroke.Color.A == 0)
            {
                return;
            }

            Color background;

            if (!TryResolveBackground(
                    icon,
                    out background))
            {
                return;
            }

            var color =
                Blend(
                    stroke.Color,
                    stroke.Opacity * EffectiveOpacity(icon),
                    background);

            var ratio =
                ContrastRatio(
                    color,
                    background);

            if (ratio + 0.005 >= 3.0)
            {
                return;
            }

            report.Add(
                "КОНТРАСТ",
                "СРЕДНЯЯ",
                context,
                "значок в " +
                Describe(FindAncestor<ButtonBase>(icon)) +
                ": " +
                ratio.ToString("0.00", CultureInfo.InvariantCulture) +
                ":1 при норме 3.0:1 (" +
                Hex(color) +
                " на " +
                Hex(background) +
                ") (§6)");
        }

        private static void AuditAutomationName(
            Control control,
            string context,
            AuditReport report)
        {
            // Внутренние части шаблонов без фокуса (например, кнопка раскрытия ComboBox) — не отдельные элементы для оператора.
            if (!control.Focusable)
            {
                return;
            }

            var peer =
                UIElementAutomationPeer.CreatePeerForElement(
                    control);

            var name =
                peer?.GetName();

            if (!string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            report.Add(
                "ДОСТУПНОСТЬ",
                "СРЕДНЯЯ",
                context,
                Describe(control) +
                " без имени для UI Automation (§8)");
        }

        private static void AuditHitSize(
            Control control,
            string context,
            AuditReport report)
        {
            if (!(control is ButtonBase))
            {
                return;
            }

            if (control.ActualWidth + 0.5 >= MinimumHitSize &&
                control.ActualHeight + 0.5 >= MinimumHitSize)
            {
                return;
            }

            report.Add(
                "ДОСТУПНОСТЬ",
                "НИЗКАЯ",
                context,
                Describe(control) +
                " меньше 24×24: " +
                Size(control.ActualWidth, control.ActualHeight) +
                " (§8)");
        }

        private static void AuditStartupViewport(
            Window window,
            string context,
            AuditReport report)
        {
            var scroll =
                (ScrollViewer)window.FindName(
                    "MapScrollViewer");

            if (scroll == null)
            {
                return;
            }

            var viewport =
                new Rect(
                    0,
                    0,
                    scroll.ViewportWidth,
                    scroll.ViewportHeight);

            var nodes =
                DeviceBorders(window)
                    .Where(item => item.IsVisible)
                    .ToArray();

            if (nodes.Length == 0)
            {
                return;
            }

            var inside = 0;
            var partial = 0;

            foreach (var node in nodes)
            {
                var bounds =
                    node.TransformToAncestor(scroll)
                        .TransformBounds(
                            new Rect(
                                0,
                                0,
                                node.ActualWidth,
                                node.ActualHeight));

                if (viewport.Contains(bounds))
                {
                    inside++;
                }
                else if (viewport.IntersectsWith(bounds))
                {
                    partial++;
                }
            }

            if (partial > 0 ||
                inside < nodes.Length)
            {
                // Перенесено в Sprint 49 (docs/sprint46-mockup-gap.md, M3): при старте действует порог
                // читаемости NetLoom.Map.ReadableZoomMin; полезный вид большого объекта — работа Sprint 49.
                // Строка остаётся сведением, чтобы изменение было видно в каждом прогоне.
                report.Add(
                    "РАСКЛАДКА",
                    "ИНФО",
                    context,
                    "после запуска на карте целиком видно " +
                    inside +
                    " из " +
                    nodes.Length +
                    " узлов, обрезано краем " +
                    partial +
                    " (перенесено в Sprint 49, акт сверки M3)");
            }
        }

        // ---------- окно на живых данных ----------

        private static MainWindow CreateLiveWindow(
            string databasePath,
            bool dark)
        {
            return CreateLiveWindow(
                databasePath,
                dark,
                new StoppedMonitoringControl());
        }

        // Sprint 47: тот же стенд, но с мониторингом, которым управляет тест.
        private static MainWindow CreateLiveWindow(
            string databasePath,
            bool dark,
            IMonitoringControl monitoringControl)
        {
            var connectionFactory =
                new SqliteConnectionFactory(
                    databasePath);

            new DatabaseInitializer(
                connectionFactory)
                .Initialize();

            var topologyRepository =
                new SqliteMaterializedTopologyRepository(
                    connectionFactory);
            var stpStore =
                new SqliteStpObservationStore(
                    connectionFactory);
            var locationRepository =
                new SqliteLocationRepository(
                    connectionFactory);
            var mapLayoutStore =
                new SqliteMapLayoutStore(
                    connectionFactory);
            var mapProvider =
                new MaterializedMapSnapshotProvider(
                    topologyRepository,
                    locationRepository,
                    new MaterializedTopologyMapProjector());
            var alertProvider =
                new MaterializedTopologyAlertSnapshotProvider(
                    topologyRepository,
                    stpStore);
            var refreshProvider =
                new MaterializedTopologyRefreshSnapshotProvider(
                    new SqliteMaterializedTopologyReadSetReader(
                        connectionFactory),
                    mapProvider,
                    alertProvider);
            var profiles =
                new AccessProfileRepository(
                        connectionFactory)
                    .GetEnabled();

            return new MainWindow(
                refreshProvider,
                new SqliteMacIpLookupReader(
                    connectionFactory),
                mapLayoutStore,
                new ManualTopologyService(
                    topologyRepository,
                    new SqliteManualTopologyAuditStore(
                        connectionFactory)),
                new LocationTopologyService(
                    locationRepository,
                    topologyRepository),
                mapLayoutStore,
                monitoringControl,
                new IdleDiscoveryControl(),
                profiles,
                new NoopMaterializer(),
                new MemoryShellStateStore(
                    new UiShellState(
                        null,
                        dark
                            ? UiShellTheme.Dark
                            : UiShellTheme.Light)));
        }

        private static void PrepareWindow(
            Window window,
            int width,
            int height)
        {
            window.Width = width;
            window.Height = height;
            window.Left = -30000;
            window.Top = -30000;
            window.ShowInTaskbar = false;
            window.WindowStartupLocation =
                WindowStartupLocation.Manual;

            window.Show();

            var deadline =
                DateTime.UtcNow.AddSeconds(10);

            while ((window.ActualWidth <= 0 ||
                    window.ActualHeight <= 0) &&
                   DateTime.UtcNow < deadline)
            {
                Settle(50);
            }

            // Ждём первой загрузки снимка топологии.
            Settle(1500);
        }

        // ---------- вспомогательные ----------

        private static IEnumerable<Border> DeviceBorders(
            Window window)
        {
            var canvas =
                window.FindName("MapCanvas") as Canvas;

            return canvas == null
                ? Enumerable.Empty<Border>()
                : canvas.Children
                    .OfType<Border>()
                    .Where(item => item.Tag is Guid);
        }

        private static IEnumerable<Border> VisibleDeviceBorders(
            Window window)
        {
            var scroll =
                (ScrollViewer)window.FindName(
                    "MapScrollViewer");

            foreach (var border in DeviceBorders(window))
            {
                if (!border.IsVisible ||
                    scroll == null)
                {
                    continue;
                }

                var bounds =
                    border.TransformToAncestor(scroll)
                        .TransformBounds(
                            new Rect(
                                0,
                                0,
                                border.ActualWidth,
                                border.ActualHeight));

                if (new Rect(
                        0,
                        0,
                        scroll.ViewportWidth,
                        scroll.ViewportHeight)
                    .Contains(bounds))
                {
                    yield return border;
                }
            }
        }

        private static string DeviceText(
            Border border)
        {
            return FindVisualDescendants<TextBlock>(border)
                .Select(item => item.Text)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .OrderByDescending(item => item.Length)
                .FirstOrDefault();
        }

        private static void ClickNode(
            Border border)
        {
            border.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = border
                });

            border.RaiseEvent(
                new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonUpEvent,
                    Source = border
                });

            Settle(400);
        }

        private static void RaiseClick(
            ButtonBase button)
        {
            if (button == null)
            {
                return;
            }

            button.RaiseEvent(
                new RoutedEventArgs(
                    ButtonBase.ClickEvent));
        }

        private static void RaiseKey(
            UIElement target,
            Key key)
        {
            var source =
                PresentationSource.FromVisual(target);

            if (source == null)
            {
                return;
            }

            target.RaiseEvent(
                new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    source,
                    0,
                    key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                });
        }

        private static bool IsInsideIgnoredContainer(
            DependencyObject element)
        {
            // Содержимое карты законно уходит за край видимой области; внутренности
            // полос прокрутки и полей ввода стандартны и обрезаются самим WPF.
            var current =
                VisualTreeHelper.GetParent(element);

            while (current != null)
            {
                var named =
                    current as FrameworkElement;

                if (named != null &&
                    named.Name == "MapCanvas")
                {
                    return true;
                }

                if (current is ScrollBar ||
                    current is TextBoxBase)
                {
                    return true;
                }

                current =
                    VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool HasTooltip(
            FrameworkElement element)
        {
            DependencyObject current =
                element;

            for (var depth = 0;
                 depth < 4 && current != null;
                 depth++)
            {
                var framework =
                    current as FrameworkElement;

                if (framework?.ToolTip != null)
                {
                    return true;
                }

                current =
                    VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool TryResolveBackground(
            DependencyObject element,
            out Color background)
        {
            var layers =
                new List<SolidColorBrush>();

            var current =
                VisualTreeHelper.GetParent(element);

            while (current != null)
            {
                Brush brush = null;

                // Учитываем только то, что реально рисует фон: панели и рамки.
                // Background у Control — лишь входной параметр шаблона и может не рисоваться.
                var panel = current as Panel;
                var border = current as Border;

                if (panel != null)
                {
                    brush = panel.Background;
                }
                else if (border != null)
                {
                    brush = border.Background;
                }

                var solid =
                    brush as SolidColorBrush;

                if (brush != null &&
                    solid == null)
                {
                    // Градиент или изображение под текстом — контраст этим способом не измерить.
                    background = default(Color);
                    return false;
                }

                if (solid != null &&
                    solid.Color.A > 0 &&
                    solid.Opacity > 0)
                {
                    layers.Add(solid);

                    if (solid.Color.A == 255 &&
                        solid.Opacity >= 1.0)
                    {
                        break;
                    }
                }

                current =
                    VisualTreeHelper.GetParent(current);
            }

            if (layers.Count == 0)
            {
                background = default(Color);
                return false;
            }

            var result =
                Colors.White;

            for (var index = layers.Count - 1;
                 index >= 0;
                 index--)
            {
                result =
                    Blend(
                        layers[index].Color,
                        layers[index].Opacity,
                        result);
            }

            background = result;
            return true;
        }

        private static double EffectiveOpacity(
            DependencyObject element)
        {
            var opacity = 1.0;
            var current = element;

            while (current != null)
            {
                var ui = current as UIElement;

                if (ui != null)
                {
                    opacity *= ui.Opacity;
                }

                current =
                    VisualTreeHelper.GetParent(current);
            }

            return opacity;
        }

        private static Color Blend(
            Color top,
            double opacity,
            Color bottom)
        {
            var alpha =
                top.A / 255.0 * opacity;

            return Color.FromRgb(
                (byte)Math.Round(top.R * alpha + bottom.R * (1 - alpha)),
                (byte)Math.Round(top.G * alpha + bottom.G * (1 - alpha)),
                (byte)Math.Round(top.B * alpha + bottom.B * (1 - alpha)));
        }

        private static double ContrastRatio(
            Color first,
            Color second)
        {
            var a = Luminance(first);
            var b = Luminance(second);

            return (Math.Max(a, b) + 0.05) /
                   (Math.Min(a, b) + 0.05);
        }

        private static double Luminance(
            Color color)
        {
            Func<byte, double> channel =
                value =>
                {
                    var c = value / 255.0;
                    return c <= 0.03928
                        ? c / 12.92
                        : Math.Pow((c + 0.055) / 1.055, 2.4);
                };

            return 0.2126 * channel(color.R) +
                   0.7152 * channel(color.G) +
                   0.0722 * channel(color.B);
        }

        private static string Hex(
            Color color)
        {
            return "#" +
                   color.R.ToString("X2") +
                   color.G.ToString("X2") +
                   color.B.ToString("X2");
        }

        private static string Size(
            double width,
            double height)
        {
            return width.ToString("0", CultureInfo.InvariantCulture) +
                   "×" +
                   height.ToString("0", CultureInfo.InvariantCulture);
        }

        private static string Describe(
            DependencyObject element)
        {
            if (element == null)
            {
                return "элемент";
            }

            var framework =
                element as FrameworkElement;

            var builder =
                new StringBuilder();

            builder.Append(
                element.GetType().Name);

            if (!string.IsNullOrEmpty(framework?.Name))
            {
                builder.Append(" #").Append(framework.Name);
            }

            var text =
                (element as TextBlock)?.Text ??
                ((element as ContentControl)?.Content as string);

            if (string.IsNullOrEmpty(text) &&
                element is ContentControl)
            {
                text =
                    FindVisualDescendants<TextBlock>(element)
                        .Select(item => item.Text)
                        .FirstOrDefault(
                            item => !string.IsNullOrWhiteSpace(item));
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var clean =
                    text.Replace('\r', ' ').Replace('\n', ' ');

                builder.Append(" «")
                    .Append(
                        clean.Length > 60
                            ? clean.Substring(0, 60) + "…"
                            : clean)
                    .Append("»");
            }

            var owner =
                NamedAncestor(element);

            if (owner != null)
            {
                builder.Append(" в #").Append(owner);
            }

            return builder.ToString();
        }

        private static string NamedAncestor(
            DependencyObject element)
        {
            var current =
                VisualTreeHelper.GetParent(element);

            while (current != null)
            {
                var framework =
                    current as FrameworkElement;

                if (!string.IsNullOrEmpty(framework?.Name) &&
                    !framework.Name.StartsWith("PART_", StringComparison.Ordinal))
                {
                    return framework.Name;
                }

                current =
                    VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static T FindAncestor<T>(
            DependencyObject element)
            where T : DependencyObject
        {
            var current =
                VisualTreeHelper.GetParent(element);

            while (current != null)
            {
                var typed = current as T;

                if (typed != null)
                {
                    return typed;
                }

                current =
                    VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static IEnumerable<T> FindVisualDescendants<T>(
            DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            var stack =
                new Stack<DependencyObject>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current =
                    stack.Pop();

                var count =
                    VisualTreeHelper.GetChildrenCount(current);

                for (var index = count - 1;
                     index >= 0;
                     index--)
                {
                    var child =
                        VisualTreeHelper.GetChild(
                            current,
                            index);

                    var typed = child as T;

                    if (typed != null)
                    {
                        yield return typed;
                    }

                    stack.Push(child);
                }
            }
        }

        private static void SaveCapture(
            Window window,
            string path)
        {
            var content =
                window.Content as FrameworkElement;

            if (content == null)
            {
                return;
            }

            content.UpdateLayout();

            var bitmap =
                new RenderTargetBitmap(
                    Math.Max(1, (int)Math.Ceiling(content.ActualWidth)),
                    Math.Max(1, (int)Math.Ceiling(content.ActualHeight)),
                    96,
                    96,
                    PixelFormats.Pbgra32);

            bitmap.Render(content);

            var encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(bitmap));

            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private static void SaveMockupComparison(
            string mockupPath,
            string livePath,
            string outputPath)
        {
            if (!File.Exists(mockupPath) ||
                !File.Exists(livePath))
            {
                return;
            }

            var mockup =
                LoadBitmap(mockupPath);
            var live =
                LoadBitmap(livePath);

            var gap = 24;
            var label = 36;
            var width =
                mockup.PixelWidth + gap + live.PixelWidth;
            var height =
                label + Math.Max(mockup.PixelHeight, live.PixelHeight);

            var visual =
                new DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(
                    Brushes.White,
                    null,
                    new Rect(0, 0, width, height));

                var typeface =
                    new Typeface("Segoe UI");

                context.DrawText(
                    new FormattedText(
                        "Макет: " + System.IO.Path.GetFileName(mockupPath),
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        18,
                        Brushes.Black,
                        1.0),
                    new Point(8, 8));

                context.DrawText(
                    new FormattedText(
                        "Сейчас: " + System.IO.Path.GetFileName(livePath),
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        18,
                        Brushes.Black,
                        1.0),
                    new Point(mockup.PixelWidth + gap + 8, 8));

                context.DrawImage(
                    mockup,
                    new Rect(0, label, mockup.PixelWidth, mockup.PixelHeight));
                context.DrawImage(
                    live,
                    new Rect(mockup.PixelWidth + gap, label, live.PixelWidth, live.PixelHeight));
            }

            var bitmap =
                new RenderTargetBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Pbgra32);

            bitmap.Render(visual);

            var encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(bitmap));

            using (var stream = File.Create(outputPath))
            {
                encoder.Save(stream);
            }
        }

        private static BitmapSource LoadBitmap(
            string path)
        {
            var image =
                new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            // Приводим к 96 dpi, чтобы пиксели макета и снимка совпадали по масштабу.
            if (Math.Abs(image.DpiX - 96.0) < 0.5)
            {
                return image;
            }

            var stride =
                image.PixelWidth * 4;
            var pixels =
                new byte[stride * image.PixelHeight];

            new FormatConvertedBitmap(
                    image,
                    PixelFormats.Pbgra32,
                    null,
                    0)
                .CopyPixels(
                    pixels,
                    stride,
                    0);

            return BitmapSource.Create(
                image.PixelWidth,
                image.PixelHeight,
                96,
                96,
                PixelFormats.Pbgra32,
                null,
                pixels,
                stride);
        }

        private static string Sanitize(
            string value)
        {
            var invalid =
                System.IO.Path.GetInvalidFileNameChars();

            return new string(
                value
                    .Select(
                        item =>
                            invalid.Contains(item) || item == ' '
                                ? '-'
                                : item)
                    .ToArray());
        }

        private static void Settle(
            int milliseconds = 150)
        {
            var deadline =
                DateTime.UtcNow.AddMilliseconds(milliseconds);

            do
            {
                var frame =
                    new DispatcherFrame();

                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new DispatcherOperationCallback(
                        state =>
                        {
                            ((DispatcherFrame)state).Continue = false;
                            return null;
                        }),
                    frame);

                Dispatcher.PushFrame(frame);
                Thread.Sleep(10);
            }
            while (DateTime.UtcNow < deadline);
        }

        private static string FindRepositoryRoot()
        {
            var directory =
                new DirectoryInfo(
                    AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(
                        System.IO.Path.Combine(
                            directory.FullName,
                            "NetLoom.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "NetLoom.sln was not found above the test output directory.");
        }

        private static void TryDelete(
            string path)
        {
            foreach (var candidate in new[]
                     {
                         path,
                         path + "-wal",
                         path + "-shm"
                     })
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        File.Delete(candidate);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void RunOnSta(
            Action action)
        {
            Exception failure = null;

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
                            failure = error;
                        }
                    });

            thread.SetApartmentState(
                ApartmentState.STA);
            thread.Start();

            if (!thread.Join(
                    TimeSpan.FromMinutes(10)))
            {
                throw new AssertFailedException(
                    "Live UI audit did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        // ---------- отчёт ----------

        private sealed class AuditReport
        {
            private readonly List<string[]> _items =
                new List<string[]>();

            private readonly HashSet<string> _keys =
                new HashSet<string>(StringComparer.Ordinal);

            public int Count =>
                _items.Count(
                    item => item[1] != "ИНФО");

            public void Add(
                string category,
                string severity,
                string context,
                string message)
            {
                // Одна строка — одна причина (§10): повторы одной причины в разных разделах
                // сводятся в одну строку со списком мест.
                var key =
                    category + "|" + message;

                if (_keys.Add(key))
                {
                    _items.Add(
                        new[]
                        {
                            category,
                            severity,
                            message,
                            context
                        });
                    return;
                }

                var existing =
                    _items.First(
                        item =>
                            item[0] + "|" + item[2] == key);

                if (!existing[3].Split(';')
                        .Select(item => item.Trim())
                        .Contains(context))
                {
                    existing[3] += "; " + context;
                }
            }

            public string Render()
            {
                var order =
                    new[] { "ВЫСОКАЯ", "СРЕДНЯЯ", "НИЗКАЯ", "ИНФО" };

                var builder =
                    new StringBuilder();

                builder.AppendLine(
                    "NetLoom live UI audit — " +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                builder.AppendLine(
                    "Найдено нарушений: " + Count +
                    ", сведений: " + (_items.Count - Count));
                builder.AppendLine();

                foreach (var item in
                    _items
                        .OrderBy(entry => Array.IndexOf(order, entry[1]))
                        .ThenBy(entry => entry[0], StringComparer.Ordinal)
                        .ThenBy(entry => entry[2], StringComparer.Ordinal))
                {
                    builder.AppendLine(
                        "[" + item[1] + "] " + item[0] + " — " + item[2]);
                    builder.AppendLine(
                        "    где: " + item[3]);
                }

                return builder.ToString();
            }
        }

        // ---------- заглушки внешних процессов ----------

        private sealed class MemoryShellStateStore :
            IUiShellStateStore
        {
            private UiShellState _state;

            public MemoryShellStateStore(
                UiShellState state)
            {
                _state = state;
            }

            public UiShellState Load()
            {
                return _state;
            }

            public void Save(
                UiShellState state)
            {
                _state = state;
            }
        }

        private sealed class StoppedMonitoringControl :
            IMonitoringControl
        {
            public MonitoringControlSnapshot Current { get; } =
                new MonitoringControlSnapshot(
                    MonitoringControlState.Stopped,
                    null,
                    null,
                    null);

            public event EventHandler<MonitoringControlSnapshotChangedEventArgs>
                SnapshotChanged
            {
                add { }
                remove { }
            }

            public Task StartAsync(
                MonitoringTarget target,
                MonitoringSessionPolicy policy,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task PollNowAsync(
                MonitoringTarget targetWhenStopped,
                MonitoringSessionPolicy policyWhenStopped,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class IdleDiscoveryControl :
            IDiscoveryControl
        {
            public DiscoveryControlSnapshot Current { get; } =
                new DiscoveryControlSnapshot(
                    DiscoveryControlState.Idle,
                    null,
                    null,
                    0,
                    0,
                    0,
                    null,
                    null);

            public event EventHandler<DiscoveryControlSnapshotChangedEventArgs>
                SnapshotChanged
            {
                add { }
                remove { }
            }

            public event EventHandler<DiscoveryCandidateDiscoveredEventArgs>
                CandidateDiscovered
            {
                add { }
                remove { }
            }

            public Task StartAsync(
                DiscoveryControlRequest request,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class NoopMaterializer :
            IDiscoveryCandidateMaterializer
        {
            public Guid Materialize(
                DiscoveryCandidateSnapshot candidate,
                DateTime observedUtc)
            {
                return Guid.Empty;
            }
        }
    }
}
