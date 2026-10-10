using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Media;
using NetLoom.Application.MonitoringControl;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;
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

        private ShellEquipmentRow[] _equipmentRows =
            new ShellEquipmentRow[0];

        private EquipmentFilterMode _equipmentFilterMode =
            EquipmentFilterMode.All;

        private bool IsMapEditMode =>
            _mapInteractionMode ==
            MapInteractionMode.Edit;

        private void InitializeShell()
        {
            InitializeOpticalTypography();

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
            EquipmentFilterTextBox.ToolTip =
                UiText.Get(
                    "EquipmentFilterPlaceholder");
            EquipmentExportCsvButton.Content =
                UiText.Get(
                    "EquipmentExportCsvAction");
            EquipmentNameHeaderText.Text =
                UiText.Get(
                    "EquipmentColumnName");
            EquipmentAddressHeaderText.Text =
                UiText.Get(
                    "EquipmentColumnAddress");
            EquipmentCategoryHeaderText.Text =
                UiText.Get(
                    "EquipmentColumnCategory");
            EquipmentDescriptionHeaderText.Text =
                UiText.Get(
                    "EquipmentColumnDescription");
            EquipmentLocationHeaderText.Text =
                UiText.Get(
                    "EquipmentColumnLocation");
            EquipmentConnectionsHeaderText.Text =
                UiText.Get(
                    "EquipmentColumnConnections");
            EquipmentUpdatedHeaderText.Text =
                UiText.Get(
                    "EquipmentColumnUpdated");
            UpdateEquipmentFilterLabels();
            ShellProfileAddButton.Content =
                UiText.Get(
                    "DiscoveryProfileManageAction");
            DiscoveryProfileComboBox.Tag =
                UiText.Get(
                    "ShellProfilePlaceholder");
            ShellSettingsTitleText.Text =
                UiText.Get(
                    "ShellSettingsSection");
            ShellSettingsMapTitleText.Text =
                UiText.Get(
                    "ShellMapSection");
            ShellProfileSettingsTitleText.Text =
                UiText.Get(
                    "DiscoveryProfileSettingsTitle");
            ShellProfileSettingsAddButton.Content =
                UiText.Get(
                    "DiscoveryProfileAddAction");
            ShellProfileEditButton.Content =
                UiText.Get(
                    "DiscoveryProfileEditAction");
            ShellProfileDeleteButton.Content =
                UiText.Get(
                    "DiscoveryProfileDeleteAction");
            SettingsPollingTitleText.Text =
                UiText.Get(
                    "SettingsPollingSectionTitle");
            SettingsMonitoringIntervalLabelText.Text =
                UiText.Get(
                    "MonitoringIntervalLabel");
            SettingsMonitoringTimeoutLabelText.Text =
                UiText.Get(
                    "MonitoringTimeoutLabel");
            SettingsMonitoringRetriesLabelText.Text =
                UiText.Get(
                    "MonitoringRetriesLabel");
            SettingsMonitoringMaxRepetitionsLabelText.Text =
                UiText.Get(
                    "MonitoringMaxRepetitionsLabel");

            // §8: у поля имя для UI Automation совпадает с видимой подписью.
            AutomationProperties.SetName(
                SettingsMonitoringIntervalTextBox,
                SettingsMonitoringIntervalLabelText.Text);
            AutomationProperties.SetName(
                SettingsMonitoringTimeoutTextBox,
                SettingsMonitoringTimeoutLabelText.Text);
            AutomationProperties.SetName(
                SettingsMonitoringRetriesTextBox,
                SettingsMonitoringRetriesLabelText.Text);
            AutomationProperties.SetName(
                SettingsMonitoringMaxRepetitionsTextBox,
                SettingsMonitoringMaxRepetitionsLabelText.Text);
            SettingsMonitoringKindsLabelText.Text =
                UiText.Get(
                    "MonitoringKindsLabel");
            SettingsMonitoringKindLldpCheckBox.Content =
                UiText.Get(
                    "MonitoringKindLldp");
            SettingsMonitoringKindCdpCheckBox.Content =
                UiText.Get(
                    "MonitoringKindCdp");
            SettingsMonitoringKindFdbCheckBox.Content =
                UiText.Get(
                    "MonitoringKindFdb");
            SettingsMonitoringKindArpCheckBox.Content =
                UiText.Get(
                    "MonitoringKindArp");
            SettingsMonitoringKindHealthCheckBox.Content =
                UiText.Get(
                    "MonitoringKindHealth");
            SettingsMonitoringKindInterfaceCheckBox.Content =
                UiText.Get(
                    "MonitoringKindInterface");
            SettingsMonitoringKindStpCheckBox.Content =
                UiText.Get(
                    "MonitoringKindStp");
            SettingsMonitoringSaveButton.Content =
                UiText.Get(
                    "SettingsPollingSaveAction");
            SettingsMonitoringStatusText.Text =
                string.Empty;
            SettingsAnimationLabelText.Text =
                UiText.Get(
                    "MapMotionSettings");
            SettingsMotionNormalButton.Content =
                UiText.Get(
                    "MapMotionNormalAction");
            SettingsMotionReducedButton.Content =
                UiText.Get(
                    "MapMotionReducedAction");
            SettingsMotionOffButton.Content =
                UiText.Get(
                    "MapMotionOffAction");
            SettingsThemeLabelText.Text =
                UiText.Get(
                    "SettingsThemeLabel");
            SettingsThemeLightButton.Content =
                UiText.Get(
                    "SettingsThemeLight");
            SettingsThemeDarkButton.Content =
                UiText.Get(
                    "SettingsThemeDark");
            ShellMonitoringRailTitleText.Text =
                UiText.Get(
                    "ShellMonitoringLabel");
            ShellEventTitleText.Text =
                UiText.Get(
                    "ShellEventsLabel");
            ShellEventEmptyText.Text =
                UiText.Get(
                    "ShellEventIdle");
            ShellAllEventsButton.Content =
                UiText.Get(
                    "ShellAllEventsAction");

            ShellAlertCountText.Text =
                UiText.Format(
                    "ShellAlertCount",
                    0);
            AlertCriticalCountText.Text =
                UiText.Format(
                    "AlertCriticalCountChip",
                    0);
            AlertWarningCountText.Text =
                UiText.Format(
                    "AlertWarningCountChip",
                    0);

            ApplyShellState(
                LoadShellState());

            DiscoveryProfileComboBox.SelectionChanged +=
                OnShellProfileSelectionChanged;

            InitializeAdr083ShellFrame();

            ShowShellSection(
                ShellSection.Map);

            SetMapInteractionMode(
                MapInteractionMode.View);

            UpdateShellMonitoringPresentation(
                _monitoringControl.Current);
        }

        private void InitializeOpticalTypography()
        {
            var family =
                (FontFamily)FindResource(
                    "NetLoom.FontFamily.Ui");
            var bodyFontSize =
                Convert.ToDouble(
                    FindResource(
                        "NetLoom.FontSize.Body"));
            var badgeFontSize =
                Convert.ToDouble(
                    FindResource(
                        "NetLoom.Navigation.BadgeFontSize"));

            var opticalOffset =
                CalculateOpticalOffset(
                    family,
                    bodyFontSize,
                    FontWeights.Normal);
            var badgeOpticalOffset =
                CalculateOpticalOffset(
                    family,
                    badgeFontSize,
                    FontWeights.SemiBold);

            Resources[
                "NetLoom.Type.OpticalOffsetY"] =
                opticalOffset;
            Resources[
                "NetLoom.Type.BadgeOpticalOffsetY"] =
                badgeOpticalOffset;

            var controlPadding =
                (Thickness)FindResource(
                    "NetLoom.Thickness.ControlPadding");

            Resources[
                "NetLoom.Thickness.ControlPaddingOptical"] =
                new Thickness(
                    controlPadding.Left,
                    Math.Max(
                        0.0,
                        controlPadding.Top +
                        opticalOffset),
                    controlPadding.Right,
                    Math.Max(
                        0.0,
                        controlPadding.Bottom -
                        opticalOffset));
        }

        private static double CalculateOpticalOffset(
            FontFamily family,
            double fontSize,
            FontWeight weight)
        {
            try
            {
                var typeface =
                    new Typeface(
                        family,
                        FontStyles.Normal,
                        weight,
                        FontStretches.Normal);

                GlyphTypeface glyphTypeface;

                if (typeface.TryGetGlyphTypeface(
                    out glyphTypeface))
                {
                    return
                        (
                            glyphTypeface.Baseline -
                            glyphTypeface.CapsHeight / 2.0 -
                            glyphTypeface.Height / 2.0
                        ) *
                        fontSize;
                }
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "UI_OPTICAL_TYPOGRAPHY_FAILED " +
                    error.Message);
            }

            return 0.0;
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

            ApplyPollingSettings(
                effectiveState.PollingSettings);

            SetMotionMode(
                effectiveState.MotionMode);

            ApplyShellTheme(
                effectiveState.Theme);

            bool autoSelected;

            var selected =
                ResolveShellProfileOption(
                    effectiveState.AccessProfileId,
                    out autoSelected);

            DiscoveryProfileComboBox.SelectedItem =
                selected;

            UpdateShellProfilePresentation();

            if (autoSelected)
            {
                SaveShellState();
            }

            UpdateDiscoveryPresentation(
                _discoveryControl.Current);

            UpdateMonitoringControlAvailability(
                _monitoringControl.Current);
        }

        private DiscoveryProfileOption
            ResolveShellProfileOption(
                Guid? profileId,
                out bool autoSelected)
        {
            autoSelected =
                false;

            var options =
                DiscoveryProfileComboBox.Items
                    .OfType<DiscoveryProfileOption>()
                    .ToArray();

            var selected =
                profileId.HasValue
                    ? options.FirstOrDefault(
                        option =>
                            option.Profile.Id ==
                            profileId.Value)
                    : null;

            if (selected == null &&
                options.Length == 1)
            {
                selected =
                    options[0];

                autoSelected =
                    true;
            }

            return selected;
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
                        _shellTheme,
                        _persistedPollingSettings,
                        _motionMode));
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

            UpdateMonitoringProfileVersion(
                selected);

            SaveShellState();

            UpdateDiscoveryPresentation(
                _discoveryControl.Current);

            UpdateMonitoringControlAvailability(
                _monitoringControl.Current);
        }

        private void UpdateMonitoringProfileVersion(
            DiscoveryProfileOption selected)
        {
            var hasProfile =
                selected != null;

            MonitoringVersionReadOnlyBorder.Visibility =
                hasProfile
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            MonitoringVersionNoProfileText.Visibility =
                hasProfile
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            MonitoringVersionValueText.Text =
                hasProfile
                    ? UiText.Format(
                        "MonitoringVersionFromProfile",
                        SnmpVersionText(
                            selected.Profile.SnmpVersion))
                    : string.Empty;
            MonitoringVersionNoProfileText.Text =
                hasProfile
                    ? string.Empty
                    : UiText.Get(
                        "MonitoringVersionNoProfile");
        }

        private void UpdateShellProfilePresentation()
        {
            var selected =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;

            UpdateMonitoringProfileVersion(
                selected);

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
                        : "NetLoom.Brush.TextPrimary");


            DiscoveryProfileSummaryText.Text =
                selected == null
                    ? UiText.Get(
                        hasProfiles
                            ? "ShellNoProfile"
                            : "ShellProfilesEmpty")
                    : selected.DisplayName;
            DiscoveryProfileHintText.Text =
                selected == null
                    ? UiText.Get(
                        hasProfiles
                            ? "DiscoveryValidationProfileRequired"
                            : "DiscoveryProfileHintMissing")
                    : UiText.Get("DiscoveryProfileReady");
            // D2: при выбранном профиле карточка — одна строка, как в макете netloom-v2-5;
            // пояснение показывается только когда профиль не выбран.
            DiscoveryProfileHintText.Visibility =
                selected == null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            DiscoveryProfileSummaryText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "NetLoom.Brush.TextPrimary");
            DiscoveryProfileCard.SetResourceReference(
                Border.BorderBrushProperty,
                "NetLoom.Brush.Border");

            RefreshProfileSettingsList(
                selected);
        }

        private void RefreshProfileSettingsList(
            DiscoveryProfileOption active)
        {
            var preferredId =
                _profileSettingsSelectedId ??
                (active == null
                    ? (Guid?)null
                    : active.Profile.Id);

            var rows =
                _discoveryProfiles
                    .OrderBy(
                        profile =>
                            profile.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(
                        profile =>
                            profile.Id)
                    .Select(
                        profile =>
                            new ProfileSettingsRow(
                                profile,
                                UiText.Format(
                                    "DiscoveryProfileDisplay",
                                    profile.Name,
                                    SnmpVersionText(
                                        profile.SnmpVersion)),
                                active != null &&
                                active.Profile.Id ==
                                    profile.Id
                                    ? UiText.Get(
                                        "DiscoveryProfileActiveLabel")
                                    : string.Empty))
                    .ToArray();

            ShellProfileSettingsList.ItemsSource =
                rows;

            ShellProfileSettingsSummaryText.Text =
                rows.Length == 0
                    ? UiText.Get(
                        "DiscoveryProfileSettingsEmpty")
                    : UiText.Get(
                        "DiscoveryProfileSettingsHint");
            // S3: в макете netloom-v2-6 над списком профилей нет поясняющей строки;
            // пустое состояние по-прежнему видно.
            ShellProfileSettingsSummaryText.Visibility =
                rows.Length == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            var selectedRow =
                preferredId.HasValue
                    ? rows.FirstOrDefault(
                        row =>
                            row.Profile.Id ==
                            preferredId.Value)
                    : null;

            if (selectedRow == null &&
                rows.Length > 0)
            {
                selectedRow =
                    rows[0];
            }

            ShellProfileSettingsList.SelectedItem =
                selectedRow;

            _profileSettingsSelectedId =
                selectedRow == null
                    ? (Guid?)null
                    : selectedRow.Profile.Id;

            UpdateProfileSettingsActions();
        }

        private void UpdateProfileSettingsActions()
        {
            var hasSelection =
                ShellProfileSettingsList.SelectedItem
                    is ProfileSettingsRow;

            ShellProfileEditButton.IsEnabled =
                hasSelection;
            ShellProfileDeleteButton.IsEnabled =
                hasSelection;
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

            UpdateSettingsChoicePresentation();
        }

        private void OnSettingsThemeLightClick(
            object sender,
            RoutedEventArgs e)
        {
            ApplyShellTheme(
                UiShellTheme.Light);

            SaveShellState();
        }

        private void OnSettingsThemeDarkClick(
            object sender,
            RoutedEventArgs e)
        {
            ApplyShellTheme(
                UiShellTheme.Dark);

            SaveShellState();
        }

        private void OnSettingsMotionNormalClick(
            object sender,
            RoutedEventArgs e)
        {
            SetMotionMode(
                MapMotionMode.Normal);
            UpdateSettingsChoicePresentation();
            SaveShellState();
        }

        private void OnSettingsMotionReducedClick(
            object sender,
            RoutedEventArgs e)
        {
            SetMotionMode(
                MapMotionMode.Reduced);
            UpdateSettingsChoicePresentation();
            SaveShellState();
        }

        private void OnSettingsMotionOffClick(
            object sender,
            RoutedEventArgs e)
        {
            SetMotionMode(
                MapMotionMode.Off);
            UpdateSettingsChoicePresentation();
            SaveShellState();
        }

        private void UpdateSettingsChoicePresentation()
        {
            if (SettingsThemeLightButton == null ||
                SettingsThemeDarkButton == null ||
                SettingsMotionNormalButton == null ||
                SettingsMotionReducedButton == null ||
                SettingsMotionOffButton == null)
            {
                return;
            }

            SetSettingsChoiceSelection(
                SettingsThemeLightButton,
                _shellTheme ==
                    UiShellTheme.Light);
            SetSettingsChoiceSelection(
                SettingsThemeDarkButton,
                _shellTheme ==
                    UiShellTheme.Dark);

            SetSettingsChoiceSelection(
                SettingsMotionNormalButton,
                _motionMode ==
                    MapMotionMode.Normal);
            SetSettingsChoiceSelection(
                SettingsMotionReducedButton,
                _motionMode ==
                    MapMotionMode.Reduced);
            SetSettingsChoiceSelection(
                SettingsMotionOffButton,
                _motionMode ==
                    MapMotionMode.Off);
        }

        private static void SetSettingsChoiceSelection(
            Button button,
            bool selected)
        {
            // §8: выбранный вариант передаётся программе как состояние, а не только цветом.
            AutomationSelection.SetSelected(
                button,
                selected,
                UiText.Get(
                    "AutomationSelectedStatus"));

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

            var previousSection =
                _shellSection;

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

            UpdateAdr083SectionPresentation(
                section);

            UpdateTopologyQualityVisibility();
            RefreshNeighborhoodForSection();

            // Sprint 49: путь относится к рабочему виду «Карты» и в других разделах не показывается.
            if (section != ShellSection.Map)
            {
                ClearMapPathState(true);
            }

            UpdateMapPathNotice();

            // G2: без выбранного объекта крошки называют открытый раздел.
            UpdateShellBreadcrumb();
            RefreshEmptyInspectorPrompt();

            if (section == ShellSection.Alerts &&
                previousSection != ShellSection.Alerts)
            {
                OnAlertsSectionEntered();
            }
            else if (section != ShellSection.Alerts)
            {
                _alertAutoSelectPending =
                    false;

                if (previousSection == ShellSection.Alerts)
                {
                    RestoreMapViewportAfterAlerts();
                }
            }
        }

        private void UpdateShellEquipmentPresentation(
            MapSnapshot snapshot)
        {
            if (snapshot == null)
            {
                _equipmentRows =
                    new ShellEquipmentRow[0];

                EquipmentSummaryText.Text =
                    UiText.Format(
                        "ShellEquipmentSummary",
                        0);

                ApplyEquipmentFilter();
                return;
            }

            _equipmentRows =
                snapshot.Nodes
                    .OrderBy(
                        node => node.Label,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(
                        node => node.Key,
                        StringComparer.Ordinal)
                    .Select(
                        node =>
                            BuildShellEquipmentRow(
                                snapshot,
                                node))
                    .ToArray();

            EquipmentSummaryText.Text =
                UiText.Format(
                    "ShellEquipmentSummary",
                    _equipmentRows.Length);

            UpdateEquipmentFilterLabels();
            ApplyEquipmentFilter();
        }

        private ShellEquipmentRow BuildShellEquipmentRow(
            MapSnapshot snapshot,
            MapNode node)
        {
            var diagnostic =
                node.DeviceId.HasValue &&
                _lastDiagnosticSnapshot != null
                    ? _lastDiagnosticSnapshot.Devices
                        .FirstOrDefault(
                            item =>
                                item.DeviceId ==
                                node.DeviceId.Value)
                    : null;

            var alert =
                node.DeviceId.HasValue
                    ? InspectorAlertForDevice(
                        node.DeviceId.Value)
                    : null;

            var isCriticalProblem =
                alert != null &&
                alert.Severity ==
                    NetLoom.Contracts.Alerts
                        .TopologyAlertSeverity.Critical;

            var isWarningProblem =
                alert != null &&
                alert.Severity ==
                    NetLoom.Contracts.Alerts
                        .TopologyAlertSeverity.Warning;

            var location =
                node.DeviceId.HasValue
                    ? LocationPathForDevice(
                        node.DeviceId.Value)
                    : node.LocationId.HasValue
                        ? LocationPath(
                            node.LocationId.Value)
                        : null;

            if (string.IsNullOrWhiteSpace(
                    location))
            {
                location =
                    UiText.Get(
                        "DiagnosticLocationNotAssigned");
            }

            var connectionCount =
                snapshot.Links.Count(
                    link =>
                        string.Equals(
                            link.SourceNodeKey,
                            node.Key,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            link.TargetNodeKey,
                            node.Key,
                            StringComparison.Ordinal));

            var isManual =
                node.Origin ==
                    MapNodeOrigin.Manual;

            // P6 (полевая проверка Sprint 46): у ручного устройства опроса нет по природе —
            // Оно не попадает в «Нет данных», для него есть фильтр «Ручные».
            var hasNoData =
                !isManual &&
                (diagnostic == null ||
                 !diagnostic.LastSeenUtc.HasValue);

            var problemText =
                alert == null
                    ? string.Empty
                    : AlertSeverityText(
                        alert.Severity) +
                      " · " +
                      BuildAlertReasonSummary(
                          alert);

            var stateGlyph =
                isCriticalProblem
                    ? UiText.Get(
                        "OperatorStatusGlyphCritical")
                    : isWarningProblem
                        ? UiText.Get(
                            "OperatorStatusGlyphWarning")
                        : hasNoData
                            ? UiText.Get(
                                "EquipmentStateGlyphNoData")
                            : UiText.Get(
                                "OperatorStatusGlyphActive");

            var description = diagnostic?.SecondaryText;

            // Sprint 48: отметка «Не подтверждено» — строкой под именем: столбец имени виден на любой ширине.
            var row = new ShellEquipmentRow(
                node.DeviceId,
                node.Label,
                node.ManagementAddress,
                NodeCategoryIconToolTip(
                    node.Category),
                description,
                location,
                connectionCount,
                // E4: в таблице «Обновлено» — коротко, как в макете («2 мин», «115 дн», «нет»).
                isManual
                    ? UiText.Get(
                        "DiagnosticValueAbsent")
                    : CompactAgeText(
                        diagnostic == null
                            ? null
                            : diagnostic.LastSeenUtc),
                problemText,
                stateGlyph,
                isCriticalProblem,
                isWarningProblem,
                hasNoData,
                isManual,
                node.DeviceId.HasValue &&
                _selectedDeviceId.HasValue &&
                node.DeviceId.Value ==
                    _selectedDeviceId.Value);

            row.UnconfirmedText =
                node.IsUnconfirmed
                    ? UiText.Get(
                        "DeviceUnconfirmedMark")
                    : null;

            return row;
        }

        private static string CompactAgeText(
            DateTime? valueUtc)
        {
            return CompactAgeText(
                valueUtc,
                DateTime.UtcNow);
        }

        // E7 (sprint46-mockup-gap): «мин» и «ч» — стандартные обозначения единиц времени,
        // Дни — полной формой со склонением: «1 день», «2 дня», «18 дней» (§3 «Числа»: «12 дн» запрещено).
        internal static string CompactAgeText(
            DateTime? valueUtc,
            DateTime nowUtc)
        {
            if (!valueUtc.HasValue)
            {
                return UiText.Get(
                    "EquipmentAgeNever");
            }

            var age =
                nowUtc -
                valueUtc.Value;

            if (age < TimeSpan.FromMinutes(1))
            {
                return UiText.Get(
                    "EquipmentAgeNow");
            }

            if (age < TimeSpan.FromHours(1))
            {
                return UiText.Format(
                    "EquipmentAgeMinutes",
                    (int)Math.Floor(
                        age.TotalMinutes));
            }

            if (age < TimeSpan.FromDays(1))
            {
                return UiText.Format(
                    "EquipmentAgeHours",
                    (int)Math.Floor(
                        age.TotalHours));
            }

            return UiText.FormatCount(
                "EquipmentAgeDays",
                (int)Math.Floor(
                    age.TotalDays));
        }

        private void OnEquipmentFilterTextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            ApplyEquipmentFilter();
        }

        private void OnEquipmentFilterModeClick(
            object sender,
            RoutedEventArgs e)
        {
            var button =
                sender as Button;

            var tag =
                button == null
                    ? null
                    : button.Tag as string;

            switch (tag)
            {
                case "Problems":
                    _equipmentFilterMode =
                        EquipmentFilterMode.Problems;
                    break;

                case "NoData":
                    _equipmentFilterMode =
                        EquipmentFilterMode.NoData;
                    break;

                case "Manual":
                    _equipmentFilterMode =
                        EquipmentFilterMode.Manual;
                    break;

                default:
                    _equipmentFilterMode =
                        EquipmentFilterMode.All;
                    break;
            }

            ApplyEquipmentFilter();
        }

        private void ApplyEquipmentFilter()
        {
            if (EquipmentList == null)
            {
                return;
            }

            var query =
                EquipmentFilterTextBox == null ||
                EquipmentFilterTextBox.Text == null
                    ? string.Empty
                    : EquipmentFilterTextBox.Text
                        .Trim();

            var filtered =
                _equipmentRows
                    .Where(
                        row =>
                            EquipmentFilterMatches(
                                row) &&
                            (
                                query.Length == 0 ||
                                row.Contains(
                                    query)
                            ))
                    .ToArray();

            // §8: одинаковые строки не пересоздаются — кнопка строки не теряет фокус при опросе.
            if (!Shell.RowContent.SameRows(
                    EquipmentList.ItemsSource,
                    filtered))
            {
                EquipmentList.ItemsSource =
                    filtered;
            }

            UpdateEquipmentFilterLabels();

            SetMapModeSelection(
                EquipmentFilterAllButton,
                _equipmentFilterMode ==
                    EquipmentFilterMode.All);
            SetMapModeSelection(
                EquipmentFilterProblemsButton,
                _equipmentFilterMode ==
                    EquipmentFilterMode.Problems);
            SetMapModeSelection(
                EquipmentFilterNoDataButton,
                _equipmentFilterMode ==
                    EquipmentFilterMode.NoData);
            SetMapModeSelection(
                EquipmentFilterManualButton,
                _equipmentFilterMode ==
                    EquipmentFilterMode.Manual);
        }

        private bool EquipmentFilterMatches(
            ShellEquipmentRow row)
        {
            switch (_equipmentFilterMode)
            {
                case EquipmentFilterMode.Problems:
                    return row.HasProblem;

                case EquipmentFilterMode.NoData:
                    return row.HasNoData;

                case EquipmentFilterMode.Manual:
                    return row.IsManual;

                default:
                    return true;
            }
        }

        private void UpdateEquipmentFilterLabels()
        {
            if (EquipmentFilterAllButton == null)
            {
                return;
            }

            EquipmentFilterAllButton.Content =
                UiText.Format(
                    "EquipmentFilterAll",
                    _equipmentRows.Length);
            EquipmentFilterProblemsButton.Content =
                UiText.Format(
                    "EquipmentFilterProblems",
                    _equipmentRows.Count(
                        row =>
                            row.HasProblem));
            EquipmentFilterNoDataButton.Content =
                UiText.Format(
                    "EquipmentFilterNoData",
                    _equipmentRows.Count(
                        row =>
                            row.HasNoData));
            EquipmentFilterManualButton.Content =
                UiText.Format(
                    "EquipmentFilterManual",
                    _equipmentRows.Count(
                        row =>
                            row.IsManual));
        }

        private void OnEquipmentExportCsvClick(
            object sender,
            RoutedEventArgs e)
        {
            var dialog =
                new SaveFileDialog
                {
                    AddExtension = true,
                    DefaultExt = ".csv",
                    Filter =
                        UiText.Get(
                            "EquipmentExportCsvFilter"),
                    FileName =
                        "netloom-equipment-" +
                        DateTime.Now.ToString(
                            "yyyyMMdd-HHmmss",
                            CultureInfo.InvariantCulture) +
                        ".csv",
                    OverwritePrompt = true,
                    Title =
                        UiText.Get(
                            "EquipmentExportCsvTitle")
                };

            if (dialog.ShowDialog(this) !=
                true)
            {
                return;
            }

            try
            {
                var rows =
                    EquipmentList.Items
                        .OfType<ShellEquipmentRow>()
                        .ToArray();

                var builder =
                    new StringBuilder();

                builder.AppendLine(
                    string.Join(
                        ",",
                        new[]
                        {
                            CsvValue(
                                UiText.Get(
                                    "EquipmentColumnName")),
                            CsvValue(
                                UiText.Get(
                                    "EquipmentColumnAddress")),
                            CsvValue(
                                UiText.Get(
                                    "EquipmentColumnCategory")),
                            CsvValue(
                                UiText.Get(
                                    "EquipmentColumnDescription")),
                            CsvValue(
                                UiText.Get(
                                    "EquipmentColumnLocation")),
                            CsvValue(
                                UiText.Get(
                                    "EquipmentColumnConnections")),
                            CsvValue(
                                UiText.Get(
                                    "EquipmentColumnUpdated"))
                        }));

                foreach (var row in rows)
                {
                    builder.AppendLine(
                        string.Join(
                            ",",
                            new[]
                            {
                                CsvValue(
                                    row.Name),
                                CsvValue(
                                    row.Address),
                                CsvValue(
                                    row.Category),
                                CsvValue(
                                    row.Description),
                                CsvValue(
                                    row.Location),
                                CsvValue(
                                    row.Connections),
                                CsvValue(
                                    row.Updated)
                            }));
                }

                File.WriteAllText(
                    dialog.FileName,
                    builder.ToString(),
                    new UTF8Encoding(
                        true));
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError(
                    error.ToString());

                MessageBox.Show(
                    this,
                    UiText.Format(
                        "EquipmentExportCsvFailed",
                        error.Message),
                    UiText.Get(
                        "EquipmentExportCsvFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string CsvValue(
            string value)
        {
            var safe =
                value ?? string.Empty;

            return
                "\"" +
                safe.Replace(
                    "\"",
                    "\"\"") +
                "\"";
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

            // Sprint 49: «Показать на карте» — действие, прежний вид которого попадает в историю.
            RecordMapView(false);

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
            // Правка начинается на рабочих позициях, а окрестность сохраняется как режим просмотра.
            RestoreNeighborhoodLayout();

            if (mode ==
                MapInteractionMode.View)
            {
                CancelMapEditingGesture();
            }

            _mapInteractionMode =
                mode;

            UpdateMapInteractionModePresentation();
            RefreshMapInteractionModeVisuals();
            if (_neighborhoodSelectedDeviceId.HasValue)
            {
                RefreshNeighborhoodPresentation();
                if (!IsMapEditMode) FitNeighborhoodToViewport();
            }
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
            ManualTopologyButton.Visibility =
                editing
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            LocationsButton.IsEnabled =
                editing;
            LocationsButton.Visibility =
                editing
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            MapOperationalFocusButton.Visibility =
                editing
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            MapExportButton.Visibility =
                editing
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            MapRefreshDataButton.Visibility =
                editing
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            Adr083MapEditModeIndicator.Visibility =
                editing
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            MapLockSelectedCheckBox.Visibility =
                Visibility.Collapsed;
        }

        private static void SetMapModeSelection(
            Button button,
            bool selected)
        {
            if (button == null)
            {
                return;
            }

            // §8: выбранный вариант передаётся программе как состояние, а не только цветом.
            AutomationSelection.SetSelected(
                button,
                selected,
                UiText.Get(
                    "AutomationSelectedStatus"));

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
            // §8: выбранный вариант передаётся программе как состояние, а не только цветом.
            AutomationSelection.SetSelected(
                button,
                selected,
                UiText.Get(
                    "AutomationSelectedStatus"));

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

            var monitoringStatus =
                MonitoringStatusSemantic(
                    snapshot.State);

            var stateText =
                OperatorStatusDecoratedDetail(
                    monitoringStatus,
                    UiText.Get(
                        MonitoringStateResourceKey(
                            snapshot.State)));

            var targetCount =
                _monitoringTargetSetSessionActive
                    ? _monitoringActiveTargetCount
                    : snapshot.ActiveTarget == null
                        ? 0
                        : 1;

            var lastPoll =
                LocalMonitoringTime(
                    snapshot.LastSuccessfulPollUtc);

            // Sprint 47: идущий цикл показывается как «Опрос: N / всего» с полосой прогресса.
            UpdateShellMonitoringProgress(
                snapshot,
                stateText);

            ShellMonitoringRailText.Text =
                UiText.Format(
                    "ShellMonitoringSummary",
                    stateText,
                    targetCount,
                    lastPoll);

            ShellMonitoringHeaderText
                .SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "NetLoom.Brush.TextPrimary");
            ShellMonitoringRailText
                .SetResourceReference(
                    TextBlock.ForegroundProperty,
                    OperatorStatusBrushKey(
                        monitoringStatus));

            Adr083MapMonitoringNoticeText.Text =
                UiText.Get(
                    "ShellMapMonitoringStoppedNotice");
            Adr083MapMonitoringNotice.Visibility =
                snapshot.State ==
                    MonitoringControlState.Stopped
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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

        private enum EquipmentFilterMode
        {
            All = 0,
            Problems = 1,
            NoData = 2,
            Manual = 3
        }

        private sealed class ShellEquipmentRow
        {
            public ShellEquipmentRow(
                Guid? deviceId,
                string name,
                string address,
                string category,
                string description,
                string location,
                int connections,
                string updated,
                string problemText,
                string stateGlyph,
                bool isCriticalProblem,
                bool isWarningProblem,
                bool hasNoData,
                bool isManual,
                bool isSelected)
            {
                DeviceId =
                    deviceId;
                Name =
                    string.IsNullOrWhiteSpace(name)
                        ? UiText.Get(
                            "DiagnosticNotAvailable")
                        : name;
                // Макет netloom-v2-2: пустой адрес — «—», ручное устройство — «Ручное» в столбце модели.
                Address =
                    string.IsNullOrWhiteSpace(address)
                        ? UiText.Get(
                            isManual
                                ? "DiagnosticValueAbsent"
                                : "DiagnosticNotAvailable")
                        : address;
                Category =
                    string.IsNullOrWhiteSpace(category)
                        ? UiText.Get(
                            "CategoryUnknown")
                        : category;
                Description =
                    string.IsNullOrWhiteSpace(description)
                        ? UiText.Get(
                            isManual
                                ? "EquipmentManualDescription"
                                : "DiagnosticValueAbsent")
                        : description;
                Location =
                    string.IsNullOrWhiteSpace(location)
                        ? UiText.Get(
                            "DiagnosticLocationNotAssigned")
                        : location;
                Connections =
                    connections.ToString(
                        CultureInfo.CurrentCulture);
                Updated =
                    string.IsNullOrWhiteSpace(updated)
                        ? UiText.Get(
                            "DiagnosticNotAvailable")
                        : updated;
                ProblemText =
                    problemText ??
                    string.Empty;
                StateGlyph =
                    stateGlyph ??
                    string.Empty;
                IsCriticalProblem =
                    isCriticalProblem;
                IsWarningProblem =
                    isWarningProblem;
                HasNoData =
                    hasNoData;
                IsManual =
                    isManual;
                IsSelected =
                    isSelected;
            }

            public Guid? DeviceId { get; }

            public string Name { get; }

            public string Address { get; }

            public string Category { get; }

            public string Description { get; }

            public string Location { get; }

            public string Connections { get; }

            public string Updated { get; }

            public string ProblemText { get; }

            public string StateGlyph { get; }

            public bool IsCriticalProblem { get; }

            public bool IsWarningProblem { get; }

            public bool HasProblem =>
                IsCriticalProblem ||
                IsWarningProblem;

            public string UnconfirmedText { get; set; }

            public string UnconfirmedHint =>
                string.IsNullOrEmpty(UnconfirmedText)
                    ? null
                    : UiText.Get(
                        "DeviceUnconfirmedHint");

            public Visibility UnconfirmedVisibility =>
                string.IsNullOrEmpty(UnconfirmedText)
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            public Visibility ProblemVisibility =>
                HasProblem
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            public bool HasNoData { get; }

            public bool IsManual { get; }

            public bool IsSelected { get; }

            public bool IsSelectable =>
                DeviceId.HasValue;

            public bool Contains(
                string query)
            {
                return
                    Contains(
                        Name,
                        query) ||
                    Contains(
                        Address,
                        query) ||
                    Contains(
                        Category,
                        query) ||
                    Contains(
                        Description,
                        query) ||
                    Contains(
                        Location,
                        query) ||
                    Contains(
                        ProblemText,
                        query);
            }

            private static bool Contains(
                string value,
                string query)
            {
                return
                    !string.IsNullOrWhiteSpace(
                        value) &&
                    value.IndexOf(
                        query,
                        StringComparison
                            .CurrentCultureIgnoreCase) >=
                    0;
            }
        }
    }
}
