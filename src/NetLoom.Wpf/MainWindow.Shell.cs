using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NetLoom.Application.MonitoringControl;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Shell;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private readonly IUiShellStateStore
            _uiShellStateStore;

        private UiShellTheme _shellTheme =
            UiShellTheme.Light;

        private ShellSection _shellSection =
            ShellSection.Map;

        private void InitializeShell()
        {
            ShellBrandText.Text =
                UiText.Get(
                    "WindowTitle");

            ShellMapButton.Content =
                UiText.Get(
                    "ShellMapSection");
            ShellEquipmentButton.Content =
                UiText.Get(
                    "ShellEquipmentSection");
            ShellAlertsButton.Content =
                UiText.Get(
                    "ShellAlertsSection");
            ShellDiscoveryButton.Content =
                UiText.Get(
                    "ShellDiscoverySection");
            ShellSearchButton.Content =
                UiText.Get(
                    "ShellSearchSection");
            ShellSettingsButton.Content =
                UiText.Get(
                    "ShellSettingsSection");

            ShellMapActionsTitleText.Text =
                UiText.Get(
                    "ShellMapActionsTitle");
            ShellSettingsTitleText.Text =
                UiText.Get(
                    "ShellSettingsSection");
            ShellMonitoringRailTitleText.Text =
                UiText.Get(
                    "ShellMonitoringLabel");
            ShellEventTitleText.Text =
                UiText.Get(
                    "ShellEventsLabel");

            ShellAlertCountText.Text =
                UiText.Format(
                    "ShellAlertCount",
                    0);
            AlertTransitionText.Text =
                UiText.Get(
                    "ShellEventIdle");

            ApplyShellState(
                LoadShellState());

            DiscoveryProfileComboBox.SelectionChanged +=
                OnShellProfileSelectionChanged;

            ShowShellSection(
                ShellSection.Map);

            UpdateShellMonitoringPresentation(
                _monitoringControl.Current);
        }

        private UiShellState LoadShellState()
        {
            try
            {
                return _uiShellStateStore.Load() ??
                    UiShellState.Default;
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "UI_SHELL_STATE_LOAD_FAILED " +
                    error.Message);

                return UiShellState.Default;
            }
        }

        private void ApplyShellState(
            UiShellState state)
        {
            var effectiveState =
                state ??
                UiShellState.Default;

            ApplyShellTheme(
                effectiveState.Theme);

            DiscoveryProfileOption selected =
                null;

            if (effectiveState.AccessProfileId
                .HasValue)
            {
                selected =
                    DiscoveryProfileComboBox
                        .Items
                        .OfType<DiscoveryProfileOption>()
                        .FirstOrDefault(
                            option =>
                                option.Profile.Id ==
                                effectiveState
                                    .AccessProfileId
                                    .Value);
            }

            DiscoveryProfileComboBox.SelectedItem =
                selected;

            UpdateShellProfilePresentation();
        }

        private void SaveShellState()
        {
            var selected =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;

            try
            {
                _uiShellStateStore.Save(
                    new UiShellState(
                        selected == null
                            ? (Guid?)null
                            : selected.Profile.Id,
                        _shellTheme));
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "UI_SHELL_STATE_SAVE_FAILED " +
                    error.Message);
            }
        }

        private void OnShellProfileSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            UpdateShellProfilePresentation();

            var selected =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;

            if (selected != null)
            {
                MonitoringVersionComboBox.SelectedItem =
                    selected.Profile.SnmpVersion;
            }

            SaveShellState();

            UpdateMonitoringControlAvailability(
                _monitoringControl.Current);
        }

        private void UpdateShellProfilePresentation()
        {
            var selected =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;

            ShellProfileStatusText.Text =
                selected == null
                    ? UiText.Get(
                        "ShellNoProfile")
                    : string.Empty;

            ShellProfileStatusText
                .SetResourceReference(
                    TextBlock.ForegroundProperty,
                    selected == null
                        ? "NetLoom.Brush.Critical"
                        : "NetLoom.Brush.Success");
        }

        private void OnShellThemeClick(
            object sender,
            RoutedEventArgs e)
        {
            ApplyShellTheme(
                _shellTheme ==
                    UiShellTheme.Light
                    ? UiShellTheme.Dark
                    : UiShellTheme.Light);

            SaveShellState();
        }

        private void ApplyShellTheme(
            UiShellTheme theme)
        {
            var dictionaries =
                Resources.MergedDictionaries;

            for (var index = 0;
                 index < dictionaries.Count;
                 index++)
            {
                var source =
                    dictionaries[index].Source;

                if (source == null)
                {
                    continue;
                }

                var text =
                    source.OriginalString ??
                    string.Empty;

                if (!text.EndsWith(
                        "Themes/Light.xaml",
                        StringComparison.OrdinalIgnoreCase) &&
                    !text.EndsWith(
                        "Themes/Dark.xaml",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dictionaries[index] =
                    new ResourceDictionary
                    {
                        Source =
                            new Uri(
                                theme ==
                                    UiShellTheme.Dark
                                    ? "/NetLoom.Wpf;component/Themes/Dark.xaml"
                                    : "/NetLoom.Wpf;component/Themes/Light.xaml",
                                UriKind.Relative)
                    };

                break;
            }

            _shellTheme =
                theme;

            var themeText =
                UiText.Get(
                    theme ==
                        UiShellTheme.Dark
                        ? "ShellThemeDark"
                        : "ShellThemeLight");

            ShellThemeButton.Content =
                themeText;
            ShellSettingsThemeButton.Content =
                themeText;
        }

        private void ShowShellTopologyEditor(
            IShellTopologyEditor editor)
        {
            _activeTopologyEditor =
                editor ??
                throw new ArgumentNullException(
                    nameof(editor));

            editor.CloseRequested +=
                OnShellTopologyEditorCloseRequested;

            ShellWorkspaceEditorHost.Content =
                editor;
            ShellWorkspaceEditorHost.Visibility =
                Visibility.Visible;
        }

        private async void OnShellTopologyEditorCloseRequested(
            object sender,
            EventArgs e)
        {
            await CloseShellTopologyEditorAsync();
        }

        private async System.Threading.Tasks.Task CloseShellTopologyEditorAsync()
        {
            var editor =
                _activeTopologyEditor;

            if (editor == null)
            {
                return;
            }

            editor.CloseRequested -=
                OnShellTopologyEditorCloseRequested;

            _activeTopologyEditor = null;
            ShellWorkspaceEditorHost.Content = null;
            ShellWorkspaceEditorHost.Visibility =
                Visibility.Collapsed;

            if (editor.HasChanges)
            {
                await RefreshTopologyAsync();
            }
        }

        private void OnShellMapClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_activeTopologyEditor != null)
            {
                _ = CloseShellTopologyEditorAsync();
            }

            ShowShellSection(
                ShellSection.Map);
        }

        private void OnShellEquipmentClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowShellSection(
                ShellSection.Equipment);
        }

        private void OnShellAlertsClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowShellSection(
                ShellSection.Alerts);
        }

        private void OnShellDiscoveryClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowShellSection(
                ShellSection.Discovery);
        }

        private void OnShellSearchClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowShellSection(
                ShellSection.Search);

            LookupQueryTextBox.Focus();
        }

        private void OnShellSettingsClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowShellSection(
                ShellSection.Settings);
        }

        private void ShowShellSection(
            ShellSection section)
        {
            if (section != ShellSection.Map &&
                _activeTopologyEditor != null)
            {
                _ = CloseShellTopologyEditorAsync();
            }

            _shellSection =
                section;

            ShellMapSidebarPanel.Visibility =
                SectionVisibility(
                    section,
                    ShellSection.Map);
            ShellEquipmentSidebarPanel.Visibility =
                SectionVisibility(
                    section,
                    ShellSection.Equipment);
            ShellAlertsSidebarPanel.Visibility =
                SectionVisibility(
                    section,
                    ShellSection.Alerts);
            ShellDiscoverySidebarPanel.Visibility =
                SectionVisibility(
                    section,
                    ShellSection.Discovery);
            ShellSearchSidebarPanel.Visibility =
                SectionVisibility(
                    section,
                    ShellSection.Search);
            ShellSettingsSidebarPanel.Visibility =
                SectionVisibility(
                    section,
                    ShellSection.Settings);

            SetNavigationSelection(
                ShellMapButton,
                section ==
                    ShellSection.Map);
            SetNavigationSelection(
                ShellEquipmentButton,
                section ==
                    ShellSection.Equipment);
            SetNavigationSelection(
                ShellAlertsButton,
                section ==
                    ShellSection.Alerts);
            SetNavigationSelection(
                ShellDiscoveryButton,
                section ==
                    ShellSection.Discovery);
            SetNavigationSelection(
                ShellSearchButton,
                section ==
                    ShellSection.Search);
            SetNavigationSelection(
                ShellSettingsButton,
                section ==
                    ShellSection.Settings);
        }

        private static Visibility
            SectionVisibility(
                ShellSection actual,
                ShellSection expected)
        {
            return actual == expected
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static void SetNavigationSelection(
            Button button,
            bool selected)
        {
            if (selected)
            {
                button.SetResourceReference(
                    Control.BackgroundProperty,
                    "NetLoom.Brush.ShellRailSelected");
                button.SetResourceReference(
                    Control.ForegroundProperty,
                    "NetLoom.Brush.ShellRailText");
                button.SetResourceReference(
                    Control.BorderBrushProperty,
                    "NetLoom.Brush.Accent");
                return;
            }

            button.ClearValue(
                Control.BackgroundProperty);
            button.ClearValue(
                Control.ForegroundProperty);
            button.ClearValue(
                Control.BorderBrushProperty);
        }

        private void UpdateShellMonitoringPresentation(
            MonitoringControlSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var stateText =
                UiText.Get(
                    MonitoringStateResourceKey(
                        snapshot.State));

            var targetCount =
                _monitoringTargetSetSessionActive
                    ? _monitoringActiveTargetCount
                    : snapshot.ActiveTarget == null
                        ? 0
                        : 1;

            var lastPoll =
                LocalMonitoringTime(
                    snapshot.LastSuccessfulPollUtc);

            ShellMonitoringHeaderText.Text =
                UiText.Format(
                    "ShellMonitoringHeader",
                    stateText);

            ShellMonitoringRailText.Text =
                UiText.Format(
                    "ShellMonitoringSummary",
                    stateText,
                    targetCount,
                    lastPoll);

            var brushKey =
                ShellMonitoringBrushKey(
                    snapshot.State);

            ShellMonitoringHeaderText
                .SetResourceReference(
                    TextBlock.ForegroundProperty,
                    brushKey);
            ShellMonitoringRailText
                .SetResourceReference(
                    TextBlock.ForegroundProperty,
                    brushKey);
        }

        private static string
            ShellMonitoringBrushKey(
                MonitoringControlState state)
        {
            switch (state)
            {
                case MonitoringControlState.Faulted:
                    return "NetLoom.Brush.Critical";

                case MonitoringControlState.Starting:
                case MonitoringControlState.Running:
                case MonitoringControlState.Polling:
                case MonitoringControlState.Stopping:
                    return "NetLoom.Brush.Success";

                case MonitoringControlState.Stopped:
                default:
                    return "NetLoom.Brush.ShellRailTextMuted";
            }
        }

        private enum ShellSection
        {
            Map = 0,
            Equipment = 1,
            Alerts = 2,
            Discovery = 3,
            Search = 4,
            Settings = 5
        }
    }
}
