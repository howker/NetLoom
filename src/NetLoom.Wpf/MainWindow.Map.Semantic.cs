using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private void ApplyNodeSemanticPresentation(MapNodeVisual visual)
    {
        var node = visual.Node;
        if (node == null) return;
        var far = _semanticLevel == MapSemanticLevel.Far;
        visual.Title.Visibility = far ? Visibility.Collapsed : Visibility.Visible;
        visual.CategoryIcon.Visibility = far ? Visibility.Collapsed : Visibility.Visible;
        visual.LockBadge.Visibility = !far && visual.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        var parts = new List<string>();
        if (node.IsUnconfirmed) parts.Add(UiText.Get("DeviceUnconfirmedMark"));
        if (_semanticLevel == MapSemanticLevel.Detailed)
        {
            var diagnostic = node.DeviceId.HasValue
                ? _lastDiagnosticSnapshot?.Devices.FirstOrDefault(item => item.DeviceId == node.DeviceId.Value)
                : null;
            if (!string.IsNullOrWhiteSpace(diagnostic?.ManagementAddress))
                parts.Add(diagnostic.ManagementAddress);
            var model = string.IsNullOrWhiteSpace(diagnostic?.SecondaryText)
                ? diagnostic?.SystemDescription : diagnostic.SecondaryText;
            if (!string.IsNullOrWhiteSpace(model)) parts.Add(model);
        }
        visual.Secondary.Text = string.Join(" · ", parts);
        visual.Secondary.Visibility = !far && parts.Count != 0 ? Visibility.Visible : Visibility.Collapsed;
        visual.Secondary.ToolTip = node.IsUnconfirmed
            ? UiText.Get("DeviceUnconfirmedHint") + (parts.Count > 1 ? "\n" + visual.Secondary.Text : string.Empty)
            : visual.Secondary.Text;

        // Значок не меняет геометрию карточки; на дальнем уровне его размер задан в экранных единицах.
        visual.StatusIcon.RenderTransformOrigin = new Point(0.5, 0.5);
        visual.StatusIcon.RenderTransform = far ? new ScaleTransform(1 / _zoom, 1 / _zoom) : Transform.Identity;
        // В окрестности ярлык получает каждое видимое устройство: их немного, наложения снимает MapLabelDeclutter.
        var important = node.DeviceId.HasValue && (node.DeviceId == _selectedDeviceId || node.DeviceId == _highlightedDeviceId ||
             _operationalFocusDeviceIds.Contains(node.DeviceId.Value) ||
             _neighborhoodDeviceIds.Contains(node.DeviceId.Value) ||
             // Устройства показанного пути: без имён путь на дальнем уровне не прочитать.
             _pathDeviceIds.Contains(node.DeviceId.Value));
        var showLabel = far && important && !visual.LabelHidden;
        visual.SemanticLabel.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
        var title = (TextBlock)visual.SemanticLabel.Child;
        // Скрытый ярлык не дублирует имя карточки (в дереве UI Automation и в поиске текста).
        title.Text = visual.SemanticLabel.Visibility == Visibility.Visible
            ? DisplayNodeLabel(node)
            : string.Empty;
        title.ToolTip = title.Text.Length == 0 ? null : title.Text;
        visual.SemanticLabel.ToolTip = title.Text;
        if (showLabel)
        {
            // RenderTransform сохраняет высоту карточки для связей и вписывания.
            visual.SemanticLabel.MaxWidth = _nodeWidth;
            visual.SemanticLabel.Measure(new Size(_nodeWidth, double.PositiveInfinity));
            var transforms = new TransformGroup();
            transforms.Children.Add(new ScaleTransform(1 / _zoom, 1 / _zoom));
            transforms.Children.Add(new TranslateTransform(0,
                -(visual.SemanticLabel.DesiredSize.Height + _linkLabelCollisionMargin) / _zoom));
            visual.SemanticLabel.RenderTransform = transforms;
        }
        else
            visual.SemanticLabel.RenderTransform = Transform.Identity;
    }

    private void ApplyLocationSemanticPresentation(MapLocationVisual visual)
    {
        ApplyLocationSemanticLayer(visual);
        var far = _semanticLevel == MapSemanticLevel.Far;
        visual.Title.Text = visual.LocationName;
        visual.StatusIcon.Visibility = Visibility.Collapsed;
        if (far && _lastMapSnapshot != null)
        {
            var locations = _lastMapSnapshot.Locations.ToDictionary(item => item.Id);
            var members = _lastMapSnapshot.Nodes.Where(node =>
                SemanticLocationContains(visual.LocationId, node.LocationId, locations)).ToArray();
            visual.Title.Text = UiText.Format("MapSemanticLocationSummary", visual.LocationName, members.Length);
            var worst = members.Select(node => NodeDegradationState(node.DeviceId))
                .OrderByDescending(state => (int)state).FirstOrDefault();
            if (worst == MapNodeDegradationState.Degraded || worst == MapNodeDegradationState.Critical)
            {
                var semantic = NodeStatusSemantic(worst);
                visual.StatusIcon.Visibility = Visibility.Visible;
                visual.StatusIcon.Data = FindResource(OperatorStatusIconGeometryKey(semantic)) as Geometry;
                visual.StatusIcon.SetResourceReference(Path.StrokeProperty, OperatorStatusBrushKey(semantic));
                visual.StatusIcon.ToolTip = OperatorStatusLabel(semantic);
            }
        }
        visual.Title.ToolTip = visual.Title.Text;
        visual.Header.ToolTip = visual.Border.ToolTip + (far ? "\n" + visual.Title.Text : string.Empty) +
            (visual.StatusIcon.Visibility == Visibility.Visible ? "\n" + visual.StatusIcon.ToolTip : string.Empty);
        visual.Header.RenderTransform = far ? new ScaleTransform(1 / _zoom, 1 / _zoom) : Transform.Identity;
        UpdateLocationHeaderFocusRing(visual, far);
        // Вкладка может выступить вправо не больше своей экранной высоты; длинное имя обрезается.
        visual.Header.MaxWidth = far
            ? visual.ExpandedWidth * _zoom + _locationHeaderHeight
            : visual.ExpandedWidth;
        visual.Header.Measure(new Size(visual.Header.MaxWidth, _locationHeaderHeight));
        if (visual.IsCollapsed)
        {
            visual.Border.Width = visual.Header.DesiredSize.Width / (far ? _zoom : 1);
            visual.Border.Height = _locationHeaderHeight / (far ? _zoom : 1);
        }

        // Скрытая вкладка не оставляет текста в дереве UI Automation и не получает Tab;
        // Рамка размещения остаётся и по наведению называет размещение.
        var hidden = far && visual.LabelHidden;
        visual.Header.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
        if (hidden)
        {
            visual.Title.Text = string.Empty;
            visual.Title.ToolTip = null;
        }
    }

    // Уровень «Издалека»: дальний уровень подписывает размещения (замечание владельца 2026-10-10).
    // Приоритеты: -1 — расхождение; 0 — выбранное устройство; 1 — подсвеченное, фокус, путь и окрестность;
    // 2 + глубина — вкладки размещений; остальные подписи связей уступают всем вкладкам.
    // Положения вкладки по порядку, первое свободное выигрывает: угол рамки; над верхним краем рамки;
    // Сетка внутри рамки построчно (шаг по строкам — высота вкладки плюс FarLabelGap, по столбцам —
    // Четверть экранной ширины рамки); у вкладки шире рамки — левый край рамки на каждой строке сетки.
    private void ApplyFarLabelDeclutter()
    {
        foreach (var visual in _nodeVisualsByIdentity.Values)
        {
            visual.LabelHidden = false;
            ApplyNodeSemanticPresentation(visual);
        }
        foreach (var visual in _locationVisualsById.Values)
        {
            // Каждый проход начинается с основного положения, а не с прошлого запасного сдвига.
            visual.LabelHidden = false;
            ApplyLocationSemanticPresentation(visual);
        }
        if (_semanticLevel != MapSemanticLevel.Far || _lastMapSnapshot == null) return;

        // Положение значка зависит от свёрнутого заголовка карточки; нужны уже пересчитанные размеры WPF.
        MapCanvas.UpdateLayout();
        var zoom = _zoom > 0.0 ? _zoom : 1.0;
        var gap = GetDoubleResource("NetLoom.Map.FarLabelGap");
        var locations = _lastMapSnapshot.Locations.ToDictionary(item => item.Id);
        var candidates = new List<MapLabelCandidate>();
        var obstacles = new List<Rect>();
        var nodeByKey = new Dictionary<string, MapNodeVisual>(StringComparer.Ordinal);
        var locationByKey = new Dictionary<string, MapLocationVisual>(StringComparer.Ordinal);
        foreach (var pair in _nodeVisualsByIdentity)
        {
            var visual = pair.Value;
            if (visual.Border.Visibility != Visibility.Visible) continue;
            var deviceId = visual.DeviceId ?? visual.Node?.DeviceId;
            if (deviceId.HasValue && (deviceId == _selectedDeviceId ||
                deviceId == _highlightedDeviceId || _operationalFocusDeviceIds.Contains(deviceId.Value) ||
                _pathDeviceIds.Contains(deviceId.Value) || _neighborhoodDeviceIds.Contains(deviceId.Value)))
                obstacles.Add(new Rect(NodeLeft(visual) * zoom, NodeTop(visual) * zoom,
                    visual.Border.ActualWidth * zoom, visual.Border.ActualHeight * zoom));
            if (visual.StatusIcon.Visibility == Visibility.Visible)
            {
                var icon = visual.StatusIcon.TransformToAncestor(MapCanvas)
                    .TransformBounds(new Rect(visual.StatusIcon.RenderSize));
                obstacles.Add(new Rect(icon.X * zoom, icon.Y * zoom, icon.Width * zoom, icon.Height * zoom));
            }
            if (visual.SemanticLabel.Visibility != Visibility.Visible) continue;
            var key = "N:" + pair.Key;
            nodeByKey[key] = visual;
            var inset = new Point(visual.Border.BorderThickness.Left + visual.Border.Padding.Left,
                visual.Border.BorderThickness.Top + visual.Border.Padding.Top);
            var size = visual.SemanticLabel.DesiredSize;
            // Неподвижные препятствия ограничивают вкладки: собственная карточка не скрывает имя выбора.
            candidates.Add(new MapLabelCandidate(key, new Rect(
                (NodeLeft(visual) + inset.X) * zoom,
                (NodeTop(visual) + inset.Y) * zoom - size.Height - _linkLabelCollisionMargin,
                size.Width, size.Height), FarNodeLabelPriority(visual), avoidObstacles: false));
        }
        var linkPriority = 2;
        // Экранные прямоугольники видимых рамок: вкладка родителя не должна вставать на рамки-потомки.
        var frameRects = new Dictionary<Guid, Rect>();
        foreach (var item in _locationVisualsById.Values)
        {
            if (item.Border.Visibility != Visibility.Visible) continue;
            frameRects[item.LocationId] = new Rect(LocationLeft(item) * zoom, LocationTop(item) * zoom,
                item.Border.Width * zoom, item.Border.Height * zoom);
        }
        foreach (var visual in _locationVisualsById.Values)
        {
            if (visual.Border.Visibility != Visibility.Visible) continue;
            var descendantFrames = frameRects
                .Where(pair => pair.Key != visual.LocationId &&
                    SemanticLocationContains(visual.LocationId, pair.Key, locations))
                .Select(pair => pair.Value).ToArray();
            Func<Rect, double, bool> touchesDescendant = (rect, margin) =>
            {
                var grown = new Rect(rect.Left - margin, rect.Top - margin,
                    rect.Width + 2.0 * margin, rect.Height + 2.0 * margin);
                return descendantFrames.Any(frameRect => grown.IntersectsWith(frameRect) &&
                    grown.Left < frameRect.Right && frameRect.Left < grown.Right &&
                    grown.Top < frameRect.Bottom && frameRect.Top < grown.Bottom);
            };
            var key = "L:" + visual.LocationId.ToString("N");
            locationByKey[key] = visual;
            var depth = 0;
            MapLocation location;
            var visited = new HashSet<Guid>();
            if (locations.TryGetValue(visual.LocationId, out location))
            {
                while (location.ParentLocationId.HasValue && visited.Add(location.Id) &&
                       locations.TryGetValue(location.ParentLocationId.Value, out location))
                    depth++;
            }
            var size = visual.Header.DesiredSize;
            var bounds = new Rect(LocationLeft(visual) * zoom, LocationTop(visual) * zoom,
                size.Width, size.Height);
            var positions = new List<Rect> { bounds };
            var frame = new Rect(LocationLeft(visual) * zoom, LocationTop(visual) * zoom,
                visual.Border.Width * zoom, visual.Border.Height * zoom);
            // Запасное положение 2: вкладка «сидит» на верхнем крае рамки снаружи.
            if (size.Height > 0.0)
            {
                var above = new Rect(frame.Left, frame.Top - size.Height, size.Width, size.Height);
                // У рамки с вложенными рамками угол занят первой дочерней: вкладка сначала встаёт над рамкой.
                if (!touchesDescendant(above, 0.0)) positions.Insert(descendantFrames.Length != 0 ? 0 : 1, above);
            }
            var step = size.Height + gap;
            var columnStep = frame.Width / 4.0;
            if (!visual.IsCollapsed && step > 0.0 && columnStep > 0.0)
            {
                // Положения 3 и 4: сетка внутри рамки построчно; у вкладки шире рамки — только левый край.
                var narrow = bounds.Width > frame.Width;
                for (var top = bounds.Top; top + bounds.Height <= frame.Bottom; top += step)
                {
                    for (var left = frame.Left; narrow ? left <= frame.Left : left + bounds.Width <= frame.Right;
                         left += columnStep)
                    {
                        var candidate = new Rect(left, top, bounds.Width, bounds.Height);
                        // Вкладка родителя стоит на собственной площади рамки, а не на дочерних рамках.
                        if (candidate != bounds && !touchesDescendant(candidate, gap)) positions.Add(candidate);
                        if (narrow) break;
                    }
                }
            }
            candidates.Add(new MapLabelCandidate(key, positions, 2 + depth));
            linkPriority = Math.Max(linkPriority, 3 + depth);
        }

        // Подписи связей обратно масштабированы вокруг центра: экранный размер равен DesiredSize.
        var linkByKey = new Dictionary<string, MapLinkVisual>(StringComparer.Ordinal);
        foreach (var pair in _linkVisualsByIdentity)
        {
            var label = pair.Value.Label;
            if (label.Visibility != Visibility.Visible) continue;
            var key = "K:" + pair.Key;
            linkByKey[key] = pair.Value;
            var size = label.DesiredSize;
            var centerX = (Canvas.GetLeft(label) + size.Width / 2.0) * zoom;
            var centerY = (Canvas.GetTop(label) + size.Height / 2.0) * zoom;
            candidates.Add(new MapLabelCandidate(key, new Rect(centerX - size.Width / 2.0,
                centerY - size.Height / 2.0, size.Width, size.Height),
                // ADR-079: расхождение первым занимает место; обычная подпись уступает вкладкам.
                HasTopologyConflict(pair.Value.Line.Tag as Guid?) ? -1 : linkPriority, avoidObstacles: false));
        }

        var shown = MapLabelDeclutter.SelectPlacements(candidates, gap, obstacles);
        foreach (var pair in nodeByKey)
        {
            if (shown.ContainsKey(pair.Key)) continue;
            pair.Value.LabelHidden = true;
            ApplyNodeSemanticPresentation(pair.Value);
        }
        foreach (var pair in locationByKey)
        {
            Rect bounds;
            if (shown.TryGetValue(pair.Key, out bounds))
            {
                var transforms = new TransformGroup();
                transforms.Children.Add(new ScaleTransform(1 / zoom, 1 / zoom));
                transforms.Children.Add(new TranslateTransform(
                    (bounds.Left - LocationLeft(pair.Value) * zoom) / zoom,
                    (bounds.Top - LocationTop(pair.Value) * zoom) / zoom));
                pair.Value.Header.RenderTransform = transforms;
                continue;
            }
            pair.Value.LabelHidden = true;
            ApplyLocationSemanticPresentation(pair.Value);
        }
        foreach (var pair in linkByKey)
        {
            // Подпись в фокусе клавиатуры не скрывается: скрытый элемент теряет фокус (K4).
            if (!shown.ContainsKey(pair.Key) && !HasTopologyConflict(pair.Value.Line.Tag as Guid?) &&
                !pair.Value.Label.IsKeyboardFocused)
                pair.Value.Label.Visibility = Visibility.Collapsed;
        }
    }

    private int FarNodeLabelPriority(MapNodeVisual visual)
    {
        var deviceId = visual.DeviceId ?? visual.Node?.DeviceId;
        return deviceId.HasValue && deviceId == _selectedDeviceId ? 0 : 1;
    }

    // Кольцо фокуса кнопки вкладки: на «Издалека» вкладка обратно масштабирована и стоит на экране 1:1.
    // Поэтому кольцу нужны обычные 2 px, а не толщина, делённая на масштаб карты (UpdateMapFocusRingScale).
    private void UpdateLocationHeaderFocusRing(MapLocationVisual visual, bool screenScale)
    {
        const string ringKey = "NetLoom.Thickness.MapFocusRing";
        const string offsetKey = "NetLoom.Thickness.MapFocusRingOffset";
        if (screenScale)
        {
            visual.Header.Resources[ringKey] = GetThicknessResource("NetLoom.Thickness.FocusRing");
            visual.Header.Resources[offsetKey] = GetThicknessResource("NetLoom.Thickness.FocusRingOffset");
        }
        else if (visual.Header.Resources.Contains(ringKey) || visual.Header.Resources.Contains(offsetKey))
        {
            // Без собственных значений вкладка снова берёт толщину из ресурсов области карты.
            visual.Header.Resources.Remove(ringKey);
            visual.Header.Resources.Remove(offsetKey);
        }
    }

    private static bool SemanticLocationContains(Guid parent, Guid? member,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var visited = new HashSet<Guid>();
        while (member.HasValue && visited.Add(member.Value))
        {
            if (member.Value == parent) return true;
            MapLocation location;
            if (!locations.TryGetValue(member.Value, out location)) return false;
            member = location.ParentLocationId;
        }
        return false;
    }

    private string SemanticLinkLabel(MapLink link)
    {
        var label = BuildLinkLabel(link);
        if (_semanticLevel != MapSemanticLevel.Detailed || string.IsNullOrWhiteSpace(label)) return label;
        var diagnostic = _lastDiagnosticSnapshot?.Links.FirstOrDefault(item =>
            link.PhysicalLinkId.HasValue && item.PhysicalLinkId == link.PhysicalLinkId.Value);
        var speed = SpeedText(diagnostic?.SpeedBps);
        return string.IsNullOrWhiteSpace(speed) ? label : label + " · " + speed;
    }
}
