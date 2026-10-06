using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NetLoom.Application.Discovery;
using NetLoom.Application.DiscoveryControl;
using NetLoom.Application.Topology;
using NetLoom.Domain.Access;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Shell;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private const int DiscoveryMaxAddresses = 4096;

        private readonly IDiscoveryControl _discoveryControl;
        private IReadOnlyList<AccessProfile> _discoveryProfiles;
        private Guid? _profileSettingsSelectedId;

        public event EventHandler<DiscoveryProfileCreateRequestedEventArgs>
            DiscoveryProfileCreateRequested;
        public event EventHandler<DiscoveryProfileUpdateRequestedEventArgs>
            DiscoveryProfileUpdateRequested;
        public event EventHandler<DiscoveryProfileDeleteRequestedEventArgs>
            DiscoveryProfileDeleteRequested;
        private readonly IDiscoveryCandidateMaterializer
            _discoveryCandidateMaterializer;

        private readonly List<DiscoveryCandidateRow>
            _discoveryCandidateRows =
                new List<DiscoveryCandidateRow>();

        private bool _discoveryClosed;

        private void InitializeDiscoveryPanel()
        {
            DiscoveryTitleText.Text =
                UiText.Get("DiscoveryTitle");
            DiscoveryStateLabelText.Text =
                UiText.Get("DiscoveryStateLabel");
            DiscoveryLastRunTitleText.Text =
                UiText.Get("DiscoveryLastRunTitle");
            DiscoveryProgressLabelText.Text =
                UiText.Get("DiscoveryProgressLabel");
            DiscoveryCurrentAddressLabelText.Text =
                UiText.Get("DiscoveryCurrentAddressLabel");
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
                UiText.Get("DiscoveryCandidatesLabel");

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

            DiscoveryCandidatesList.ItemsSource =
                new DiscoveryCandidateRow[0];

            _discoveryControl.SnapshotChanged +=
                OnDiscoverySnapshotChanged;
            _discoveryControl.CandidateDiscovered +=
                OnDiscoveryCandidateDiscovered;

            DiscoveryMessageText.Text =
                string.Empty;

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
                    430);

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

            _discoveryCandidateRows.Clear();
            RefreshDiscoveryCandidateRows();
            DiscoveryMessageText.Text =
                string.Empty;

            try
            {
                await _discoveryControl
                    .StartAsync(
                        request,
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

            var existingIndex =
                _discoveryCandidateRows.FindIndex(
                    row =>
                        string.Equals(
                            row.Address,
                            address,
                            StringComparison.OrdinalIgnoreCase));

            var row =
                new DiscoveryCandidateRow(
                    address,
                    UiText.Format(
                        "DiscoveryCandidateSummary",
                        address,
                        string.IsNullOrWhiteSpace(
                            candidate.SysName)
                            ? UiText.Get(
                                "DiscoveryUnnamedCandidate")
                            : candidate.SysName,
                        UiText.Get(
                            candidate.SnmpResponded
                                ? "DiscoverySnmpResponded"
                                : "DiscoverySnmpUnavailable")));

            if (existingIndex >= 0)
            {
                _discoveryCandidateRows[existingIndex] =
                    row;
            }
            else
            {
                _discoveryCandidateRows.Add(
                    row);
            }

            RefreshDiscoveryCandidateRows();

            try
            {
                _discoveryCandidateMaterializer
                    .Materialize(
                        candidate,
                        DateTime.UtcNow);

                await RefreshTopologyAsync();

                FocusDiscoveredDevice(
                    address);
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

            var discoveryStatus =
                DiscoveryStatusSemantic(
                    snapshot.State);

            ApplyOperatorStatus(
                DiscoveryStateGlyphText,
                DiscoveryStateValueText,
                discoveryStatus,
                UiText.Get(
                    DiscoveryStateResourceKey(
                        snapshot.State)));

            DiscoveryProgressValueText.Text =
                snapshot.TotalAddresses > 0
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

            var canStart =
                snapshot.State ==
                    DiscoveryControlState.Idle ||
                snapshot.State ==
                    DiscoveryControlState.Completed ||
                snapshot.State ==
                    DiscoveryControlState.Stopped ||
                snapshot.State ==
                    DiscoveryControlState.Faulted;

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
        }

        private void RefreshDiscoveryCandidateRows()
        {
            DiscoveryCandidatesList.ItemsSource =
                _discoveryCandidateRows
                    .OrderBy(
                        row =>
                            row.Address,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            UpdateDiscoveryResultsSurface(
                _discoveryControl.Current);
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
                _discoveryCandidateRows.Count == 0 &&
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

        private sealed class DiscoveryCandidateRow
        {
            public DiscoveryCandidateRow(
                string address,
                string summary)
            {
                Address = address;
                Summary = summary;
            }

            public string Address { get; }

            public string Summary { get; }
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
