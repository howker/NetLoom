using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf
{
    public partial class MainWindow
    {
        private TopologyQualityReport _topologyQualityReport = TopologyQualityProjection.Build(null, null);

        private void UpdateTopologyQuality()
        {
            _topologyConflicts = TopologyConflictProjection.Build(_lastDiagnosticSnapshot,
                TopologyConflictAcknowledgements?.List());
            var frames = _locationVisualsById.Values.Where(visual => visual.LocationId != Guid.Empty)
                .Select(visual => new LocationOverlapFrame(visual.LocationId, visual.ParentLocationId,
                    visual.Title.Text, ExpandedLocationBounds(visual))).ToArray();
            // Оператору показываются только наложения сохранённой (ручной) геометрии;
            // Несохранённые рамки раскладывает карта, и их наложение — не пробел данных.
            var overlaps = LocationOverlapProjection.Build(frames)
                .Where(overlap => _persistedLocationLayouts.ContainsKey(overlap.First.Id) ||
                    (!overlap.ChildOutsideParent && _persistedLocationLayouts.ContainsKey(overlap.Second.Id)))
                .ToArray();
            var nextReport = TopologyQualityProjection.Build(_lastDiagnosticSnapshot, _lastMapSnapshot,
                _topologyConflicts, overlaps);
            var unchanged = _topologyQualityReport.Count == nextReport.Count &&
                _topologyQualityReport.Items.Zip(nextReport.Items, (previous, next) =>
                    previous.PhysicalLinkId == next.PhysicalLinkId &&
                    previous.DeviceId == next.DeviceId && previous.LocationId == next.LocationId &&
                    previous.OtherLocationId == next.OtherLocationId && previous.Subject == next.Subject &&
                    previous.Reasons.Count == next.Reasons.Count &&
                    previous.Reasons.Zip(next.Reasons, (oldReason, newReason) =>
                        oldReason.Kind == newReason.Kind && oldReason.Text == newReason.Text)
                        .All(equal => equal)).All(equal => equal);
            _topologyQualityReport = nextReport;
            var summary = UiText.Format("MapQualitySummary", _topologyQualityReport.Count);
            ApplyOperatorStatus(MapQualityGlyph, MapQualitySummaryText, OperatorStatusSemantic.Unknown, summary);
            // §6: кисть «неизвестно» (TextDisabled) на приглушённой полосе даёт 2.8:1; значок остаётся нейтральным.
            MapQualityGlyph.SetResourceReference(TextBlock.ForegroundProperty, "NetLoom.Brush.TextSecondary");
            AutomationProperties.SetName(MapQualityToggle, summary);
            // Текст ссылки сохраняет акцент; нейтральную семантику несёт значок недостаточных данных.
            MapQualitySummaryText.SetResourceReference(TextBlock.ForegroundProperty, "NetLoom.Brush.AccentText");
            MapQualitySummaryText.ToolTip = summary;
            // Неизменившиеся причины не пересоздают кнопки и не снимают фокус при очередном опросе.
            if (!unchanged)
                MapQualityItems.ItemsSource = _topologyQualityReport.Items
                    .Select(item => new TopologyQualityItemRow(item)).ToArray();
            UpdateTopologyQualityVisibility();
        }

        private void UpdateTopologyQualityVisibility()
        {
            if (MapQualityNotice == null) return;
            var visible = _shellSection == ShellSection.Map && _topologyQualityReport.Count > 0;
            MapQualityNotice.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) MapQualityToggle.IsChecked = false;
        }

        private void OnMapQualityDetailsLoaded(object sender, RoutedEventArgs e)
        {
            UpdateMapQualityPanelWidth();
        }

        private void OnMapQualityViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMapQualityPanelWidth();
        }

        private void UpdateMapQualityPanelWidth()
        {
            if (MapQualityDetails == null || MapScrollViewer == null) return;
            var margin = MapQualityDetails.Margin;
            MapQualityDetails.MaxWidth = Math.Max(0, MapScrollViewer.ActualWidth - margin.Left - margin.Right);
        }

        private void CloseMapQualityDetails(bool restoreFocus)
        {
            MapQualityToggle.IsChecked = false;
            if (restoreFocus) MapQualityToggle.Focus();
        }

        private void OnMapQualityPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || MapQualityToggle?.IsChecked != true ||
                MapQualityDetails?.IsKeyboardFocusWithin != true) return;

            // Обработчик окна из XAML вызывается до общего Escape режима правки карты.
            CloseMapQualityDetails(true);
            e.Handled = true;
        }

        private void OnMapQualityMapPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Щелчок продолжает выбирать объект или перемещать карту после закрытия панели.
            if (MapQualityToggle.IsChecked == true) CloseMapQualityDetails(false);
            OnMapPreviewMouseDown(sender, e);
        }

        private void OnMapQualityShowClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as TopologyQualityItem;
            if (item == null) return;

            // Sprint 49: «Показать на карте» — действие, прежний вид которого попадает в историю.
            RecordMapView(false);

            // Используем тот же выбор и вписывание объектов, что у действия «Показать на карте».
            if (item.LocationId.HasValue && item.OtherLocationId.HasValue)
            {
                SelectLocation(item.LocationId.Value);
                Rect first;
                Rect second;
                if (TryGetExpandedLocationBounds(item.LocationId.Value, out first) &&
                    TryGetExpandedLocationBounds(item.OtherLocationId.Value, out second))
                    TryFitMapBoundsToViewport(new[] { first, second });
            }
            else if (item.PhysicalLinkId.HasValue)
            {
                SelectAlertPhysicalContext(item.PhysicalLinkId.Value);
                FocusAlertContextToViewport(new[] { item.PhysicalLinkId.Value }, null);
            }
            else if (item.DeviceId.HasValue)
            {
                SelectAlertDeviceContext(item.DeviceId.Value);
                FocusSelectedMapAtNativeZoom(() => AnimateDiscoveryFocus(item.DeviceId.Value));
            }

            CloseMapQualityDetails(true);
            e.Handled = true;
        }

        private sealed class TopologyQualityItemRow
        {
            public TopologyQualityItemRow(TopologyQualityItem item)
            {
                Item = item;
                ShowText = UiText.Get("MapQualityShow");
                ShowName = UiText.Format("MapQualityShowSubjectName", item.Subject);
            }
            public TopologyQualityItem Item { get; }
            public string Subject => Item.Subject;
            public IReadOnlyList<TopologyQualityReason> Reasons => Item.Reasons;
            public string ShowText { get; }
            public string ShowName { get; }
        }
    }
}
