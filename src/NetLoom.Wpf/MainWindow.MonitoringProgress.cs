using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using NetLoom.Application.MonitoringControl;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf;

// Sprint 47: видимый прогресс цикла опроса. Расширяет постоянное состояние мониторинга в верхней строке
// (ADR-083 п. 2), отдельного раздела или панели не возвращает. Подробности — по нажатию на состояние.
public partial class MainWindow
{
    // Сколько имён показывать в «Сейчас опрашивается» и «Не ответили»; остальные — «и ещё N».
    private const int MonitoringCycleNamesShown = 3;

    // Последний завершённый цикл, уже записанный в ленту событий: (номер, время завершения).
    private Tuple<int, DateTime> _shellCycleEventKey;

    // Предупреждения схемы без предупреждений опроса; к ним добавляется «Устройство не отвечает».
    private NetLoom.Contracts.Alerts.TopologyAlertSnapshot _lastTopologyAlertSnapshot;

    // Устройства, о которых уже показано «Устройство не отвечает» (ключ набора — для пересчёта только при изменении).
    private string _unreachableAlertDevicesKey = string.Empty;

    private void UpdateShellMonitoringProgress(
        MonitoringControlSnapshot snapshot,
        string stateText)
    {
        var cycle =
            ActiveMonitoringCycle(
                snapshot);

        ShellMonitoringHeaderText.Text =
            cycle == null
                ? UiText.Format(
                    "ShellMonitoringHeader",
                    stateText)
                : UiText.Format(
                    "ShellMonitoringCycleHeader",
                    cycle.Done,
                    cycle.TotalTargets);

        // Видимая подпись входит в имя кнопки (§8).
        AutomationProperties.SetName(
            ShellMonitoringDetailsButton,
            ShellMonitoringHeaderText.Text);

        ApplyCycleProgress(
            ShellMonitoringProgressBar,
            cycle);

        UpdateShellMonitoringDetails(
            snapshot,
            cycle);

        AddShellCycleEventIfCompleted(
            snapshot);

        ApplyMonitoringAlertsIfChanged(
            snapshot);

        RefreshInspectorOnSelectedOutcome(
            snapshot);
    }

    // Итог последнего опроса выбранного устройства, уже показанный в инспекторе.
    private string _selectedDeviceOutcomeKey;

    // Новый опрос выбранного устройства обновляет в инспекторе доступность (ICMP, SNMP, TCP);
    // Без нового итога инспектор не пересоздаётся (§5).
    private void RefreshInspectorOnSelectedOutcome(
        MonitoringControlSnapshot snapshot)
    {
        if (!_selectedDeviceId.HasValue ||
            _lastDiagnosticSnapshot == null)
        {
            _selectedDeviceOutcomeKey = null;
            return;
        }

        var outcome =
            snapshot.TargetOutcomes
                .FirstOrDefault(
                    item =>
                        item.DeviceId ==
                        _selectedDeviceId.Value);

        var key =
            _selectedDeviceId.Value.ToString("N") +
            "|" +
            snapshot.State +
            "|" +
            (outcome == null
                ? "-"
                : outcome.LastAttemptUtc.Ticks +
                  "|" +
                  outcome.LastAttemptSucceeded);

        if (string.Equals(
                key,
                _selectedDeviceOutcomeKey,
                StringComparison.Ordinal))
        {
            return;
        }

        var firstSeen =
            _selectedDeviceOutcomeKey == null;

        _selectedDeviceOutcomeKey =
            key;

        if (!firstSeen)
        {
            ShowSelectedDiagnostic();
        }
    }

    private NetLoom.Contracts.Alerts.TopologyAlertSnapshot MergeMonitoringAlerts(
        MonitoringControlSnapshot snapshot)
    {
        var merged =
            NetLoom.Application.Alerts.MonitoringAlertProjection.Merge(
                _lastTopologyAlertSnapshot,
                snapshot.TargetOutcomes);

        _unreachableAlertDevicesKey =
            UnreachableDevicesKey(
                merged);

        return merged;
    }

    private static string UnreachableDevicesKey(
        NetLoom.Contracts.Alerts.TopologyAlertSnapshot snapshot)
    {
        return string.Join(
            ",",
            snapshot.Alerts
                .Where(
                    alert =>
                        alert.Kind ==
                        NetLoom.Contracts.Alerts.TopologyAlertKind.DeviceUnreachable)
                .Select(
                    alert => alert.AlertKey)
                .OrderBy(
                    key => key,
                    StringComparer.Ordinal));
    }

    // Sprint 47 (Г3): устройство перестало или снова начало отвечать — пересобираем общий снимок
    // Предупреждений: список, счётчик, лента, «Оборудование», карточка на карте и инспектор.
    private void ApplyMonitoringAlertsIfChanged(
        MonitoringControlSnapshot snapshot)
    {
        if (_lastTopologyAlertSnapshot == null)
        {
            return;
        }

        var candidate =
            NetLoom.Application.Alerts.MonitoringAlertProjection.Merge(
                _lastTopologyAlertSnapshot,
                snapshot.TargetOutcomes);

        if (string.Equals(
                UnreachableDevicesKey(
                    candidate),
                _unreachableAlertDevicesKey,
                StringComparison.Ordinal))
        {
            return;
        }

        _unreachableAlertDevicesKey =
            UnreachableDevicesKey(
                candidate);

        _lastAlertSnapshot =
            candidate;

        if (_lastMapSnapshot != null)
        {
            foreach (var node in
                _lastMapSnapshot.Nodes)
            {
                MapNodeVisual visual;

                if (_nodeVisualsByIdentity.TryGetValue(
                        NodeIdentity(node),
                        out visual))
                {
                    ApplyNodeDegradationPresentation(
                        visual,
                        node.DeviceId);
                }
            }
        }

        ShowSelectedDiagnostic();

        ShowAlerts(
            candidate,
            _alertTransitionTracker.Observe(
                candidate));
    }

    private MonitoringTargetOutcome MonitoringOutcome(
        Guid deviceId)
    {
        return _monitoringControl.Current.TargetOutcomes
            .FirstOrDefault(
                outcome =>
                    outcome.DeviceId ==
                    deviceId);
    }

    // «Опросов подряд без ответа: 2 · последний ответ: 5 мин назад». Последний ответ — из сеанса,
    // Иначе из базы (время последних данных устройства).
    private string DeviceUnreachableReason(
        Guid deviceId)
    {
        var outcome =
            MonitoringOutcome(
                deviceId);

        var device =
            _lastDiagnosticSnapshot == null
                ? null
                : _lastDiagnosticSnapshot.Devices
                    .FirstOrDefault(
                        item =>
                            item.DeviceId ==
                            deviceId);

        var lastResponseUtc =
            outcome != null &&
            outcome.LastSuccessUtc.HasValue
                ? outcome.LastSuccessUtc
                : device == null
                    ? null
                    : device.LastSeenUtc;

        var reason =
            UiText.Format(
                "AlertReasonDeviceUnreachable",
                outcome == null
                    ? NetLoom.Application.Alerts.MonitoringAlertProjection.ConsecutiveFailuresForAlert
                    : outcome.ConsecutiveFailures,
                lastResponseUtc.HasValue
                    ? RelativeTimeText(
                        lastResponseUtc)
                    : UiText.Get(
                        "AlertLastResponseUnknown"));

        // Подсказка, где искать: устройство живо, но SNMP молчит — или его нет в сети вовсе.
        var icmp =
            outcome == null ||
            outcome.Availability == null
                ? null
                : outcome.Availability.IcmpReachable;

        return icmp.HasValue
            ? reason +
              " · " +
              UiText.Get(
                  icmp.Value
                      ? "AlertReasonIcmpAnswers"
                      : "AlertReasonIcmpSilent")
            : reason;
    }

    // Sprint 47: «Доступен / Частично доступен / Недоступен» (словарь ТЗ §10) по последнему опросу сеанса.
    // Только пока мониторинг работает: после остановки действует строка «Мониторинг остановлен».
    private bool TryGetSessionAvailabilityState(
        Guid deviceId,
        out string text)
    {
        text = null;

        var state =
            _monitoringControl.Current.State;

        if (state !=
                MonitoringControlState.Running &&
            state !=
                MonitoringControlState.Polling)
        {
            return false;
        }

        var outcome =
            MonitoringOutcome(
                deviceId);

        if (outcome == null ||
            outcome.LastAttemptSkipped)
        {
            return false;
        }

        var icmp =
            outcome.Availability == null
                ? null
                : outcome.Availability.IcmpReachable;

        text =
            outcome.LastAttemptSucceeded
                ? UiText.Format(
                    "InspectorAvailabilityAvailable",
                    RelativeTimeText(
                        outcome.LastAttemptUtc))
                : icmp == true
                    ? UiText.Get(
                        "InspectorAvailabilityPartial")
                    : UiText.Get(
                        "InspectorAvailabilityUnavailable");

        return true;
    }

    private IEnumerable<DiagnosticFieldRow> MonitoringAvailabilityFields(
        Guid deviceId)
    {
        var outcome =
            MonitoringOutcome(
                deviceId);

        if (outcome == null ||
            outcome.LastAttemptSkipped)
        {
            yield break;
        }

        var availability =
            outcome.Availability;

        yield return new DiagnosticFieldRow(
            UiText.Get(
                "DiagnosticFieldIcmp"),
            availability == null ||
            !availability.IcmpReachable.HasValue
                ? UiText.Get(
                    "AvailabilityNotChecked")
                : UiText.Get(
                    availability.IcmpReachable.Value
                        ? "AvailabilityAnswers"
                        : "AvailabilitySilent"));

        yield return new DiagnosticFieldRow(
            UiText.Get(
                "DiagnosticFieldSnmp"),
            UiText.Get(
                outcome.LastAttemptSucceeded
                    ? "AvailabilityAnswers"
                    : "AvailabilitySilent"));

        if (availability == null ||
            availability.CheckedTcpPorts.Count == 0)
        {
            yield return new DiagnosticFieldRow(
                UiText.Get(
                    "DiagnosticFieldTcp"),
                UiText.Get(
                    "AvailabilityNotChecked"));
            yield break;
        }

        var closed =
            availability.CheckedTcpPorts
                .Except(
                    availability.OpenTcpPorts)
                .ToArray();

        var parts =
            new List<string>();

        if (availability.OpenTcpPorts.Count > 0)
        {
            parts.Add(
                UiText.Format(
                    "AvailabilityTcpOpen",
                    string.Join(
                        ", ",
                        availability.OpenTcpPorts)));
        }

        if (closed.Length > 0)
        {
            parts.Add(
                UiText.Format(
                    "AvailabilityTcpClosed",
                    string.Join(
                        ", ",
                        closed)));
        }

        yield return new DiagnosticFieldRow(
            UiText.Get(
                "DiagnosticFieldTcp"),
            string.Join(
                " · ",
                parts));
    }

    // Цикл, который идёт прямо сейчас: начат, не завершён, мониторинг работает или опрашивает.
    private static MonitoringCycleProgress ActiveMonitoringCycle(
        MonitoringControlSnapshot snapshot)
    {
        var cycle =
            snapshot.CurrentCycle;

        var working =
            snapshot.State ==
                MonitoringControlState.Running ||
            snapshot.State ==
                MonitoringControlState.Polling;

        return working &&
               cycle != null &&
               cycle.StartedUtc.HasValue &&
               !cycle.IsCompleted
            ? cycle
            : null;
    }

    private static void ApplyCycleProgress(
        System.Windows.Controls.ProgressBar bar,
        MonitoringCycleProgress cycle)
    {
        if (cycle == null)
        {
            bar.Visibility =
                Visibility.Collapsed;
            return;
        }

        bar.Maximum =
            cycle.TotalTargets;
        bar.Value =
            cycle.Done;
        bar.Visibility =
            Visibility.Visible;
    }

    private void UpdateShellMonitoringDetails(
        MonitoringControlSnapshot snapshot,
        MonitoringCycleProgress activeCycle)
    {
        // Без идущего цикла показываем итог последнего завершённого — в том числе после остановки.
        var shownCycle =
            activeCycle ??
            snapshot.LastCompletedCycle;

        ShellMonitoringDetailsTitleText.Text =
            UiText.Get(
                MonitoringDetailsTitleKey(
                    snapshot,
                    activeCycle));

        ShellMonitoringDetailsLastCycleText.Text =
            UiText.Get(
                "ShellMonitoringDetailsLastCycle");

        ShellMonitoringDetailsEmptyText.Text =
            UiText.Get(
                "ShellMonitoringDetailsNoCycles");

        ShellMonitoringDetailsLastCycleText.Visibility =
            activeCycle == null &&
            shownCycle != null &&
            snapshot.State !=
                MonitoringControlState.Running &&
            snapshot.State !=
                MonitoringControlState.Polling
                ? Visibility.Visible
                : Visibility.Collapsed;

        ShellMonitoringDetailsEmptyText.Visibility =
            shownCycle == null
                ? Visibility.Visible
                : Visibility.Collapsed;

        ApplyCycleProgress(
            ShellMonitoringDetailsProgressBar,
            activeCycle);

        ShellMonitoringCurrentPanel.Visibility =
            activeCycle == null
                ? Visibility.Collapsed
                : Visibility.Visible;

        ShellMonitoringCurrentLabelText.Text =
            UiText.Get(
                "MonitoringCycleFieldCurrent");

        ShellMonitoringCurrentValueText.Text =
            activeCycle == null ||
            activeCycle.InProgress.Count == 0
                ? UiText.Get(
                    "DiagnosticValueAbsent")
                : NamesWithRest(
                    activeCycle.InProgress
                        .Select(
                            target =>
                                UiText.Format(
                                    "MonitoringCycleCurrentDevice",
                                    MonitoringDeviceName(
                                        target.DeviceId,
                                        target.TargetAddress == null
                                            ? null
                                            : target.TargetAddress.ToString()),
                                    target.TargetAddress))
                        .ToArray());

        ShellMonitoringDetailsFieldsList.ItemsSource =
            shownCycle == null
                ? new DiagnosticFieldRow[0]
                : MonitoringCycleFields(
                    snapshot,
                    shownCycle,
                    activeCycle != null);

        var schedulerRunning =
            (snapshot.State ==
                 MonitoringControlState.Running ||
             snapshot.State ==
                 MonitoringControlState.Polling) &&
            !_monitoringStandalonePollActive;

        var stopped =
            snapshot.State ==
                MonitoringControlState.Stopped ||
            snapshot.State ==
                MonitoringControlState.Faulted;

        // Действие по состоянию (Sprint 47): при работающем расписании — внеочередной цикл,
        // При остановленном мониторинге — опрос выбранного устройства. Запуск и остановка — без действия.
        ShellMonitoringCycleActionButton.Content =
            UiText.Get(
                schedulerRunning
                    ? "MonitoringCycleRunNowAction"
                    : "MonitoringPollSelectedAction");

        ShellMonitoringCycleActionButton.Visibility =
            schedulerRunning || stopped
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private static string MonitoringDetailsTitleKey(
        MonitoringControlSnapshot snapshot,
        MonitoringCycleProgress activeCycle)
    {
        switch (snapshot.State)
        {
            case MonitoringControlState.Starting:
                return "ShellMonitoringDetailsTitleStarting";

            case MonitoringControlState.Stopping:
                return "ShellMonitoringDetailsTitleStopping";

            case MonitoringControlState.Faulted:
                return "ShellMonitoringDetailsTitleFaulted";

            case MonitoringControlState.Running:
            case MonitoringControlState.Polling:
                return activeCycle != null
                    ? "ShellMonitoringDetailsTitleRunning"
                    : snapshot.LastCompletedCycle != null
                        ? "ShellMonitoringDetailsTitleWaiting"
                        : "ShellMonitoringDetailsTitleStarted";

            default:
                return "ShellMonitoringDetailsTitleStopped";
        }
    }

    private DiagnosticFieldRow[] MonitoringCycleFields(
        MonitoringControlSnapshot snapshot,
        MonitoringCycleProgress cycle,
        bool isActive)
    {
        var rows =
            new List<DiagnosticFieldRow>();

        rows.Add(
            CountField(
                "MonitoringCycleFieldSucceeded",
                cycle.Succeeded));
        rows.Add(
            CountField(
                "MonitoringCycleFieldFailed",
                cycle.Failed));

        if (cycle.Skipped > 0)
        {
            rows.Add(
                CountField(
                    "MonitoringCycleFieldSkipped",
                    cycle.Skipped));
        }

        if (isActive)
        {
            rows.Add(
                CountField(
                    "MonitoringCycleFieldRemaining",
                    cycle.Remaining));
        }

        rows.Add(
            new DiagnosticFieldRow(
                UiText.Get(
                    "MonitoringCycleFieldStarted"),
                LocalMonitoringTime(
                    cycle.StartedUtc)));
        rows.Add(
            new DiagnosticFieldRow(
                UiText.Get(
                    "MonitoringCycleFieldCompleted"),
                cycle.CompletedUtc.HasValue
                    ? LocalMonitoringTime(
                        cycle.CompletedUtc)
                    : UiText.Get(
                        "DiagnosticValueAbsent")));

        // Кто не ответил на последнюю попытку — без этого «Ошибки: 2» не говорит, где искать.
        var notAnswered =
            snapshot.TargetOutcomes
                .Where(
                    outcome =>
                        !outcome.LastAttemptSucceeded &&
                        !outcome.LastAttemptSkipped)
                .Select(
                    outcome =>
                        MonitoringDeviceName(
                            outcome.DeviceId,
                            outcome.TargetAddress == null
                                ? null
                                : outcome.TargetAddress.ToString()))
                .ToArray();

        if (notAnswered.Length > 0)
        {
            rows.Add(
                new DiagnosticFieldRow(
                    UiText.Get(
                        "MonitoringCycleFieldNotAnswered"),
                    NamesWithRest(
                        notAnswered)));
        }

        return rows.ToArray();
    }

    private static DiagnosticFieldRow CountField(
        string labelKey,
        int value)
    {
        return new DiagnosticFieldRow(
            UiText.Get(
                labelKey),
            value.ToString(
                System.Globalization.CultureInfo.CurrentCulture));
    }

    private static string NamesWithRest(
        string[] names)
    {
        var shown =
            string.Join(
                ", ",
                names.Take(
                    MonitoringCycleNamesShown));

        return names.Length <= MonitoringCycleNamesShown
            ? shown
            : UiText.Format(
                "MonitoringCycleNamesMore",
                shown,
                names.Length -
                MonitoringCycleNamesShown);
    }

    // Имя устройства из последних данных схемы; если устройства там нет — адрес.
    private string MonitoringDeviceName(
        Guid deviceId,
        string address)
    {
        var device =
            _lastDiagnosticSnapshot == null
                ? null
                : _lastDiagnosticSnapshot.Devices
                    .FirstOrDefault(
                        item =>
                            item.DeviceId ==
                            deviceId);

        return device != null &&
               !string.IsNullOrWhiteSpace(
                   device.DisplayName)
            ? device.DisplayName
            : address ??
              UiText.Get(
                  "DiagnosticValueAbsent");
    }

    // Событие «Цикл опроса завершён» в нижней ленте. Хранится одно — последнее: лента не забивается
    // Циклами каждые несколько минут и не вытесняет события предупреждений.
    private void AddShellCycleEventIfCompleted(
        MonitoringControlSnapshot snapshot)
    {
        var cycle =
            snapshot.LastCompletedCycle;

        if (cycle == null ||
            !cycle.CompletedUtc.HasValue)
        {
            return;
        }

        var key =
            Tuple.Create(
                cycle.CycleNumber,
                cycle.CompletedUtc.Value);

        if (key.Equals(
                _shellCycleEventKey))
        {
            return;
        }

        _shellCycleEventKey =
            key;

        _shellEventRows.RemoveAll(
            row => row.IsMonitoringCycle);

        _shellEventRows.Insert(
            0,
            ShellEventRow.MonitoringCycle(
                cycle.CompletedUtc.Value
                    .ToLocalTime()
                    .ToString(
                        "HH:mm",
                        System.Globalization.CultureInfo.CurrentCulture),
                UiText.Get(
                    "ShellEventCycleCompletedTitle"),
                cycle.Skipped > 0
                    ? UiText.Format(
                        "ShellEventCycleCompletedScopeSkipped",
                        cycle.Succeeded,
                        cycle.Failed,
                        cycle.Skipped)
                    : UiText.Format(
                        "ShellEventCycleCompletedScope",
                        cycle.Succeeded,
                        cycle.Failed)));

        RenderShellEventList();
    }

    private void OnShellMonitoringDetailsClick(
        object sender,
        RoutedEventArgs e)
    {
        ShowShellMonitoringDetails();
    }

    private void ShowShellMonitoringDetails()
    {
        UpdateShellMonitoringDetails(
            _monitoringControl.Current,
            ActiveMonitoringCycle(
                _monitoringControl.Current));

        ShellMonitoringDetailsPopup.IsOpen = true;

        // §8: при открытии фокус внутри; Tab ходит по кругу, Esc закрывает и возвращает фокус.
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new Action(
                () =>
                {
                    if (ShellMonitoringCycleActionButton.IsVisible)
                    {
                        ShellMonitoringCycleActionButton.Focus();
                    }
                    else
                    {
                        ShellMonitoringDetailsPanel.Focus();
                    }
                }));
    }

    private void OnShellMonitoringDetailsPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        ShellMonitoringDetailsPopup.IsOpen = false;
        ShellMonitoringDetailsButton.Focus();
        e.Handled = true;
    }

    private void OnShellMonitoringCycleActionClick(
        object sender,
        RoutedEventArgs e)
    {
        // Тот же путь, что «Опросить сейчас»: при работающем расписании — POLL_NOW для всего набора,
        // Без мониторинга — разовый опрос выбранного устройства. Сообщение об ошибке видно в панели.
        OnMonitoringPollNowClick(
            sender,
            e);
    }
}
