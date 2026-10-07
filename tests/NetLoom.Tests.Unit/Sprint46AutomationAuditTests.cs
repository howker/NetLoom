using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    // Проверка дерева UI Automation (UI_DESIGN_RULES §8, §10 «Два прохода»): то, что видит экранный диктор
    // И инструменты вроде Inspect / Accessibility Insights. Настоящее окно на копии базы стенда.
    // Проверяется: у каждого элемента управления есть имя и видимая подпись входит в него; у полей — подпись;
    // У заголовков разделов и групп — уровень заголовка; ненавязчивые обновления объявляются (LiveSetting);
    // Выбранный раздел рейла и выбранный вариант переключателя видны как состояние, а не только цветом.
    // Отчёт — artifacts/ui-audit-automation/automation-findings.txt и дерево по разделам.
    public sealed partial class Sprint46LiveUiAuditTests
    {
        private static readonly AutomationControlType[] InteractiveControlTypes =
        {
            AutomationControlType.Button,
            AutomationControlType.CheckBox,
            AutomationControlType.ComboBox,
            AutomationControlType.Edit,
            AutomationControlType.Hyperlink,
            AutomationControlType.ListItem,
            AutomationControlType.MenuItem,
            AutomationControlType.RadioButton,
            AutomationControlType.TabItem,
            AutomationControlType.Thumb
        };

        // Группы взаимоисключающих вариантов: выбранный вариант должен быть виден программе как состояние.
        private static readonly string[][] ExclusiveGroups =
        {
            new[] { "ShellMapButton", "ShellEquipmentButton", "ShellAlertsButton", "ShellDiscoveryButton", "ShellSettingsButton" },
            new[] { "MapViewModeButton", "MapEditModeButton" },
            new[] { "EquipmentFilterAllButton", "EquipmentFilterProblemsButton", "EquipmentFilterNoDataButton", "EquipmentFilterManualButton" },
            new[] { "SettingsMotionNormalButton", "SettingsMotionReducedButton", "SettingsMotionOffButton" },
            new[] { "SettingsThemeLightButton", "SettingsThemeDarkButton" }
        };

        // Области, которые обновляются сами и должны объявляться ненавязчиво (§8 «Объявление изменений»).
        private static readonly string[] LiveRegionNames =
        {
            "ShellEventList",
            "ShellMonitoringHeaderText",
            "DiscoveryStateValueText"
        };

        [TestMethod]
        [TestCategory("LiveUiAudit")]
        public void AutomationTreeGivesEveryControlANameRoleAndState()
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
                    "ui-audit-automation");

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
                    "netloom-ui-automation-" +
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
                        AuditAutomation(
                            workDatabase,
                            outputDirectory,
                            report));
            }
            finally
            {
                File.WriteAllText(
                    Path.Combine(
                        outputDirectory,
                        "automation-findings.txt"),
                    report.Render(),
                    new UTF8Encoding(false));

                TryDelete(workDatabase);
            }

            if (report.Count > 0)
            {
                Assert.Fail(
                    "UI Automation audit found " +
                    report.Count +
                    " issue(s). See artifacts/ui-audit-automation/automation-findings.txt.");
            }
        }

        private static void AuditAutomation(
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
                    1440,
                    900);

                foreach (var sectionButton in Sections)
                {
                    var section =
                        sectionButton
                            .Replace("Shell", string.Empty)
                            .Replace("Button", string.Empty);

                    RaiseClick(
                        (ButtonBase)window.FindName(
                            sectionButton));
                    Settle();

                    // Инспектор с выбранным объектом — его заголовки и вкладки тоже часть дерева.
                    if (section == "Equipment")
                    {
                        var row =
                            FindVisualDescendants<Button>(
                                    (DependencyObject)window.FindName(
                                        "EquipmentList"))
                                .FirstOrDefault(
                                    item => item.IsVisible);

                        RaiseClick(row);
                        Settle();
                    }

                    var lines =
                        new List<string>();

                    var root =
                        UIElementAutomationPeer.CreatePeerForElement(
                            window);

                    AuditPeer(
                        root,
                        0,
                        section,
                        lines,
                        report);

                    File.WriteAllLines(
                        Path.Combine(
                            outputDirectory,
                            "tree-" + section + ".txt"),
                        lines,
                        new UTF8Encoding(false));

                    AuditHeadings(
                        window,
                        section,
                        report);

                    AuditLiveRegions(
                        window,
                        section,
                        report);

                    AuditExclusiveState(
                        window,
                        section,
                        report);
                }
            }
            finally
            {
                window.Close();
                Settle();
            }
        }

        private static void AuditPeer(
            AutomationPeer peer,
            int depth,
            string context,
            List<string> lines,
            AuditReport report)
        {
            if (peer == null ||
                peer.IsOffscreen())
            {
                return;
            }

            var type =
                peer.GetAutomationControlType();
            var name =
                peer.GetName();

            lines.Add(
                new string(' ', depth * 2) +
                type +
                (string.IsNullOrEmpty(name) ? string.Empty : " «" + name + "»") +
                (peer.IsEnabled() ? string.Empty : " [неактивен]") +
                (string.IsNullOrEmpty(peer.GetItemStatus()) ? string.Empty : " {" + peer.GetItemStatus() + "}"));

            var owner =
                (peer as UIElementAutomationPeer)?.Owner as FrameworkElement;

            if (InteractiveControlTypes.Contains(type) &&
                owner != null &&
                !IsInsideIgnoredContainer(owner))
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    report.Add(
                        "UIA",
                        "ВЫСОКАЯ",
                        context,
                        "у элемента управления нет имени для UI Automation: " + type + " " + Describe(owner) + " (§8)");
                }
                else
                {
                    var visible =
                        VisibleLabel(owner);

                    if (!string.IsNullOrWhiteSpace(visible) &&
                        name.IndexOf(
                            visible.Trim(),
                            StringComparison.CurrentCultureIgnoreCase) < 0)
                    {
                        report.Add(
                            "UIA",
                            "СРЕДНЯЯ",
                            context,
                            "видимая подпись «" + visible.Trim() + "» не входит в имя «" + name + "»: " + Describe(owner) + " (§8)");
                    }
                }
            }

            var children =
                peer.GetChildren();

            if (children == null)
            {
                return;
            }

            foreach (var child in children)
            {
                AuditPeer(
                    child,
                    depth + 1,
                    context,
                    lines,
                    report);
            }
        }

        // Видимая подпись простого элемента: текст содержимого или единственный текст внутри.
        // Составные строки (таблица, лента событий) и символы-значки («‹», «›») подписью не считаются.
        private static string VisibleLabel(
            FrameworkElement owner)
        {
            var content =
                owner as ContentControl;

            if (content == null)
            {
                return null;
            }

            var text =
                content.Content as string;

            if (text == null)
            {
                var texts =
                    FindVisualDescendants<TextBlock>(owner)
                        .Where(
                            item =>
                                item.IsVisible &&
                                !string.IsNullOrWhiteSpace(item.Text))
                        .ToList();

                text =
                    texts.Count == 1
                        ? texts[0].Text
                        : null;
            }

            if (text == null ||
                !text.Any(char.IsLetterOrDigit))
            {
                return null;
            }

            return text.Replace("_", string.Empty);
        }

        // Уровень заголовка (AutomationProperties.HeadingLevel) есть только в WPF для .NET Core 3.0+;
        // Этот тест собран под net48, где такого API нет. Приложение оператора (NetLoom.Desktop) — net8.0-windows,
        // Поэтому уровни заголовков задаются там, а проверяются в Inspect (§10 «не проверено» записывается явно).
        private static void AuditHeadings(
            Window window,
            string context,
            AuditReport report)
        {
            report.Add(
                "UIA",
                "ИНФО",
                "все разделы",
                "уровни заголовков (HeadingLevel) этим тестом не проверены: в net48 нет API; проверить в Inspect на сборке net8.0-windows");
        }

        private static void AuditLiveRegions(
            Window window,
            string context,
            AuditReport report)
        {
            foreach (var name in LiveRegionNames)
            {
                var element =
                    window.FindName(name) as UIElement;

                if (element == null ||
                    !element.IsVisible)
                {
                    continue;
                }

                var peer =
                    UIElementAutomationPeer.CreatePeerForElement(
                        element);

                if (peer != null &&
                    peer.GetLiveSetting() ==
                        AutomationLiveSetting.Off)
                {
                    report.Add(
                        "UIA",
                        "СРЕДНЯЯ",
                        context,
                        "обновляемая область не объявляется (LiveSetting=Off): #" + name + " (§8 «Объявление изменений»)");
                }
            }
        }

        private static void AuditExclusiveState(
            Window window,
            string context,
            AuditReport report)
        {
            foreach (var group in ExclusiveGroups)
            {
                var buttons =
                    group
                        .Select(
                            name =>
                                window.FindName(name) as FrameworkElement)
                        .Where(
                            item =>
                                item != null &&
                                item.IsVisible)
                        .ToList();

                if (buttons.Count < 2)
                {
                    continue;
                }

                var exposesState =
                    buttons.Any(
                        button =>
                        {
                            var peer =
                                UIElementAutomationPeer.CreatePeerForElement(
                                    button);

                            if (peer == null)
                            {
                                return false;
                            }

                            var selection =
                                peer.GetPattern(
                                    PatternInterface.SelectionItem) as ISelectionItemProvider;

                            var toggle =
                                peer.GetPattern(
                                    PatternInterface.Toggle) as IToggleProvider;

                            return (selection != null &&
                                    selection.IsSelected) ||
                                   (toggle != null &&
                                    toggle.ToggleState ==
                                        ToggleState.On) ||
                                   !string.IsNullOrWhiteSpace(
                                       peer.GetItemStatus());
                        });

                if (!exposesState)
                {
                    report.Add(
                        "UIA",
                        "СРЕДНЯЯ",
                        context,
                        "выбранный вариант виден только цветом, программе не передаётся состояние: " +
                        string.Join(" / ", group) +
                        " (§8 «у каждого элемента имя, роль и состояние»)");
                }
            }
        }
    }
}
