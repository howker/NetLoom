using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NetLoom.Application.MonitoringControl;
using NetLoom.Contracts.TopologyMap;
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

        private MapInteractionMode _mapInteractionMode =
            MapInteractionMode.View;

        private bool IsMapEditMode =>
            _mapInteractionMode ==
            MapInteractionMode.Edit;

        private void InitializeShell()
        {
            ShellBrandText.Text =
                UiText.Get(
                    "ShellBrandName");

            ShellMapButtonText.Text =
                UiText.Get(
                    "ShellMapSection");
            ShellEquipmentButtonText.Text =
                UiText.Get(
                    "ShellEquipmentSection");
            ShellMonitoringButtonText.Text =
                UiText.Get(
                    "ShellMonitoringLabel");
            ShellAlertsButtonText.Text =
                UiText.Get(
                    "ShellAlertsSection");
            ShellDiscoveryButtonText.Text =
                UiText.Get(
                    "ShellDiscoverySection");
            ShellSearchButtonText.Text =
                UiText.Get(
                    "ShellSearchSection");
            ShellSettingsButtonText.Text =
                UiText.Get(
                    "ShellSettingsSection");

            MapInteractionModeLabelText.Text =
                UiText.Get(
                    "MapInteractionModeLabel");
            MapViewModeButton.Content =
                UiText.Get(
                    "MapInteractionModeView");
            MapViewModeButton.ToolTip =
                UiText.Get(
                    "MapInteractionModeViewHint");
            MapEditModeButton.Content =
                UiText.Get(
                    "MapInteractionModeEdit");
            MapEditModeButton.ToolTip =
                UiText.Get(
                    "MapInteractionModeEditHint");
            MapEditModeIndicatorText.Text =
                UiText.Get(
                    "MapInteractionModeEditIndicator");
            MapEditModeHintText.Text =
                UiText.Get(
                    "MapInteractionModeEditHint");

            ShellMapActionsTitleText.Text =
                UiText.Get(
                    "ShellMapActionsTitle");
            EquipmentTitleText.Text =
                UiText.Get(
                    "ShellEquipmentSection");
            EquipmentSummaryText.Text =
                UiText.Format(
                    "ShellEquipmentSummary",
                    0);
            EquipmentList.ItemsSource =
                new ShellEquipmentRow[0];
            ShellProfileAddButton.Content =
                UiText.Get(
                    "DiscoveryProfileAddAction");
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

            SetMapInteractionMode(
                MapInteractionMode.View);

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

            var hasProfiles =
                DiscoveryProfileComboBox.Items.Count > 0;

            DiscoveryProfileComboBox.Visibility =
                hasProfiles
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            DiscoveryProfileComboBox.IsEnabled =
                hasProfiles;

            ShellProfileAddButton.Visibility =
                hasProfiles
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            ShellProfileStatusText.Text =
                selected == null
                    ? UiText.Get(
                        hasProfiles
                            ? "ShellNoProfile"
                            : "ShellProfilesEmpty")
                    : string.Empty;

            ShellProfileStatusText
                .SetResourceReference(
                    TextBlock.ForegroundProperty,
                    selected == null
                        ? (hasProfiles
                            ? "NetLoom.Brush.Warning"
                            : "NetLoom.Brush.TextSecondary")
                        : "NetLoom.Brush.Success");


            DiscoveryProfileSummaryText.Text =
                selected == null
                    ? UiText.Get("ShellProfilesEmpty")
                    : selected.DisplayName;
            DiscoveryProfileHintText.Text =
                selected == null
                    ? UiText.Get("DiscoveryProfileHintMissing")
                    : UiText.Get("DiscoveryProfileReady");
            DiscoveryProfileSummaryText.SetResourceReference(
                TextBlock.ForegroundProperty,
                selected == null
                    ? "NetLoom.Brush.TextPrimary"
                    : "NetLoom.Brush.Success");
            DiscoveryProfileCard.SetResourceReference(
                Border.BorderBrushProperty,
                selected == null
                    ? "NetLoom.Brush.Border"
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

            var targetTheme =
                theme == UiShellTheme.Light
                    ? UiShellTheme.Dark
                    : UiShellTheme.Light;

            var themeText =
                UiText.Get(
                    targetTheme ==
                        UiShellTheme.Dark
                        ? "ShellThemeDark"
                        : "ShellThemeLight");

            ShellThemeButton.Content =
                themeText;
            ShellThemeButton.Tag =
                targetTheme;
            ShellSettingsThemeButton.Content =
                themeText;
            ShellSettingsThemeButton.Tag =
                targetTheme;
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

        private void OnShellMonitoringClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowShellSection(
                ShellSection.Monitoring);
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
            ShellMonitoringSidebarPanel.Visibility =
                SectionVisibility(
                    section,
                    ShellSection.Monitoring);
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
                ShellMonitoringButton,
                section ==
                    ShellSection.Monitoring);
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

        private void UpdateShellEquipmentPresentation(
            MapSnapshot snapshot)
        {
            if (snapshot == null)
            {
                EquipmentSummaryText.Text =
                    UiText.Format(
                        "ShellEquipmentSummary",
                        0);
                EquipmentList.ItemsSource =
                    new ShellEquipmentRow[0];
                return;
            }

            var rows =
                snapshot.Nodes
                    .OrderBy(
                        node => node.Label,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(
                        node => node.Key,
                        StringComparer.Ordinal)
                    .Select(
                        node =>
                            new ShellEquipmentRow(
                                node.DeviceId,
                                node.Label,
                                node.ManagementAddress))
                    .ToArray();

            EquipmentSummaryText.Text =
                UiText.Format(
                    "ShellEquipmentSummary",
                    rows.Length);
            EquipmentList.ItemsSource =
                rows;
        }

        private void OnShellEquipmentDeviceClick(
            object sender,
            RoutedEventArgs e)
        {
            var button =
                sender as Button;

            if (button == null ||
                !(button.Tag is Guid) ||
                (Guid)button.Tag == Guid.Empty)
            {
                return;
            }

            StopStartupTopologyFit();

            var deviceId =
                (Guid)button.Tag;

            _highlightedDeviceId =
                null;
            _selectedDeviceId =
                deviceId;
            _selectedInterfaceId =
                null;
            _selectedPhysicalLinkId =
                null;
            _selectedLocationId =
                null;

            RedrawCurrentMap();
            ShowSelectedDiagnostic();
            UpdateSelectedLayoutControl();

            FocusSelectedMapAtNativeZoom(
                () =>
                    AnimateDiscoveryFocus(
                        deviceId));
        }

        private void OnMapViewModeClick(
            object sender,
            RoutedEventArgs e)
        {
            SetMapInteractionMode(
                MapInteractionMode.View);
        }

        private void OnMapEditModeClick(
            object sender,
            RoutedEventArgs e)
        {
            SetMapInteractionMode(
                MapInteractionMode.Edit);
        }

        private void SetMapInteractionMode(
            MapInteractionMode mode)
        {
            if (mode ==
                MapInteractionMode.View)
            {
                CancelMapEditingGesture();
            }

            _mapInteractionMode =
                mode;

            UpdateMapInteractionModePresentation();
            RefreshMapInteractionModeVisuals();
        }

        private void UpdateMapInteractionModePresentation()
        {
            var editing =
                IsMapEditMode;

            SetMapModeSelection(
                MapViewModeButton,
                !editing);

            SetMapModeSelection(
                MapEditModeButton,
                editing);

            MapEditModeIndicator.Visibility =
                editing
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ManualTopologyButton.IsEnabled =
                editing;

            LocationsButton.IsEnabled =
                editing;

            MapLockSelectedCheckBox.Visibility =
                editing
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private static void SetMapModeSelection(
            Button button,
            bool selected)
        {
            if (button == null)
            {
                return;
            }

            if (selected)
            {
                button.SetResourceReference(
                    Control.BackgroundProperty,
                    "NetLoom.Brush.AccentSoft");
                button.SetResourceReference(
                    Control.BorderBrushProperty,
                    "NetLoom.Brush.Accent");
                button.SetResourceReference(
                    Control.ForegroundProperty,
                    "NetLoom.Brush.TextPrimary");
                return;
            }

            button.ClearValue(
                Control.BackgroundProperty);
            button.ClearValue(
                Control.BorderBrushProperty);
            button.ClearValue(
                Control.ForegroundProperty);
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
                button.SetResourceReference(
                    Control.BorderThicknessProperty,
                    "NetLoom.Thickness.NavigationSelectedBorder");
                return;
            }

            button.ClearValue(
                Control.BackgroundProperty);
            button.ClearValue(
                Control.ForegroundProperty);
            button.ClearValue(
                Control.BorderBrushProperty);
            button.ClearValue(
                Control.BorderThicknessProperty);
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

        private enum MapInteractionMode
        {
            View = 0,
            Edit = 1
        }

        private enum ShellSection
        {
            Map = 0,
            Equipment = 1,
            Monitoring = 2,
            Alerts = 3,
            Discovery = 4,
            Search = 5,
            Settings = 6
        }

        private sealed class ShellEquipmentRow
        {
            public ShellEquipmentRow(
                Guid? deviceId,
                string name,
                string address)
            {
                DeviceId =
                    deviceId;
                Name =
                    string.IsNullOrWhiteSpace(name)
                        ? UiText.Get(
                            "DiagnosticNotAvailable")
                        : name;
                Address =
                    string.IsNullOrWhiteSpace(address)
                        ? string.Empty
                        : address;
            }

            public Guid? DeviceId { get; }

            public string Name { get; }

            public string Address { get; }

            public bool IsSelectable =>
                DeviceId.HasValue;
        }
    }
}
