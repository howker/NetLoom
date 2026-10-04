using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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
        private const double Adr083AlertsListWidth = 420.0;
        private const double Adr083MapToolbarCompactWidth = 1000.0;
        private const double Adr083MapToolbarDefaultRightPadding = 12.0;
        private const double Adr083MapToolbarRevealRightPadding = 56.0;
        private const double Adr083EquipmentHideDescriptionWidth = 980.0;
        private const double Adr083EquipmentHideCategoryWidth = 820.0;

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

            ShellMapSurface.SizeChanged +=
                OnShellMapSurfaceSizeChanged;

            UpdateAdr083SectionPresentation(
                ShellSection.Map);
            UpdateAdr083InspectorLayout();
            UpdateAdr083MapToolbarResponsivePresentation();
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

            RefreshAdr083GlobalSearchResults();
            ShellGlobalSearchPopup.IsOpen =
                true;

            if (!Adr083ShouldUseLegacyLookup(
                    query))
            {
                return;
            }

            LookupQueryTextBox.Text =
                query;

            await QueueLookupAsync();
        }

        private static bool Adr083ShouldUseLegacyLookup(
            string query)
        {
            IPAddress address;

            if (IPAddress.TryParse(
                    query,
                    out address))
            {
                return true;
            }

            var compact =
                new string(
                    query
                        .Where(
                            character =>
                                character != ':' &&
                                character != '-' &&
                                character != '.' &&
                                !char.IsWhiteSpace(character))
                        .ToArray());

            return compact.Length == 12 &&
                compact.All(
                    Uri.IsHexDigit);
        }

        private void RefreshAdr083GlobalSearchResults()
        {
            if (ShellGlobalSearchTextBox == null ||
                ShellGlobalSearchResultsList == null)
            {
                return;
            }

            var query =
                ShellGlobalSearchTextBox.Text == null
                    ? string.Empty
                    : ShellGlobalSearchTextBox.Text.Trim();

            if (query.Length == 0)
            {
                ShellGlobalSearchResultsList.ItemsSource =
                    null;
                return;
            }

            var rows =
                BuildAdr083GlobalSearchRows(
                    query);

            var view =
                CollectionViewSource.GetDefaultView(
                    rows);

            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(
                new PropertyGroupDescription(
                    "GroupName"));

            ShellGlobalSearchResultsList.ItemsSource =
                view;

            ShellGlobalSearchStatusText.Text =
                rows.Length == 0
                    ? UiText.Format(
                        "ShellGlobalSearchNoResults",
                        query)
                    : UiText.Format(
                        "ShellGlobalSearchResultCount",
                        rows.Length);
        }

        private Adr083GlobalSearchRow[]
            BuildAdr083GlobalSearchRows(
                string query)
        {
            var rows =
                new List<Adr083GlobalSearchRow>();
            var keys =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            if (_lastDiagnosticSnapshot != null)
            {
                foreach (var device in
                    _lastDiagnosticSnapshot.Devices)
                {
                    var deviceName =
                        string.IsNullOrWhiteSpace(
                            device.DisplayName)
                            ? device.DeviceId.ToString("D")
                            : device.DisplayName;
                    var address =
                        device.ManagementAddress ??
                        string.Empty;

                    if (Adr083SearchContains(
                            deviceName,
                            query) ||
                        Adr083SearchContains(
                            address,
                            query))
                    {
                        AddAdr083GlobalSearchRow(
                            rows,
                            keys,
                            new Adr083GlobalSearchRow(
                                UiText.Get(
                                    "ShellGlobalSearchGroupDevices"),
                                string.IsNullOrWhiteSpace(address)
                                    ? deviceName
                                    : deviceName +
                                      " · " +
                                      address,
                                device.DeviceId,
                                null,
                                null));
                    }

                    foreach (var item in
                        device.Interfaces)
                    {
                        if (Adr083SearchContains(
                                item.MacAddress,
                                query))
                        {
                            AddAdr083GlobalSearchRow(
                                rows,
                                keys,
                                new Adr083GlobalSearchRow(
                                    UiText.Get(
                                        "ShellGlobalSearchGroupMacAddresses"),
                                    deviceName +
                                        " · " +
                                        item.MacAddress,
                                    device.DeviceId,
                                    item.InterfaceId,
                                    null));
                        }

                        if (Adr083SearchContains(
                                item.DisplayName,
                                query) ||
                            Adr083SearchContains(
                                item.IfName,
                                query) ||
                            Adr083SearchContains(
                                item.IfAlias,
                                query) ||
                            Adr083SearchContains(
                                item.IfDescription,
                                query))
                        {
                            var interfaceName =
                                !string.IsNullOrWhiteSpace(
                                    item.DisplayName)
                                    ? item.DisplayName
                                    : !string.IsNullOrWhiteSpace(
                                        item.IfName)
                                        ? item.IfName
                                        : item.InterfaceId.ToString("D");

                            AddAdr083GlobalSearchRow(
                                rows,
                                keys,
                                new Adr083GlobalSearchRow(
                                    UiText.Get(
                                        "ShellGlobalSearchGroupPorts"),
                                    deviceName +
                                        " · " +
                                        interfaceName,
                                    device.DeviceId,
                                    item.InterfaceId,
                                    null));
                        }
                    }
                }
            }

            if (_lastMapSnapshot != null)
            {
                foreach (var node in
                    _lastMapSnapshot.Nodes)
                {
                    if (!node.DeviceId.HasValue)
                    {
                        continue;
                    }

                    var name =
                        DisplayNodeLabel(
                            node);

                    if (!Adr083SearchContains(
                            name,
                            query))
                    {
                        continue;
                    }

                    AddAdr083GlobalSearchRow(
                        rows,
                        keys,
                        new Adr083GlobalSearchRow(
                            UiText.Get(
                                "ShellGlobalSearchGroupDevices"),
                            name,
                            node.DeviceId.Value,
                            null,
                            null));
                }
            }

            if (Adr083ShouldUseLegacyLookup(query) &&
                string.Equals(
                    LookupQueryTextBox.Text == null
                        ? string.Empty
                        : LookupQueryTextBox.Text.Trim(),
                    query,
                    StringComparison.OrdinalIgnoreCase))
            {
                foreach (var lookupRow in
                    LookupResultsList.Items
                        .OfType<LookupCandidateRow>())
                {
                    var candidate =
                        lookupRow.Candidate;
                    var groupName =
                        Adr083LooksLikeMacAddress(query)
                            ? UiText.Get(
                                "ShellGlobalSearchGroupMacAddresses")
                            : UiText.Get(
                                "ShellGlobalSearchGroupDevices");

                    AddAdr083GlobalSearchRow(
                        rows,
                        keys,
                        new Adr083GlobalSearchRow(
                            groupName,
                            lookupRow.Summary,
                            candidate.DeviceId,
                            candidate.InterfaceId,
                            lookupRow));
                }
            }

            return rows
                .Take(30)
                .ToArray();
        }

        private static bool Adr083LooksLikeMacAddress(
            string query)
        {
            var compact =
                new string(
                    (query ?? string.Empty)
                        .Where(
                            character =>
                                character != ':' &&
                                character != '-' &&
                                character != '.' &&
                                !char.IsWhiteSpace(character))
                        .ToArray());

            return compact.Length == 12 &&
                compact.All(
                    Uri.IsHexDigit);
        }

        private static bool Adr083SearchContains(
            string value,
            string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(
                    query,
                    StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static void AddAdr083GlobalSearchRow(
            ICollection<Adr083GlobalSearchRow> rows,
            ISet<string> keys,
            Adr083GlobalSearchRow row)
        {
            var key =
                row.GroupName +
                "\u001f" +
                (row.DeviceId.HasValue
                    ? row.DeviceId.Value.ToString("D")
                    : string.Empty) +
                "\u001f" +
                (row.InterfaceId.HasValue
                    ? row.InterfaceId.Value.ToString("D")
                    : string.Empty) +
                "\u001f" +
                (!row.DeviceId.HasValue &&
                 !row.InterfaceId.HasValue
                    ? row.Summary
                    : string.Empty);

            if (keys.Add(key))
            {
                rows.Add(row);
            }
        }

        private void OnShellGlobalSearchPreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ShellGlobalSearchPopup.IsOpen =
                    false;
                ShellGlobalSearchTextBox.Focus();
                e.Handled =
                    true;
                return;
            }

            if (e.Key == Key.Down &&
                ReferenceEquals(
                    sender,
                    ShellGlobalSearchTextBox) &&
                ShellGlobalSearchResultsList.Items.Count > 0)
            {
                if (ShellGlobalSearchResultsList.SelectedIndex < 0)
                {
                    ShellGlobalSearchResultsList.SelectedIndex =
                        0;
                }

                ShellGlobalSearchResultsList.Focus();
                e.Handled =
                    true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (ShellGlobalSearchResultsList.SelectedIndex < 0 &&
                    ShellGlobalSearchResultsList.Items.Count > 0)
                {
                    ShellGlobalSearchResultsList.SelectedIndex =
                        0;
                }

                ActivateAdr083GlobalSearchSelection();
                e.Handled =
                    true;
            }
        }

        private void OnShellGlobalSearchResultMouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            ActivateAdr083GlobalSearchSelection();
        }

        private void ActivateAdr083GlobalSearchSelection()
        {
            var row =
                ShellGlobalSearchResultsList.SelectedItem
                as Adr083GlobalSearchRow;

            if (row == null)
            {
                return;
            }

            if (row.LookupRow != null)
            {
                LookupDetailsText.Text =
                    row.LookupRow.Details;
            }

            if (!row.DeviceId.HasValue)
            {
                ShellGlobalSearchPopup.IsOpen =
                    false;
                return;
            }

            StopStartupTopologyFit();

            _highlightedDeviceId =
                row.DeviceId.Value;
            _selectedDeviceId =
                row.DeviceId.Value;
            _selectedInterfaceId =
                row.InterfaceId;
            _selectedPhysicalLinkId =
                null;
            _selectedLocationId =
                null;

            ShowShellSection(
                ShellSection.Map);
            RedrawCurrentMap();
            ShowSelectedDiagnostic();
            UpdateSelectedLayoutControl();
            BringHighlightedDeviceIntoView();

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
                RefreshAdr083GlobalSearchResults();
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
            UpdateAdr083MapToolbarResponsivePresentation();
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
            UpdateAdr083MapToolbarResponsivePresentation();
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
            UpdateAdr083EquipmentResponsiveColumns();
        }

        private void OnShellMapSurfaceSizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            UpdateAdr083MapToolbarResponsivePresentation();
        }

        private void UpdateAdr083SectionPresentation(
            ShellSection section)
        {
            ShellSectionSurface.ClearValue(
                FrameworkElement.WidthProperty);
            ShellSectionSurface.HorizontalAlignment =
                HorizontalAlignment.Stretch;

            ShellMapSurface.Margin =
                new Thickness(0.0);
            Adr083MapPrimaryToolbar.Visibility =
                Visibility.Visible;

            ShellSectionSurface.Visibility =
                section == ShellSection.Map
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            ShellMapSurface.Visibility =
                section == ShellSection.Map ||
                section == ShellSection.Alerts
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (section == ShellSection.Alerts)
            {
                ShellSectionSurface.Width =
                    Adr083AlertsListWidth;
                ShellSectionSurface.HorizontalAlignment =
                    HorizontalAlignment.Left;
                ShellMapSurface.Margin =
                    new Thickness(
                        Adr083AlertsListWidth,
                        0.0,
                        0.0,
                        0.0);
                Adr083MapPrimaryToolbar.Visibility =
                    Visibility.Collapsed;
            }

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
            UpdateAdr083EquipmentResponsiveColumns();
            UpdateAdr083MapToolbarResponsivePresentation();
        }

        private void UpdateAdr083MapToolbarResponsivePresentation()
        {
            if (ShellMapSurface == null ||
                Adr083MapToolbar == null ||
                Adr083MapEditModeIndicatorText == null ||
                ShellInspectorRevealButton == null)
            {
                return;
            }

            var mapWidth =
                ShellMapSurface.ActualWidth;

            Adr083MapEditModeIndicatorText.Text =
                UiText.Get(
                    mapWidth > 0.0 &&
                    mapWidth < Adr083MapToolbarCompactWidth
                        ? "ShellMapEditModeNarrow"
                        : "ShellMapEditModeCompact");

            var rightPadding =
                ShellMapSurface.Visibility == Visibility.Visible &&
                ShellInspectorRevealButton.Visibility == Visibility.Visible
                    ? Adr083MapToolbarRevealRightPadding
                    : Adr083MapToolbarDefaultRightPadding;

            var padding =
                Adr083MapToolbar.Padding;

            Adr083MapToolbar.Padding =
                new Thickness(
                    padding.Left,
                    padding.Top,
                    rightPadding,
                    padding.Bottom);
        }

        private void UpdateAdr083EquipmentResponsiveColumns()
        {
            if (ShellSectionSurface == null)
            {
                return;
            }

            var width =
                ShellSectionSurface.ActualWidth;

            if (width <= 0.0)
            {
                width =
                    ShellWorkspaceGrid.ColumnDefinitions.Count > 2
                        ? ShellWorkspaceGrid
                            .ColumnDefinitions[2]
                            .ActualWidth
                        : 0.0;
            }

            Resources[
                "NetLoom.Equipment.DescriptionColumnWidth"] =
                width <
                    Adr083EquipmentHideDescriptionWidth
                    ? new GridLength(0.0)
                    : new GridLength(
                        1.35,
                        GridUnitType.Star);

            Resources[
                "NetLoom.Equipment.CategoryColumnWidth"] =
                width <
                    Adr083EquipmentHideCategoryWidth
                    ? new GridLength(0.0)
                    : new GridLength(
                        1.1,
                        GridUnitType.Star);
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

        private sealed class Adr083GlobalSearchRow
        {
            public Adr083GlobalSearchRow(
                string groupName,
                string summary,
                Guid? deviceId,
                Guid? interfaceId,
                LookupCandidateRow lookupRow)
            {
                GroupName = groupName;
                Summary = summary;
                DeviceId = deviceId;
                InterfaceId = interfaceId;
                LookupRow = lookupRow;
            }

            public string GroupName { get; }

            public string Summary { get; }

            public Guid? DeviceId { get; }

            public Guid? InterfaceId { get; }

            public LookupCandidateRow LookupRow { get; }
        }
    }
}
