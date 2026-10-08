using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Application.Locations;
using NetLoom.Wpf.Discovery;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.Shell;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private IDiscoveryInboxActions _discoveryInboxActions;
        private Button _discoveryInboxCompletedButton;
        private bool _discoveryInboxActionRunning;

        public static readonly DependencyProperty HasDiscoveryInboxActionsProperty =
            DependencyProperty.Register(nameof(HasDiscoveryInboxActions), typeof(bool),
                typeof(MainWindow), new PropertyMetadata(false));

        public bool HasDiscoveryInboxActions
        {
            get => (bool)GetValue(HasDiscoveryInboxActionsProperty);
            private set => SetValue(HasDiscoveryInboxActionsProperty, value);
        }

        public IDiscoveryInboxActions DiscoveryInboxActions
        {
            get => _discoveryInboxActions;
            set
            {
                _discoveryInboxActions = value;
                HasDiscoveryInboxActions = value != null;
                if (DiscoveryInboxGroupsList != null) RefreshDiscoveryInbox();
            }
        }

        private DiscoveryInboxRow[] SelectedDiscoveryInboxRows() =>
            DiscoveryInboxGroupsList.Items.Cast<DiscoveryInboxGroup>()
                .SelectMany(group => group.Rows).Where(row => row.IsSelected).ToArray();

        private void OnDiscoveryInboxSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DiscoveryInboxRow.IsSelected))
                UpdateDiscoveryInboxActionsBar();
        }

        private void UpdateDiscoveryInboxActionsBar()
        {
            var rows = SelectedDiscoveryInboxRows();
            DiscoveryInboxActionsBar.Visibility = HasDiscoveryInboxActions &&
                DiscoveryInboxGroupsList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            DiscoveryInboxSelectedText.Text = UiText.Format("DiscoveryInboxSelectedCount", rows.Length);
            // Итог прошлого действия относится к прошлому выбору: новый выбор его убирает.
            if (rows.Length > 0) ShowDiscoveryInboxResult(null);
            SetDiscoveryInboxButtonState(DiscoveryInboxAcceptButton, DiscoveryInboxAction.Accept, rows);
            SetDiscoveryInboxButtonState(DiscoveryInboxIgnoreButton, DiscoveryInboxAction.Ignore, rows);
            SetDiscoveryInboxButtonState(DiscoveryInboxUnmanagedButton, DiscoveryInboxAction.MarkUnmanaged, rows);
            SetDiscoveryInboxButtonState(DiscoveryInboxPlacementButton, DiscoveryInboxAction.AssignPlacement, rows);
        }

        private void SetDiscoveryInboxButtonState(Button button, DiscoveryInboxAction action,
            IEnumerable<DiscoveryInboxRow> rows)
        {
            // §8: завершившая действие кнопка сохраняет фокус до перехода оператора, но уже неактивна
            // (вид и UI Automation — InertState). Повтор без выбора отсеивается обработчиком;
            // После ухода фокуса кнопка отключается обычным образом.
            var applicable = rows.Any(row =>
                NetLoom.Application.DiscoveryInbox.DiscoveryInboxActions.CanApply(action, row.Result));
            var keepsFocus = button == _discoveryInboxCompletedButton;
            button.IsEnabled = !_discoveryInboxActionRunning && DiscoveryInboxCanRetry &&
                (applicable || keepsFocus);
            InertState.SetIsInert(button, keepsFocus && !applicable);
        }

        private async void OnDiscoveryInboxAcceptClick(object sender, RoutedEventArgs e) =>
            await ApplyDiscoveryInboxActionAsync(DiscoveryInboxAction.Accept, sender as Button);
        private async void OnDiscoveryInboxIgnoreClick(object sender, RoutedEventArgs e) =>
            await ApplyDiscoveryInboxActionAsync(DiscoveryInboxAction.Ignore, sender as Button);
        private async void OnDiscoveryInboxUnmanagedClick(object sender, RoutedEventArgs e) =>
            await ApplyDiscoveryInboxActionAsync(DiscoveryInboxAction.MarkUnmanaged, sender as Button);
        private async void OnDiscoveryInboxPlacementClick(object sender, RoutedEventArgs e) =>
            await ApplyDiscoveryInboxActionAsync(DiscoveryInboxAction.AssignPlacement, sender as Button);

        private async Task ApplyDiscoveryInboxActionAsync(DiscoveryInboxAction action, Button button)
        {
            var rows = SelectedDiscoveryInboxRows();
            if (DiscoveryInboxActions == null || _discoveryInboxActionRunning || !DiscoveryInboxCanRetry ||
                !rows.Any(row => NetLoom.Application.DiscoveryInbox.DiscoveryInboxActions.CanApply(action, row.Result))) return;
            Guid? placement = null;
            if (action == DiscoveryInboxAction.AssignPlacement)
            {
                try
                {
                    var dialog = CreateDiscoveryInboxPlacementDialog();
                    if (dialog.ShowDialog() != true)
                    {
                        button?.Focus();
                        return;
                    }
                    placement = (Guid?)dialog.Tag;
                    if (!placement.HasValue) return;
                }
                catch (Exception error)
                {
                    ShowDiscoveryActionFailure(error);
                    button?.Focus();
                    return;
                }
            }
            _discoveryInboxActionRunning = true;
            _discoveryInboxCompletedButton = null;
            UpdateDiscoveryInboxActionsBar();
            try
            {
                var addresses = rows.Select(row => row.Address).ToArray();
                var runId = rows[0].RunId;
                var now = DiscoveryRunClock();
                DiscoveryInboxActionResult result;
                switch (action)
                {
                    case DiscoveryInboxAction.Accept:
                        result = DiscoveryInboxActions.Accept(runId, addresses, now); break;
                    case DiscoveryInboxAction.Ignore:
                        result = DiscoveryInboxActions.Ignore(runId, addresses, now); break;
                    case DiscoveryInboxAction.MarkUnmanaged:
                        result = DiscoveryInboxActions.MarkUnmanaged(runId, addresses, now); break;
                    default:
                        result = DiscoveryInboxActions.AssignPlacement(runId, addresses, now, placement.Value); break;
                }
                foreach (var row in rows) row.IsSelected = false;
                RefreshDiscoveryInbox();
                await RefreshTopologyAsync();
                RefreshDiscoveryInbox();
                DiscoveryMessageText.Text = string.Empty;
                ShowDiscoveryInboxResult(DiscoveryInboxActionMessage(action, result));
            }
            catch (Exception error) { ShowDiscoveryActionFailure(error); }
            finally
            {
                _discoveryInboxActionRunning = false;
                RestoreDiscoveryInboxActionFocus(button);
            }
        }

        private void ShowDiscoveryInboxResult(string message)
        {
            DiscoveryInboxResultText.Text = message ?? string.Empty;
            DiscoveryInboxResultText.Visibility = string.IsNullOrEmpty(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private string DiscoveryInboxActionMessage(DiscoveryInboxAction action, DiscoveryInboxActionResult result)
        {
            var key = "DiscoveryInboxApplied" + action;
            return result.Skipped == 0 ? UiText.Format(key, result.Applied) :
                UiText.Format(key + "WithSkipped", result.Applied, result.Skipped);
        }

        private void RestoreDiscoveryInboxActionFocus(Button button)
        {
            _discoveryInboxCompletedButton = button;
            UpdateDiscoveryInboxActionsBar();
            if (button == null || !button.IsVisible) return;
            button.LostKeyboardFocus -= OnDiscoveryInboxActionLostFocus;
            button.LostKeyboardFocus += OnDiscoveryInboxActionLostFocus;
            button.BringIntoView();
            button.Focus();
            Keyboard.Focus(button);
        }

        private void OnDiscoveryInboxActionLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_discoveryInboxCompletedButton != sender) return;
            _discoveryInboxCompletedButton = null;
            ((Button)sender).LostKeyboardFocus -= OnDiscoveryInboxActionLostFocus;
            UpdateDiscoveryInboxActionsBar();
        }

        private async Task UndoDiscoveryInboxIgnoreAsync(DiscoveryInboxRow row)
        {
            if (DiscoveryInboxActions == null || _discoveryInboxActionRunning) return;
            _discoveryInboxActionRunning = true;
            UpdateDiscoveryInboxActionsBar();
            try
            {
                var result = DiscoveryInboxActions.UndoIgnore(row.RunId, row.Address, DiscoveryRunClock());
                RefreshDiscoveryInbox();
                await RefreshTopologyAsync();
                RefreshDiscoveryInbox();
                DiscoveryMessageText.Text = string.Empty;
                ShowDiscoveryInboxResult(UiText.Format("DiscoveryInboxIgnoreUndone", result.Applied));
                RestoreDiscoveryInboxFocus(row.Address);
            }
            catch (Exception error) { ShowDiscoveryActionFailure(error); }
            finally
            {
                _discoveryInboxActionRunning = false;
                UpdateDiscoveryInboxActionsBar();
            }
        }

        private string DiscoveryInboxPlacementPath(Guid? deviceId)
        {
            if (!deviceId.HasValue) return null;
            var snapshot = _locationTopologyService.GetSnapshot();
            var locationId = snapshot.Devices.FirstOrDefault(device => device.DeviceId == deviceId)?.LocationId;
            return locationId.HasValue ? DiscoveryInboxLocationPath(snapshot, locationId.Value) : null;
        }

        private static string DiscoveryInboxLocationPath(LocationTopologySnapshot snapshot, Guid locationId)
        {
            var locations = snapshot.Locations.ToDictionary(location => location.Id);
            var names = new List<string>();
            var visited = new HashSet<Guid>();
            LocationTopologyLocation current;
            while (visited.Add(locationId) && locations.TryGetValue(locationId, out current))
            {
                names.Add(current.Name);
                if (!current.ParentLocationId.HasValue) break;
                locationId = current.ParentLocationId.Value;
            }
            names.Reverse();
            return string.Join(UiText.Get("DiscoveryInboxPlacementSeparator"), names);
        }

        internal Window CreateDiscoveryInboxPlacementDialog(LocationTopologySnapshot placements = null)
        {
            var snapshot = placements ?? _locationTopologyService.GetSnapshot();
            var content = new StackPanel();
            var options = snapshot.Locations.Select(location => new DiscoveryInboxPlacementOption(
                location.Id, DiscoveryInboxLocationPath(snapshot, location.Id)))
                .OrderBy(option => option.Path, StringComparer.CurrentCultureIgnoreCase).ToArray();
            var dialog = CreateThemedDialog(UiText.Get("DiscoveryInboxPlacementTitle"), content,
                Convert.ToDouble(FindResource("NetLoom.Width.DiscoveryPlacementDialog")));
            content.SetResourceReference(MarginProperty, "NetLoom.Thickness.PanelPadding");
            KeyboardNavigation.SetTabNavigation(content, KeyboardNavigationMode.Cycle);
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            actions.SetResourceReference(MarginProperty, "NetLoom.Thickness.SectionGapTop");
            if (options.Length == 0)
            {
                var empty = new TextBlock { Text = UiText.Get("DiscoveryInboxNoPlacements"), TextWrapping = TextWrapping.Wrap };
                empty.SetResourceReference(StyleProperty, "NetLoom.Style.MutedText");
                content.Children.Add(empty);
                var close = DiscoveryInboxDialogButton("DiscoveryInboxPlacementClose");
                close.IsCancel = true;
                close.Click += (sender, args) => dialog.Close();
                actions.Children.Add(close);
                dialog.Loaded += (sender, args) => close.Focus();
            }
            else
            {
                var label = new TextBlock { Text = UiText.Get("DiscoveryInboxPlacementLabel"), TextWrapping = TextWrapping.Wrap };
                label.SetResourceReference(MarginProperty, "NetLoom.Thickness.FieldLabelCompact");
                var list = new ListBox { ItemsSource = options, DisplayMemberPath = nameof(DiscoveryInboxPlacementOption.Path), SelectedIndex = 0 };
                list.SetResourceReference(MaxHeightProperty, "NetLoom.Height.DiscoveryPlacementList");
                AutomationProperties.SetName(list, label.Text);
                AutomationProperties.SetLabeledBy(list, label);
                content.Children.Add(label);
                content.Children.Add(list);
                var cancel = DiscoveryInboxDialogButton("DiscoveryProfileCancelAction");
                cancel.IsCancel = true;
                var assign = DiscoveryInboxDialogButton("DiscoveryInboxPlacementConfirm");
                assign.IsDefault = true;
                assign.SetResourceReference(MarginProperty, "NetLoom.Thickness.InlineGap");
                assign.Click += (sender, args) =>
                {
                    var selected = list.SelectedItem as DiscoveryInboxPlacementOption;
                    if (selected == null) return;
                    dialog.Tag = selected.Id;
                    dialog.DialogResult = true;
                };
                actions.Children.Add(cancel);
                actions.Children.Add(assign);
                dialog.Loaded += (sender, args) => list.Focus();
            }
            content.Children.Add(actions);
            return dialog;
        }

        private static Button DiscoveryInboxDialogButton(string key)
        {
            var button = new Button { Content = UiText.Get(key) };
            button.SetResourceReference(StyleProperty, "NetLoom.Style.SecondaryButton");
            return button;
        }

        private sealed class DiscoveryInboxPlacementOption
        {
            internal DiscoveryInboxPlacementOption(Guid id, string path) { Id = id; Path = path; }
            public Guid Id { get; }
            public string Path { get; }
        }
    }
}
