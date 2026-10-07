using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NetLoom.Application.Alerts;
using NetLoom.Application.MapLayout;
using NetLoom.Contracts.Alerts;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private readonly TopologyAlertTransitionTracker
        _alertTransitionTracker;

    private readonly Dictionary<string, AlertEventState>
        _activeAlertEventStateByKey =
            new Dictionary<string, AlertEventState>(
                StringComparer.Ordinal);

    private readonly Dictionary<string, DateTime>
        _alertFirstSeenUtcByKey =
            new Dictionary<string, DateTime>(
                StringComparer.Ordinal);

    private readonly List<ShellEventRow>
        _shellEventRows =
            new List<ShellEventRow>();

    private TopologyAlertSnapshot _lastAlertSnapshot;

    // A2 (sprint46-mockup-gap): карточка выбрана после «Показать на карте» или при входе в раздел.
    // Решение владельца 2026-10-06. Ключ выбранной карточки переживает обновление списка.
    private string _selectedAlertKey;

    // A4: при входе в «Предупреждения» без выбранной карточки выбирается первая.
    // Карта показывает её участников; если список ещё пуст, выбор ждёт первых данных.
    private bool _alertAutoSelectPending;

    // Карта у «Карты» и «Предупреждений» общая, а вписывание сохраняет вид в базу (восстановление после перезапуска).
    // Автоматический выбор не должен менять рабочий вид карты: прежний вид запоминаем и возвращаем при выходе.
    // Явное «Показать на карте» — намерение оператора, после него вид не возвращается.
    private bool _mapViewportHeldForAlerts;

    private MapViewportLayout _mapViewportBeforeAlerts;

    private static string AlertSeverityText(
        TopologyAlertSeverity severity)
    {
        return OperatorStatusDecoratedLabel(
            AlertStatusSemantic(
                severity));
    }

    private static string AlertKindText(
        TopologyAlertKind kind)
    {
        switch (kind)
        {
            case TopologyAlertKind.ForwardingCycle:
                return UiText.Get(
                    "AlertKindForwardingCycle");

            case TopologyAlertKind
                .RingProtectionDegraded:
                return UiText.Get(
                    "AlertKindRingProtectionDegraded");

            default:
                return kind.ToString();
        }
    }

    private static string AlertReasonText(
        TopologyAlertReason reason)
    {
        switch (reason)
        {
            case TopologyAlertReason
                .ConfirmedForwardingCycle:
                return UiText.Get(
                    "AlertReasonConfirmedForwardingCycle");

            case TopologyAlertReason
                .DisabledRingLink:
                return UiText.Get(
                    "AlertReasonDisabledRingLink");

            case TopologyAlertReason
                .MultipleBlockingRingLinks:
                return UiText.Get(
                    "AlertReasonMultipleBlockingRingLinks");

            default:
                return reason.ToString();
        }
    }

    private string BuildAlertScope(
        TopologyAlert alert)
    {
        var names =
            new List<string>();

        if (_lastDiagnosticSnapshot != null)
        {
            var physicalLinkIds =
                new HashSet<Guid>(
                    alert.PhysicalLinkIds);

            foreach (var link in
                     _lastDiagnosticSnapshot.Links
                         .Where(
                             item =>
                                 physicalLinkIds.Contains(
                                     item.PhysicalLinkId)))
            {
                if (!string.IsNullOrWhiteSpace(
                        link.DeviceAName))
                {
                    names.Add(
                        link.DeviceAName.Trim());
                }

                if (!string.IsNullOrWhiteSpace(
                        link.DeviceBName))
                {
                    names.Add(
                        link.DeviceBName.Trim());
                }
            }
        }

        if (names.Count == 0 &&
            _lastMapSnapshot != null)
        {
            var nodesByKey =
                _lastMapSnapshot.Nodes
                    .ToDictionary(
                        item => item.Key,
                        StringComparer.Ordinal);

            var physicalLinkIds =
                new HashSet<Guid>(
                    alert.PhysicalLinkIds);

            foreach (var link in
                     _lastMapSnapshot.Links
                         .Where(
                             item =>
                                 item.PhysicalLinkId.HasValue &&
                                 physicalLinkIds.Contains(
                                     item.PhysicalLinkId.Value)))
            {
                NetLoom.Contracts.TopologyMap.MapNode node;

                if (nodesByKey.TryGetValue(
                        link.SourceNodeKey,
                        out node))
                {
                    names.Add(
                        node.Label);
                }

                if (nodesByKey.TryGetValue(
                        link.TargetNodeKey,
                        out node))
                {
                    names.Add(
                        node.Label);
                }
            }
        }

        var distinctNames =
            names
                .Where(
                    item =>
                        !string.IsNullOrWhiteSpace(
                            item))
                .Distinct(
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        if (distinctNames.Length == 0)
        {
            return UiText.Format(
                "AlertAffectedLinks",
                alert.PhysicalLinkIds.Count);
        }

        const int maxVisibleNames = 3;

        var visibleNames =
            distinctNames
                .Take(
                    maxVisibleNames)
                .ToArray();

        var result =
            string.Join(
                " ↔ ",
                visibleNames);

        if (distinctNames.Length >
            maxVisibleNames)
        {
            result +=
                UiText.Format(
                    "AlertMoreDevices",
                    distinctNames.Length -
                    maxVisibleNames);
        }

        return result;
    }

    private static string BuildAlertReasonSummary(
        TopologyAlert alert)
    {
        return string.Join(
            " · ",
            alert.Reasons
                .Select(
                    AlertReasonText));
    }

    private static string BuildAlertTechnicalDetails(
        TopologyAlert alert)
    {
        var lines =
            new List<string>
            {
                UiText.Format(
                    "AlertInstance",
                    alert.InstanceId)
            };

        if (alert.RelatedRegionKeys.Count > 0)
        {
            lines.Add(
                UiText.Format(
                    "AlertRegions",
                    string.Join(
                        ", ",
                        alert.RelatedRegionKeys)));
        }

        lines.Add(
            UiText.Format(
                "AlertLinks",
                string.Join(
                    ", ",
                    alert.PhysicalLinkIds
                        .Select(
                            id =>
                                id.ToString("D")))));

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string AlertExpansionKey(
        TopologyAlert alert)
    {
        var links =
            string.Join(
                ",",
                alert.PhysicalLinkIds
                    .OrderBy(
                        id => id)
                    .Select(
                        id => id.ToString("D")));
        var regions =
            string.Join(
                ",",
                alert.RelatedRegionKeys
                    .OrderBy(
                        key => key,
                        StringComparer.Ordinal));
        var reasons =
            string.Join(
                ",",
                alert.Reasons
                    .OrderBy(
                        reason => reason)
                    .Select(
                        reason => reason.ToString()));

        return (alert.InstanceId ?? string.Empty) +
               "|" +
               alert.Kind +
               "|" +
               links +
               "|" +
               regions +
               "|" +
               reasons;
    }

    private AlertRow BuildAlertRow(
        TopologyAlert alert,
        DateTime generatedUtc,
        bool isTechnicalDetailsExpanded)
    {
        return new AlertRow(
            AlertExpansionKey(
                alert),
            AlertSeverityText(
                alert.Severity),
            AlertKindText(
                alert.Kind),
            BuildAlertScope(
                alert),
            BuildAlertReasonSummary(
                alert),
            AlertFirstSeenUtc(
                    alert,
                    generatedUtc)
                .ToLocalTime()
                .ToString(
                    "HH:mm",
                    CultureInfo.CurrentCulture),
            BuildAlertTechnicalDetails(
                alert),
            alert.PhysicalLinkIds.ToArray(),
            alert.Severity ==
                TopologyAlertSeverity.Critical,
            isTechnicalDetailsExpanded,
            UiText.Get(
                "AlertShowOnMapAction"),
            UiText.Get(
                "AlertTechnicalDetails"));
    }

    private DateTime AlertFirstSeenUtc(
        TopologyAlert alert,
        DateTime fallbackUtc)
    {
        DateTime firstSeenUtc;

        return _alertFirstSeenUtcByKey.TryGetValue(
                alert.AlertKey,
                out firstSeenUtc)
            ? firstSeenUtc
            : fallbackUtc;
    }

    private void CaptureAlertEvents(
        TopologyAlertSnapshot snapshot)
    {
        var currentByKey =
            snapshot.Alerts.ToDictionary(
                alert => alert.AlertKey,
                StringComparer.Ordinal);

        foreach (var pair in currentByKey)
        {
            if (_activeAlertEventStateByKey.ContainsKey(
                pair.Key))
            {
                continue;
            }

            var state =
                AlertEventState.FromAlert(
                    pair.Value,
                    BuildAlertScope(
                        pair.Value),
                    BuildAlertReasonSummary(
                        pair.Value));

            _activeAlertEventStateByKey[
                pair.Key] =
                    state;

            _alertFirstSeenUtcByKey[
                pair.Key] =
                    snapshot.GeneratedUtc;

            AddShellEvent(
                state,
                snapshot.GeneratedUtc,
                false);
        }

        var resolvedKeys =
            _activeAlertEventStateByKey.Keys
                .Where(
                    key =>
                        !currentByKey.ContainsKey(
                            key))
                .ToArray();

        foreach (var key in resolvedKeys)
        {
            var state =
                _activeAlertEventStateByKey[key];

            AddShellEvent(
                state,
                snapshot.GeneratedUtc,
                true);

            _activeAlertEventStateByKey.Remove(
                key);
            _alertFirstSeenUtcByKey.Remove(
                key);
        }

        ShellEventList.ItemsSource =
            _shellEventRows
                .OrderByDescending(
                    row =>
                        !row.IsResolved &&
                        row.IsCritical)
                .ThenByDescending(
                    row =>
                        !row.IsResolved &&
                        row.IsWarning)
                .Take(3)
                .ToArray();

        ShellEventEmptyText.Visibility =
            _shellEventRows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void AddShellEvent(
        AlertEventState state,
        DateTime occurredUtc,
        bool isResolved)
    {
        _shellEventRows.Insert(
            0,
            new ShellEventRow(
                state.AlertKey,
                occurredUtc
                    .ToLocalTime()
                    .ToString(
                        "HH:mm",
                        CultureInfo.CurrentCulture),
                isResolved
                    ? UiText.Format(
                        "ShellEventResolvedTitle",
                        state.Title)
                    : state.Title,
                state.Scope,
                state.PhysicalLinkIds,
                state.IsCritical,
                !state.IsCritical && !isResolved,
                isResolved));

        const int maxEventRows = 20;

        if (_shellEventRows.Count >
            maxEventRows)
        {
            _shellEventRows.RemoveRange(
                maxEventRows,
                _shellEventRows.Count -
                maxEventRows);
        }
    }

    private void OnShellEventClick(
        object sender,
        RoutedEventArgs e)
    {
        var button =
            sender as Button;
        var row =
            button == null
                ? null
                : button.DataContext as ShellEventRow;

        ShowShellSection(
            ShellSection.Alerts);

        if (row == null ||
            row.PhysicalLinkIds.Length == 0)
        {
            return;
        }

        // A2: событие ведёт к своей карточке; автоматический выбор первой здесь не нужен.
        _alertAutoSelectPending =
            false;
        _selectedAlertKey =
            CurrentAlertRows()
                .Where(
                    item =>
                        item.PhysicalLinkIds.Contains(
                            row.PhysicalLinkIds[0]))
                .Select(
                    item => item.ExpansionKey)
                .FirstOrDefault();

        SelectAlertPhysicalContext(
            row.PhysicalLinkIds[0]);
    }

    private void OnShellAllEventsClick(
        object sender,
        RoutedEventArgs e)
    {
        ShowShellSection(
            ShellSection.Alerts);
    }

    private void SelectAlertPhysicalContext(
        Guid physicalLinkId)
    {
        if (physicalLinkId == Guid.Empty)
        {
            return;
        }

        StopStartupTopologyFit();

        _highlightedDeviceId =
            null;
        _selectedDeviceId =
            null;
        _selectedInterfaceId =
            null;
        _selectedPhysicalLinkId =
            physicalLinkId;
        _selectedLocationId =
            null;

        RedrawCurrentMap();
        ShowSelectedDiagnostic();
        UpdateSelectedLayoutControl();
    }

    private void ShowAlerts(
        TopologyAlertSnapshot snapshot,
        TopologyAlertTransitionKind transition)
    {
        _lastAlertSnapshot =
            snapshot;

        CaptureAlertEvents(
            snapshot);

        var expandedAlertKeys =
            new HashSet<string>(
                AlertList.Items
                    .OfType<AlertRow>()
                    .Where(
                        row =>
                            row.IsTechnicalDetailsExpanded)
                    .Select(
                        row => row.ExpansionKey),
                StringComparer.Ordinal);

        var rows =
            snapshot.Alerts
                .Select(
                    alert =>
                        BuildAlertRow(
                            alert,
                            snapshot.GeneratedUtc,
                            expandedAlertKeys.Contains(
                                AlertExpansionKey(
                                    alert))))
                .ToArray();

        AlertList.ItemsSource =
            rows;

        ApplyAlertCardSelection(
            rows);

        var criticalCount =
            snapshot.Alerts.Count(
                alert =>
                    alert.Severity ==
                    TopologyAlertSeverity.Critical);

        var warningCount =
            snapshot.Alerts.Count(
                alert =>
                    alert.Severity ==
                    TopologyAlertSeverity.Warning);

        AlertCriticalCountText.Text =
            UiText.Format(
                "AlertCriticalCountChip",
                criticalCount);
        AlertWarningCountText.Text =
            UiText.Format(
                "AlertWarningCountChip",
                warningCount);

        if (rows.Length == 0)
        {
            AlertStatusText.Text =
                UiText.Get(
                    "AlertNone");
        }
        else
        {
            AlertStatusText.Text =
                UiText.Format(
                    "AlertSummary",
                    criticalCount,
                    warningCount);
        }

        var alertBrushKey =
            criticalCount > 0
                ? "NetLoom.Brush.Critical"
                : warningCount > 0
                    ? "NetLoom.Brush.Warning"
                    : "NetLoom.Brush.ShellRailTextMuted";

        ShellAlertCountText.Text =
            UiText.Format(
                "ShellAlertCount",
                rows.Length);
        ShellAlertCountText.SetResourceReference(
            TextBlock.ForegroundProperty,
            alertBrushKey);

        ConfigureShellAlertBadge(
            rows.Length,
            alertBrushKey);

        UpdateShellEquipmentPresentation(
            _lastMapSnapshot);

        if (_alertAutoSelectPending)
        {
            // При первом обновлении окно может быть ещё не размечено — вписываем после раскладки.
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(
                    TryAutoSelectFirstAlert));
        }
    }

    private AlertRow[] CurrentAlertRows()
    {
        return AlertList.Items
            .OfType<AlertRow>()
            .ToArray();
    }

    private void ApplyAlertCardSelection(
        IEnumerable<AlertRow> rows)
    {
        var list =
            rows.ToArray();

        if (_selectedAlertKey != null &&
            !list.Any(
                row =>
                    string.Equals(
                        row.ExpansionKey,
                        _selectedAlertKey,
                        StringComparison.Ordinal)))
        {
            _selectedAlertKey =
                null;
        }

        foreach (var row in list)
        {
            row.IsSelected =
                _selectedAlertKey != null &&
                string.Equals(
                    row.ExpansionKey,
                    _selectedAlertKey,
                    StringComparison.Ordinal);
        }
    }

    // Выбор карточки держится, пока выбрана одна из её связей; выбор другого объекта на карте его снимает.
    private void SynchronizeAlertCardSelectionWithMap()
    {
        if (_selectedAlertKey == null)
        {
            return;
        }

        var rows =
            CurrentAlertRows();

        var selected =
            rows.FirstOrDefault(
                row =>
                    string.Equals(
                        row.ExpansionKey,
                        _selectedAlertKey,
                        StringComparison.Ordinal));

        if (selected == null ||
            !_selectedPhysicalLinkId.HasValue ||
            !selected.PhysicalLinkIds.Contains(
                _selectedPhysicalLinkId.Value))
        {
            _selectedAlertKey =
                null;
        }

        ApplyAlertCardSelection(
            rows);
    }

    private void OnAlertsSectionEntered()
    {
        if (_selectedAlertKey != null &&
            CurrentAlertRows()
                .Any(
                    row =>
                        row.IsSelected))
        {
            return;
        }

        _alertAutoSelectPending =
            true;

        // Карта раздела становится видимой в этом же проходе; участников вписываем после раскладки.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                TryAutoSelectFirstAlert));
    }

    private void TryAutoSelectFirstAlert()
    {
        if (!_alertAutoSelectPending ||
            _shellSection != ShellSection.Alerts)
        {
            return;
        }

        var first =
            CurrentAlertRows()
                .FirstOrDefault(
                    row =>
                        row.PhysicalLinkIds.Length > 0);

        if (first == null ||
            _lastMapSnapshot == null)
        {
            return;
        }

        if (!_mapViewportHeldForAlerts)
        {
            _mapViewportBeforeAlerts =
                CurrentMapViewport();
            _mapViewportHeldForAlerts =
                true;
        }

        // Автоматический выбор без пульсации: выразительная анимация — только по действию оператора (§7).
        ShowAlertRowOnMap(
            first,
            false);
    }

    private MapViewportLayout CurrentMapViewport()
    {
        return new MapViewportLayout(
            _zoom,
            MapVirtualWorkspace
                .ToLogicalPan(
                    MapScrollViewer
                        .HorizontalOffset,
                    _virtualOriginX,
                    _zoom),
            MapVirtualWorkspace
                .ToLogicalPan(
                    MapScrollViewer
                        .VerticalOffset,
                    _virtualOriginY,
                    _zoom));
    }

    // Возвращает вид карты, сменённый автоматическим выбором карточки, и сохраняет его.
    // Возвращает true, если вид был восстановлен.
    private bool RestoreMapViewportAfterAlerts()
    {
        if (!_mapViewportHeldForAlerts ||
            _mapViewportBeforeAlerts == null)
        {
            _mapViewportHeldForAlerts =
                false;
            return false;
        }

        var viewport =
            _mapViewportBeforeAlerts;

        _mapViewportHeldForAlerts =
            false;
        _mapViewportBeforeAlerts =
            null;

        _zoom =
            viewport.Zoom;
        _pendingPanX =
            viewport.PanX;
        _pendingPanY =
            viewport.PanY;

        ApplyZoomTransform();
        UpdateZoomText();
        RestoreViewportOffsets();

        try
        {
            _mapLayoutStore.SaveViewport(
                _mapLayoutId,
                viewport);
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError(
                error.ToString());

            MapStatusText.Text =
                UiText.Get(
                    "MapLayoutSaveFailed");
        }

        return true;
    }

    private void ShowAlertRowOnMap(
        AlertRow row,
        bool pulse)
    {
        _alertAutoSelectPending =
            false;
        _selectedAlertKey =
            row.ExpansionKey;

        SelectAlertPhysicalContext(
            row.PrimaryPhysicalLinkId);

        FocusAlertContextToViewport(
            row.PhysicalLinkIds,
            pulse
                ? () =>
                    PulseAlertContext(
                        row.PhysicalLinkIds)
                : (Action)null);
    }

    private void ConfigureShellAlertBadge(
        int count,
        string brushKey)
    {
        // §8: число на значке входит в имя кнопки рейла — диктор слышит то же, что видно.
        System.Windows.Automation.AutomationProperties.SetName(
            ShellAlertsButton,
            count > 0
                ? UiText.Format(
                    "ShellAlertsButtonNameWithCount",
                    count)
                : UiText.Get(
                    "ShellAlertsSection"));

        if (count <= 0)
        {
            ShellAlertsBadge.Visibility =
                Visibility.Collapsed;
            return;
        }

        var badgeHeight =
            Convert.ToDouble(
                FindResource(
                    "NetLoom.Navigation.BadgeHeight"));
        var isSingleDigit =
            count < 10;

        ShellAlertsBadge.Width =
            isSingleDigit
                ? badgeHeight
                : double.NaN;
        ShellAlertsBadge.Padding =
            (Thickness)FindResource(
                isSingleDigit
                    ? "NetLoom.Thickness.NavigationBadgePaddingSingle"
                    : "NetLoom.Thickness.NavigationBadgePaddingMulti");
        ShellAlertsBadge.SetResourceReference(
            Border.BackgroundProperty,
            brushKey);
        ShellAlertsBadgeText.Text =
            count > 99
                ? UiText.Get(
                    "ShellAlertBadgeOverflow")
                : count.ToString(
                    CultureInfo.CurrentCulture);
        ShellAlertsBadge.Visibility =
            Visibility.Visible;
    }

    private void OnAlertShowOnMapClick(
        object sender,
        RoutedEventArgs e)
    {
        var button =
            sender as Button;
        var row =
            button == null
                ? null
                : button.DataContext as AlertRow;

        if (row == null ||
            row.PhysicalLinkIds.Length == 0)
        {
            return;
        }

        _mapViewportHeldForAlerts =
            false;
        _mapViewportBeforeAlerts =
            null;

        ShowAlertRowOnMap(
            row,
            true);
    }

    private void FocusAlertContextToViewport(
        IReadOnlyCollection<Guid> physicalLinkIds,
        Action completed)
    {
        if (_lastMapSnapshot == null ||
            physicalLinkIds == null ||
            physicalLinkIds.Count == 0)
        {
            FocusSelectedMapAtNativeZoom(
                completed);
            return;
        }

        if (physicalLinkIds.Count == 1)
        {
            FocusSelectedMapAtNativeZoom(
                completed);
            return;
        }

        var linkIds =
            new HashSet<Guid>(
                physicalLinkIds);
        var endpointKeys =
            new HashSet<string>(
                _lastMapSnapshot.Links
                    .Where(
                        item =>
                            item.PhysicalLinkId.HasValue &&
                            linkIds.Contains(
                                item.PhysicalLinkId.Value))
                    .SelectMany(
                        item =>
                            new[]
                            {
                                item.SourceNodeKey,
                                item.TargetNodeKey
                            }),
                StringComparer.Ordinal);

        var bounds =
            new List<Rect>();

        foreach (var node in
                 _lastMapSnapshot.Nodes
                     .Where(
                         item =>
                             endpointKeys.Contains(
                                 item.Key)))
        {
            MapNodeVisual visual;

            if (_nodeVisualsByIdentity.TryGetValue(
                    NodeIdentity(
                        node),
                    out visual))
            {
                bounds.Add(
                    NodeBounds(
                        visual));
            }
        }

        if (!TryFitMapBoundsToViewport(
                bounds))
        {
            FocusSelectedMapAtNativeZoom(
                completed);
            return;
        }

        if (_zoom > 1.0)
        {
            _zoom = 1.0;
            ApplyZoomTransform();
            UpdateZoomText();
        }

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(
                () =>
                    completed?.Invoke()));
    }

    private void PulseAlertContext(
        IReadOnlyCollection<Guid> physicalLinkIds)
    {
        if (physicalLinkIds == null ||
            physicalLinkIds.Count == 0)
        {
            return;
        }

        var linkIds =
            new HashSet<Guid>(
                physicalLinkIds);

        foreach (var linkVisual in
                 _linkVisualsByIdentity.Values
                     .Where(
                         item =>
                             item.Line.Tag is Guid &&
                             linkIds.Contains(
                                 (Guid)item.Line.Tag)))
        {
            AnimatePulse(
                linkVisual.Line,
                MapMotionKind.AlertPulse,
                1.0,
                3);

            AnimatePulse(
                linkVisual.Label,
                MapMotionKind.AlertPulse,
                1.0,
                3);
        }

        if (_lastMapSnapshot == null)
        {
            return;
        }

        var endpointKeys =
            new HashSet<string>(
                _lastMapSnapshot.Links
                    .Where(
                        item =>
                            item.PhysicalLinkId.HasValue &&
                            linkIds.Contains(
                                item.PhysicalLinkId.Value))
                    .SelectMany(
                        item =>
                            new[]
                            {
                                item.SourceNodeKey,
                                item.TargetNodeKey
                            }),
                StringComparer.Ordinal);

        foreach (var node in
                 _lastMapSnapshot.Nodes
                     .Where(
                         item =>
                             endpointKeys.Contains(
                                 item.Key) &&
                             item.DeviceId.HasValue))
        {
            AnimateDiscoveryFocus(
                node.DeviceId.Value);
        }
    }

    private sealed class AlertEventState
    {
        private AlertEventState(
            string alertKey,
            string title,
            string scope,
            string reason,
            Guid[] physicalLinkIds,
            bool isCritical)
        {
            AlertKey = alertKey;
            Title = title;
            Scope = scope;
            Reason = reason;
            PhysicalLinkIds =
                physicalLinkIds ??
                new Guid[0];
            IsCritical = isCritical;
        }

        public string AlertKey { get; }

        public string Title { get; }

        public string Scope { get; }

        public string Reason { get; }

        public Guid[] PhysicalLinkIds { get; }

        public bool IsCritical { get; }

        public static AlertEventState FromAlert(
            TopologyAlert alert,
            string scope,
            string reason)
        {
            return new AlertEventState(
                alert.AlertKey,
                AlertKindText(
                    alert.Kind),
                scope,
                reason,
                alert.PhysicalLinkIds.ToArray(),
                alert.Severity ==
                    TopologyAlertSeverity.Critical);
        }
    }

    private sealed class ShellEventRow
    {
        public ShellEventRow(
            string alertKey,
            string timeText,
            string title,
            string scope,
            Guid[] physicalLinkIds,
            bool isCritical,
            bool isWarning,
            bool isResolved)
        {
            AlertKey = alertKey;
            TimeText = timeText;
            Title = title;
            Scope = scope;
            PhysicalLinkIds =
                physicalLinkIds ??
                new Guid[0];
            IsCritical = isCritical;
            IsWarning = isWarning;
            IsResolved = isResolved;
        }

        public string AlertKey { get; }

        public string TimeText { get; }

        public string Title { get; }

        public string Scope { get; }

        public Guid[] PhysicalLinkIds { get; }

        public bool IsCritical { get; }

        public bool IsWarning { get; }

        public bool IsResolved { get; }
    }

    private sealed class AlertRow :
        INotifyPropertyChanged
    {
        private bool _isSelected;

        public AlertRow(
            string expansionKey,
            string severityText,
            string title,
            string scope,
            string reason,
            string timeText,
            string technicalDetails,
            Guid[] physicalLinkIds,
            bool isCritical,
            bool isTechnicalDetailsExpanded,
            string showOnMapText,
            string technicalDetailsLabel)
        {
            ExpansionKey =
                expansionKey ??
                string.Empty;
            SeverityText =
                severityText;
            Title =
                title;
            Scope =
                scope;
            Reason =
                reason;
            TimeText =
                timeText;
            TechnicalDetails =
                technicalDetails;
            PhysicalLinkIds =
                physicalLinkIds ??
                new Guid[0];
            PrimaryPhysicalLinkId =
                PhysicalLinkIds.Length == 0
                    ? Guid.Empty
                    : PhysicalLinkIds[0];
            IsCritical =
                isCritical;
            IsTechnicalDetailsExpanded =
                isTechnicalDetailsExpanded;
            ShowOnMapText =
                showOnMapText;
            TechnicalDetailsLabel =
                technicalDetailsLabel;
        }

        public string ExpansionKey { get; }

        public string SeverityText { get; }

        public string Title { get; }

        public string Scope { get; }

        public string Reason { get; }

        public string TimeText { get; }

        public string TechnicalDetails { get; }

        public Guid[] PhysicalLinkIds { get; }

        public Guid PrimaryPhysicalLinkId { get; }

        public bool IsCritical { get; }

        public bool IsTechnicalDetailsExpanded { get; set; }

        public string ShowOnMapText { get; }

        public string TechnicalDetailsLabel { get; }

        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }

            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected =
                    value;

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(
                        nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private sealed class EmptyTopologyAlertSnapshotProvider :
        ITopologyAlertSnapshotProvider
    {
        public TopologyAlertSnapshot GetSnapshot(
            string instanceId)
        {
            return new TopologyAlertSnapshot(
                DateTime.UtcNow,
                instanceId,
                new TopologyAlert[0]);
        }
    }
}
