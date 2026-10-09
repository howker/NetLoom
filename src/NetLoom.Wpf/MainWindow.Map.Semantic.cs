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
        var important = visual.StatusIcon.Visibility == Visibility.Visible ||
            (node.DeviceId.HasValue && (node.DeviceId == _selectedDeviceId || node.DeviceId == _highlightedDeviceId ||
             _operationalFocusDeviceIds.Contains(node.DeviceId.Value)));
        visual.SemanticLabel.Visibility = far && important ? Visibility.Visible : Visibility.Collapsed;
        var title = (TextBlock)visual.SemanticLabel.Child;
        // Скрытый ярлык не дублирует имя карточки (в дереве UI Automation и в поиске текста).
        title.Text = visual.SemanticLabel.Visibility == Visibility.Visible
            ? DisplayNodeLabel(node)
            : string.Empty;
        title.ToolTip = title.Text.Length == 0 ? null : title.Text;
        visual.SemanticLabel.ToolTip = title.Text;
        if (far && important)
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
