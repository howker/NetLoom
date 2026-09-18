using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NetLoom.Application.Locations;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf
{
    public partial class LocationTopologyWindow : Window
    {
        private readonly ILocationTopologyService
            _service;

        private LocationTopologySnapshot
            _snapshot;

        private Guid?
            _editingLocationId;

        private Guid?
            _browseLocationId;

        private LocationEditorMode
            _editorMode;

        private bool
            _refreshing;

        private bool
            _settingEditorFields;

        private string
            _editBaselineName;

        private string
            _editBaselineDescription;

        private Guid?
            _editBaselineParentId;

        public LocationTopologyWindow(
            ILocationTopologyService service)
            : this(
                service,
                null)
        {
        }

        public LocationTopologyWindow(
            ILocationTopologyService service,
            Guid? initialLocationId)
        {
            _service =
                service ??
                throw new ArgumentNullException(
                    nameof(service));

            InitializeComponent();
            ApplyLocalizedText();
            RefreshSnapshot();

            LocationTopologyStatusText.Text =
                UiText.Get(
                    "LocationTopologyStatusReady");

            if (initialLocationId.HasValue)
            {
                SelectLocation(
                    initialLocationId.Value);
            }
            else
            {
                BeginCreateLocation();
            }
        }

        public bool HasChanges { get; private set; }

        private void ApplyLocalizedText()
        {
            Title =
                UiText.Get(
                    "LocationTopologyWindowTitle");

            LocationTopologyTitleText.Text =
                UiText.Get(
                    "LocationTopologyTitle");

            LocationTopologyIntroText.Text =
                UiText.Get(
                    "LocationTopologyIntro");

            LocationTopologyInteractionHintText.Text =
                UiText.Get(
                    "LocationTopologyInteractionHint");

            LocationTopologyCloseButton.Content =
                UiText.Get(
                    "LocationTopologyClose");

            LocationsTab.Header =
                UiText.Get(
                    "LocationTopologyLocationsTab");

            DevicesTab.Header =
                UiText.Get(
                    "LocationTopologyDevicesTab");

            LocationsListLabelText.Text =
                UiText.Get(
                    "LocationTopologyLocationsList");

            LocationNameLabelText.Text =
                UiText.Get(
                    "LocationTopologyName");

            LocationParentLabelText.Text =
                UiText.Get(
                    "LocationTopologyParent");

            LocationDescriptionLabelText.Text =
                UiText.Get(
                    "LocationTopologyDescription");

            LocationNewButton.Content =
                UiText.Get(
                    "LocationTopologyNew");

            LocationEditButton.Content =
                UiText.Get(
                    "LocationTopologyEdit");

            LocationCreateButton.Content =
                UiText.Get(
                    "LocationTopologyCreate");

            LocationSaveButton.Content =
                UiText.Get(
                    "LocationTopologySave");

            LocationCancelButton.Content =
                UiText.Get(
                    "LocationTopologyCancel");

            LocationDeleteButton.Content =
                UiText.Get(
                    "LocationTopologyDelete");

            LocationContextEdit.Header =
                UiText.Get(
                    "LocationTopologyEdit");

            LocationContextDelete.Header =
                UiText.Get(
                    "LocationTopologyDelete");

            LocationDevicesListLabelText.Text =
                UiText.Get(
                    "LocationTopologyDevicesList");

            LocationDeviceTitleText.Text =
                UiText.Get(
                    "LocationTopologySelectedDevice");

            LocationDeviceLocationLabelText.Text =
                UiText.Get(
                    "LocationTopologyDeviceLocation");

            LocationDeviceAssignButton.Content =
                UiText.Get(
                    "LocationTopologyAssign");
        }

        private void RefreshSnapshot()
        {
            Guid? selectedLocationId =
                SelectedLocationId();

            Guid? selectedDeviceId =
                SelectedDeviceId();

            _refreshing = true;

            try
            {
                _snapshot =
                    _service.GetSnapshot();

                var locationRows =
                    _snapshot.Locations
                        .Select(
                            item =>
                                new LocationRow(
                                    item,
                                    BuildLocationPath(
                                        item.Id)))
                        .OrderBy(
                            row => row.Summary,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();

                LocationsList.ItemsSource =
                    locationRows;

                var deviceRows =
                    _snapshot.Devices
                        .Select(
                            item =>
                                new DeviceRow(
                                    item,
                                    UiText.Format(
                                        "LocationTopologyDeviceSummary",
                                        item.DisplayName,
                                        item.LocationId.HasValue
                                            ? BuildLocationPath(
                                                item.LocationId.Value)
                                            : UiText.Get(
                                                "LocationTopologyUnassigned"))))
                        .OrderBy(
                            row => row.Summary,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();

                LocationDevicesList.ItemsSource =
                    deviceRows;

                if (selectedLocationId.HasValue)
                {
                    SelectLocation(
                        selectedLocationId.Value);
                }

                if (selectedDeviceId.HasValue)
                {
                    SelectDevice(
                        selectedDeviceId.Value);
                }

                RefreshDeviceLocationOptions();
                RefreshSelectedDeviceEditor();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void BuildParentOptions(
            Guid? excludedLocationId)
        {
            var options =
                new List<LocationOption>
                {
                    new LocationOption(
                        null,
                        UiText.Get(
                            "LocationTopologyNoParent"))
                };

            foreach (var item in
                _snapshot.Locations
                    .OrderBy(
                        candidate =>
                            BuildLocationPath(
                                candidate.Id),
                        StringComparer.CurrentCultureIgnoreCase))
            {
                if (excludedLocationId.HasValue &&
                    (item.Id ==
                        excludedLocationId.Value ||
                     IsDescendant(
                        item.Id,
                        excludedLocationId.Value)))
                {
                    continue;
                }

                options.Add(
                    new LocationOption(
                        item.Id,
                        BuildLocationPath(
                            item.Id)));
            }

            LocationParentComboBox.ItemsSource =
                options;
        }

        private void RefreshDeviceLocationOptions()
        {
            var options =
                new List<LocationOption>
                {
                    new LocationOption(
                        null,
                        UiText.Get(
                            "LocationTopologyUnassigned"))
                };

            options.AddRange(
                _snapshot.Locations
                    .OrderBy(
                        item =>
                            BuildLocationPath(
                                item.Id),
                        StringComparer.CurrentCultureIgnoreCase)
                    .Select(
                        item =>
                            new LocationOption(
                                item.Id,
                                BuildLocationPath(
                                    item.Id))));

            LocationDeviceLocationComboBox.ItemsSource =
                options;

            var selected =
                SelectedDeviceRow();

            SelectLocationOption(
                LocationDeviceLocationComboBox,
                selected == null
                    ? (Guid?)null
                    : selected.Item.LocationId);
        }

        private bool IsDescendant(
            Guid locationId,
            Guid possibleAncestorId)
        {
            var byId =
                _snapshot.Locations
                    .ToDictionary(
                        item => item.Id);

            var currentId =
                (Guid?)locationId;

            var visited =
                new HashSet<Guid>();

            while (currentId.HasValue &&
                   visited.Add(
                       currentId.Value))
            {
                if (currentId.Value ==
                    possibleAncestorId)
                {
                    return true;
                }

                LocationTopologyLocation current;

                if (!byId.TryGetValue(
                        currentId.Value,
                        out current))
                {
                    break;
                }

                currentId =
                    current.ParentLocationId;
            }

            return false;
        }

        private string BuildLocationPath(
            Guid locationId)
        {
            var byId =
                _snapshot.Locations
                    .ToDictionary(
                        item => item.Id);

            var names =
                new List<string>();

            var currentId =
                (Guid?)locationId;

            var visited =
                new HashSet<Guid>();

            while (currentId.HasValue &&
                   visited.Add(
                       currentId.Value))
            {
                LocationTopologyLocation current;

                if (!byId.TryGetValue(
                        currentId.Value,
                        out current))
                {
                    break;
                }

                names.Add(
                    current.Name);

                currentId =
                    current.ParentLocationId;
            }

            names.Reverse();

            return string.Join(
                " / ",
                names);
        }

        private void BeginEmptyBrowse()
        {
            _editorMode =
                LocationEditorMode.Browse;

            _editingLocationId = null;
            _browseLocationId = null;

            _settingEditorFields = true;

            try
            {
                LocationsList.SelectedItem = null;

                LocationNameTextBox.Text =
                    string.Empty;

                LocationDescriptionTextBox.Text =
                    string.Empty;

                BuildParentOptions(
                    null);

                SelectLocationOption(
                    LocationParentComboBox,
                    null);
            }
            finally
            {
                _settingEditorFields = false;
            }

            CaptureEditorBaseline();
            ApplyEditorMode();
        }

        private void BeginBrowse(
            LocationRow row)
        {
            if (row == null)
            {
                BeginEmptyBrowse();
                return;
            }

            _editorMode =
                LocationEditorMode.Browse;

            _editingLocationId = null;
            _browseLocationId =
                row.Item.Id;

            _settingEditorFields = true;

            try
            {
                BuildParentOptions(
                    row.Item.Id);

                LocationNameTextBox.Text =
                    row.Item.Name;

                LocationDescriptionTextBox.Text =
                    row.Item.Description ??
                    string.Empty;

                SelectLocationOption(
                    LocationParentComboBox,
                    row.Item.ParentLocationId);
            }
            finally
            {
                _settingEditorFields = false;
            }

            CaptureEditorBaseline();
            ApplyEditorMode();
        }

        private void BeginCreateLocation()
        {
            _editorMode =
                LocationEditorMode.Create;

            _editingLocationId = null;

            _settingEditorFields = true;

            try
            {
                LocationNameTextBox.Text =
                    string.Empty;

                LocationDescriptionTextBox.Text =
                    string.Empty;

                BuildParentOptions(
                    null);

                SelectLocationOption(
                    LocationParentComboBox,
                    _browseLocationId);
            }
            finally
            {
                _settingEditorFields = false;
            }

            CaptureEditorBaseline();
            ApplyEditorMode();

            LocationNameTextBox.Focus();
        }

        private void BeginLocationEdit(
            LocationRow row)
        {
            if (row == null)
            {
                return;
            }

            _editorMode =
                LocationEditorMode.Edit;

            _editingLocationId =
                row.Item.Id;

            _browseLocationId =
                row.Item.Id;

            _settingEditorFields = true;

            try
            {
                BuildParentOptions(
                    row.Item.Id);

                LocationNameTextBox.Text =
                    row.Item.Name;

                LocationDescriptionTextBox.Text =
                    row.Item.Description ??
                    string.Empty;

                SelectLocationOption(
                    LocationParentComboBox,
                    row.Item.ParentLocationId);
            }
            finally
            {
                _settingEditorFields = false;
            }

            CaptureEditorBaseline();
            ApplyEditorMode();
        }

        private void CaptureEditorBaseline()
        {
            _editBaselineName =
                LocationNameTextBox.Text ??
                string.Empty;

            _editBaselineDescription =
                LocationDescriptionTextBox.Text ??
                string.Empty;

            _editBaselineParentId =
                SelectedLocationOption(
                    LocationParentComboBox);
        }

        private bool IsEditorDirty()
        {
            return !string.Equals(
                       _editBaselineName,
                       LocationNameTextBox.Text ??
                           string.Empty,
                       StringComparison.Ordinal) ||
                   !string.Equals(
                       _editBaselineDescription,
                       LocationDescriptionTextBox.Text ??
                           string.Empty,
                       StringComparison.Ordinal) ||
                   _editBaselineParentId !=
                       SelectedLocationOption(
                           LocationParentComboBox);
        }

        private void ApplyEditorMode()
        {
            var browse =
                _editorMode ==
                LocationEditorMode.Browse;

            var creating =
                _editorMode ==
                LocationEditorMode.Create;

            var editing =
                _editorMode ==
                LocationEditorMode.Edit;

            var hasSelection =
                _browseLocationId.HasValue;

            LocationsList.IsEnabled =
                browse;

            DevicesTab.IsEnabled =
                browse;

            LocationNameTextBox.IsReadOnly =
                browse;

            LocationDescriptionTextBox.IsReadOnly =
                browse;

            LocationParentComboBox.IsEnabled =
                !browse;

            LocationNewButton.IsEnabled =
                browse;

            LocationEditButton.IsEnabled =
                hasSelection &&
                (browse ||
                 (creating &&
                  !IsEditorDirty()));

            LocationCreateButton.IsEnabled =
                creating;

            LocationSaveButton.IsEnabled =
                editing &&
                IsEditorDirty();

            LocationCancelButton.IsEnabled =
                creating ||
                editing;

            LocationDeleteButton.IsEnabled =
                browse &&
                hasSelection;
        }

        private void OnLocationEditorValueChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (_settingEditorFields ||
                LocationSaveButton == null ||
                LocationCancelButton == null)
            {
                return;
            }

            ApplyEditorMode();
        }

        private void OnLocationSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing ||
                _settingEditorFields ||
                _editorMode !=
                    LocationEditorMode.Browse)
            {
                return;
            }

            BeginBrowse(
                LocationsList.SelectedItem
                    as LocationRow);
        }

        private void OnLocationListPreviewMouseRightButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_editorMode !=
                LocationEditorMode.Browse)
            {
                e.Handled = true;
                return;
            }

            var item =
                ItemsControl.ContainerFromElement(
                    LocationsList,
                    e.OriginalSource
                        as DependencyObject)
                as ListBoxItem;

            if (item != null)
            {
                item.IsSelected = true;
                item.Focus();
            }
        }

        private void OnLocationListDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_editorMode !=
                LocationEditorMode.Browse)
            {
                return;
            }

            var row =
                LocationsList.SelectedItem
                    as LocationRow;

            if (row == null)
            {
                return;
            }

            BeginLocationEdit(
                row);

            LocationNameTextBox.Focus();
            LocationNameTextBox.SelectAll();
        }

        private void OnLocationListKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (_editorMode !=
                    LocationEditorMode.Browse ||
                e.Key != Key.Delete)
            {
                return;
            }

            e.Handled = true;
            DeleteSelectedLocation();
        }

        private void OnLocationContextEditClick(
            object sender,
            RoutedEventArgs e)
        {
            OnLocationListDoubleClick(
                LocationsList,
                null);
        }

        private void OnLocationContextDeleteClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_editorMode ==
                LocationEditorMode.Browse)
            {
                DeleteSelectedLocation();
            }
        }

        private void OnNewLocationClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_editorMode ==
                LocationEditorMode.Browse)
            {
                BeginCreateLocation();
            }
        }

        private void OnEditLocationClick(
            object sender,
            RoutedEventArgs e)
        {
            var browse =
                _editorMode ==
                LocationEditorMode.Browse;

            var pristineCreate =
                _editorMode ==
                    LocationEditorMode.Create &&
                !IsEditorDirty();

            if (!browse &&
                !pristineCreate)
            {
                return;
            }

            BeginLocationEdit(
                LocationsList.SelectedItem
                    as LocationRow);
        }

        private void OnCancelLocationClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_browseLocationId.HasValue)
            {
                SelectLocation(
                    _browseLocationId.Value);
            }
            else
            {
                BeginEmptyBrowse();
            }
        }

        private void OnCreateLocationClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_editorMode !=
                LocationEditorMode.Create)
            {
                return;
            }

            var name =
                LocationNameTextBox.Text == null
                    ? string.Empty
                    : LocationNameTextBox.Text.Trim();

            if (name.Length == 0)
            {
                ShowValidationMessage();
                return;
            }

            try
            {
                var locationId =
                    _service.CreateLocation(
                        SelectedLocationOption(
                            LocationParentComboBox),
                        name,
                        LocationDescriptionTextBox.Text);

                HasChanges = true;

                RefreshSnapshot();
                SelectLocation(
                    locationId);
                BeginCreateLocation();

                LocationTopologyStatusText.Text =
                    UiText.Get(
                        "LocationTopologyCreated");
            }
            catch (Exception error)
            {
                ShowOperationFailure(
                    error);
            }
        }

        private void OnSaveLocationClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_editorMode !=
                    LocationEditorMode.Edit ||
                !_editingLocationId.HasValue ||
                !IsEditorDirty())
            {
                return;
            }

            var name =
                LocationNameTextBox.Text == null
                    ? string.Empty
                    : LocationNameTextBox.Text.Trim();

            if (name.Length == 0)
            {
                ShowValidationMessage();
                return;
            }

            try
            {
                var id =
                    _editingLocationId.Value;

                _service.UpdateLocation(
                    id,
                    SelectedLocationOption(
                        LocationParentComboBox),
                    name,
                    LocationDescriptionTextBox.Text);

                HasChanges = true;

                RefreshSnapshot();
                SelectLocation(
                    id);

                LocationTopologyStatusText.Text =
                    UiText.Get(
                        "LocationTopologySaved");
            }
            catch (Exception error)
            {
                ShowOperationFailure(
                    error);
            }
        }

        private void OnDeleteLocationClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_editorMode ==
                LocationEditorMode.Browse)
            {
                DeleteSelectedLocation();
            }
        }

        private void DeleteSelectedLocation()
        {
            var row =
                LocationsList.SelectedItem
                    as LocationRow;

            if (row == null)
            {
                return;
            }

            var answer =
                MessageBox.Show(
                    this,
                    UiText.Format(
                        "LocationTopologyDeleteConfirm",
                        row.Item.Name),
                    UiText.Get(
                        "LocationTopologyWindowTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

            if (answer !=
                MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _service.DeleteLocation(
                    row.Item.Id);

                HasChanges = true;
                _browseLocationId = null;

                RefreshSnapshot();
                BeginEmptyBrowse();

                LocationTopologyStatusText.Text =
                    UiText.Get(
                        "LocationTopologyDeleted");
            }
            catch (Exception error)
            {
                Trace.TraceError(
                    "LOCATION_TOPOLOGY_DELETE_FAILED " +
                    error);

                MessageBox.Show(
                    this,
                    UiText.Get(
                        "LocationTopologyDeleteBlocked"),
                    UiText.Get(
                        "LocationTopologyWindowTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void OnDeviceSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_refreshing)
            {
                return;
            }

            RefreshSelectedDeviceEditor();
        }

        private void RefreshSelectedDeviceEditor()
        {
            var row =
                SelectedDeviceRow();

            LocationDeviceNameText.Text =
                row == null
                    ? UiText.Get(
                        "LocationTopologyNoDeviceSelected")
                    : row.Item.DisplayName;

            SelectLocationOption(
                LocationDeviceLocationComboBox,
                row == null
                    ? (Guid?)null
                    : row.Item.LocationId);

            LocationDeviceAssignButton.IsEnabled =
                row != null;
        }

        private void OnAssignDeviceClick(
            object sender,
            RoutedEventArgs e)
        {
            var row =
                SelectedDeviceRow();

            if (row == null)
            {
                return;
            }

            try
            {
                var deviceId =
                    row.Item.DeviceId;

                _service.AssignDevice(
                    deviceId,
                    SelectedLocationOption(
                        LocationDeviceLocationComboBox));

                HasChanges = true;

                RefreshSnapshot();
                SelectDevice(
                    deviceId);

                LocationTopologyStatusText.Text =
                    UiText.Get(
                        "LocationTopologyDeviceAssigned");
            }
            catch (Exception error)
            {
                ShowOperationFailure(
                    error);
            }
        }

        private void ShowValidationMessage()
        {
            MessageBox.Show(
                this,
                UiText.Get(
                    "LocationTopologyValidationName"),
                UiText.Get(
                    "LocationTopologyWindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowOperationFailure(
            Exception error)
        {
            Trace.TraceError(
                "LOCATION_TOPOLOGY_OPERATION_FAILED " +
                error);

            MessageBox.Show(
                this,
                UiText.Get(
                    "LocationTopologyOperationFailed"),
                UiText.Get(
                    "LocationTopologyWindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void OnCloseClick(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private Guid? SelectedLocationId()
        {
            var row =
                LocationsList.SelectedItem
                    as LocationRow;

            return row == null
                ? (Guid?)null
                : row.Item.Id;
        }

        private Guid? SelectedDeviceId()
        {
            var row =
                SelectedDeviceRow();

            return row == null
                ? (Guid?)null
                : row.Item.DeviceId;
        }

        private DeviceRow SelectedDeviceRow()
        {
            return LocationDevicesList.SelectedItem
                as DeviceRow;
        }

        private void SelectLocation(
            Guid locationId)
        {
            var rows =
                LocationsList.ItemsSource
                    as IEnumerable<LocationRow>;

            var row =
                rows == null
                    ? null
                    : rows.FirstOrDefault(
                        item =>
                            item.Item.Id ==
                            locationId);

            LocationsList.SelectedItem =
                row;

            if (row != null)
            {
                LocationsList.ScrollIntoView(
                    row);

                BeginBrowse(
                    row);
            }
        }

        private void SelectDevice(
            Guid deviceId)
        {
            var rows =
                LocationDevicesList.ItemsSource
                    as IEnumerable<DeviceRow>;

            var row =
                rows == null
                    ? null
                    : rows.FirstOrDefault(
                        item =>
                            item.Item.DeviceId ==
                            deviceId);

            LocationDevicesList.SelectedItem =
                row;

            if (row != null)
            {
                LocationDevicesList.ScrollIntoView(
                    row);
            }

            RefreshSelectedDeviceEditor();
        }

        private static Guid? SelectedLocationOption(
            ComboBox comboBox)
        {
            var option =
                comboBox.SelectedItem
                    as LocationOption;

            return option == null
                ? (Guid?)null
                : option.LocationId;
        }

        private static void SelectLocationOption(
            ComboBox comboBox,
            Guid? locationId)
        {
            var options =
                comboBox.ItemsSource
                    as IEnumerable<LocationOption>;

            if (options == null)
            {
                comboBox.SelectedItem = null;
                return;
            }

            comboBox.SelectedItem =
                options.FirstOrDefault(
                    item =>
                        item.LocationId ==
                        locationId);
        }

        private enum LocationEditorMode
        {
            Browse = 0,
            Create = 1,
            Edit = 2
        }

        private sealed class LocationRow
        {
            public LocationRow(
                LocationTopologyLocation item,
                string summary)
            {
                Item = item;
                Summary = summary;
            }

            public LocationTopologyLocation Item { get; }

            public string Summary { get; }
        }

        private sealed class DeviceRow
        {
            public DeviceRow(
                LocationTopologyDevice item,
                string summary)
            {
                Item = item;
                Summary = summary;
            }

            public LocationTopologyDevice Item { get; }

            public string Summary { get; }
        }

        private sealed class LocationOption
        {
            public LocationOption(
                Guid? locationId,
                string text)
            {
                LocationId = locationId;
                Text = text;
            }

            public Guid? LocationId { get; }

            public string Text { get; }
        }
    }
}
