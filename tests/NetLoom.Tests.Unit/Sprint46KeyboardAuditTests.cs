using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    // Проход только клавиатурой (UI_DESIGN_RULES §8, §10 «Два прохода»).
    // Настоящее окно на копии базы стенда; Tab подаётся через InputManager, как от клавиатуры.
    // Проверяется: всё интерактивное достижимо по Tab, фокус не уходит на невидимое,
    // Рамка фокуса видна (снимок до и после, контраст не ниже 3:1, толщина около 2 px),
    // Esc в поле ввода не уводит из раздела, у мышиных действий карты есть путь с клавиатуры.
    // Отчёт и порядок обхода — artifacts/ui-audit-keyboard.
    public sealed partial class Sprint46LiveUiAuditTests
    {
        private const int KeyboardAuditMaxSteps = 400;

        // Порог отделяет тематическую рамку от системного пунктира по замерам стенда:
        // Пунктир в 1 px даёт 0.58–0.65 периметра, рамка 2 px — от 1.17 периметра, даже когда
        // Часть рамки ложится на рамку сегментированной группы того же оттенка. Нет рамки — 0.
        private const double FocusRingPerimeterFactor = 1.0;

        private const double FocusRingContrast = 3.0;

        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void KeyboardOnlyPassReachesEveryControlWithVisibleFocus()
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
                    Path.Combine(
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
                Path.Combine(
                    repositoryRoot,
                    "artifacts",
                    "ui-audit-keyboard");

            Directory.CreateDirectory(
                outputDirectory);

            foreach (var file in
                Directory.GetFiles(
                    outputDirectory))
            {
                File.Delete(file);
            }

            var workDatabase =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-ui-keyboard-" +
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
                        foreach (var dark in new[] { false, true })
                        {
                            AuditKeyboard(
                                workDatabase,
                                dark,
                                outputDirectory,
                                report);
                        }
                    });
            }
            finally
            {
                File.WriteAllText(
                    Path.Combine(
                        outputDirectory,
                        "keyboard-findings.txt"),
                    report.Render(),
                    new UTF8Encoding(false));

                TryDelete(workDatabase);
            }

            if (report.Count > 0)
            {
                Assert.Fail(
                    "Keyboard-only pass found " +
                    report.Count +
                    " issue(s). See artifacts/ui-audit-keyboard/keyboard-findings.txt.");
            }
        }

        private static void AuditKeyboard(
            string databasePath,
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
                    1440,
                    900);

                foreach (var sectionButton in Sections)
                {
                    var section =
                        sectionButton
                            .Replace("Shell", string.Empty)
                            .Replace("Button", string.Empty);

                    var context =
                        section + " " + theme;

                    RaiseClick(
                        (ButtonBase)window.FindName(
                            sectionButton));
                    Settle();

                    var order =
                        WalkTabOrder(
                            window,
                            context,
                            report);

                    File.WriteAllLines(
                        Path.Combine(
                            outputDirectory,
                            "tab-order-" + Sanitize(context) + ".txt"),
                        order.Select(
                            (item, index) =>
                                (index + 1).ToString("000") +
                                "  " +
                                Describe(item)),
                        new UTF8Encoding(false));

                    AuditUnreachable(
                        window,
                        order,
                        context,
                        report);

                    AuditFocusVisibility(
                        window,
                        order,
                        context,
                        outputDirectory,
                        report);

                    if (sectionButton == "ShellMapButton")
                    {
                        AuditMapKeyboardPath(
                            window,
                            context,
                            report);
                    }

                    if (sectionButton == "ShellDiscoveryButton")
                    {
                        AuditEscapeInField(
                            window,
                            (UIElement)window.FindName(
                                "DiscoveryStartAddressTextBox"),
                            "ShellDiscoverySidebarPanel",
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

        // ---------- обход по Tab ----------

        private static List<UIElement> WalkTabOrder(
            Window window,
            string context,
            AuditReport report)
        {
            Keyboard.ClearFocus();
            window.Activate();
            window.MoveFocus(
                new TraversalRequest(
                    FocusNavigationDirection.First));
            Settle(50);

            var order =
                new List<UIElement>();

            var seen =
                new HashSet<UIElement>();

            for (var step = 0;
                 step < KeyboardAuditMaxSteps;
                 step++)
            {
                var focused =
                    Keyboard.FocusedElement as UIElement;

                if (focused == null)
                {
                    report.Add(
                        "КЛАВИАТУРА",
                        "ВЫСОКАЯ",
                        context,
                        "фокус потерян при обходе по Tab (после " + Describe(order.LastOrDefault()) + ")");
                    break;
                }

                if (!seen.Add(focused))
                {
                    // Круг замкнулся — обход закончен.
                    break;
                }

                order.Add(focused);

                if (!IsShownInWindow(
                        window,
                        focused))
                {
                    report.Add(
                        "КЛАВИАТУРА",
                        "СРЕДНЯЯ",
                        context,
                        "фокус попадает на невидимый элемент: " + Describe(focused) + " (§8)");
                }

                PressKey(
                    window,
                    Key.Tab);
                Settle(30);

                if (ReferenceEquals(
                        Keyboard.FocusedElement,
                        focused))
                {
                    report.Add(
                        "КЛАВИАТУРА",
                        "ВЫСОКАЯ",
                        context,
                        "Tab не уводит фокус дальше: " + Describe(focused) + " (ловушка фокуса, §8)");
                    break;
                }
            }

            return order;
        }

        private static void PressKey(
            Window window,
            Key key)
        {
            var source =
                PresentationSource.FromVisual(window);

            if (source == null)
            {
                return;
            }

            MarkKeyboardAsLastInput();

            // Только PreviewKeyDown: InputManager сам продолжает его событием KeyDown,
            // Как при настоящем нажатии. Два события дали бы двойной шаг Tab.
            InputManager.Current.ProcessInput(
                new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    source,
                    Environment.TickCount,
                    key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                });
        }

        // WPF рисует FocusVisualStyle, только если последним устройством ввода была клавиатура.
        // Событие, поданное через ProcessInput, это поле не обновляет, а настоящее нажатие — обновляет;
        // Поэтому тест выставляет его так же, как его выставил бы Tab оператора.
        private static void MarkKeyboardAsLastInput()
        {
            var property =
                typeof(InputManager).GetProperty(
                    "MostRecentInputDevice",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);

            if (property != null &&
                property.CanWrite)
            {
                property.SetValue(
                    InputManager.Current,
                    Keyboard.PrimaryDevice);
            }
        }

        // ---------- достижимость ----------

        private static void AuditUnreachable(
            Window window,
            List<UIElement> order,
            string context,
            AuditReport report)
        {
            var reached =
                new HashSet<UIElement>(
                    order);

            foreach (var control in
                FindVisualDescendants<Control>(
                    window))
            {
                if (!IsInteractive(control) ||
                    !IsShownInWindow(
                        window,
                        control) ||
                    IsTemplatePart(control))
                {
                    continue;
                }

                if (!control.Focusable)
                {
                    // Кнопки на холсте карты — часть клавиатурной навигации по карте, перенесённой в Sprint 49 (K4).
                    var onMap =
                        IsInsideIgnoredContainer(
                            control);

                    report.Add(
                        "КЛАВИАТУРА",
                        onMap
                            ? "ИНФО"
                            : "СРЕДНЯЯ",
                        context,
                        "элемент управления не принимает фокус (Focusable=false), действие только мышью: " +
                        Describe(control) +
                        (onMap
                            ? " (перенесено в Sprint 49, акт сверки K4)"
                            : " (§8)"));
                    continue;
                }

                if (!control.IsTabStop ||
                    reached.Contains(control) ||
                    reached.Any(
                        item =>
                            item.IsDescendantOf(control) ||
                            control.IsDescendantOf(item)))
                {
                    continue;
                }

                report.Add(
                    "КЛАВИАТУРА",
                    "ВЫСОКАЯ",
                    context,
                    "недостижимо по Tab: " + Describe(control) + " (§8)");
            }
        }

        private static bool IsInteractive(
            Control control)
        {
            return control.IsEnabled &&
                   (control is ButtonBase ||
                    control is TextBoxBase ||
                    control is PasswordBox ||
                    control is ComboBox ||
                    control is ListBoxItem ||
                    control is TabItem);
        }

        // Части шаблонов (кнопки полос прокрутки, раскрытие списка) — не самостоятельные цели Tab.
        private static bool IsTemplatePart(
            Control control)
        {
            if (control.TemplatedParent is ScrollBar ||
                control.TemplatedParent is ComboBox ||
                control.TemplatedParent is Thumb)
            {
                return true;
            }

            var current =
                VisualTreeHelper.GetParent(control);

            while (current != null)
            {
                if (current is ScrollBar)
                {
                    return true;
                }

                current =
                    VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool IsShownInWindow(
            Window window,
            UIElement element)
        {
            var framework =
                element as FrameworkElement;

            if (framework == null ||
                !framework.IsVisible ||
                framework.ActualWidth < 1 ||
                framework.ActualHeight < 1)
            {
                return false;
            }

            var root =
                window.Content as FrameworkElement;

            if (root == null ||
                !framework.IsDescendantOf(root))
            {
                return false;
            }

            var bounds =
                framework
                    .TransformToAncestor(root)
                    .TransformBounds(
                        new Rect(
                            0,
                            0,
                            framework.ActualWidth,
                            framework.ActualHeight));

            return bounds.IntersectsWith(
                new Rect(
                    0,
                    0,
                    root.ActualWidth,
                    root.ActualHeight));
        }

        // ---------- видимость рамки фокуса ----------

        // Снимки окна с рамкой фокуса для просмотра глазами (§10 «доказательство»): рейл, панель карты,
        // Главная кнопка, флажок.
        private static readonly HashSet<string> FocusSampleNames =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "ShellEquipmentButton",
                "MapRefreshDataButton",
                "DiscoveryStartButton",
                "SettingsMonitoringKindLldpCheckBox"
            };

        private static void AuditFocusVisibility(
            Window window,
            List<UIElement> order,
            string context,
            string outputDirectory,
            AuditReport report)
        {
            var root =
                window.Content as FrameworkElement;

            if (root == null)
            {
                return;
            }

            for (var index = 0;
                 index < order.Count;
                 index++)
            {
                var element =
                    order[index];

                var framework =
                    element as FrameworkElement;

                if (framework == null ||
                    !IsShownInWindow(
                        window,
                        framework))
                {
                    continue;
                }

                // Сначала фокус на далёком нейтральном элементе, затем на проверяемом.
                // ClearFocus не годится: WPF возвращает фокус последнему элементу окна.
                // Последний ввод — клавиатура (Tab), поэтому WPF показывает рамку так же, как оператору.
                var neutral =
                    (UIElement)window.FindName(
                        ReferenceEquals(
                            element,
                            window.FindName(
                                "ShellGlobalSearchTextBox"))
                            ? "ShellSettingsButton"
                            : "ShellGlobalSearchTextBox");

                Keyboard.Focus(
                    neutral);
                Settle(20);

                var region =
                    FocusRegion(
                        root,
                        framework);

                if (region.Width < 2 ||
                    region.Height < 2)
                {
                    continue;
                }

                var before =
                    RenderRegion(
                        window,
                        region);

                // Фокус приходит настоящим Tab с предыдущего элемента обхода — так WPF рисует
                // И шаблонную рамку, и слой FocusVisualStyle ровно как у оператора.
                if (index > 0)
                {
                    Keyboard.Focus(
                        order[index - 1]);
                    Settle(20);
                    PressKey(
                        window,
                        Key.Tab);
                }
                else
                {
                    MarkKeyboardAsLastInput();
                    Keyboard.Focus(
                        framework);
                }

                Settle(40);

                if (!framework.IsKeyboardFocusWithin)
                {
                    continue;
                }

                var regionAfter =
                    FocusRegion(
                        root,
                        framework);

                if (regionAfter.Width < 2 ||
                    regionAfter != region)
                {
                    // Элемент сдвинулся при получении фокуса (прокрутка) — сравнение недостоверно.
                    continue;
                }

                var after =
                    RenderRegion(
                        window,
                        region);

                var changed =
                    CountContrastChanges(
                        before,
                        after);

                var perimeter =
                    2.0 *
                    (framework.ActualWidth +
                     framework.ActualHeight);

                if (FocusSampleNames.Contains(
                        framework.Name))
                {
                    SaveFocusSample(
                        window,
                        Path.Combine(
                            outputDirectory,
                            "focus-" +
                            Sanitize(
                                context) +
                            "-" +
                            framework.Name +
                            ".png"));
                }

                if (changed <
                    perimeter *
                    FocusRingPerimeterFactor)
                {
                    report.Add(
                        "КЛАВИАТУРА",
                        "СРЕДНЯЯ",
                        context,
                        "рамка фокуса не видна или тоньше 2 px (§8): " +
                        Describe(framework) +
                        " — заметно изменилось " +
                        changed +
                        " пикс. при периметре " +
                        Math.Round(perimeter));
                }
            }

            Keyboard.ClearFocus();
        }

        private static Int32Rect FocusRegion(
            FrameworkElement root,
            FrameworkElement element)
        {
            // Строки списков пересоздаются при обновлении данных — отсоединённый элемент не измеряем.
            if (!element.IsDescendantOf(root))
            {
                return new Int32Rect();
            }

            var bounds =
                element
                    .TransformToAncestor(root)
                    .TransformBounds(
                        new Rect(
                            0,
                            0,
                            element.ActualWidth,
                            element.ActualHeight));

            // Рамка фокуса может лежать снаружи элемента — берём поле в 4 px.
            bounds.Inflate(
                4,
                4);

            bounds.Intersect(
                new Rect(
                    0,
                    0,
                    root.ActualWidth,
                    root.ActualHeight));

            if (bounds.IsEmpty)
            {
                return new Int32Rect();
            }

            return new Int32Rect(
                (int)Math.Floor(bounds.X),
                (int)Math.Floor(bounds.Y),
                (int)Math.Floor(bounds.Width),
                (int)Math.Floor(bounds.Height));
        }

        private static byte[] RenderRegion(
            Window window,
            Int32Rect region)
        {
            // Окно целиком, вместе со слоем украшений: системная рамка фокуса рисуется в нём.
            var root =
                (FrameworkElement)window.Content;

            root.UpdateLayout();

            var width =
                Math.Max(1, (int)Math.Ceiling(root.ActualWidth));
            var height =
                Math.Max(1, (int)Math.Ceiling(root.ActualHeight));

            var bitmap =
                new RenderTargetBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Pbgra32);

            var decorator =
                VisualTreeHelper.GetParent(root) as Visual;

            while (decorator != null &&
                   !(decorator is System.Windows.Documents.AdornerDecorator))
            {
                decorator =
                    VisualTreeHelper.GetParent(decorator) as Visual;
            }

            bitmap.Render(
                decorator ?? root);

            var pixels =
                new byte[region.Width * region.Height * 4];

            bitmap.CopyPixels(
                region,
                pixels,
                region.Width * 4,
                0);

            return pixels;
        }

        private static void SaveFocusSample(
            Window window,
            string path)
        {
            var root =
                (FrameworkElement)window.Content;

            var full =
                new Int32Rect(
                    0,
                    0,
                    Math.Max(1, (int)Math.Floor(root.ActualWidth)),
                    Math.Max(1, (int)Math.Floor(root.ActualHeight)));

            var pixels =
                RenderRegion(
                    window,
                    full);

            var source =
                BitmapSource.Create(
                    full.Width,
                    full.Height,
                    96,
                    96,
                    PixelFormats.Pbgra32,
                    null,
                    pixels,
                    full.Width * 4);

            var encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(
                    source));

            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private static int CountContrastChanges(
            byte[] before,
            byte[] after)
        {
            var count = 0;

            for (var i = 0;
                 i + 3 < before.Length &&
                 i + 3 < after.Length;
                 i += 4)
            {
                if (before[i] == after[i] &&
                    before[i + 1] == after[i + 1] &&
                    before[i + 2] == after[i + 2])
                {
                    continue;
                }

                var first =
                    Color.FromRgb(
                        before[i + 2],
                        before[i + 1],
                        before[i]);

                var second =
                    Color.FromRgb(
                        after[i + 2],
                        after[i + 1],
                        after[i]);

                if (ContrastRatio(
                        first,
                        second) >=
                    FocusRingContrast)
                {
                    count++;
                }
            }

            return count;
        }

        // ---------- карта и Esc ----------

        private static void AuditMapKeyboardPath(
            Window window,
            string context,
            AuditReport report)
        {
            var nodes =
                VisibleDeviceBorders(window)
                    .ToList();

            if (nodes.Count == 0 ||
                nodes.Any(
                    node => node.Focusable))
            {
                return;
            }

            // Перенесено в Sprint 49 решением владельца 2026-10-07 (docs/sprint46-mockup-gap.md, K4).
            report.Add(
                "КЛАВИАТУРА",
                "ИНФО",
                context,
                "узлы и связи карты выбираются только мышью (Focusable=false); выбор с клавиатуры — через " +
                "«Оборудование», поиск Ctrl+K и «Показать на карте» (перенесено в Sprint 49, акт сверки K4)");
        }

        private static void AuditEscapeInField(
            Window window,
            UIElement field,
            string sectionPanelName,
            string context,
            AuditReport report)
        {
            var panel =
                window.FindName(
                    sectionPanelName) as UIElement;

            if (field == null ||
                panel == null ||
                !field.IsVisible)
            {
                return;
            }

            Keyboard.Focus(
                field);
            Settle(30);

            PressKey(
                window,
                Key.Escape);
            Settle();

            if (!panel.IsVisible)
            {
                report.Add(
                    "КЛАВИАТУРА",
                    "СРЕДНЯЯ",
                    context,
                    "Esc в поле ввода уводит из раздела на «Карту»; по §8 Esc закрывает всплывающее окно, диалог или редактор");

                RaiseClick(
                    (ButtonBase)window.FindName(
                        "ShellDiscoveryButton"));
                Settle();
            }
        }
    }
}
