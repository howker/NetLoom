using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.Rings;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

// Sprint 50: вид кольца — выбор, режим «Показать: кольцо» на карте и инспектор кольца.
public partial class MainWindow
{
    // Кольцо, показанное в инспекторе (для сохранения раскрытых технических деталей).
    private string _inspectorRingKeyShown;

    // Режим кольца включён автоматически при выборе карточки предупреждения: выход из раздела его снимает.
    private bool _ringViewHeldForAlerts;

    private MapOperationalFocusMode _operationalFocusModeBeforeAlerts;

    private string _operationalFocusRingKeyBeforeAlerts;

    private sealed class InspectorRingRow
    {
        public InspectorRingRow(string ringKey, string text)
        {
            RingKey = ringKey;
            Text = text;
        }

        public string RingKey { get; }

        public string Text { get; }
    }

    private IReadOnlyList<RingDiagnostic> CurrentRings()
    {
        return _lastDiagnosticSnapshot == null
            ? (IReadOnlyList<RingDiagnostic>)new RingDiagnostic[0]
            : _lastDiagnosticSnapshot.Rings;
    }

    private RingDiagnostic RingByKey(string ringKey)
    {
        if (string.IsNullOrEmpty(ringKey))
        {
            return null;
        }

        return CurrentRings().FirstOrDefault(
            ring => string.Equals(ring.RingKey, ringKey, StringComparison.Ordinal));
    }

    private string DeviceLocationNameForRing(Guid deviceId)
    {
        if (_lastDiagnosticSnapshot == null)
        {
            return null;
        }

        var device = _lastDiagnosticSnapshot.Devices.FirstOrDefault(item => item.DeviceId == deviceId);
        return device == null ? null : device.LocationName;
    }

    private string RingTitleText(RingDiagnostic ring)
    {
        return RingPresentation.Title(ring, DeviceLocationNameForRing);
    }

    // Текст пункта меню и кнопки инспектора: «{заголовок} · {состояние}».
    private string RingLabelText(RingDiagnostic ring)
    {
        return UiText.Format(
            "RingMenuItem",
            RingTitleText(ring),
            UiText.Get(RingPresentation.Describe(ring).StatusKey));
    }

    private IEnumerable<RingDiagnostic> RingsInDisplayOrder(IEnumerable<RingDiagnostic> rings)
    {
        return rings
            .OrderBy(ring => RingTitleText(ring), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(ring => ring.RingKey, StringComparer.Ordinal);
    }

    // Кольцо режима «Показать» исчезло из снимка: режим снимается, цели пусты.
    private void RefreshRingFocusTargets()
    {
        var ring = RingByKey(_operationalFocusRingKey);

        if (ring == null)
        {
            _operationalFocusMode = MapOperationalFocusMode.None;
            _operationalFocusRingKey = null;
            return;
        }

        foreach (var linkId in ring.PhysicalLinkIds)
        {
            _operationalFocusPhysicalLinkIds.Add(linkId);
        }

        foreach (var deviceId in ring.DeviceIds)
        {
            _operationalFocusDeviceIds.Add(deviceId);
        }
    }

    // Связь показанного кольца оформляется так же, как связь показанного пути.
    private bool IsRingFocusLink(Guid? physicalLinkId)
    {
        return _operationalFocusMode == MapOperationalFocusMode.Ring &&
               physicalLinkId.HasValue &&
               _operationalFocusPhysicalLinkIds.Contains(physicalLinkId.Value);
    }

    private MenuItem CreateRingMenuItem()
    {
        var menu = new MenuItem
        {
            Header = UiText.Get("RingMenu")
        };

        AutomationProperties.SetName(menu, UiText.Get("RingMenu"));

        var rings = CurrentRings();

        if (rings.Count == 0)
        {
            var none = new MenuItem
            {
                Header = UiText.Get("RingMenuNone"),
                IsEnabled = false
            };

            AutomationProperties.SetName(none, UiText.Get("RingMenuNone"));
            menu.Items.Add(none);
            return menu;
        }

        foreach (var ring in RingsInDisplayOrder(rings))
        {
            var label = RingLabelText(ring);

            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = string.Equals(ring.RingKey, _selectedRingKey, StringComparison.Ordinal) ||
                            (_operationalFocusMode == MapOperationalFocusMode.Ring &&
                             string.Equals(ring.RingKey, _operationalFocusRingKey, StringComparison.Ordinal)),
                Tag = ring.RingKey
            };

            AutomationProperties.SetName(item, label);
            item.Click += OnRingMenuItemClick;
            menu.Items.Add(item);
        }

        return menu;
    }

    private void OnRingMenuItemClick(object sender, RoutedEventArgs e)
    {
        var item = sender as MenuItem;
        var ringKey = item == null ? null : item.Tag as string;

        if (ringKey == null)
        {
            return;
        }

        SelectRing(ringKey, true, false);
    }

    private void OnInspectorRingClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var ringKey = button == null ? null : button.Tag as string;

        if (ringKey == null)
        {
            return;
        }

        // Вид кольца показывается на карте: из раздела без карты сначала открывается «Карта».
        if (ShellMapSurface.Visibility != Visibility.Visible)
        {
            ShowShellSection(ShellSection.Map);
        }

        SelectRing(ringKey, true, false);
    }

    // Выбор кольца: снимает выбор устройства, порта, связи и размещения, включает режим «Кольцо» и вписывает кольцо.
    // Прежний вид кладётся в историю (Esc и Alt+← его возвращают), если recordView.
    private void SelectRing(string ringKey, bool recordView, bool temporaryForAlerts)
    {
        var ring = RingByKey(ringKey);

        if (ring == null)
        {
            return;
        }

        StopStartupTopologyFit();

        if (recordView)
        {
            RecordMapView(true);
        }

        ClearMapPathState(false);

        _ringViewHeldForAlerts = temporaryForAlerts;

        _highlightedDeviceId = null;
        _selectedDeviceId = null;
        _selectedInterfaceId = null;
        _selectedPhysicalLinkId = null;
        _selectedLocationId = null;
        _selectedRingKey = ring.RingKey;
        _operationalFocusRingKey = ring.RingKey;

        RedrawCurrentMap();
        SetOperationalFocusMode(MapOperationalFocusMode.Ring);
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
        ApplyLinkFocusPresentation();

        if (recordView)
        {
            RevealInspectorForExplicitSelection();
        }
    }

    // Режим кольца, включённый автоматически в «Предупреждениях», при выходе из раздела возвращает прежний режим.
    private void CaptureOperationalFocusBeforeAlerts()
    {
        _operationalFocusModeBeforeAlerts = _operationalFocusMode;
        _operationalFocusRingKeyBeforeAlerts = _operationalFocusRingKey;
        _ringViewHeldForAlerts = false;
    }

    private void RestoreOperationalFocusAfterAlerts()
    {
        if (!_ringViewHeldForAlerts)
        {
            return;
        }

        _ringViewHeldForAlerts = false;
        _operationalFocusMode = _operationalFocusModeBeforeAlerts;
        _operationalFocusRingKey = _operationalFocusRingKeyBeforeAlerts;

        RefreshOperationalFocusTargets();
        UpdateOperationalFocusMenuState();
        ReapplyOperationalFocusPresentation();
        ApplyLinkFocusPresentation();
    }

    // Кольцо, на которое указывает предупреждение (ключ кольца среди связанных областей), либо null.
    private string AlertRingKey(NetLoom.Contracts.Alerts.TopologyAlert alert)
    {
        if (alert == null || alert.RelatedRegionKeys == null)
        {
            return null;
        }

        foreach (var key in alert.RelatedRegionKeys)
        {
            if (RingByKey(key) != null)
            {
                return key;
            }
        }

        return null;
    }

    // Блок «Кольца» в обзоре инспектора устройства и связи: по кнопке на кольцо.
    private void ShowInspectorRingBlocks(IEnumerable<RingDiagnostic> rings)
    {
        var rows = RingsInDisplayOrder(rings)
            .Select(ring => new InspectorRingRow(ring.RingKey, RingLabelText(ring)))
            .ToArray();

        if (rows.Length == 0)
        {
            ClearInspectorRingBlocks();
            return;
        }

        InspectorRingsTitleText.Text = UiText.Get("InspectorRingsTitle");

        var previous = InspectorRingButtons.ItemsSource as InspectorRingRow[];

        // Повторный опрос не пересоздаёт кнопки, чтобы не терять клавиатурный фокус.
        if (previous == null ||
            previous.Length != rows.Length ||
            previous.Zip(rows, (a, b) => a.RingKey == b.RingKey && a.Text == b.Text).Any(same => !same))
        {
            InspectorRingButtons.ItemsSource = rows;
        }

        InspectorRingsTitleText.Visibility = Visibility.Visible;
        InspectorRingButtons.Visibility = Visibility.Visible;
    }

    private void ClearInspectorRingBlocks()
    {
        if (InspectorRingButtons.Items.Count > 0)
        {
            InspectorRingButtons.ItemsSource = new InspectorRingRow[0];
        }

        InspectorRingsTitleText.Text = string.Empty;
        InspectorRingsTitleText.Visibility = Visibility.Collapsed;
        InspectorRingButtons.Visibility = Visibility.Collapsed;
    }

    private void ShowDeviceRingBlocks(Guid deviceId)
    {
        ShowInspectorRingBlocks(CurrentRings().Where(ring => ring.DeviceIds.Contains(deviceId)));
    }

    private void ShowLinkRingBlocks(Guid physicalLinkId)
    {
        ShowInspectorRingBlocks(CurrentRings().Where(ring => ring.PhysicalLinkIds.Contains(physicalLinkId)));
    }

    private string RingDeviceName(Guid deviceId)
    {
        var device = _lastDiagnosticSnapshot == null
            ? null
            : _lastDiagnosticSnapshot.Devices.FirstOrDefault(item => item.DeviceId == deviceId);

        return device == null ? null : DisplayDeviceName(device.DisplayName);
    }

    private string RingBlockedPortText(RingBlockedPort port)
    {
        string portName = null;

        if (_lastDiagnosticSnapshot != null)
        {
            if (port.InterfaceId.HasValue)
            {
                portName = DisplayLinkEndpointInterfaceName(port.InterfaceId, null);
            }
            else
            {
                var link = _lastDiagnosticSnapshot.Links.FirstOrDefault(
                    item => item.PhysicalLinkId == port.PhysicalLinkId);

                if (link != null)
                {
                    portName = DisplayLinkEndpointInterfaceName(
                        null,
                        link.DeviceAId == port.DeviceId ? link.InterfaceAName : link.InterfaceBName);
                }
            }
        }

        return (RingDeviceName(port.DeviceId) ?? DisplayDeviceName(null)) + " " + (portName ?? DisplayInterfaceName(null));
    }

    private void ShowRingDiagnostic(RingDiagnostic ring)
    {
        var preserveExpanded =
            string.Equals(_inspectorRingKeyShown, ring.RingKey, StringComparison.Ordinal) &&
            InspectorTechnicalDetailsExpander.IsExpanded;

        _inspectorEntityId = null;
        _inspectorRingKeyShown = ring.RingKey;
        _inspectorPrimaryAlert = null;

        InspectorEntityTypeText.Text = UiText.Get("InspectorEntityRing");
        InspectorEntityIdText.Text = UiText.Format("InspectorEntityId", ring.RingKey);
        InspectorTechnicalDetailsExpander.Visibility = Visibility.Visible;
        InspectorTechnicalDetailsExpander.IsExpanded = preserveExpanded;

        ClearInspectorOperatorStatus();

        ConfigureInspectorTabs(false, false, false);

        DiagnosticInterfaceList.ItemsSource = new DiagnosticEntityRow[0];
        DiagnosticLinkList.ItemsSource = new DiagnosticEntityRow[0];
        DiagnosticStatusText.Text = string.Empty;
        DiagnosticStatusText.Visibility = Visibility.Collapsed;
        InspectorPrimaryActionButton.Visibility = Visibility.Collapsed;

        DiagnosticElementTitleText.Text = RingTitleText(ring);
        DiagnosticElementSubtitleText.Text = RingPresentation.KindText(ring);

        // Состояние защиты: строка под заголовком и пояснение под ней; цвет только по тону, норма нейтральна.
        var status = RingPresentation.Describe(ring);
        var statusText = UiText.Get(status.StatusKey);

        InspectorProblemText.Text = statusText;
        InspectorProblemText.SetResourceReference(
            System.Windows.Controls.TextBlock.ForegroundProperty,
            status.Tone == RingStatusTone.Critical
                ? "NetLoom.Brush.Critical"
                : status.Tone == RingStatusTone.Warning
                    ? "NetLoom.Brush.Warning"
                    : "NetLoom.Brush.TextPrimary");
        InspectorProblemText.Visibility = Visibility.Visible;

        InspectorProblemExplanationText.Text = status.ExplanationText ?? string.Empty;
        InspectorProblemExplanationText.Visibility = string.IsNullOrWhiteSpace(status.ExplanationText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        DiagnosticPrimaryTitleText.Text = UiText.Get("DiagnosticStateTitle");

        var fields = new List<DiagnosticFieldRow>
        {
            Field("RingFieldStatus", statusText),
            Field("RingFieldMembers", UiText.FormatCount("DiagnosticDeviceCount", ring.DeviceIds.Count))
        };

        if (ring.Kind == PhysicalRedundancyRegionKind.CorePairRing && ring.CoreDeviceIds.Count > 0)
        {
            var coreNames = ring.CoreDeviceIds
                .Select(id => RingDeviceName(id) ?? DisplayDeviceName(null))
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            fields.Add(
                Field(
                    "RingFieldCores",
                    coreNames.Length == 2
                        ? UiText.Format("RingCoresJoin", coreNames[0], coreNames[1])
                        : string.Join(", ", coreNames)));
        }

        var rootName = ring.RootDeviceId.HasValue ? RingDeviceName(ring.RootDeviceId.Value) : null;

        fields.Add(
            Field(
                "RingFieldRoot",
                rootName != null
                    ? rootName
                    : !string.IsNullOrWhiteSpace(ring.DesignatedRoot)
                        ? UiText.Format("RingRootOutside", ring.DesignatedRoot)
                        : UiText.Get("RingStatusUnresolved")));

        fields.Add(
            Field(
                "RingFieldBlockedPort",
                ring.BlockedPorts.Count == 0
                    ? UiText.Get("RingBlockedPortNone")
                    : string.Join(
                        Environment.NewLine,
                        ring.BlockedPorts
                            .Select(port => new { Port = port, Device = RingDeviceName(port.DeviceId) ?? string.Empty })
                            .OrderBy(item => item.Device, StringComparer.CurrentCultureIgnoreCase)
                            .Select(item => RingBlockedPortText(item.Port)))));

        if (ring.LastTopologyChangeUtc.HasValue)
        {
            fields.Add(Field("RingFieldLastTopologyChange", RelativeTimeText(ring.LastTopologyChangeUtc)));
        }

        DiagnosticFieldsList.ItemsSource = fields;

        // Участники кольца: имена по порядку имени, ядра помечены.
        DiagnosticSecondaryTitleText.Text = UiText.Get("RingMembersTitle");
        DiagnosticSecondaryList.ItemsSource = ring.DeviceIds
            .Select(id => new { Id = id, Name = RingDeviceName(id) ?? DisplayDeviceName(null) })
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(
                item => new DiagnosticTextRow(
                    ring.CoreDeviceIds.Contains(item.Id)
                        ? UiText.Format("RingMemberCore", item.Name)
                        : item.Name))
            .ToArray();

        DiagnosticTertiaryTitleText.Text = string.Empty;
        DiagnosticTertiaryList.ItemsSource = new DiagnosticTextRow[0];
    }
}
