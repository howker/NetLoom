using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf;

public partial class MainWindow : Window
{
    private const double NodeWidth = 190.0;
    private const double NodeHeight = 92.0;

    public MainWindow()
    {
        InitializeComponent();

        Title = UiText.Get("WindowTitle");
        MapTitleText.Text = UiText.Get("MapTitle");

        ShowMap(
            new MapSnapshot(
                DateTime.UtcNow,
                new MapNode[0],
                new MapLink[0]));
    }

    public void ShowMap(MapSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        MapCanvas.Children.Clear();

        if (snapshot.Nodes.Count == 0)
        {
            MapStatusText.Text =
                UiText.Get("MapNotLoaded");

            return;
        }

        var nodes =
            snapshot.Nodes.ToDictionary(
                node => node.Key,
                StringComparer.Ordinal);

        var locations =
            snapshot.Locations.ToDictionary(
                location => location.Id);

        foreach (var link in snapshot.Links)
        {
            DrawLink(link, nodes);
        }

        foreach (var node in snapshot.Nodes)
        {
            DrawNode(
                node,
                locations);
        }

        MapStatusText.Text =
            UiText.Format(
                "MapSummary",
                snapshot.Nodes.Count,
                snapshot.Links.Count,
                snapshot.Locations.Count);
    }

    private void DrawLink(
        MapLink link,
        IReadOnlyDictionary<string, MapNode> nodes)
    {
        MapNode source;
        MapNode target;

        if (!nodes.TryGetValue(
                link.SourceNodeKey,
                out source) ||
            !nodes.TryGetValue(
                link.TargetNodeKey,
                out target))
        {
            return;
        }

        var x1 = source.X + (NodeWidth / 2.0);
        var y1 = source.Y + (NodeHeight / 2.0);
        var x2 = target.X + (NodeWidth / 2.0);
        var y2 = target.Y + (NodeHeight / 2.0);

        var line =
            new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke =
                    SystemColors.ControlDarkBrush,
                StrokeThickness = 2.0
            };

        MapCanvas.Children.Add(line);

        var label =
            new TextBlock
            {
                Text =
                    ConfidenceText(link.Confidence) +
                    " • " +
                    FreshnessText(link.Freshness) +
                    " • " +
                    UiText.Format(
                        "EvidenceCount",
                        link.Evidence.Count),

                Background =
                    SystemColors.WindowBrush,

                Padding =
                    new Thickness(4, 2, 4, 2)
            };

        Canvas.SetLeft(
            label,
            ((x1 + x2) / 2.0) - 45.0);

        Canvas.SetTop(
            label,
            ((y1 + y2) / 2.0) - 12.0);

        MapCanvas.Children.Add(label);
    }

    private void DrawNode(
        MapNode node,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        var title =
            new TextBlock
            {
                Text = node.Label,
                FontWeight =
                    FontWeights.SemiBold,
                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        var secondary =
            new TextBlock
            {
                Text =
                    string.IsNullOrWhiteSpace(
                        node.SecondaryText)
                        ? string.Empty
                        : node.SecondaryText,

                Margin =
                    new Thickness(0, 5, 0, 0),

                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        var locationText =
            new TextBlock
            {
                Text =
                    BuildLocationText(
                        node,
                        locations),

                Margin =
                    new Thickness(0, 4, 0, 0),

                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        var content =
            new StackPanel();

        content.Children.Add(title);
        content.Children.Add(secondary);
        content.Children.Add(locationText);

        var border =
            new Border
            {
                Width = NodeWidth,
                Height = NodeHeight,
                Padding = new Thickness(10),
                BorderThickness =
                    new Thickness(1),
                BorderBrush =
                    SystemColors.ControlDarkBrush,
                Background =
                    SystemColors.WindowBrush,
                Child = content
            };

        Canvas.SetLeft(border, node.X);
        Canvas.SetTop(border, node.Y);

        MapCanvas.Children.Add(border);
    }

    private static string BuildLocationText(
        MapNode node,
        IReadOnlyDictionary<Guid, MapLocation> locations)
    {
        if (!node.LocationId.HasValue)
        {
            return UiText.Get(
                "LocationUnassigned");
        }

        MapLocation location;

        if (!locations.TryGetValue(
                node.LocationId.Value,
                out location))
        {
            return UiText.Get(
                "LocationUnknown");
        }

        return UiText.Format(
            "LocationNamed",
            location.Name);
    }

    private static string ConfidenceText(
        MapConfidence confidence)
    {
        switch (confidence)
        {
            case MapConfidence.High:
                return UiText.Get(
                    "ConfidenceHigh");

            case MapConfidence.Medium:
                return UiText.Get(
                    "ConfidenceMedium");

            default:
                return UiText.Get(
                    "ConfidenceLow");
        }
    }

    private static string FreshnessText(
        MapFreshness freshness)
    {
        switch (freshness)
        {
            case MapFreshness.Fresh:
                return UiText.Get(
                    "FreshnessFresh");

            case MapFreshness.Aging:
                return UiText.Get(
                    "FreshnessAging");

            default:
                return UiText.Get(
                    "FreshnessStale");
        }
    }
}
