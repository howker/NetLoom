using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private const double Adr083InspectorDefaultWidth = 360.0;
        private const double Adr083InspectorMinWidth = 320.0;
        private const double Adr083InspectorMaxWidth = 520.0;
        private const double Adr083MapMinimumWindowRatio = 0.60;

        private double _adr083InspectorPreferredWidth =
            Adr083InspectorDefaultWidth;

        private bool _adr083InspectorCollapsed;
        private bool _adr083InspectorHiddenForSection;
        private bool _adr083InspectorOverlay;

        private void InitializeAdr083ShellFrame()
        {
            ConfigureAdr083NavigationButton(
                ShellMapButton,
                "ShellMapSection");
            ConfigureAdr083NavigationButton(
                ShellEquipmentButton,
                "ShellEquipmentSection");
            ConfigureAdr083NavigationButton(
                ShellAlertsButton,
                "ShellAlertsSection");
            ConfigureAdr083NavigationButton(
                ShellDiscoveryButton,
                "ShellDiscoverySection");
            ConfigureAdr083NavigationButton(
                ShellSettingsButton,
                "ShellSettingsSection");

            AutomationProperties.SetName(
                ShellGlobalSearchTextBox,
                UiText.Get(
                    "ShellGlobalSearchAutomationName"));
            AutomationProperties.SetName(
                DiscoveryProfileComboBox,
                UiText.Get(
                    "DiscoveryProfileLabel"));

            ShellMonitoringStartButton.Content =
                UiText.Get(
                    "MonitoringStartAction");
            ShellMonitoringStopButton.Content =
                UiText.Get(
                    "MonitoringStopAction");

            MapOperationalFocusButton.Content =
                UiText.Get(
                    "ShellMapFocusAction");
            MapExportButton.Content =
                UiText.Get(
                    "ShellMapExportAction");
            MapRefreshDataButton.Content =
                UiText.Get(
                    "ShellMapRefreshAction");
            Adr083MapEditModeIndicatorText.Text =
                UiText.Get(
                    "ShellMapEditModeCompact");

            AutomationProperties.SetName(
                ShellInspectorCollapseButton,
                UiText.Get(
                    "ShellInspectorCollapseAction"));
            ShellInspectorCollapseButton.ToolTip =
                UiText.Get(
                    "ShellInspectorCollapseAction");

            AutomationProperties.SetName(
                ShellInspectorRevealButton,
                UiText.Get(
                    "ShellInspectorRevealAction"));
            ShellInspectorRevealButton.ToolTip =
                UiText.Get(
                    "ShellInspectorRevealAction");

            ShellGlobalSearchStatusText.Text =
                UiText.Get(
                    "LookupReady");

            UpdateAdr083SectionPresentation(
                ShellSection.Map);
            UpdateAdr083InspectorLayout();
        }

        private static void ConfigureAdr083NavigationButton(
            Button button,
            string resourceKey)
        {
            if (button == null)
            {
                return;
            }

            var name =
                UiText.Get(
                    resourceKey);

            button.ToolTip =
                name;

            AutomationProperties.SetName(
                button,
                name);
        }

        private async void OnShellGlobalSearchTextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            var query =
                ShellGlobalSearchTextBox.Text == null
                    ? string.Empty
                    : ShellGlobalSearchTextBox.Text.Trim();

            if (query.Length == 0)
            {
                ShellGlobalSearchPopup.IsOpen =
                    false;
                ShellGlobalSearchResultsList.ItemsSource =
                    null;
                ShellGlobalSearchStatusText.Text =
                    UiText.Get(
                        "LookupReady");
                return;
            }

            LookupQueryTextBox.Text =
                query;

            await QueueLookupAsync();

            ShellGlobalSearchResultsList.ItemsSource =
                LookupResultsList.ItemsSource;
            ShellGlobalSearchStatusText.Text =
                LookupStatusText.Text;
            ShellGlobalSearchPopup.IsOpen =
                true;
        }

        private void OnShellGlobalSearchSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ShellGlobalSearchResultsList.SelectedItem == null)
            {
                return;
            }

            LookupResultsList.SelectedItem =
                ShellGlobalSearchResultsList.SelectedItem;

            OnLookupSelectionChanged(
                LookupResultsList,
                e);

            ShellGlobalSearchPopup.IsOpen =
                false;
        }

        private void FocusAdr083GlobalSearch()
        {
            ShellGlobalSearchTextBox.Focus();
            ShellGlobalSearchTextBox.SelectAll();

            if (!string.IsNullOrWhiteSpace(
                    ShellGlobalSearchTextBox.Text))
            {
                ShellGlobalSearchPopup.IsOpen =
                    true;
            }
        }

        private void OnAdr083MapOperationalFocusClick(
            object sender,
            RoutedEventArgs e)
        {
            var contextMenu =
                new ContextMenu();

            var root =
                CreateOperationalFocusMenu();

            foreach (var item in
                root.Items.Cast<object>().ToArray())
            {
                root.Items.Remove(
                    item);
                contextMenu.Items.Add(
                    item);
            }

            MapOperationalFocusButton.ContextMenu =
                contextMenu;
            contextMenu.PlacementTarget =
                MapOperationalFocusButton;
            contextMenu.Placement =
                PlacementMode.Bottom;
            contextMenu.IsOpen =
                true;
        }

        private void OnShellInspectorCollapseClick(
            object sender,
            RoutedEventArgs e)
        {
            _adr083InspectorCollapsed =
                true;
            UpdateAdr083InspectorLayout();
        }

        private void OnShellInspectorRevealClick(
            object sender,
            RoutedEventArgs e)
        {
            _adr083InspectorCollapsed =
                false;
            _adr083InspectorHiddenForSection =
                false;
            UpdateAdr083InspectorLayout();
        }

        private void OnShellInspectorSplitterDragCompleted(
            object sender,
            DragCompletedEventArgs e)
        {
            if (_adr083InspectorOverlay ||
                ShellInspectorPanel.Visibility !=
                    Visibility.Visible)
            {
                return;
            }

            _adr083InspectorPreferredWidth =
                Math.Max(
                    Adr083InspectorMinWidth,
                    Math.Min(
                        Adr083InspectorMaxWidth,
                        ShellInspectorColumn.ActualWidth));

            UpdateAdr083InspectorLayout();
        }

        private void OnShellWorkspaceSizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            UpdateAdr083InspectorLayout();
        }

        private void UpdateAdr083SectionPresentation(
            ShellSection section)
        {
            ShellSectionSurface.Visibility =
                section == ShellSection.Map
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            ShellMapSurface.Visibility =
                Visibility.Visible;

            switch (section)
            {
                case ShellSection.Map:
                case ShellSection.Equipment:
                    _adr083InspectorHiddenForSection =
                        false;
                    _adr083InspectorCollapsed =
                        false;
                    break;

                case ShellSection.Alerts:
                case ShellSection.Discovery:
                    _adr083InspectorHiddenForSection =
                        false;
                    _adr083InspectorCollapsed =
                        true;
                    break;

                case ShellSection.Settings:
                    _adr083InspectorHiddenForSection =
                        true;
                    _adr083InspectorCollapsed =
                        true;
                    break;

                default:
                    // Разделы Monitoring/Search сохраняются только для совместимости.
                    // Старые командные пути и тесты всё ещё используют эти разделы.
                    // В рейле ADR-083 эти разделы больше не показываются.
                    _adr083InspectorHiddenForSection =
                        true;
                    _adr083InspectorCollapsed =
                        true;
                    break;
            }

            UpdateAdr083InspectorLayout();
        }

        private void UpdateAdr083InspectorLayout()
        {
            if (ShellWorkspaceGrid == null ||
                ShellInspectorColumn == null ||
                ShellInspectorPanel == null)
            {
                return;
            }

            if (_adr083InspectorHiddenForSection)
            {
                _adr083InspectorOverlay =
                    false;
                ShellInspectorPanel.Visibility =
                    Visibility.Collapsed;
                ShellInspectorSplitter.Visibility =
                    Visibility.Collapsed;
                ShellInspectorRevealButton.Visibility =
                    Visibility.Collapsed;
                ShellInspectorColumn.MinWidth =
                    0.0;
                ShellInspectorColumn.Width =
                    new GridLength(0.0);
                return;
            }

            if (_adr083InspectorCollapsed)
            {
                _adr083InspectorOverlay =
                    false;
                ShellInspectorPanel.Visibility =
                    Visibility.Collapsed;
                ShellInspectorSplitter.Visibility =
                    Visibility.Collapsed;
                ShellInspectorRevealButton.Visibility =
                    Visibility.Visible;
                ShellInspectorColumn.MinWidth =
                    0.0;
                ShellInspectorColumn.Width =
                    new GridLength(0.0);
                return;
            }

            var windowWidth =
                Math.Max(
                    1.0,
                    ActualWidth);
            var railWidth =
                56.0;
            var reservedMapWidth =
                windowWidth -
                railWidth -
                _adr083InspectorPreferredWidth;

            _adr083InspectorOverlay =
                reservedMapWidth <
                    windowWidth *
                    Adr083MapMinimumWindowRatio;

            ShellInspectorPanel.Visibility =
                Visibility.Visible;
            ShellInspectorRevealButton.Visibility =
                Visibility.Collapsed;

            if (_adr083InspectorOverlay)
            {
                ShellInspectorColumn.MinWidth =
                    0.0;
                ShellInspectorColumn.Width =
                    new GridLength(0.0);
                ShellInspectorSplitter.Visibility =
                    Visibility.Collapsed;

                Grid.SetColumn(
                    ShellInspectorPanel,
                    2);
                ShellInspectorPanel.Width =
                    _adr083InspectorPreferredWidth;
                ShellInspectorPanel.HorizontalAlignment =
                    HorizontalAlignment.Right;
                return;
            }

            Grid.SetColumn(
                ShellInspectorPanel,
                3);
            ShellInspectorPanel.ClearValue(
                WidthProperty);
            ShellInspectorPanel.HorizontalAlignment =
                HorizontalAlignment.Stretch;
            ShellInspectorColumn.MinWidth =
                Adr083InspectorMinWidth;
            ShellInspectorColumn.MaxWidth =
                Adr083InspectorMaxWidth;
            ShellInspectorColumn.Width =
                new GridLength(
                    _adr083InspectorPreferredWidth);
            ShellInspectorSplitter.Visibility =
                Visibility.Visible;
        }
    }
}
