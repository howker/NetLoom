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
        var important = visual.StatusIcon.Visibility == Visibility.Visible ||
            (node.DeviceId.HasValue && (node.DeviceId == _selectedDeviceId || node.DeviceId == _highlightedDeviceId ||
             _operationalFocusDeviceIds.Contains(node.DeviceId.Value) ||
             _neighborhoodDeviceIds.Contains(node.DeviceId.Value) ||
             // Устройства показанного пути: без имён путь на дальнем уровне не прочитать.
             _pathDeviceIds.Contains(node.DeviceId.Value)));
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

    // Уровень «Издалека»: подписи (ярлыки устройств и вкладки размещений) не накладываются друг на друга.
    // Приоритеты: 0 — выбранное устройство; 1 — участники фокуса и подсвеченное; 2 — устройства с проблемой;
    // 3 — вкладки верхнего уровня; 3 + глубина — вложенные вкладки; 5 — прочие ярлыки.
    private void ApplyFarLabelDeclutter()
    {
        foreach (var visual in _nodeVisualsByIdentity.Values)
        {
            if (!visual.LabelHidden) continue;
            visual.LabelHidden = false;
            ApplyNodeSemanticPresentation(visual);
        }
        foreach (var visual in _locationVisualsById.Values)
        {
            if (!visual.LabelHidden) continue;
            visual.LabelHidden = false;
            ApplyLocationSemanticPresentation(visual);
        }
        if (_semanticLevel != MapSemanticLevel.Far || _lastMapSnapshot == null) return;

        var zoom = _zoom > 0.0 ? _zoom : 1.0;
        var locations = _lastMapSnapshot.Locations.ToDictionary(item => item.Id);
        var candidates = new List<MapLabelCandidate>();
        var nodeByKey = new Dictionary<string, MapNodeVisual>(StringComparer.Ordinal);
        var locationByKey = new Dictionary<string, MapLocationVisual>(StringComparer.Ordinal);
        foreach (var pair in _nodeVisualsByIdentity)
        {
            var visual = pair.Value;
            if (visual.Border.Visibility != Visibility.Visible ||
                visual.SemanticLabel.Visibility != Visibility.Visible) continue;
            var key = "N:" + pair.Key;
            nodeByKey[key] = visual;
            var inset = new Point(visual.Border.BorderThickness.Left + visual.Border.Padding.Left,
                visual.Border.BorderThickness.Top + visual.Border.Padding.Top);
            var size = visual.SemanticLabel.DesiredSize;
            // Ярлык обратно масштабирован и стоит над карточкой: на экране его размер равен DesiredSize.
            candidates.Add(new MapLabelCandidate(key, new Rect(
                (NodeLeft(visual) + inset.X) * zoom,
                (NodeTop(visual) + inset.Y) * zoom - size.Height - _linkLabelCollisionMargin,
                size.Width, size.Height), FarNodeLabelPriority(visual)));
        }
        foreach (var visual in _locationVisualsById.Values)
        {
            if (visual.Border.Visibility != Visibility.Visible) continue;
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
            candidates.Add(new MapLabelCandidate(key, new Rect(LocationLeft(visual) * zoom,
                LocationTop(visual) * zoom, size.Width, size.Height), 3 + depth));
        }

        // Подписи связей, видимые на дальнем уровне (фокус, путь, расхождение), — обратно масштабированы
        // Вокруг центра: на экране их размер равен DesiredSize. Уступают именам устройств.
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
                // ADR-079: расхождение видно всегда — его подпись первой занимает место и не скрывается.
                HasTopologyConflict(pair.Value.Line.Tag as Guid?) ? -1 : 3));
        }

        var shown = MapLabelDeclutter.SelectVisible(candidates, GetDoubleResource("NetLoom.Map.FarLabelGap"));
        foreach (var pair in nodeByKey)
        {
            if (shown.Contains(pair.Key)) continue;
            pair.Value.LabelHidden = true;
            ApplyNodeSemanticPresentation(pair.Value);
        }
        foreach (var pair in locationByKey)
        {
            if (shown.Contains(pair.Key)) continue;
            pair.Value.LabelHidden = true;
            ApplyLocationSemanticPresentation(pair.Value);
        }
        foreach (var pair in linkByKey)
        {
            // Подпись в фокусе клавиатуры не скрывается: скрытый элемент теряет фокус (K4).
            if (!shown.Contains(pair.Key) && !HasTopologyConflict(pair.Value.Line.Tag as Guid?) &&
                !pair.Value.Label.IsKeyboardFocused)
                pair.Value.Label.Visibility = Visibility.Collapsed;
        }
    }

    private int FarNodeLabelPriority(MapNodeVisual visual)
    {
        var deviceId = visual.DeviceId ?? visual.Node?.DeviceId;
        if (deviceId.HasValue)
        {
            if (deviceId == _selectedDeviceId) return 0;
            // Устройства показанного пути важны так же, как участники предупреждения.
            if (deviceId == _highlightedDeviceId || _operationalFocusDeviceIds.Contains(deviceId.Value) ||
                _pathDeviceIds.Contains(deviceId.Value)) return 1;
        }
        return visual.StatusIcon.Visibility == Visibility.Visible ? 2 : 5;
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
