using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NetLoom.Application.Discovery;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.Snmp;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Discovery;
using NetLoom.Wpf.Shell;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private const int DiscoveryMaxAddresses = 4096;

        private readonly IDiscoveryControl _discoveryControl;
        private IReadOnlyList<AccessProfile> _discoveryProfiles;
        private Guid? _profileSettingsSelectedId;

        public event EventHandler<DiscoveryProfileCheckRequestedEventArgs>
            DiscoveryProfileCheckRequested;

        public event EventHandler<DiscoveryProfileCreateRequestedEventArgs>
            DiscoveryProfileCreateRequested;
        public event EventHandler<DiscoveryProfileUpdateRequestedEventArgs>
            DiscoveryProfileUpdateRequested;
        public event EventHandler<DiscoveryProfileDeleteRequestedEventArgs>
            DiscoveryProfileDeleteRequested;
        private readonly IDiscoveryCandidateMaterializer
            _discoveryCandidateMaterializer;

        private readonly IDiscoveryRunJournal _discoveryRunJournal;
        private Guid? _activeDiscoveryRunId;
        private bool _discoveryRetryActive;
        private Guid? _discoveryInboxRunId;
        private string _discoveryRetryFocusAddress;

        public static readonly DependencyProperty DiscoveryInboxCanRetryProperty =
            DependencyProperty.Register(nameof(DiscoveryInboxCanRetry), typeof(bool),
                typeof(MainWindow), new PropertyMetadata(true));

        public bool DiscoveryInboxCanRetry
        {
            get => (bool)GetValue(DiscoveryInboxCanRetryProperty);
            private set
            {
                SetValue(DiscoveryInboxCanRetryProperty, value);
                if (DiscoveryInboxActionsBar != null) UpdateDiscoveryInboxActionsBar();
            }
        }

        private bool _discoveryRunAddedDevices;
        private bool _discoveryRunInProgress;
        private Task _discoveryTopologyRefreshTask = Task.CompletedTask;
        private DiscoveryRunRecord _discoveryLatestRun;
        // Часы итога запуска; галерея подставляет свои, чтобы показать правдоподобную длительность.
        internal Func<DateTime> DiscoveryRunClock { get; set; } =
            () => DateTime.UtcNow;

        private bool _discoveryClosed;

        private void InitializeDiscoveryPanel()
        {
            DiscoveryTitleText.Text =
                UiText.Get("DiscoveryTitle");
            DiscoveryStateLabelText.Text =
                UiText.Get("DiscoveryStateLabel");
            DiscoveryLastRunTitleText.Text =
                UiText.Get("DiscoveryLastRunTitle");
            DiscoveryRunStartedLabelText.Text =
                UiText.Get("DiscoveryRunStartedLabel");
            DiscoveryRunDurationLabelText.Text =
                UiText.Get("DiscoveryRunDurationLabel");
            DiscoveryRunCheckedLabelText.Text =
                UiText.Get("DiscoveryRunCheckedLabel");
            DiscoveryRunFoundLabelText.Text =
                UiText.Get("DiscoveryRunFoundLabel");
            DiscoveryRunErrorsLabelText.Text =
                UiText.Get("DiscoveryRunErrorsLabel");
            DiscoveryRunKnownUnchangedLabelText.Text =
                UiText.Get("DiscoveryRunKnownUnchangedLabel");
            DiscoveryProgressLabelText.Text =
                UiText.Get("DiscoveryProgressLabel");
            DiscoveryCurrentAddressLabelText.Text =
                UiText.Get("DiscoveryCurrentAddressLabel");
            DiscoveryPhaseLabelText.Text =
                UiText.Get("DiscoveryPhaseLabel");
            DiscoveryRangeTitleText.Text =
                UiText.Get("DiscoveryRangeTitle");
            DiscoveryStartAddressLabelText.Text =
                UiText.Get("DiscoveryStartAddressLabel");
            DiscoveryEndAddressLabelText.Text =
                UiText.Get("DiscoveryEndAddressLabel");
            DiscoverySubnetMaskLabelText.Text =
                UiText.Get("DiscoverySubnetMaskLabel");

            // §8: у поля имя для UI Automation совпадает с видимой подписью.
            AutomationProperties.SetName(
                DiscoveryStartAddressTextBox,
                DiscoveryStartAddressLabelText.Text);
            AutomationProperties.SetName(
                DiscoveryEndAddressTextBox,
                DiscoveryEndAddressLabelText.Text);
            AutomationProperties.SetName(
                DiscoverySubnetMaskTextBox,
                DiscoverySubnetMaskLabelText.Text);
            DiscoveryProfileLabelText.Text =
                UiText.Get("DiscoveryProfileLabel");
            DiscoverySidebarProfileLabelText.Text =
                UiText.Get("DiscoveryProfileLabel");
            DiscoveryProfileAddButton.Content =
                UiText.Get("DiscoveryProfileSettingsAction");
            DiscoveryEmptyTitleText.Text =
                UiText.Get("DiscoveryEmptyTitle");
            DiscoveryEmptyBodyText.Text =
                UiText.Get("DiscoveryEmptyBody");
            DiscoveryStartButton.Content =
                UiText.Get("DiscoveryStartAction");
            DiscoveryStopButton.Content =
                UiText.Get("DiscoveryStopAction");
            DiscoveryWarningText.Text =
                UiText.Get("DiscoveryWarning");
            DiscoveryCandidatesLabelText.Text =
                UiText.Get("DiscoveryProgressSectionTitle");
            DiscoveryInboxEmptyText.Text = UiText.Get("DiscoveryInboxEmpty");
            DiscoveryInboxAcceptButton.Content = UiText.Get("DiscoveryInboxAccept");
            DiscoveryInboxIgnoreButton.Content = UiText.Get("DiscoveryInboxIgnore");
            DiscoveryInboxUnmanagedButton.Content = UiText.Get("DiscoveryInboxMarkUnmanaged");
            DiscoveryInboxPlacementButton.Content = UiText.Get("DiscoveryInboxAssignPlacement");

            DiscoveryStartAddressTextBox.Text =
                string.Empty;
            DiscoveryEndAddressTextBox.Text =
                string.Empty;
            DiscoverySubnetMaskTextBox.Text =
                "255.255.255.0";

            DiscoveryProfileComboBox.ItemsSource =
                _discoveryProfiles
                    .Select(
                        profile =>
                            new DiscoveryProfileOption(
                                profile,
                                UiText.Format(
                                    "DiscoveryProfileDisplay",
                                    profile.Name,
                                    SnmpVersionText(
                                        profile.SnmpVersion))))
                    .ToArray();

            DiscoveryProfileComboBox.SelectedIndex =
                -1;

            _discoveryControl.SnapshotChanged +=
                OnDiscoverySnapshotChanged;
            _discoveryControl.CandidateDiscovered +=
                OnDiscoveryCandidateDiscovered;

            DiscoveryMessageText.Text =
                string.Empty;

            _discoveryLatestRun =
                _discoveryRunJournal.CloseInterruptedRuns(DateTime.UtcNow);

            if (_discoveryLatestRun != null &&
                _discoveryLatestRun.FinishedUtc.HasValue)
            {
                RenderDiscoveryRunSummary();
            }

            RefreshDiscoveryInbox();
            UpdateDiscoveryPresentation(
                _discoveryControl.Current);
        }

        private void CloseDiscoveryPanel()
        {
            if (_discoveryClosed)
            {
                return;
            }

            _discoveryClosed = true;

            _discoveryControl.SnapshotChanged -=
                OnDiscoverySnapshotChanged;
            _discoveryControl.CandidateDiscovered -=
                OnDiscoveryCandidateDiscovered;

            var disposable =
                _discoveryControl as IDisposable;

            disposable?.Dispose();
        }

        private void RefreshDiscoveryProfileOptions(
            Guid? selectedProfileId)
        {
            var options =
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
                            new DiscoveryProfileOption(
                                profile,
                                UiText.Format(
                                    "DiscoveryProfileDisplay",
                                    profile.Name,
                                    SnmpVersionText(
                                        profile.SnmpVersion))))
                    .ToArray();

            DiscoveryProfileComboBox.ItemsSource =
                options;

            var selectedIndex =
                selectedProfileId.HasValue
                    ? Array.FindIndex(
                        options,
                        option =>
                            option.Profile.Id ==
                            selectedProfileId.Value)
                    : -1;

            if (selectedIndex < 0 &&
                options.Length == 1)
            {
                selectedIndex =
                    0;
            }

            DiscoveryProfileComboBox.SelectedIndex =
                selectedIndex;

            DiscoveryMessageText.Text =
                string.Empty;

            UpdateShellProfilePresentation();
        }

        private void OnShellProfileManageClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowShellSection(
                ShellSection.Settings);

            if (_discoveryProfiles.Count == 0)
            {
                ShellProfileSettingsAddButton.Focus();
            }
            else
            {
                ShellProfileSettingsList.Focus();
            }
        }

        private void OnShellProfileSettingsAddClick(
            object sender,
            RoutedEventArgs e)
        {
            ShowDiscoveryProfileDialog(
                null);
        }

        private void OnShellProfileSettingsSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            var selected =
                ShellProfileSettingsList.SelectedItem
                    as ProfileSettingsRow;

            _profileSettingsSelectedId =
                selected == null
                    ? (Guid?)null
                    : selected.Profile.Id;

            UpdateProfileSettingsActions();
        }

        private void OnShellProfileEditClick(
            object sender,
            RoutedEventArgs e)
        {
            var selected =
                ShellProfileSettingsList.SelectedItem
                    as ProfileSettingsRow;

            if (selected == null)
            {
                return;
            }

            ShowDiscoveryProfileDialog(
                selected.Profile);
        }

        private void OnShellProfileRowEditClick(
            object sender,
            RoutedEventArgs e)
        {
            var button =
                sender as Button;
            var row =
                button == null
                    ? null
                    : button.DataContext
                        as ProfileSettingsRow;

            if (row == null)
            {
                return;
            }

            ShellProfileSettingsList.SelectedItem =
                row;

            OnShellProfileEditClick(
                sender,
                e);
        }

        private void OnShellProfileRowDeleteClick(
            object sender,
            RoutedEventArgs e)
        {
            var button =
                sender as Button;
            var row =
                button == null
                    ? null
                    : button.DataContext
                        as ProfileSettingsRow;

            if (row == null)
            {
                return;
            }

            ShellProfileSettingsList.SelectedItem =
                row;

            OnShellProfileDeleteClick(
                sender,
                e);
        }

        private void OnShellProfileDeleteClick(
            object sender,
            RoutedEventArgs e)
        {
            var selected =
                ShellProfileSettingsList.SelectedItem
                    as ProfileSettingsRow;

            if (selected == null)
            {
                return;
            }

            var requestHandler =
                DiscoveryProfileDeleteRequested;

            if (requestHandler == null)
            {
                ShellProfileSettingsSummaryText.Text =
                    UiText.Get(
                        "DiscoveryProfileChangeUnavailable");

                return;
            }

            var active =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;
            var activeProfileId =
                active == null
                    ? (Guid?)null
                    : active.Profile.Id;
            var deletingActive =
                activeProfileId.HasValue &&
                activeProfileId.Value ==
                    selected.Profile.Id;

            if (!ShowProfileDeleteConfirmation(
                    selected.Profile,
                    deletingActive))
            {
                return;
            }

            var request =
                new DiscoveryProfileDeleteRequestedEventArgs(
                    selected.Profile.Id);

            requestHandler(
                this,
                request);

            if (!request.Deleted)
            {
                ShellProfileSettingsSummaryText.Text =
                    UiText.Format(
                        "DiscoveryProfileDeleteFailed",
                        string.IsNullOrWhiteSpace(
                            request.FailureMessage)
                            ? UiText.Get(
                                "DiagnosticNotAvailable")
                            : request.FailureMessage);

                return;
            }

            _discoveryProfiles =
                _discoveryProfiles
                    .Where(
                        profile =>
                            profile.Id !=
                            selected.Profile.Id)
                    .ToArray();

            var fallback =
                _discoveryProfiles
                    .OrderBy(
                        profile =>
                            profile.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(
                        profile =>
                            profile.Id)
                    .FirstOrDefault();

            var nextActiveProfileId =
                deletingActive
                    ? (Guid?)null
                    : activeProfileId;

            _profileSettingsSelectedId =
                nextActiveProfileId ??
                (fallback == null
                    ? (Guid?)null
                    : fallback.Id);

            RefreshDiscoveryProfileOptions(
                nextActiveProfileId);

            UpdateDiscoveryPresentation(
                _discoveryControl.Current);

            SaveShellState();

            UpdateMonitoringControlAvailability(
                _monitoringControl.Current);
        }

        private void ShowDiscoveryProfileDialog(
            AccessProfile editingProfile)
        {
            var creating =
                editingProfile == null;
            var activeOption =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;
            var activeProfileId =
                activeOption == null
                    ? (Guid?)null
                    : activeOption.Profile.Id;

            var createRequestHandler =
                DiscoveryProfileCreateRequested;
            var updateRequestHandler =
                DiscoveryProfileUpdateRequested;

            if ((creating &&
                 createRequestHandler == null) ||
                (!creating &&
                 updateRequestHandler == null))
            {
                var message =
                    UiText.Get(
                        creating
                            ? "DiscoveryProfileCreationUnavailable"
                            : "DiscoveryProfileChangeUnavailable");

                DiscoveryMessageText.Text =
                    message;
                ShellProfileSettingsSummaryText.Text =
                    message;

                return;
            }

            var nameTextBox =
                new TextBox
                {
                    Text =
                        creating
                            ? string.Empty
                            : editingProfile.Name
                };

            var versionOptions =
                new[]
                {
                    new DiscoverySnmpVersionOption(
                        SnmpVersion.V1,
                        "v1"),
                    new DiscoverySnmpVersionOption(
                        SnmpVersion.V2C,
                        "v2c")
                };

            var versionComboBox =
                new ComboBox
                {
                    DisplayMemberPath =
                        "DisplayName",
                    ItemsSource =
                        versionOptions,
                    SelectedIndex =
                        creating
                            ? 1
                            : Array.FindIndex(
                                versionOptions,
                                option =>
                                    option.Version ==
                                    editingProfile.SnmpVersion)
                };

            if (versionComboBox.SelectedIndex < 0)
            {
                versionComboBox.SelectedIndex =
                    1;
            }

            var communityPasswordBox =
                new PasswordBox();

            var nameLabel =
                new TextBlock
                {
                    Text =
                        UiText.Get(
                            "DiscoveryProfileNameLabel")
                };
            var versionLabel =
                new TextBlock
                {
                    Text =
                        UiText.Get(
                            "DiscoveryProfileVersionLabel")
                };
            var communityLabel =
                new TextBlock
                {
                    Text =
                        UiText.Get(
                            "DiscoveryProfileCommunityLabel")
                };
            var communityHint =
                new TextBlock
                {
                    Text =
                        UiText.Get(
                            "DiscoveryProfileCommunityKeepHint"),
                    TextWrapping =
                        TextWrapping.Wrap,
                    Visibility =
                        creating
                            ? Visibility.Collapsed
                            : Visibility.Visible
                };
            var errorText =
                new TextBlock
                {
                    TextWrapping =
                        TextWrapping.Wrap,
                    Visibility =
                        Visibility.Collapsed
                };

            var checkTitle = new TextBlock { Text = UiText.Get("DiscoveryProfileCheckTitle") };
            var checkAddress = new TextBox
            {
                Name = "DiscoveryProfileCheckAddress",
                Text = DiscoveryStartAddressTextBox.Text ?? string.Empty
            };
            var checkAddressHint = new TextBlock { Text = UiText.Get("DiscoveryProfileCheckAddressHint") };
            var checkError = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            var checkButton = new Button
            {
                Name = "DiscoveryProfileCheckButton",
                Content = UiText.Get("DiscoveryProfileCheckAction"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var checkProgress = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            var checkRows = new StackPanel { Name = "DiscoveryProfileCheckRows" };
            AutomationProperties.SetLabeledBy(checkAddress, checkTitle);
            AutomationProperties.SetName(checkAddress, checkTitle.Text);
            LiveRegion.SetIsPolite(checkProgress, true);
            LiveRegion.SetIsPolite(checkError, true);

            var saveButton =
                new Button
                {
                    Content =
                        UiText.Get(
                            "DiscoveryProfileSaveAction"),
                    MinWidth =
                        90,
                    IsDefault =
                        true
                };
            var cancelButton =
                new Button
                {
                    Content =
                        UiText.Get(
                            "DiscoveryProfileCancelAction"),
                    MinWidth =
                        90,
                    IsCancel =
                        true
                };
            var actionPanel =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal,
                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };

            actionPanel.Children.Add(
                cancelButton);
            actionPanel.Children.Add(
                saveButton);

            var content =
                new StackPanel();

            content.Children.Add(
                nameLabel);
            content.Children.Add(
                nameTextBox);
            content.Children.Add(
                versionLabel);
            content.Children.Add(
                versionComboBox);
            content.Children.Add(
                communityLabel);
            content.Children.Add(
                communityPasswordBox);
            content.Children.Add(
                communityHint);
            content.Children.Add(
                errorText);
            content.Children.Add(checkTitle);
            content.Children.Add(checkAddress);
            content.Children.Add(checkAddressHint);
            content.Children.Add(checkError);
            content.Children.Add(checkButton);
            content.Children.Add(checkProgress);
            content.Children.Add(checkRows);
            content.Children.Add(
                actionPanel);

            var dialog =
                CreateThemedDialog(
                    creating
                        ? UiText.Get(
                            "DiscoveryProfileDialogTitle")
                        : UiText.Get(
                            "DiscoveryProfileEditDialogTitle"),
                    content,
                    (double)FindResource("NetLoom.Width.DiscoveryProfileDialog"));

            content.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.PanelPadding");
            nameLabel.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.FieldLabel");
            versionLabel.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.FieldLabel");
            versionLabel.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.FieldLabel");
            communityLabel.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.FieldLabel");
            communityLabel.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.FieldLabel");
            communityHint.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.MutedText");
            communityHint.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.GapXsTop");
            errorText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "NetLoom.Brush.Critical");
            errorText.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.GapSmTop");
            actionPanel.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.SectionGapTop");
            cancelButton.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.SecondaryButton");
            saveButton.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.InlineGap");

            checkTitle.SetResourceReference(FrameworkElement.StyleProperty, "NetLoom.Style.FieldLabel");
            checkTitle.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.SectionGapTop");
            checkAddress.SetResourceReference(Control.FontFamilyProperty, "NetLoom.FontFamily.Mono");
            checkAddressHint.SetResourceReference(FrameworkElement.StyleProperty, "NetLoom.Style.MutedText");
            checkAddressHint.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.GapXsTop");
            checkError.SetResourceReference(TextBlock.ForegroundProperty, "NetLoom.Brush.Critical");
            checkError.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.GapSmTop");
            checkButton.SetResourceReference(FrameworkElement.StyleProperty, "NetLoom.Style.SecondaryButton");
            checkButton.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.GapSmTop");
            checkProgress.SetResourceReference(FrameworkElement.StyleProperty, "NetLoom.Style.MutedText");
            checkProgress.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.GapSmTop");
            var dialogClosed = false;
            dialog.Closed += (sender, args) => dialogClosed = true;
            System.Windows.Input.KeyboardNavigation.SetTabNavigation(dialog,
                System.Windows.Input.KeyboardNavigationMode.Cycle);
            dialog.Loaded += (sender, args) => nameTextBox.Focus();

            checkButton.Click += async (sender, args) =>
            {
                System.Net.IPAddress address;
                if (!System.Net.IPAddress.TryParse((checkAddress.Text ?? string.Empty).Trim(), out address)
                    || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
                    || (checkAddress.Text ?? string.Empty).Trim().Split('.').Length != 4)
                {
                    checkRows.Children.Clear();
                    checkProgress.Visibility = Visibility.Collapsed;
                    checkError.Text = UiText.Get("DiscoveryProfileCheckAddressInvalid");
                    checkError.Visibility = Visibility.Visible;
                    checkAddress.Focus();
                    return;
                }
                var selectedVersion = versionComboBox.SelectedItem as DiscoverySnmpVersionOption;
                var handler = DiscoveryProfileCheckRequested;
                if (handler == null || selectedVersion == null)
                {
                    checkError.Text = UiText.Get("DiscoveryProfileCheckUnavailable");
                    checkError.Visibility = Visibility.Visible;
                    return;
                }
                var community = communityPasswordBox.Password ?? string.Empty;
                if (creating && community.Length == 0)
                {
                    checkError.Text = UiText.Get("DiscoveryProfileValidationCommunity");
                    checkError.Visibility = Visibility.Visible;
                    communityPasswordBox.Focus();
                    return;
                }
                var bytes = community.Length == 0 ? null : Encoding.UTF8.GetBytes(community);
                checkError.Visibility = Visibility.Collapsed;
                checkRows.Children.Clear();
                checkButton.IsEnabled = false;
                checkProgress.Text = UiText.Get("DiscoveryProfileChecking");
                checkProgress.Visibility = Visibility.Visible;
                try
                {
                    var request = new DiscoveryProfileCheckRequestedEventArgs(address, selectedVersion.Version,
                        bytes, creating ? (Guid?)null : editingProfile.Id);
                    handler(this, request);
                    if (request.Result == null) throw new InvalidOperationException();
                    var report = await request.Result;
                    if (dialogClosed) return;
                    foreach (var item in report.Items) checkRows.Children.Add(CreateProfileCheckRow(item));
                    checkProgress.Text = UiText.Get("DiscoveryProfileCheckCompleted");
                }
                catch
                {
                    if (dialogClosed) return;
                    checkProgress.Visibility = Visibility.Collapsed;
                    checkError.Text = UiText.Get("DiscoveryProfileCheckUnavailable");
                    checkError.Visibility = Visibility.Visible;
                }
                finally
                {
                    if (bytes != null) Array.Clear(bytes, 0, bytes.Length);
                    if (!dialogClosed) checkButton.IsEnabled = true;
                }
            };

            Action<string> showError =
                message =>
                {
                    errorText.Text =
                        message;
                    errorText.Visibility =
                        Visibility.Visible;
                };

            saveButton.Click +=
                (buttonSender, buttonArgs) =>
                {
                    var name =
                        (nameTextBox.Text ??
                            string.Empty)
                            .Trim();

                    if (name.Length == 0)
                    {
                        showError(
                            UiText.Get(
                                "DiscoveryProfileValidationName"));

                        return;
                    }

                    if (_discoveryProfiles.Any(
                            profile =>
                                (creating ||
                                 profile.Id !=
                                    editingProfile.Id) &&
                                string.Equals(
                                    profile.Name,
                                    name,
                                    StringComparison.OrdinalIgnoreCase)))
                    {
                        showError(
                            UiText.Get(
                                "DiscoveryProfileValidationDuplicateName"));

                        return;
                    }

                    var selectedVersion =
                        versionComboBox.SelectedItem
                            as DiscoverySnmpVersionOption;

                    if (selectedVersion == null)
                    {
                        showError(
                            UiText.Get(
                                "DiscoveryProfileValidationVersion"));

                        return;
                    }

                    var community =
                        communityPasswordBox.Password ??
                        string.Empty;

                    if (creating &&
                        community.Length == 0)
                    {
                        showError(
                            UiText.Get(
                                "DiscoveryProfileValidationCommunity"));

                        return;
                    }

                    byte[] communityUtf8 =
                        community.Length == 0
                            ? null
                            : Encoding.UTF8.GetBytes(
                                community);

                    try
                    {
                        AccessProfile savedProfile;
                        string failureMessage;

                        if (creating)
                        {
                            var request =
                                new DiscoveryProfileCreateRequestedEventArgs(
                                    name,
                                    selectedVersion.Version,
                                    communityUtf8);

                            createRequestHandler(
                                this,
                                request);

                            savedProfile =
                                request.CreatedProfile;
                            failureMessage =
                                request.FailureMessage;
                        }
                        else
                        {
                            var request =
                                new DiscoveryProfileUpdateRequestedEventArgs(
                                    editingProfile.Id,
                                    name,
                                    selectedVersion.Version,
                                    communityUtf8);

                            updateRequestHandler(
                                this,
                                request);

                            savedProfile =
                                request.UpdatedProfile;
                            failureMessage =
                                request.FailureMessage;
                        }

                        if (savedProfile == null)
                        {
                            showError(
                                UiText.Format(
                                    creating
                                        ? "DiscoveryProfileSaveFailed"
                                        : "DiscoveryProfileUpdateFailed",
                                    string.IsNullOrWhiteSpace(
                                        failureMessage)
                                        ? UiText.Get(
                                            "DiagnosticNotAvailable")
                                        : failureMessage));

                            return;
                        }

                        if (creating)
                        {
                            _discoveryProfiles =
                                _discoveryProfiles
                                    .Concat(
                                        new[]
                                        {
                                            savedProfile
                                        })
                                    .ToArray();
                        }
                        else
                        {
                            _discoveryProfiles =
                                _discoveryProfiles
                                    .Select(
                                        profile =>
                                            profile.Id ==
                                                savedProfile.Id
                                                ? savedProfile
                                                : profile)
                                    .ToArray();
                        }

                        _profileSettingsSelectedId =
                            savedProfile.Id;

                        RefreshDiscoveryProfileOptions(
                            activeProfileId);

                        UpdateDiscoveryPresentation(
                            _discoveryControl.Current);

                        DiscoveryMessageText.Text =
                            string.Empty;

                        dialog.DialogResult =
                            true;
                    }
                    finally
                    {
                        if (communityUtf8 != null)
                        {
                            Array.Clear(
                                communityUtf8,
                                0,
                                communityUtf8.Length);
                        }

                        communityPasswordBox.Password =
                            string.Empty;
                    }
                };

            dialog.ShowDialog();
        }

        private FrameworkElement CreateProfileCheckRow(SnmpProfileCheckItem item)
        {
            var neutralAbsent = item.Status == SnmpProfileCheckStatus.Absent
                && (item.Kind == SnmpProfileCheckKind.BridgeMib || item.Kind == SnmpProfileCheckKind.QBridgeMib);
            var semantic = item.Status == SnmpProfileCheckStatus.Failed ? OperatorStatusSemantic.Critical
                : item.Status == SnmpProfileCheckStatus.Partial || item.Status == SnmpProfileCheckStatus.Absent && !neutralAbsent
                    ? OperatorStatusSemantic.Warning : OperatorStatusSemantic.Normal;
            var row = new Grid { DataContext = item };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.GapSmTop");
            var glyph = new TextBlock
            {
                Text = item.Status == SnmpProfileCheckStatus.NotChecked || neutralAbsent
                    ? UiText.Get("OperatorStatusGlyphNotChecked")
                    : semantic == OperatorStatusSemantic.Warning ? UiText.Get("OperatorStatusGlyphProfileWarning")
                    : semantic == OperatorStatusSemantic.Critical ? UiText.Get("OperatorStatusGlyphProfileFailed")
                    : OperatorStatusGlyph(semantic),
                VerticalAlignment = VerticalAlignment.Center
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, OperatorStatusBrushKey(semantic));
            glyph.SetResourceReference(FrameworkElement.MinWidthProperty, "NetLoom.Status.GlyphMinWidth");
            glyph.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.StatusGlyph");
            var label = new TextBlock
            {
                Text = UiText.Get("DiscoveryProfileCheck" + item.Kind),
                VerticalAlignment = VerticalAlignment.Center
            };
            var detail = new TextBlock
            {
                Text = ProfileCheckDetail(item),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            detail.SetResourceReference(FrameworkElement.MarginProperty, "NetLoom.Thickness.FieldGapLeft");
            detail.SetResourceReference(FrameworkElement.StyleProperty, "NetLoom.Style.MutedText");
            System.Windows.Documents.Typography.SetNumeralAlignment(detail, FontNumeralAlignment.Tabular);
            Grid.SetColumn(label, 1);
            Grid.SetColumn(detail, 2);
            row.Children.Add(glyph);
            row.Children.Add(label);
            row.Children.Add(detail);
            return row;
        }

        private static string ProfileCheckDetail(SnmpProfileCheckItem item)
        {
            if (item.Status == SnmpProfileCheckStatus.NotChecked) return UiText.Get("DiscoveryProfileCheckNotChecked");
            if (item.Status == SnmpProfileCheckStatus.Failed)
            {
                if (!item.Failure.HasValue) return UiText.Get("DiscoveryProfileCheckNoResponse");
                var key = item.Failure == SnmpTransportFailure.UnsupportedCredentials ? "Unsupported"
                    : item.Failure.Value.ToString();
                return UiText.Get("DiscoveryErrorSnmp" + key);
            }
            if (item.Status == SnmpProfileCheckStatus.Partial) return UiText.Get("DiscoveryProfileCheckPartial");
            if (item.Status == SnmpProfileCheckStatus.Absent) return UiText.Get("DiscoveryProfileCheckAbsent");
            if (item.Kind == SnmpProfileCheckKind.Availability && item.Milliseconds.HasValue)
                return UiText.Format("DiscoveryProfileCheckMilliseconds", item.Milliseconds.Value);
            if (item.Kind == SnmpProfileCheckKind.IfMib && item.Count.HasValue)
                return UiText.FormatCount("DiscoveryProfileCheckInterfaces", item.Count.Value);
            if (item.Kind == SnmpProfileCheckKind.LldpMib && item.Count.HasValue)
                return UiText.FormatCount("DiscoveryProfileCheckNeighbors", item.Count.Value);
            return string.Empty;
        }

        private bool ShowProfileDeleteConfirmation(
            AccessProfile profile,
            bool isActive)
        {
            var prompt =
                new TextBlock
                {
                    Text =
                        UiText.Format(
                            "DiscoveryProfileDeleteConfirm",
                            profile.Name),
                    TextWrapping =
                        TextWrapping.Wrap
                };

            var warning =
                new TextBlock
                {
                    Text =
                        UiText.Get(
                            "DiscoveryProfileDeleteActiveWarning"),
                    TextWrapping =
                        TextWrapping.Wrap,
                    Visibility =
                        isActive
                            ? Visibility.Visible
                            : Visibility.Collapsed
                };

            warning.SetResourceReference(
                TextBlock.ForegroundProperty,
                "NetLoom.Brush.Warning");

            var deleteButton =
                new Button
                {
                    Content =
                        UiText.Get(
                            "DiscoveryProfileDeleteConfirmAction"),
                    MinWidth =
                        90,
                    IsDefault =
                        true
                };

            deleteButton.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.SecondaryButton");
            deleteButton.SetResourceReference(
                Control.ForegroundProperty,
                "NetLoom.Brush.Critical");
            deleteButton.SetResourceReference(
                Control.BorderBrushProperty,
                "NetLoom.Brush.Critical");

            var cancelButton =
                new Button
                {
                    Content =
                        UiText.Get(
                            "DiscoveryProfileCancelAction"),
                    MinWidth =
                        90,
                    IsCancel =
                        true
                };

            cancelButton.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.SecondaryButton");

            var actions =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal,
                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };

            actions.Children.Add(
                cancelButton);
            actions.Children.Add(
                deleteButton);

            var content =
                new StackPanel();

            content.Children.Add(
                prompt);
            content.Children.Add(
                warning);
            content.Children.Add(
                actions);

            var dialog =
                CreateThemedDialog(
                    UiText.Get(
                        "DiscoveryProfileDeleteDialogTitle"),
                    content,
                    480);

            content.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.PanelPadding");
            warning.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.GapSmTop");
            actions.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.SectionGapTop");
            deleteButton.Margin =
                (Thickness)dialog.FindResource(
                    "NetLoom.Thickness.InlineGap");

            deleteButton.Click +=
                (buttonSender, buttonArgs) =>
                    dialog.DialogResult =
                        true;

            return dialog.ShowDialog() ==
                true;
        }

        private Window CreateThemedDialog(
            string title,
            FrameworkElement content,
            double width)
        {
            var dialog =
                new Window
                {
                    Owner =
                        this,
                    Title =
                        title,
                    Width =
                        width,
                    SizeToContent =
                        SizeToContent.Height,
                    ResizeMode =
                        ResizeMode.NoResize,
                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,
                    ShowInTaskbar =
                        false,
                    WindowStyle =
                        WindowStyle.None
                };

            dialog.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source =
                        new Uri(
                            "/NetLoom.Wpf;component/Themes/DesignTokens.xaml",
                            UriKind.Relative)
                });
            dialog.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source =
                        new Uri(
                            _shellTheme ==
                                UiShellTheme.Dark
                                ? "/NetLoom.Wpf;component/Themes/Dark.xaml"
                                : "/NetLoom.Wpf;component/Themes/Light.xaml",
                            UriKind.Relative)
                });
            dialog.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source =
                        new Uri(
                            "/NetLoom.Wpf;component/Themes/DeviceIcons.xaml",
                            UriKind.Relative)
                });
            dialog.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source =
                        new Uri(
                            "/NetLoom.Wpf;component/Themes/Controls.xaml",
                            UriKind.Relative)
                });

            dialog.SetResourceReference(
                Control.BackgroundProperty,
                "NetLoom.Brush.Window");
            dialog.SetResourceReference(
                Control.ForegroundProperty,
                "NetLoom.Brush.TextPrimary");
            dialog.SetResourceReference(
                Control.FontFamilyProperty,
                "NetLoom.FontFamily.Ui");
            dialog.SetResourceReference(
                Control.FontSizeProperty,
                "NetLoom.FontSize.Body");

            var dialogTitle =
                new TextBlock
                {
                    Text =
                        title,
                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            dialogTitle.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.WindowTitle");

            var closeGlyph =
                new System.Windows.Shapes.Path
                {
                    Data =
                        System.Windows.Media.Geometry.Parse(
                            "M 0,0 L 8,8 M 8,0 L 0,8"),
                    Width =
                        10,
                    Height =
                        10,
                    Stretch =
                        System.Windows.Media.Stretch.Uniform,
                    StrokeThickness =
                        1.6,
                    IsHitTestVisible =
                        false
                };

            closeGlyph.SetResourceReference(
                System.Windows.Shapes.Shape.StrokeProperty,
                "NetLoom.Brush.TextPrimary");

            var closeButton =
                new Button
                {
                    Content =
                        closeGlyph,
                    Width =
                        Convert.ToDouble(
                            dialog.FindResource(
                                "NetLoom.Control.MinHeight")),
                    Height =
                        Convert.ToDouble(
                            dialog.FindResource(
                                "NetLoom.Control.MinHeight")),
                    Padding =
                        new Thickness(
                            0),
                    HorizontalAlignment =
                        HorizontalAlignment.Right,
                    IsCancel =
                        true
                };

            closeButton.SetResourceReference(
                FrameworkElement.StyleProperty,
                "NetLoom.Style.SecondaryButton");
            closeButton.Click +=
                (closeSender, closeArgs) =>
                    dialog.Close();

            var titleGrid =
                new Grid();

            titleGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1.0,
                            GridUnitType.Star)
                });
            titleGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            Grid.SetColumn(
                dialogTitle,
                0);
            Grid.SetColumn(
                closeButton,
                1);
            titleGrid.Children.Add(
                dialogTitle);
            titleGrid.Children.Add(
                closeButton);

            var titleBar =
                new Border
                {
                    Padding =
                        (Thickness)dialog.FindResource(
                            "NetLoom.Thickness.ShellHeaderPadding"),
                    BorderThickness =
                        (Thickness)dialog.FindResource(
                            "NetLoom.Thickness.BorderBottom"),
                    Child =
                        titleGrid
                };

            titleBar.SetResourceReference(
                Border.BackgroundProperty,
                "NetLoom.Brush.Surface");
            titleBar.SetResourceReference(
                Border.BorderBrushProperty,
                "NetLoom.Brush.Border");

            titleBar.MouseLeftButtonDown +=
                (titleSender, titleArgs) =>
                {
                    if (titleArgs.ChangedButton ==
                        System.Windows.Input.MouseButton.Left)
                    {
                        dialog.DragMove();
                    }
                };

            var dialogRoot =
                new Grid();

            dialogRoot.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });
            dialogRoot.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        new GridLength(
                            1.0,
                            GridUnitType.Star)
                });

            Grid.SetRow(
                titleBar,
                0);
            Grid.SetRow(
                content,
                1);
            dialogRoot.Children.Add(
                titleBar);
            dialogRoot.Children.Add(
                content);

            var dialogChrome =
                new Border
                {
                    BorderThickness =
                        (Thickness)dialog.FindResource(
                            "NetLoom.Thickness.BorderThin"),
                    Child =
                        dialogRoot
                };

            dialogChrome.SetResourceReference(
                Border.BackgroundProperty,
                "NetLoom.Brush.Window");
            dialogChrome.SetResourceReference(
                Border.BorderBrushProperty,
                "NetLoom.Brush.BorderStrong");

            dialog.Content =
                dialogChrome;

            return dialog;
        }

        private async void OnDiscoveryStartClick(
            object sender,
            RoutedEventArgs e)
        {
            DiscoveryControlRequest request;
            string validation;

            if (!TryBuildDiscoveryRequest(
                    out request,
                    out validation))
            {
                DiscoveryMessageText.Text =
                    validation;
                return;
            }

            DiscoveryMessageText.Text =
                string.Empty;

            try
            {
                var selected =
                    (DiscoveryProfileOption)DiscoveryProfileComboBox.SelectedItem;
                var start = _discoveryRunJournal.BeginRun(
                    request,
                    selected.Profile.Name,
                    DiscoveryRunClock());
                _activeDiscoveryRunId = start.RunId;
                RefreshDiscoveryInbox();
                request = request.WithExcludedAddresses(start.ExcludedAddresses);

                await _discoveryControl
                    .StartAsync(
                        request,
                        _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
                when (_lifetimeCancellation
                    .IsCancellationRequested)
            {
                FinishFailedDiscoveryStart();
            }
            catch (Exception error)
            {
                FinishFailedDiscoveryStart();
                ShowDiscoveryActionFailure(
                    error);
            }
        }

        private void FinishFailedDiscoveryStart()
        {
            if (!_activeDiscoveryRunId.HasValue)
            {
                return;
            }

            _discoveryLatestRun = _discoveryRunJournal.FinishRun(
                _activeDiscoveryRunId.Value,
                _discoveryControl.Current,
                DiscoveryRunClock());
            _activeDiscoveryRunId = null;
            _discoveryRetryActive = false;
            RefreshDiscoveryInbox();
            RestoreDiscoveryInboxFocus();
            UpdateDiscoveryPresentation(_discoveryControl.Current);
        }

        private async void OnDiscoveryStopClick(
            object sender,
            RoutedEventArgs e)
        {
            DiscoveryMessageText.Text =
                string.Empty;

            try
            {
                await _discoveryControl
                    .StopAsync(
                        _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
                when (_lifetimeCancellation
                    .IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                ShowDiscoveryActionFailure(
                    error);
            }
        }

        private void OnDiscoverySnapshotChanged(
            object sender,
            DiscoveryControlSnapshotChangedEventArgs e)
        {
            if (_discoveryClosed ||
                _lifetimeCancellation
                    .IsCancellationRequested)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        () =>
                        {
                            if (!_discoveryClosed)
                            {
                                UpdateDiscoveryPresentation(
                                    e.Snapshot);
                            }
                        }));
                return;
            }

            UpdateDiscoveryPresentation(
                e.Snapshot);
        }

        private void FocusDiscoveredDevice(
            string managementAddress)
        {
            if (string.IsNullOrWhiteSpace(
                    managementAddress) ||
                _lastMapSnapshot == null)
            {
                return;
            }

            foreach (var node in
                _lastMapSnapshot.Nodes)
            {
                if (!node.DeviceId.HasValue ||
                    !string.Equals(
                        node.ManagementAddress,
                        managementAddress,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var deviceId =
                    node.DeviceId.Value;

                _highlightedDeviceId = null;
                _selectedDeviceId = deviceId;
                _selectedInterfaceId = null;
                _selectedPhysicalLinkId = null;
                _selectedLocationId = null;

                RedrawCurrentMap();
                ShowSelectedDiagnostic();
                UpdateSelectedLayoutControl();

                FocusSelectedMapAtNativeZoom(
                    () =>
                    {
                        if (!_selectedDeviceId.HasValue ||
                            _selectedDeviceId.Value != deviceId)
                        {
                            return;
                        }

                        AnimateDiscoveryFocus(
                            deviceId);
                    });

                return;
            }
        }

        private void OnDiscoveryCandidateDiscovered(
            object sender,
            DiscoveryCandidateDiscoveredEventArgs e)
        {
            if (_discoveryClosed ||
                _lifetimeCancellation
                    .IsCancellationRequested)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        () =>
                            PresentDiscoveryCandidate(
                                e.Candidate)));
                return;
            }

            PresentDiscoveryCandidate(
                e.Candidate);
        }

        private async void PresentDiscoveryCandidate(
            DiscoveryCandidateSnapshot candidate)
        {
            if (_discoveryClosed ||
                candidate == null)
            {
                return;
            }

            var address =
                candidate.Address.ToString();

            try
            {
                var knownDeviceIds = _lastMapSnapshot?.Nodes
                    .Where(node => node.DeviceId.HasValue)
                    .Select(node => node.DeviceId.Value)
                    .ToArray() ?? new Guid[0];
                Guid? deviceId;

                if (_activeDiscoveryRunId.HasValue)
                {
                    var result = _discoveryRunJournal.RecordCandidate(
                        _activeDiscoveryRunId.Value,
                        candidate,
                        DiscoveryRunClock());
                    deviceId = result.DeviceId;
                    RefreshDiscoveryInbox();
                }
                else
                {
                    deviceId = _discoveryCandidateMaterializer.Materialize(
                        candidate,
                        DiscoveryRunClock());
                }

                if (deviceId.HasValue && !knownDeviceIds.Contains(deviceId.Value))
                {
                    _discoveryRunAddedDevices = true;
                }

                var refreshTask = RefreshTopologyAsync();
                _discoveryTopologyRefreshTask = _discoveryTopologyRefreshTask.IsCompleted
                    ? refreshTask
                    : Task.WhenAll(_discoveryTopologyRefreshTask, refreshTask);
                await refreshTask;

                if (deviceId.HasValue)
                {
                    FocusDiscoveredDevice(
                        address);
                }
            }
            catch (Exception error)
            {
                DiscoveryMessageText.Text =
                    UiText.Format(
                        "DiscoveryMaterializationFailed",
                        address,
                        error.Message);
            }
        }

        private bool TryBuildDiscoveryRequest(
            out DiscoveryControlRequest request,
            out string validation)
        {
            request = null;
            validation = null;

            var startAddress =
                (DiscoveryStartAddressTextBox.Text ??
                    string.Empty)
                    .Trim();

            var endAddress =
                (DiscoveryEndAddressTextBox.Text ??
                    string.Empty)
                    .Trim();

            var subnetMask =
                (DiscoverySubnetMaskTextBox.Text ??
                    string.Empty)
                    .Trim();

            if (startAddress.Length == 0 ||
                endAddress.Length == 0 ||
                subnetMask.Length == 0)
            {
                validation =
                    UiText.Get(
                        "DiscoveryValidationRangeRequired");
                return false;
            }

            try
            {
                Ipv4RangeExpander.Expand(
                    startAddress,
                    endAddress,
                    subnetMask,
                    DiscoveryMaxAddresses);
            }
            catch (InvalidOperationException)
            {
                validation =
                    UiText.Format(
                        "DiscoveryValidationRangeTooLarge",
                        DiscoveryMaxAddresses);
                return false;
            }
            catch (FormatException)
            {
                validation =
                    UiText.Get(
                        "DiscoveryValidationRangeInvalid");
                return false;
            }
            catch (ArgumentException error)
            {
                validation =
                    string.Equals(
                        error.Message,
                        "DISCOVERY_RANGE_CROSSES_SUBNET",
                        StringComparison.Ordinal)
                        ? UiText.Get(
                            "DiscoveryValidationRangeCrossesSubnet")
                        : UiText.Get(
                            "DiscoveryValidationRangeInvalid");
                return false;
            }

            var selected =
                DiscoveryProfileComboBox.SelectedItem
                    as DiscoveryProfileOption;

            if (selected == null)
            {
                validation =
                    _discoveryProfiles.Count == 0
                        ? UiText.Get(
                            "DiscoveryNoProfiles")
                        : UiText.Get(
                            "DiscoveryValidationProfileRequired");
                return false;
            }

            request =
                new DiscoveryControlRequest(
                    startAddress,
                    endAddress,
                    subnetMask,
                    selected.Profile.Id,
                    selected.Profile.SnmpVersion);

            return true;
        }

        private void UpdateDiscoveryPresentation(
            DiscoveryControlSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            TrackDiscoveryRun(
                snapshot);

            var lastRunState =
                snapshot.State == DiscoveryControlState.Idle &&
                _discoveryLatestRun != null
                    ? _discoveryLatestRun.State
                    : snapshot.State;
            var discoveryStatus =
                DiscoveryStatusSemantic(
                    lastRunState);

            ApplyOperatorStatus(
                DiscoveryStateGlyphText,
                DiscoveryStateValueText,
                discoveryStatus,
                UiText.Get(
                    DiscoveryStateResourceKey(
                        lastRunState)));

            // Sprint 48: после перезапуска приложения прогресс — из сохранённого запуска, как и его состояние.
            var showsSavedRun =
                snapshot.State == DiscoveryControlState.Idle &&
                snapshot.TotalAddresses == 0 &&
                _discoveryLatestRun != null;

            DiscoveryProgressValueText.Text =
                showsSavedRun
                    ? UiText.Format(
                        "DiscoveryProgressValue",
                        _discoveryLatestRun.ProcessedAddresses,
                        _discoveryLatestRun.TotalAddresses,
                        _discoveryLatestRun.FoundCandidates)
                    : snapshot.TotalAddresses > 0
                        ? UiText.Format(
                            "DiscoveryProgressValue",
                            snapshot.ProcessedAddresses,
                            snapshot.TotalAddresses,
                            snapshot.FoundCandidates)
                        : UiText.Get(
                            "DiscoveryProgressPending");

            DiscoveryCurrentAddressValueText.Text =
                snapshot.CurrentAddress == null
                    ? UiText.Get(
                        "DiagnosticNotAvailable")
                    : snapshot.CurrentAddress
                        .ToString();

            // Sprint 48: только реально выполняемые фазы обнаружения — ICMP, TCP (если заданы порты), SNMP.
            DiscoveryPhaseValueText.Text =
                snapshot.CurrentPhase.HasValue
                    ? UiText.Format(
                        "DiscoveryPhaseValue",
                        snapshot.CurrentPhase.Value.ToString().ToUpperInvariant(),
                        snapshot.PhaseStep,
                        snapshot.PhaseCount)
                    : UiText.Get(
                        "DiagnosticNotAvailable");

            // Sprint 48: адрес и этап имеют смысл только во время запуска; после завершения строки скрываются.
            var runInProgress =
                snapshot.State == DiscoveryControlState.Starting ||
                snapshot.State == DiscoveryControlState.Running ||
                snapshot.State == DiscoveryControlState.Stopping;
            var runRowsVisibility =
                runInProgress
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            DiscoveryCurrentAddressLabelText.Visibility =
                runRowsVisibility;
            DiscoveryCurrentAddressValueText.Visibility =
                runRowsVisibility;
            DiscoveryPhaseLabelText.Visibility =
                runRowsVisibility;
            DiscoveryPhaseValueText.Visibility =
                runRowsVisibility;

            var canStart =
                snapshot.State ==
                    DiscoveryControlState.Idle ||
                snapshot.State ==
                    DiscoveryControlState.Completed ||
                snapshot.State ==
                    DiscoveryControlState.Stopped ||
                snapshot.State ==
                    DiscoveryControlState.Faulted;

            DiscoveryInboxCanRetry = canStart && !_activeDiscoveryRunId.HasValue;

            DiscoveryStartAddressTextBox.IsEnabled =
                canStart;
            DiscoveryEndAddressTextBox.IsEnabled =
                canStart;
            DiscoverySubnetMaskTextBox.IsEnabled =
                canStart;
            var hasSelectedProfile =
                DiscoveryProfileComboBox.SelectedItem
                    is DiscoveryProfileOption;

            DiscoveryProfileComboBox.IsEnabled =
                canStart &&
                _discoveryProfiles.Count > 0;
            DiscoveryProfileAddButton.IsEnabled =
                canStart;
            DiscoveryStartButton.IsEnabled =
                canStart &&
                hasSelectedProfile;
            DiscoveryStartButton.ToolTip =
                canStart &&
                !hasSelectedProfile
                    ? UiText.Get(
                        _discoveryProfiles.Count > 0
                            ? "DiscoveryValidationProfileRequired"
                            : "DiscoveryNoProfiles")
                    : null;
            DiscoveryStopButton.IsEnabled =
                snapshot.State ==
                    DiscoveryControlState.Starting ||
                snapshot.State ==
                    DiscoveryControlState.Running;

            DiscoveryStartButton.Visibility =
                DiscoveryStopButton.IsEnabled
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            DiscoveryStopButton.Visibility =
                DiscoveryStopButton.IsEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (snapshot.State !=
                    DiscoveryControlState.Faulted &&
                string.IsNullOrWhiteSpace(
                    DiscoveryMessageText.Text))
            {
                DiscoveryMessageText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "NetLoom.Brush.TextSecondary");
            }

            if (snapshot.State ==
                    DiscoveryControlState.Faulted &&
                !string.IsNullOrWhiteSpace(
                    snapshot.FaultMessage))
            {
                DiscoveryMessageText.Text =
                    UiText.Format(
                        "DiscoveryFaultMessage",
                        snapshot.FaultMessage);
                DiscoveryMessageText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "NetLoom.Brush.Critical");
            }

            UpdateDiscoveryResultsSurface(
                snapshot);
            RenderDiscoveryRunSummary();
        }

        private async void TrackDiscoveryRun(
            DiscoveryControlSnapshot snapshot)
        {
            if (IsDiscoveryRunActive(snapshot.State))
            {
                if (!_discoveryRunInProgress)
                {
                    _discoveryRunAddedDevices = false;
                    _discoveryRunInProgress = true;
                }

                return;
            }

            if ((snapshot.State == DiscoveryControlState.Completed ||
                 snapshot.State == DiscoveryControlState.Stopped ||
                 snapshot.State == DiscoveryControlState.Faulted) &&
                _activeDiscoveryRunId.HasValue)
            {
                var runId = _activeDiscoveryRunId.Value;
                _discoveryRunJournal.FinishRun(runId, snapshot, DiscoveryRunClock());
                _discoveryLatestRun = _discoveryRunJournal.GetRun(runId);
                var wasRetry = _discoveryRetryActive;
                _activeDiscoveryRunId = null;
                _discoveryRetryActive = false;
                RefreshDiscoveryInbox();
                if (wasRetry) RestoreDiscoveryInboxFocus();
            }

            var restartMonitoring = _discoveryRunInProgress &&
                _discoveryRunAddedDevices &&
                (snapshot.State == DiscoveryControlState.Completed ||
                 snapshot.State == DiscoveryControlState.Stopped) &&
                _monitoringTargetSetSessionActive &&
                _monitoringControl.Current.State == MonitoringControlState.Running;

            _discoveryRunInProgress = false;
            _discoveryRunAddedDevices = false;

            if (restartMonitoring)
            {
                // Sprint 48: новые результаты обнаружения опрашиваются сразу — набор целей перезапускается после запуска обнаружения (на ходу Engine набор не меняет).
                // Дожидаемся обновлений от кандидатов и читаем итоговый набор перед перезапуском.
                await _discoveryTopologyRefreshTask;
                await RefreshTopologyAsync();
                await RestartMonitoringTargetSetAsync();
            }
        }

        private static bool IsDiscoveryRunActive(
            DiscoveryControlState state)
        {
            return state == DiscoveryControlState.Starting ||
                   state == DiscoveryControlState.Running ||
                   state == DiscoveryControlState.Stopping;
        }

        private void RenderDiscoveryRunSummary()
        {
            if (_discoveryLatestRun == null ||
                !_discoveryLatestRun.FinishedUtc.HasValue ||
                _activeDiscoveryRunId.HasValue ||
                IsDiscoveryRunActive(_discoveryControl.Current.State))
            {
                DiscoveryRunSummaryPanel.Visibility =
                    Visibility.Collapsed;
                return;
            }

            DiscoveryRunSummaryPanel.Visibility =
                Visibility.Visible;
            DiscoveryRunStartedValueText.Text =
                _discoveryLatestRun.StartedUtc
                    .ToLocalTime()
                    .ToString("G", CultureInfo.CurrentCulture);
            DiscoveryRunDurationValueText.Text =
                DiscoveryRunDurationText(
                    _discoveryLatestRun.FinishedUtc.Value -
                    _discoveryLatestRun.StartedUtc);
            DiscoveryRunCheckedValueText.Text =
                UiText.FormatCount(
                    "DiscoveryRunChecked",
                    _discoveryLatestRun.TotalAddresses,
                    _discoveryLatestRun.ProcessedAddresses);
            DiscoveryRunFoundValueText.Text =
                UiText.Format(
                    "DiscoveryRunFoundValue",
                    _discoveryLatestRun.FoundCandidates,
                    _discoveryLatestRun.SnmpResponded);
            DiscoveryRunErrorsValueText.Text =
                _discoveryLatestRun.ErrorCount.ToString(
                    CultureInfo.CurrentCulture);
            DiscoveryRunKnownUnchangedValueText.Text =
                _discoveryLatestRun.KnownUnchangedCount.ToString(
                    CultureInfo.CurrentCulture);
        }

        private static string DiscoveryRunDurationText(
            TimeSpan duration)
        {
            var totalSeconds =
                (long)Math.Floor(duration.TotalSeconds);

            if (totalSeconds < 60)
            {
                return UiText.Format(
                    "DiscoveryRunDurationSeconds",
                    totalSeconds);
            }

            if (totalSeconds < 60 * 60)
            {
                return UiText.Format(
                    "DiscoveryRunDurationMinutes",
                    totalSeconds / 60,
                    totalSeconds % 60);
            }

            return UiText.Format(
                "DiscoveryRunDurationHours",
                totalSeconds / (60 * 60),
                totalSeconds / 60 % 60);
        }

        private void RefreshDiscoveryInbox()
        {
            var focusedRow = (System.Windows.Input.Keyboard.FocusedElement as Button)?.DataContext
                as DiscoveryInboxRow;
            var run = _activeDiscoveryRunId.HasValue
                ? _discoveryRunJournal.GetRun(_activeDiscoveryRunId.Value)
                : _discoveryLatestRun;
            DiscoveryInboxCanRetry = !_activeDiscoveryRunId.HasValue &&
                !IsDiscoveryRunActive(_discoveryControl.Current.State);
            DiscoveryInboxPanel.Visibility = run == null ? Visibility.Collapsed : Visibility.Visible;
            if (run == null)
            {
                DiscoveryInboxGroupsList.ItemsSource = new DiscoveryInboxGroup[0];
                _discoveryInboxRunId = null;
                UpdateDiscoveryInboxActionsBar();
                return;
            }

            var groups = DiscoveryInboxProjection.Build(
                _discoveryRunJournal.GetResults(run.Id), run,
                id => _discoveryProfiles.FirstOrDefault(profile => profile.Id == id)?.Name,
                DiscoveryRunClock(),
                id => _lastMapSnapshot?.Nodes.FirstOrDefault(node => node.DeviceId == id)?.Label,
                DiscoveryInboxPlacementPath);
            var previous = DiscoveryInboxGroupsList.Items.Cast<DiscoveryInboxGroup>().ToArray();
            var sameRun = _discoveryInboxRunId == run.Id;
            if (sameRun)
            {
                foreach (var group in groups)
                {
                    var oldGroup = previous.FirstOrDefault(item => item.Group == group.Group);
                    if (oldGroup != null) group.IsExpanded = oldGroup.IsExpanded;
                }
            }
            var changed = !sameRun || previous.Length != groups.Count ||
                !previous.Zip(groups, (oldGroup, group) => oldGroup.Group == group.Group &&
                    oldGroup.Count == group.Count && oldGroup.Rows.Zip(group.Rows,
                        (oldRow, row) => oldRow.HasSameContent(row)).All(equal => equal)).All(equal => equal);
            if (changed)
            {
                foreach (var row in previous.SelectMany(group => group.Rows))
                    row.PropertyChanged -= OnDiscoveryInboxSelectionChanged;
                if (sameRun)
                {
                    var selected = new HashSet<string>(previous.SelectMany(group => group.Rows)
                        .Where(row => row.IsSelected).Select(row => row.Address));
                    foreach (var row in groups.SelectMany(group => group.Rows))
                        row.IsSelected = selected.Contains(row.Address);
                }
                DiscoveryInboxGroupsList.ItemsSource = groups;
                foreach (var row in groups.SelectMany(group => group.Rows))
                    row.PropertyChanged += OnDiscoveryInboxSelectionChanged;
            }
            UpdateDiscoveryInboxActionsBar();
            _discoveryInboxRunId = run.Id;
            DiscoveryInboxTitleText.Text = DiscoveryInboxProjection.BuildTitle(run);
            DiscoveryInboxSummaryText.Text = DiscoveryInboxProjection.BuildSummary(groups, run);
            DiscoveryInboxEmptyText.Visibility = groups.Count == 0 && run.FinishedUtc.HasValue
                ? Visibility.Visible : Visibility.Collapsed;
            UpdateDiscoveryResultsSurface(_discoveryControl.Current);
            if (changed && focusedRow != null && DiscoveryInboxCanRetry)
                RestoreDiscoveryInboxFocus(focusedRow.Address);
        }

        private async void OnDiscoveryInboxRetryClick(object sender, RoutedEventArgs e)
        {
            var row = (sender as Button)?.DataContext as DiscoveryInboxRow;
            if (row == null || !DiscoveryInboxCanRetry) return;
            if (row.CanUndoIgnore)
            {
                await UndoDiscoveryInboxIgnoreAsync(row);
                return;
            }
            if (!row.CanRetry) return;
            var run = _discoveryRunJournal.GetRun(row.RunId);
            if (run == null) return;
            var selected = DiscoveryProfileComboBox.SelectedItem as DiscoveryProfileOption;
            var profile = _discoveryProfiles.FirstOrDefault(item => item.Id == run.AccessProfileId)
                ?? selected?.Profile;
            if (profile == null)
            {
                DiscoveryMessageText.Text = UiText.Get("DiscoveryValidationProfileRequired");
                return;
            }

            DiscoveryMessageText.Text = string.Empty;
            try
            {
                var request = new DiscoveryControlRequest(row.Address, row.Address,
                    "255.255.255.255", profile.Id, profile.SnmpVersion);
                _discoveryRunJournal.BeginRetry(row.RunId, row.Address, DiscoveryRunClock());
                _activeDiscoveryRunId = row.RunId;
                _discoveryRetryActive = true;
                _discoveryRetryFocusAddress = row.Address;
                DiscoveryInboxCanRetry = false;
                await _discoveryControl.StartAsync(request, _lifetimeCancellation.Token);
                if (_discoveryRetryActive) DiscoveryStopButton.Focus();
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
                FinishFailedDiscoveryStart();
            }
            catch (Exception error)
            {
                FinishFailedDiscoveryStart();
                ShowDiscoveryActionFailure(error);
            }
        }

        private void RestoreDiscoveryInboxFocus(string address = null)
        {
            address = address ?? _discoveryRetryFocusAddress;
            _discoveryRetryFocusAddress = null;
            if (address == null) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_discoveryClosed) return;
                DiscoveryInboxGroupsList.UpdateLayout();
                ExpandDiscoveryInboxAddress(DiscoveryInboxGroupsList, address);
                DiscoveryInboxGroupsList.UpdateLayout();
                var button = FindRetryButton(DiscoveryInboxGroupsList, address);
                if (button != null && button.IsVisible && button.IsEnabled)
                {
                    button.BringIntoView();
                    if (button.Focus()) return;
                }
                DiscoveryStartButton.Focus();
            }));
        }

        private static bool ExpandDiscoveryInboxAddress(DependencyObject root, string address)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                var expander = child as Expander;
                var group = expander?.DataContext as DiscoveryInboxGroup;
                if (group != null && group.Rows.Any(row => row.Address == address && row.CanRetry))
                {
                    expander.IsExpanded = true;
                    return true;
                }
                if (ExpandDiscoveryInboxAddress(child, address)) return true;
            }
            return false;
        }

        private static Button FindRetryButton(DependencyObject root, string address)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                var button = child as Button;
                var row = button?.DataContext as DiscoveryInboxRow;
                if (row != null && row.Address == address && row.CanRetry) return button;
                var found = FindRetryButton(child, address);
                if (found != null) return found;
            }
            return null;
        }

        private void UpdateDiscoveryResultsSurface(
            DiscoveryControlSnapshot snapshot)
        {
            if (snapshot == null ||
                DiscoveryResultsEmptyCard == null ||
                DiscoveryResultsPanel == null)
            {
                return;
            }

            var isBeforeFirstRun =
                _discoveryLatestRun == null &&
                !_activeDiscoveryRunId.HasValue &&
                snapshot.State ==
                    DiscoveryControlState.Idle &&
                snapshot.TotalAddresses == 0 &&
                snapshot.ProcessedAddresses == 0;

            DiscoveryResultsEmptyCard.Visibility =
                isBeforeFirstRun
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            DiscoveryResultsPanel.Visibility =
                isBeforeFirstRun
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        private void ShowDiscoveryActionFailure(
            Exception error)
        {
            DiscoveryMessageText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "NetLoom.Brush.Critical");

            var message =
                error == null
                    ? null
                    : error.Message;

            switch (message)
            {
                case "DISCOVERY_SNMP_COMMUNITY_REQUIRED":
                    DiscoveryMessageText.Text =
                        UiText.Get(
                            "DiscoveryProfileCredentialsMissing");
                    return;

                case "DISCOVERY_SNMP_V3_SECURITY_PROTOCOLS_NOT_STORED":
                    DiscoveryMessageText.Text =
                        UiText.Get(
                            "DiscoveryV3SecurityUnsupported");
                    return;

                default:
                    DiscoveryMessageText.Text =
                        UiText.Format(
                            "DiscoveryActionFailed",
                            string.IsNullOrWhiteSpace(
                                message)
                                ? UiText.Get(
                                    "DiagnosticNotAvailable")
                                : message);
                    return;
            }
        }

        private static string DiscoveryStateResourceKey(
            DiscoveryControlState state)
        {
            switch (state)
            {
                case DiscoveryControlState.Idle:
                    return "DiscoveryStateIdle";
                case DiscoveryControlState.Starting:
                    return "DiscoveryStateStarting";
                case DiscoveryControlState.Running:
                    return "DiscoveryStateRunning";
                case DiscoveryControlState.Stopping:
                    return "DiscoveryStateStopping";
                case DiscoveryControlState.Completed:
                    return "DiscoveryStateCompleted";
                case DiscoveryControlState.Stopped:
                    return "DiscoveryStateStopped";
                case DiscoveryControlState.Faulted:
                    return "DiscoveryStateFaulted";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state));
            }
        }

        private static string SnmpVersionText(
            SnmpVersion version)
        {
            switch (version)
            {
                case SnmpVersion.V1:
                    return "v1";
                case SnmpVersion.V2C:
                    return "v2c";
                case SnmpVersion.V3:
                    return "v3";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(version));
            }
        }

        public sealed class DiscoveryProfileCheckRequestedEventArgs : EventArgs
        {
            public DiscoveryProfileCheckRequestedEventArgs(System.Net.IPAddress address, SnmpVersion snmpVersion,
                byte[] communityUtf8, Guid? profileId)
            {
                Address = address ?? throw new ArgumentNullException(nameof(address));
                SnmpVersion = snmpVersion;
                CommunityUtf8 = communityUtf8;
                ProfileId = profileId;
            }

            public System.Net.IPAddress Address { get; }
            public SnmpVersion SnmpVersion { get; }
            public byte[] CommunityUtf8 { get; }
            public Guid? ProfileId { get; }
            public Task<SnmpProfileCheckReport> Result { get; set; }
        }

        public sealed class DiscoveryProfileCreateRequestedEventArgs :
            EventArgs
        {
            public DiscoveryProfileCreateRequestedEventArgs(
                string name,
                SnmpVersion snmpVersion,
                byte[] communityUtf8)
            {
                Name =
                    name ??
                    throw new ArgumentNullException(
                        nameof(name));

                SnmpVersion =
                    snmpVersion;

                CommunityUtf8 =
                    communityUtf8 ??
                    throw new ArgumentNullException(
                        nameof(communityUtf8));
            }

            public string Name { get; }

            public SnmpVersion SnmpVersion { get; }

            public byte[] CommunityUtf8 { get; }

            public AccessProfile CreatedProfile { get; set; }

            public string FailureMessage { get; set; }
        }

        public sealed class DiscoveryProfileUpdateRequestedEventArgs :
            EventArgs
        {
            public DiscoveryProfileUpdateRequestedEventArgs(
                Guid profileId,
                string name,
                SnmpVersion snmpVersion,
                byte[] communityUtf8)
            {
                if (profileId == Guid.Empty)
                {
                    throw new ArgumentException(
                        "Access profile id is required.",
                        nameof(profileId));
                }

                ProfileId =
                    profileId;
                Name =
                    name ??
                    throw new ArgumentNullException(
                        nameof(name));
                SnmpVersion =
                    snmpVersion;
                CommunityUtf8 =
                    communityUtf8;
            }

            public Guid ProfileId { get; }

            public string Name { get; }

            public SnmpVersion SnmpVersion { get; }

            public byte[] CommunityUtf8 { get; }

            public AccessProfile UpdatedProfile { get; set; }

            public string FailureMessage { get; set; }
        }

        public sealed class DiscoveryProfileDeleteRequestedEventArgs :
            EventArgs
        {
            public DiscoveryProfileDeleteRequestedEventArgs(
                Guid profileId)
            {
                if (profileId == Guid.Empty)
                {
                    throw new ArgumentException(
                        "Access profile id is required.",
                        nameof(profileId));
                }

                ProfileId =
                    profileId;
            }

            public Guid ProfileId { get; }

            public bool Deleted { get; set; }

            public string FailureMessage { get; set; }
        }

        private sealed class DiscoverySnmpVersionOption
        {
            public DiscoverySnmpVersionOption(
                SnmpVersion version,
                string displayName)
            {
                Version =
                    version;
                DisplayName =
                    displayName;
            }

            public SnmpVersion Version { get; }

            public string DisplayName { get; }
        }

        private sealed class ProfileSettingsRow
        {
            public ProfileSettingsRow(
                AccessProfile profile,
                string displayName,
                string activeText)
            {
                Profile = profile ??
                    throw new ArgumentNullException(
                        nameof(profile));
                DisplayName = displayName ??
                    throw new ArgumentNullException(
                        nameof(displayName));
                ActiveText =
                    activeText ??
                    string.Empty;
                ActiveVisibility =
                    string.IsNullOrWhiteSpace(
                        ActiveText)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
            }

            public AccessProfile Profile { get; }

            public string DisplayName { get; }

            public string ActiveText { get; }

            public Visibility ActiveVisibility { get; }

            public string EditAction =>
                UiText.Get(
                    "DiscoveryProfileEditAction");

            public string DeleteAction =>
                UiText.Get(
                    "DiscoveryProfileDeleteAction");
        }

        private sealed class DiscoveryProfileOption
        {
            public DiscoveryProfileOption(
                AccessProfile profile,
                string displayName)
            {
                Profile = profile ??
                    throw new ArgumentNullException(
                        nameof(profile));
                DisplayName = displayName ??
                    throw new ArgumentNullException(
                        nameof(displayName));
            }

            public AccessProfile Profile { get; }

            public string DisplayName { get; }
        }

        private sealed class EmptyDiscoveryControl :
            IDiscoveryControl
        {
            private readonly DiscoveryControlSnapshot
                _current =
                    new DiscoveryControlSnapshot(
                        DiscoveryControlState.Idle,
                        null,
                        null,
                        0,
                        0,
                        0,
                        null,
                        null);

            public DiscoveryControlSnapshot Current =>
                _current;

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
                cancellationToken.ThrowIfCancellationRequested();

                throw new InvalidOperationException(
                    "DISCOVERY_CONTROL_UNAVAILABLE");
            }

            public Task StopAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class EmptyDiscoveryCandidateMaterializer :
            IDiscoveryCandidateMaterializer
        {
            public Guid Materialize(
                DiscoveryCandidateSnapshot candidate,
                DateTime observedUtc)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_MATERIALIZER_UNAVAILABLE");
            }
        }
    }
}
