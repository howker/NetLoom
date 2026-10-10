using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NetLoom.Application.Topology;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private IReadOnlyList<TopologyConflict> _topologyConflicts = Array.Empty<TopologyConflict>();
        private ITopologyConflictAcknowledgementStore _topologyConflictAcknowledgements;

        public ITopologyConflictAcknowledgementStore TopologyConflictAcknowledgements
        {
            get => _topologyConflictAcknowledgements;
            set
            {
                if (ReferenceEquals(_topologyConflictAcknowledgements, value)) return;
                _topologyConflictAcknowledgements = value;
                UpdateTopologyQuality();
                ApplyLinkFocusPresentation();
                ShowSelectedDiagnostic();
            }
        }

        private bool HasTopologyConflict(Guid? linkId) => linkId.HasValue &&
            _topologyConflicts.Any(conflict => conflict.ManualLinkId == linkId || conflict.ObservedLinkId == linkId);

        private void ShowTopologyConflictBlocks(PhysicalLinkDiagnostic link)
        {
            var conflicts = _topologyConflicts.Where(conflict =>
                conflict.ManualLinkId == link.PhysicalLinkId || conflict.ObservedLinkId == link.PhysicalLinkId).ToArray();
            var previous = InspectorTopologyConflicts.Items.Cast<TopologyConflictRow>().ToArray();
            var canAcknowledge = TopologyConflictAcknowledgements != null;
            if (previous.Length == conflicts.Length && previous.Zip(conflicts, (row, conflict) =>
                    row.Conflict.ManualLinkId == conflict.ManualLinkId &&
                    row.Conflict.ObservedLinkId == conflict.ObservedLinkId &&
                    row.CanAcknowledge == canAcknowledge).All(equal => equal))
            {
                // Повторный опрос обновляет текст, сохраняя кнопки и клавиатурный фокус.
                for (var i = 0; i < conflicts.Length; i++) previous[i].Update(conflicts[i]);
            }
            else
                InspectorTopologyConflicts.ItemsSource = conflicts
                    .Select(conflict => new TopologyConflictRow(this, conflict, canAcknowledge)).ToArray();
            InspectorTopologyConflicts.Visibility = conflicts.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ClearTopologyConflictBlocks()
        {
            if (InspectorTopologyConflicts.Items.Count > 0)
                InspectorTopologyConflicts.ItemsSource = Array.Empty<TopologyConflictRow>();
            InspectorTopologyConflicts.Visibility = Visibility.Collapsed;
        }

        private void OnTopologyConflictEvidenceClick(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as TopologyConflictRow;
            if (row == null) return;
            SelectAlertPhysicalContext(row.Conflict.ObservedLinkId);
            InspectorTabControl.SelectedItem = InspectorEvidenceTab;
            InspectorEvidenceTab.Focus();
        }

        private void OnTopologyConflictKeepManualClick(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as TopologyConflictRow;
            if (row == null || TopologyConflictAcknowledgements == null) return;
            try
            {
                TopologyConflictAcknowledgements.Acknowledge(
                    row.Conflict.ManualLinkId, row.Conflict.ObservedLinkId, DateTime.UtcNow);
            }
            catch (Exception)
            {
                row.ShowSaveFailure();
                (sender as Button)?.Focus();
                return;
            }
            UpdateTopologyQuality();
            ApplyLinkFocusPresentation();
            ShowSelectedDiagnostic();
            // Кнопка исчезла вместе с блоком: возвращаем фокус внутрь инспектора.
            InspectorOverviewTab.Focus();
            Keyboard.Focus(InspectorOverviewTab);
        }

        private async void OnTopologyConflictChangeManualClick(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as TopologyConflictRow;
            if (row == null) return;
            SetMapInteractionMode(MapInteractionMode.Edit);
            await OpenManualTopologyEditorAsync(null, row.Conflict.ManualLinkId);
        }

        private string ConflictEndpoints(PhysicalLinkDiagnostic link, Guid sharedDeviceId)
        {
            var sharedIsA = link.DeviceAId == sharedDeviceId;
            return UiText.Format("TopologyConflictEndpoints",
                DisplayDeviceName(sharedIsA ? link.DeviceAName : link.DeviceBName),
                DisplayLinkEndpointInterfaceName(sharedIsA ? link.InterfaceAId : link.InterfaceBId,
                    sharedIsA ? link.InterfaceAName : link.InterfaceBName),
                DisplayDeviceName(sharedIsA ? link.DeviceBName : link.DeviceAName),
                DisplayLinkEndpointInterfaceName(sharedIsA ? link.InterfaceBId : link.InterfaceAId,
                    sharedIsA ? link.InterfaceBName : link.InterfaceAName));
        }

        private sealed class TopologyConflictRow : INotifyPropertyChanged
        {
            private readonly MainWindow _owner;

            public TopologyConflictRow(MainWindow owner, TopologyConflict conflict, bool canAcknowledge)
            {
                _owner = owner;
                CanAcknowledge = canAcknowledge;
                Update(conflict);
            }
            public TopologyConflict Conflict { get; private set; }
            public bool CanAcknowledge { get; }
            public string Heading => UiText.Get("TopologyConflictHeading");
            public string ErrorText { get; private set; } = string.Empty;
            public void ShowSaveFailure()
            {
                ErrorText = UiText.Get("TopologyConflictSaveFailed");
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorText)));
            }
            public string ManualText { get; private set; }
            public string ObservedText { get; private set; }
            public string EvidenceText => UiText.Get("TopologyConflictOpenEvidence");
            public string KeepManualText => UiText.Get("TopologyConflictKeepManual");
            public string ChangeManualText => UiText.Get("TopologyConflictChangeManual");
            public event PropertyChangedEventHandler PropertyChanged;

            public void Update(TopologyConflict conflict)
            {
                Conflict = conflict;
                var manual = UiText.Format("TopologyConflictManual", _owner.ConflictEndpoints(conflict.ManualLink, conflict.SharedDeviceId));
                var observed = UiText.Format("TopologyConflictObserved",
                    _owner.ConflictEndpoints(conflict.ObservedLink, conflict.SharedDeviceId),
                    conflict.ObservedLink.SourceSummary ?? UiText.Get("DiagnosticValueAbsent"),
                    RelativeTimeText(conflict.ObservedLink.LastSeenUtc));
                if (ManualText != manual)
                {
                    ManualText = manual;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ManualText)));
                }
                if (ObservedText != observed)
                {
                    ObservedText = observed;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ObservedText)));
                }
            }
        }
    }
}
