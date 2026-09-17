using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NetLoom.Application.Topology;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf
{
    public partial class ManualTopologyWindow : Window
    {
        private readonly IManualTopologyService
            _service;

        private ManualTopologyEditorSnapshot
            _snapshot;

        private readonly List<CategoryOption>
            _categoryOptions =
                new List<CategoryOption>();

        private readonly List<MediaOption>
            _mediaOptions =
                new List<MediaOption>();

        private bool
            _refreshing;

        public ManualTopologyWindow(
            IManualTopologyService service)
        {
            _service = service ??
                throw new ArgumentNullException(
                    nameof(service));

            InitializeComponent();
            ApplyLocalizedText();
            BuildOptions();
            RefreshSnapshot();

            ManualTopologyStatusText.Text =
                UiText.Get(
                    "ManualTopologyStatusReady");
        }

        public bool HasChanges { get; private set; }

        private void ApplyLocalizedText()
        {
            Title =
                UiText.Get(
                    "ManualTopologyWindowTitle");

            ManualTopologyTitleText.Text =
                UiText.Get(
                    "ManualTopologyTitle");

            ManualTopologyIntroText.Text =
                UiText.Get(
                    "ManualTopologyIntro");

            ManualTopologyCloseButton.Content =
                UiText.Get(
                    "ManualTopologyClose");

            ManualDevicesTab.Header =
                UiText.Get(
                    "ManualTopologyDevicesTab");

            ManualPortsTab.Header =
                UiText.Get(
                    "ManualTopologyPortsTab");

            ManualLinksTab.Header =
                UiText.Get(
                    "ManualTopologyLinksTab");

            ManualDevicesListLabelText.Text =
                UiText.Get(
                    "ManualTopologyDevicesList");

            ManualDeviceNameLabelText.Text =
                UiText.Get(
                    "ManualTopologyDeviceName");

            ManualDeviceCategoryLabelText.Text =
                UiText.Get(
                    "ManualTopologyDeviceCategory");

            ManualDeviceNotesLabelText.Text =
                UiText.Get(
                    "ManualTopologyNotes");

            ManualPortDeviceLabelText.Text =
                UiText.Get(
                    "ManualTopologyPortDevice");

            ManualPortsListLabelText.Text =
                UiText.Get(
                    "ManualTopologyPortsList");

            ManualPortNameLabelText.Text =
                UiText.Get(
                    "ManualTopologyPortName");

            ManualPortMediaLabelText.Text =
                UiText.Get(
                    "ManualTopologyMediaType");

            ManualLinksListLabelText.Text =
                UiText.Get(
                    "ManualTopologyLinksList");

            ManualLinkEndpointATitleText.Text =
                UiText.Get(
                    "ManualTopologyEndpointA");

            ManualLinkEndpointBTitleText.Text =
                UiText.Get(
                    "ManualTopologyEndpointB");

            ManualLinkMediaLabelText.Text =
                UiText.Get(
                    "ManualTopologyMediaType");

            ManualLinkNotesLabelText.Text =
                UiText.Get(
                    "ManualTopologyNotes");

            foreach (var button in
                new[]
                {
                    ManualDeviceNewButton,
                    ManualPortNewButton,
                    ManualLinkNewButton
                })
            {
                button.Content =
                    UiText.Get(
                        "ManualTopologyNew");
            }

            foreach (var button in
                new[]
                {
                    ManualDeviceCreateButton,
                    ManualPortCreateButton,
                    ManualLinkCreateButton
                })
            {
                button.Content =
                    UiText.Get(
                        "ManualTopologyCreate");
            }

            foreach (var button in
                new[]
                {
                    ManualDeviceSaveButton,
                    ManualPortSaveButton,
                    ManualLinkSaveButton
                })
            {
                button.Content =
                    UiText.Get(
                        "ManualTopologySave");
            }

            foreach (var button in
                new[]
                {
                    ManualDeviceDeleteButton,
                    ManualPortDeleteButton,
                    ManualLinkDeleteButton
                })
            {
                button.Content =
                    UiText.Get(
                        "ManualTopologyDelete");
            }
        }

        private void BuildOptions()
        {
            _categoryOptions.Clear();

            _categoryOptions.Add(
                new CategoryOption(
                    ManualTopologyDeviceCategory.Unknown,
                    UiText.Get(
                        "CategoryUnknown")));

            _categoryOptions.Add(
                new CategoryOption(
                    ManualTopologyDeviceCategory.MediaConverter,
                    UiText.Get(
                        "CategoryMediaConverter")));

            _categoryOptions.Add(
                new CategoryOption(
                    ManualTopologyDeviceCategory.UnmanagedSwitch,
                    UiText.Get(
                        "CategoryUnmanagedSwitch")));

            _categoryOptions.Add(
                new CategoryOption(
                    ManualTopologyDeviceCategory.OpticalConverter,
                    UiText.Get(
                        "CategoryOpticalConverter")));

            _categoryOptions.Add(
                new CategoryOption(
                    ManualTopologyDeviceCategory.PassiveNetworkEquipment,
                    UiText.Get(
                        "CategoryPassiveNetworkEquipment")));

            ManualDeviceCategoryComboBox.ItemsSource =
                _categoryOptions;

            _mediaOptions.Clear();

            AddMediaOption(
                "Copper",
                "ManualTopologyMediaCopper");

            AddMediaOption(
                "Fiber",
                "ManualTopologyMediaFiber");

            AddMediaOption(
                "Wireless",
                "ManualTopologyMediaWireless");

            AddMediaOption(
                "Logical",
                "ManualTopologyMediaLogical");

            AddMediaOption(
                "Unknown",
                "ManualTopologyMediaUnknown");

            ManualPortMediaComboBox.ItemsSource =
                _mediaOptions;

            ManualLinkMediaComboBox.ItemsSource =
                _mediaOptions;
        }

        private void AddMediaOption(
            string value,
            string resourceKey)
        {
            _mediaOptions.Add(
                new MediaOption(
                    value,
                    UiText.Get(resourceKey)));
        }

        private void RefreshSnapshot()
        {
            _refreshing = true;

            try
            {
                _snapshot =
                    _service.GetSnapshot();

                var deviceRows =
                    _snapshot.Devices
                        .Select(
                            device =>
                                new DeviceRow(
                                    device,
                                    DeviceSummary(device)))
                        .OrderBy(
                            row => row.Summary,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();

                ManualDevicesList.ItemsSource =
                    deviceRows;

                ManualPortDeviceComboBox.ItemsSource =
                    deviceRows;

                ManualLinkDeviceAComboBox.ItemsSource =
                    deviceRows;

                ManualLinkDeviceBComboBox.ItemsSource =
                    deviceRows;

                var linkRows =
                    _snapshot.Links
                        .Select(
                            link =>
                                new LinkRow(
                                    link,
                                    LinkSummary(link)))
                        .OrderBy(
                            row => row.Summary,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();

                ManualLinksList.ItemsSource =
                    linkRows;

                RefreshPortList();
                RefreshEndpointPorts(
                    ManualLinkDeviceAComboBox,
                    ManualLinkPortAComboBox);

                RefreshEndpointPorts(
                    ManualLinkDeviceBComboBox,
                    ManualLinkPortBComboBox);
            }
            finally
            {
                _refreshing = false;
            }

            UpdateDeviceControls();
            UpdatePortControls();
            UpdateLinkControls();
        }

        private string DeviceSummary(
            ManualTopologyDeviceItem device)
        {
            return UiText.Format(
                device.IsManual
                    ? "ManualTopologyDeviceSummaryManual"
                    : "ManualTopologyDeviceSummaryAutomatic",
                device.DisplayName);
        }

        private string PortSummary(
            ManualTopologyPortItem port)
        {
            var media =
                MediaDisplay(
                    port.MediaType);

            return UiText.Format(
                port.IsManual
                    ? "ManualTopologyPortSummaryManual"
                    : "ManualTopologyPortSummaryAutomatic",
                port.DisplayName,
                media);
        }

        private string LinkSummary(
            ManualTopologyLinkItem link)
        {
            var left =
                EndpointSummary(
                    link.DeviceAId,
                    link.InterfaceAId);

            var right =
                EndpointSummary(
                    link.DeviceBId,
                    link.InterfaceBId);

            return UiText.Format(
                link.IsManual
                    ? "ManualTopologyLinkSummaryManual"
                    : "ManualTopologyLinkSummaryAutomatic",
                left,
                right,
                MediaDisplay(link.MediaType));
        }

        private string EndpointSummary(
            Guid deviceId,
            Guid? interfaceId)
        {
            var device =
                _snapshot.Devices
                    .FirstOrDefault(
                        item =>
                            item.DeviceId == deviceId);

            var deviceName =
                device == null
                    ? deviceId.ToString("D")
                    : device.DisplayName;

            if (!interfaceId.HasValue)
            {
                return deviceName;
            }

            var port =
                _snapshot.Ports
                    .FirstOrDefault(
                        item =>
                            item.InterfaceId ==
                            interfaceId.Value);

            return port == null
                ? deviceName
                : UiText.Format(
                    "ManualTopologyEndpointSummary",
                    deviceName,
                    port.DisplayName);
        }

        private string MediaDisplay(
            string value)
        {
            var normalized =
                Normalize(value);

            if (normalized == null)
            {
                return UiText.Get(
                    "ManualTopologyMediaUnknown");
            }

            foreach (var option in
                _mediaOptions)
            {
                if (string.Equals(
                    option.Value,
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return option.Text;
                }
            }

            return normalized;
        }

        private void OnDeviceSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing)
            {
                return;
            }

            var row =
                ManualDevicesList.SelectedItem
                    as DeviceRow;

            if (row == null)
            {
                UpdateDeviceControls();
                return;
            }

            ManualDeviceNameTextBox.Text =
                row.Item.DisplayName;

            SelectCategory(
                row.Item.Category);

            ManualDeviceNotesTextBox.Text =
                row.Item.Notes ?? string.Empty;

            UpdateDeviceControls();
        }

        private void OnNewDeviceClick(
            object sender,
            RoutedEventArgs e)
        {
            ManualDevicesList.SelectedItem =
                null;

            ManualDeviceNameTextBox.Text =
                string.Empty;

            ManualDeviceNotesTextBox.Text =
                string.Empty;

            ManualDeviceCategoryComboBox.SelectedIndex =
                0;

            ManualTopologyStatusText.Text =
                UiText.Get(
                    "ManualTopologyStatusNewDevice");

            UpdateDeviceControls();
        }

        private void OnCreateDeviceClick(
            object sender,
            RoutedEventArgs e)
        {
            var name =
                RequireText(
                    ManualDeviceNameTextBox.Text,
                    "ManualTopologyValidationDeviceName");

            if (name == null)
            {
                return;
            }

            var category =
                SelectedCategory();

            if (!category.HasValue)
            {
                ShowValidation(
                    "ManualTopologyValidationCategory");

                return;
            }

            Execute(
                () =>
                {
                    var id =
                        _service.CreateDevice(
                            name,
                            category.Value,
                            ManualDeviceNotesTextBox.Text);

                    RefreshSnapshot();
                    SelectDevice(
                        id);
                },
                "ManualTopologyStatusDeviceCreated");
        }

        private void OnSaveDeviceClick(
            object sender,
            RoutedEventArgs e)
        {
            var row =
                ManualDevicesList.SelectedItem
                    as DeviceRow;

            if (row == null ||
                !row.Item.CanEdit)
            {
                ShowValidation(
                    "ManualTopologyValidationManualDevice");

                return;
            }

            var name =
                RequireText(
                    ManualDeviceNameTextBox.Text,
                    "ManualTopologyValidationDeviceName");

            if (name == null)
            {
                return;
            }

            var category =
                SelectedCategory();

            if (!category.HasValue)
            {
                ShowValidation(
                    "ManualTopologyValidationCategory");

                return;
            }

            Execute(
                () =>
                {
                    _service.UpdateDevice(
                        row.Item.DeviceId,
                        name,
                        category.Value,
                        ManualDeviceNotesTextBox.Text);

                    var id =
                        row.Item.DeviceId;

                    RefreshSnapshot();
                    SelectDevice(id);
                },
                "ManualTopologyStatusDeviceSaved");
        }

        private void OnDeleteDeviceClick(
            object sender,
            RoutedEventArgs e)
        {
            var row =
                ManualDevicesList.SelectedItem
                    as DeviceRow;

            if (row == null ||
                !row.Item.CanEdit)
            {
                ShowValidation(
                    "ManualTopologyValidationManualDevice");

                return;
            }

            try
            {
                _service.DeleteDevice(
                    row.Item.DeviceId);

                HasChanges = true;
                RefreshSnapshot();
                OnNewDeviceClick(
                    this,
                    new RoutedEventArgs());

                ManualTopologyStatusText.Text =
                    UiText.Get(
                        "ManualTopologyStatusDeviceDeleted");
            }
            catch (InvalidOperationException exception)
            {
                Trace.TraceError(
                    "MANUAL_TOPOLOGY_DELETE_DEVICE_FAILED " +
                    exception);

                ManualTopologyStatusText.Text =
                    UiText.Get(
                        "ManualTopologyDeleteConnectedDevice");
            }
            catch (Exception exception)
            {
                ShowOperationFailure(
                    exception);
            }
        }

        private void OnPortDeviceSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing)
            {
                return;
            }

            RefreshPortList();
            OnNewPortClick(
                this,
                new RoutedEventArgs());
        }

        private void RefreshPortList()
        {
            var device =
                SelectedDevice(
                    ManualPortDeviceComboBox);

            if (device == null)
            {
                ManualPortsList.ItemsSource =
                    new PortRow[0];

                return;
            }

            ManualPortsList.ItemsSource =
                _snapshot.Ports
                    .Where(
                        port =>
                            port.DeviceId ==
                            device.Item.DeviceId)
                    .Select(
                        port =>
                            new PortRow(
                                port,
                                PortSummary(port)))
                    .OrderBy(
                        row => row.Summary,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
        }

        private void OnPortSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing)
            {
                return;
            }

            var row =
                ManualPortsList.SelectedItem
                    as PortRow;

            if (row != null)
            {
                ManualPortNameTextBox.Text =
                    row.Item.DisplayName;

                SelectMedia(
                    ManualPortMediaComboBox,
                    row.Item.MediaType);
            }

            UpdatePortControls();
        }

        private void OnNewPortClick(
            object sender,
            RoutedEventArgs e)
        {
            ManualPortsList.SelectedItem =
                null;

            ManualPortNameTextBox.Text =
                string.Empty;

            ManualPortMediaComboBox.SelectedItem =
                null;

            ManualPortMediaComboBox.Text =
                string.Empty;

            ManualTopologyStatusText.Text =
                UiText.Get(
                    "ManualTopologyStatusNewPort");

            UpdatePortControls();
        }

        private void OnCreatePortClick(
            object sender,
            RoutedEventArgs e)
        {
            var device =
                SelectedDevice(
                    ManualPortDeviceComboBox);

            if (device == null ||
                !device.Item.IsManual)
            {
                ShowValidation(
                    "ManualTopologyValidationPortManualDevice");

                return;
            }

            var name =
                RequireText(
                    ManualPortNameTextBox.Text,
                    "ManualTopologyValidationPortName");

            if (name == null)
            {
                return;
            }

            Execute(
                () =>
                {
                    var id =
                        _service.CreatePort(
                            device.Item.DeviceId,
                            name,
                            SelectedMedia(
                                ManualPortMediaComboBox));

                    RefreshSnapshot();
                    SelectPortDevice(
                        device.Item.DeviceId);
                    SelectPort(id);
                },
                "ManualTopologyStatusPortCreated");
        }

        private void OnSavePortClick(
            object sender,
            RoutedEventArgs e)
        {
            var row =
                ManualPortsList.SelectedItem
                    as PortRow;

            if (row == null ||
                !row.Item.CanEdit)
            {
                ShowValidation(
                    "ManualTopologyValidationManualPort");

                return;
            }

            var name =
                RequireText(
                    ManualPortNameTextBox.Text,
                    "ManualTopologyValidationPortName");

            if (name == null)
            {
                return;
            }

            Execute(
                () =>
                {
                    _service.UpdatePort(
                        row.Item.InterfaceId,
                        name,
                        SelectedMedia(
                            ManualPortMediaComboBox));

                    var id =
                        row.Item.InterfaceId;

                    var deviceId =
                        row.Item.DeviceId;

                    RefreshSnapshot();
                    SelectPortDevice(deviceId);
                    SelectPort(id);
                },
                "ManualTopologyStatusPortSaved");
        }

        private void OnDeletePortClick(
            object sender,
            RoutedEventArgs e)
        {
            var row =
                ManualPortsList.SelectedItem
                    as PortRow;

            if (row == null ||
                !row.Item.CanEdit)
            {
                ShowValidation(
                    "ManualTopologyValidationManualPort");

                return;
            }

            var deviceId =
                row.Item.DeviceId;

            try
            {
                _service.DeletePort(
                    row.Item.InterfaceId);

                HasChanges = true;
                RefreshSnapshot();
                SelectPortDevice(deviceId);
                OnNewPortClick(
                    this,
                    new RoutedEventArgs());

                ManualTopologyStatusText.Text =
                    UiText.Get(
                        "ManualTopologyStatusPortDeleted");
            }
            catch (InvalidOperationException exception)
            {
                Trace.TraceError(
                    "MANUAL_TOPOLOGY_DELETE_PORT_FAILED " +
                    exception);

                ManualTopologyStatusText.Text =
                    UiText.Get(
                        "ManualTopologyDeleteConnectedPort");
            }
            catch (Exception exception)
            {
                ShowOperationFailure(
                    exception);
            }
        }

        private void OnLinkSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing)
            {
                return;
            }

            var row =
                ManualLinksList.SelectedItem
                    as LinkRow;

            if (row == null)
            {
                UpdateLinkControls();
                return;
            }

            SelectLinkEndpoint(
                ManualLinkDeviceAComboBox,
                ManualLinkPortAComboBox,
                row.Item.DeviceAId,
                row.Item.InterfaceAId);

            SelectLinkEndpoint(
                ManualLinkDeviceBComboBox,
                ManualLinkPortBComboBox,
                row.Item.DeviceBId,
                row.Item.InterfaceBId);

            SelectMedia(
                ManualLinkMediaComboBox,
                row.Item.MediaType);

            ManualLinkNotesTextBox.Text =
                row.Item.Notes ?? string.Empty;

            UpdateLinkControls();
        }

        private void OnNewLinkClick(
            object sender,
            RoutedEventArgs e)
        {
            ManualLinksList.SelectedItem =
                null;

            ManualLinkDeviceAComboBox.SelectedItem =
                null;

            ManualLinkDeviceBComboBox.SelectedItem =
                null;

            ManualLinkPortAComboBox.ItemsSource =
                NoPortChoices();

            ManualLinkPortBComboBox.ItemsSource =
                NoPortChoices();

            ManualLinkPortAComboBox.SelectedIndex =
                0;

            ManualLinkPortBComboBox.SelectedIndex =
                0;

            ManualLinkMediaComboBox.SelectedItem =
                null;

            ManualLinkMediaComboBox.Text =
                string.Empty;

            ManualLinkNotesTextBox.Text =
                string.Empty;

            ManualTopologyStatusText.Text =
                UiText.Get(
                    "ManualTopologyStatusNewLink");

            UpdateLinkControls();
        }

        private void OnLinkDeviceASelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing)
            {
                return;
            }

            RefreshEndpointPorts(
                ManualLinkDeviceAComboBox,
                ManualLinkPortAComboBox);
        }

        private void OnLinkDeviceBSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing)
            {
                return;
            }

            RefreshEndpointPorts(
                ManualLinkDeviceBComboBox,
                ManualLinkPortBComboBox);
        }

        private void RefreshEndpointPorts(
            ComboBox deviceCombo,
            ComboBox portCombo)
        {
            var device =
                SelectedDevice(
                    deviceCombo);

            var rows =
                new List<PortChoice>(
                    NoPortChoices());

            if (device != null)
            {
                rows.AddRange(
                    _snapshot.Ports
                        .Where(
                            port =>
                                port.DeviceId ==
                                device.Item.DeviceId)
                        .Select(
                            port =>
                                new PortChoice(
                                    port.InterfaceId,
                                    PortSummary(port)))
                        .OrderBy(
                            row => row.Summary,
                            StringComparer.CurrentCultureIgnoreCase));
            }

            portCombo.ItemsSource =
                rows;

            portCombo.SelectedIndex =
                0;
        }

        private PortChoice[] NoPortChoices()
        {
            return new[]
            {
                new PortChoice(
                    null,
                    UiText.Get(
                        "ManualTopologyNoPort"))
            };
        }

        private void OnCreateLinkClick(
            object sender,
            RoutedEventArgs e)
        {
            var deviceA =
                SelectedDevice(
                    ManualLinkDeviceAComboBox);

            var deviceB =
                SelectedDevice(
                    ManualLinkDeviceBComboBox);

            if (deviceA == null ||
                deviceB == null)
            {
                ShowValidation(
                    "ManualTopologyValidationEndpoints");

                return;
            }

            if (deviceA.Item.DeviceId ==
                deviceB.Item.DeviceId)
            {
                ShowValidation(
                    "ManualTopologyValidationDifferentDevices");

                return;
            }

            try
            {
                _service.CreateLink(
                    deviceA.Item.DeviceId,
                    SelectedPort(
                        ManualLinkPortAComboBox),
                    deviceB.Item.DeviceId,
                    SelectedPort(
                        ManualLinkPortBComboBox),
                    SelectedMedia(
                        ManualLinkMediaComboBox),
                    ManualLinkNotesTextBox.Text);

                HasChanges = true;
                RefreshSnapshot();
                OnNewLinkClick(
                    this,
                    new RoutedEventArgs());

                ManualTopologyStatusText.Text =
                    UiText.Get(
                        "ManualTopologyStatusLinkCreated");
            }
            catch (InvalidOperationException exception)
            {
                Trace.TraceError(
                    "MANUAL_TOPOLOGY_CREATE_LINK_FAILED " +
                    exception);

                ManualTopologyStatusText.Text =
                    UiText.Get(
                        "ManualTopologyLinkConflict");
            }
            catch (Exception exception)
            {
                ShowOperationFailure(
                    exception);
            }
        }

        private void OnSaveLinkClick(
            object sender,
            RoutedEventArgs e)
        {
            var row =
                ManualLinksList.SelectedItem
                    as LinkRow;

            if (row == null ||
                !row.Item.CanEdit)
            {
                ShowValidation(
                    "ManualTopologyValidationManualLink");

                return;
            }

            Execute(
                () =>
                {
                    _service.UpdateLink(
                        row.Item.PhysicalLinkId,
                        SelectedMedia(
                            ManualLinkMediaComboBox),
                        ManualLinkNotesTextBox.Text);

                    var id =
                        row.Item.PhysicalLinkId;

                    RefreshSnapshot();
                    SelectLink(id);
                },
                "ManualTopologyStatusLinkSaved");
        }

        private void OnDeleteLinkClick(
            object sender,
            RoutedEventArgs e)
        {
            var row =
                ManualLinksList.SelectedItem
                    as LinkRow;

            if (row == null ||
                !row.Item.CanEdit)
            {
                ShowValidation(
                    "ManualTopologyValidationManualLink");

                return;
            }

            Execute(
                () =>
                {
                    _service.DeleteLink(
                        row.Item.PhysicalLinkId);

                    RefreshSnapshot();
                    OnNewLinkClick(
                        this,
                        new RoutedEventArgs());
                },
                "ManualTopologyStatusLinkDeleted");
        }

        private void UpdateDeviceControls()
        {
            var row =
                ManualDevicesList.SelectedItem
                    as DeviceRow;

            var editing =
                row != null;

            var canEdit =
                editing &&
                row.Item.CanEdit;

            ManualDeviceCreateButton.IsEnabled =
                !editing;

            ManualDeviceSaveButton.IsEnabled =
                canEdit;

            ManualDeviceDeleteButton.IsEnabled =
                canEdit;

            ManualDeviceNameTextBox.IsReadOnly =
                editing &&
                !canEdit;

            ManualDeviceCategoryComboBox.IsEnabled =
                !editing ||
                canEdit;

            ManualDeviceNotesTextBox.IsReadOnly =
                editing &&
                !canEdit;
        }

        private void UpdatePortControls()
        {
            var device =
                SelectedDevice(
                    ManualPortDeviceComboBox);

            var row =
                ManualPortsList.SelectedItem
                    as PortRow;

            var editing =
                row != null;

            var canCreate =
                !editing &&
                device != null &&
                device.Item.IsManual;

            var canEdit =
                editing &&
                row.Item.CanEdit;

            ManualPortCreateButton.IsEnabled =
                canCreate;

            ManualPortSaveButton.IsEnabled =
                canEdit;

            ManualPortDeleteButton.IsEnabled =
                canEdit;

            ManualPortNameTextBox.IsReadOnly =
                editing &&
                !canEdit;

            ManualPortMediaComboBox.IsEnabled =
                !editing
                    ? canCreate
                    : canEdit;
        }

        private void UpdateLinkControls()
        {
            var row =
                ManualLinksList.SelectedItem
                    as LinkRow;

            var editing =
                row != null;

            var canEdit =
                editing &&
                row.Item.CanEdit;

            ManualLinkDeviceAComboBox.IsEnabled =
                !editing;

            ManualLinkPortAComboBox.IsEnabled =
                !editing;

            ManualLinkDeviceBComboBox.IsEnabled =
                !editing;

            ManualLinkPortBComboBox.IsEnabled =
                !editing;

            ManualLinkCreateButton.IsEnabled =
                !editing;

            ManualLinkSaveButton.IsEnabled =
                canEdit;

            ManualLinkDeleteButton.IsEnabled =
                canEdit;

            ManualLinkMediaComboBox.IsEnabled =
                !editing ||
                canEdit;

            ManualLinkNotesTextBox.IsReadOnly =
                editing &&
                !canEdit;
        }

        private void Execute(
            Action action,
            string successKey)
        {
            try
            {
                action();
                HasChanges = true;

                ManualTopologyStatusText.Text =
                    UiText.Get(
                        successKey);
            }
            catch (Exception exception)
            {
                ShowOperationFailure(
                    exception);
            }
        }

        private void ShowOperationFailure(
            Exception exception)
        {
            Trace.TraceError(
                "MANUAL_TOPOLOGY_UI_OPERATION_FAILED " +
                exception);

            ManualTopologyStatusText.Text =
                UiText.Get(
                    "ManualTopologyOperationFailed");
        }

        private void ShowValidation(
            string key)
        {
            ManualTopologyStatusText.Text =
                UiText.Get(
                    key);
        }

        private string RequireText(
            string value,
            string validationKey)
        {
            var normalized =
                Normalize(value);

            if (normalized == null)
            {
                ShowValidation(
                    validationKey);
            }

            return normalized;
        }

        private ManualTopologyDeviceCategory?
            SelectedCategory()
        {
            var option =
                ManualDeviceCategoryComboBox.SelectedItem
                    as CategoryOption;

            return option == null
                ? (ManualTopologyDeviceCategory?)null
                : option.Value;
        }

        private void SelectCategory(
            ManualTopologyDeviceCategory value)
        {
            ManualDeviceCategoryComboBox.SelectedItem =
                _categoryOptions
                    .FirstOrDefault(
                        option =>
                            option.Value == value);
        }

        private string SelectedMedia(
            ComboBox combo)
        {
            var selected =
                combo.SelectedItem
                    as MediaOption;

            return selected == null
                ? Normalize(combo.Text)
                : selected.Value;
        }

        private void SelectMedia(
            ComboBox combo,
            string value)
        {
            var normalized =
                Normalize(value);

            var option =
                _mediaOptions
                    .FirstOrDefault(
                        item =>
                            string.Equals(
                                item.Value,
                                normalized,
                                StringComparison.OrdinalIgnoreCase));

            combo.SelectedItem =
                option;

            if (option == null)
            {
                combo.Text =
                    normalized ?? string.Empty;
            }
        }

        private DeviceRow SelectedDevice(
            ComboBox combo)
        {
            return combo.SelectedItem
                as DeviceRow;
        }

        private Guid? SelectedPort(
            ComboBox combo)
        {
            var item =
                combo.SelectedItem
                    as PortChoice;

            return item == null
                ? (Guid?)null
                : item.InterfaceId;
        }

        private void SelectDevice(
            Guid deviceId)
        {
            foreach (var item in
                ManualDevicesList.Items)
            {
                var row =
                    item as DeviceRow;

                if (row != null &&
                    row.Item.DeviceId ==
                    deviceId)
                {
                    ManualDevicesList.SelectedItem =
                        row;

                    return;
                }
            }
        }

        private void SelectPortDevice(
            Guid deviceId)
        {
            SelectDeviceRow(
                ManualPortDeviceComboBox,
                deviceId);

            RefreshPortList();
        }

        private void SelectPort(
            Guid interfaceId)
        {
            foreach (var item in
                ManualPortsList.Items)
            {
                var row =
                    item as PortRow;

                if (row != null &&
                    row.Item.InterfaceId ==
                    interfaceId)
                {
                    ManualPortsList.SelectedItem =
                        row;

                    return;
                }
            }
        }

        private void SelectLink(
            Guid physicalLinkId)
        {
            foreach (var item in
                ManualLinksList.Items)
            {
                var row =
                    item as LinkRow;

                if (row != null &&
                    row.Item.PhysicalLinkId ==
                    physicalLinkId)
                {
                    ManualLinksList.SelectedItem =
                        row;

                    return;
                }
            }
        }

        private void SelectLinkEndpoint(
            ComboBox deviceCombo,
            ComboBox portCombo,
            Guid deviceId,
            Guid? interfaceId)
        {
            SelectDeviceRow(
                deviceCombo,
                deviceId);

            RefreshEndpointPorts(
                deviceCombo,
                portCombo);

            foreach (var item in
                portCombo.Items)
            {
                var choice =
                    item as PortChoice;

                if (choice != null &&
                    choice.InterfaceId ==
                    interfaceId)
                {
                    portCombo.SelectedItem =
                        choice;

                    break;
                }
            }
        }

        private static void SelectDeviceRow(
            ComboBox combo,
            Guid deviceId)
        {
            foreach (var item in
                combo.Items)
            {
                var row =
                    item as DeviceRow;

                if (row != null &&
                    row.Item.DeviceId ==
                    deviceId)
                {
                    combo.SelectedItem =
                        row;

                    return;
                }
            }
        }

        private static string Normalize(
            string value)
        {
            return string.IsNullOrWhiteSpace(
                value)
                ? null
                : value.Trim();
        }

        private void OnCloseClick(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private sealed class CategoryOption
        {
            public CategoryOption(
                ManualTopologyDeviceCategory value,
                string text)
            {
                Value = value;
                Text = text;
            }

            public ManualTopologyDeviceCategory
                Value { get; }

            public string Text { get; }
        }

        private sealed class MediaOption
        {
            public MediaOption(
                string value,
                string text)
            {
                Value = value;
                Text = text;
            }

            public string Value { get; }

            public string Text { get; }
        }

        private sealed class DeviceRow
        {
            public DeviceRow(
                ManualTopologyDeviceItem item,
                string summary)
            {
                Item = item;
                Summary = summary;
            }

            public ManualTopologyDeviceItem
                Item { get; }

            public string Summary { get; }
        }

        private sealed class PortRow
        {
            public PortRow(
                ManualTopologyPortItem item,
                string summary)
            {
                Item = item;
                Summary = summary;
            }

            public ManualTopologyPortItem
                Item { get; }

            public string Summary { get; }
        }

        private sealed class LinkRow
        {
            public LinkRow(
                ManualTopologyLinkItem item,
                string summary)
            {
                Item = item;
                Summary = summary;
            }

            public ManualTopologyLinkItem
                Item { get; }

            public string Summary { get; }
        }

        private sealed class PortChoice
        {
            public PortChoice(
                Guid? interfaceId,
                string summary)
            {
                InterfaceId = interfaceId;
                Summary = summary;
            }

            public Guid? InterfaceId { get; }

            public string Summary { get; }
        }
    }
}
