using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
                _topologyQualityReport.Gaps.Zip(nextReport.Gaps, (previous, next) =>
                    previous.Kind == next.Kind && previous.PhysicalLinkId == next.PhysicalLinkId &&
                    previous.DeviceId == next.DeviceId && previous.LocationId == next.LocationId &&
                    previous.OtherLocationId == next.OtherLocationId && previous.Subject == next.Subject &&
                    previous.Detail == next.Detail).All(equal => equal);
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
                MapQualityGroups.ItemsSource = _topologyQualityReport.Gaps.GroupBy(gap => gap.Kind)
                    .Select(group => new TopologyQualityGroupRow(
                        UiText.Format(TopologyQualityGroupResourceKey(group.Key), group.Count()),
                        group.Select(gap => new TopologyQualityGapRow(gap)).ToArray())).ToArray();
            UpdateTopologyQualityVisibility();
        }

        private void UpdateTopologyQualityVisibility()
        {
            if (MapQualityNotice == null) return;
            var visible = _shellSection == ShellSection.Map && _topologyQualityReport.Count > 0;
            MapQualityNotice.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) MapQualityToggle.IsChecked = false;
        }

        private static string TopologyQualityGroupResourceKey(TopologyQualityGapKind kind)
        {
            switch (kind)
            {
                case TopologyQualityGapKind.ObservedLink: return "MapQualityObservedGroup";
                case TopologyQualityGapKind.InferredLink: return "MapQualityInferredGroup";
                case TopologyQualityGapKind.OneSidedLldp: return "MapQualityOneSidedGroup";
                case TopologyQualityGapKind.ManualObservedConflict: return "TopologyConflictQualityGroup";
                case TopologyQualityGapKind.LocationOverlap: return "MapQualityLocationOverlapGroup";
                default: return "MapQualitySyntheticGroup";
            }
        }

        private void OnMapQualityShowClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var gap = button?.Tag as TopologyQualityGap;
            if (gap == null) return;

            // Используем тот же выбор и вписывание объектов, что у действия «Показать на карте».
            if (gap.LocationId.HasValue && gap.OtherLocationId.HasValue)
            {
                SelectLocation(gap.LocationId.Value);
                Rect first;
                Rect second;
                if (TryGetExpandedLocationBounds(gap.LocationId.Value, out first) &&
                    TryGetExpandedLocationBounds(gap.OtherLocationId.Value, out second))
                    TryFitMapBoundsToViewport(new[] { first, second });
            }
            else if (gap.PhysicalLinkId.HasValue)
            {
                SelectAlertPhysicalContext(gap.PhysicalLinkId.Value);
                FocusAlertContextToViewport(new[] { gap.PhysicalLinkId.Value }, null);
            }
            else if (gap.DeviceId.HasValue)
            {
                SelectAlertDeviceContext(gap.DeviceId.Value);
                FocusSelectedMapAtNativeZoom(() => AnimateDiscoveryFocus(gap.DeviceId.Value));
            }

            // Перемещение карты не уводит клавиатурный фокус из списка причин.
            button.Focus();
            e.Handled = true;
        }

        private sealed class TopologyQualityGroupRow
        {
            public TopologyQualityGroupRow(string heading, IReadOnlyList<TopologyQualityGapRow> gaps)
            {
                Heading = heading;
                Gaps = gaps;
            }
            public string Heading { get; }
            public IReadOnlyList<TopologyQualityGapRow> Gaps { get; }
        }

        private sealed class TopologyQualityGapRow
        {
            public TopologyQualityGapRow(TopologyQualityGap gap)
            {
                Gap = gap;
                Text = string.IsNullOrEmpty(gap.Detail) ? gap.Subject : UiText.Format("MapQualityGapDetail", gap.Subject, gap.Detail);
                ShowText = UiText.Get("MapQualityShow");
                ShowName = string.IsNullOrEmpty(gap.Detail)
                    ? UiText.Format("MapQualityShowSubjectName", gap.Subject)
                    : UiText.Format("MapQualityShowName", gap.Subject, gap.Detail);
            }
            public TopologyQualityGap Gap { get; }
            public string Text { get; }
            public string ShowText { get; }
            public string ShowName { get; }
        }
    }
}
