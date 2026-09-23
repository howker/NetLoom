using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.MapLayout;
using NetLoom.Contracts.TopologyMap;

namespace NetLoom.Application.Export
{
    public static class TopologyExportDiagramPolicy
    {
        public const double NodeWidth = 160.0;
        public const double NodeHeight = 56.0;
        public const double LocationDefaultWidth = 560.0;
        public const double LocationDefaultHeight = 360.0;
        public const double LocationMinWidth = 240.0;
        public const double LocationMinHeight = 140.0;
        public const double LocationHeaderHeight = 36.0;
        public const double LocationContentPadding = 48.0;
        public const double ContentMargin = 48.0;

        public const int MaxPixelDimension = 8192;
        public const long MaxPixelCount = 24000000L;
    }

    public sealed class TopologyExportDiagramNode
    {
        internal TopologyExportDiagramNode(
            MapNode source,
            double x,
            double y,
            double width,
            double height)
        {
            Source =
                source ??
                throw new ArgumentNullException(
                    nameof(source));

            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public MapNode Source { get; }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }
    }

    public sealed class TopologyExportDiagramLocation
    {
        internal TopologyExportDiagramLocation(
            MapLocation source,
            int depth,
            double x,
            double y,
            double width,
            double height)
        {
            Source =
                source ??
                throw new ArgumentNullException(
                    nameof(source));

            Depth = depth;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public MapLocation Source { get; }

        public int Depth { get; }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }
    }

    public sealed class TopologyExportDiagramLink
    {
        internal TopologyExportDiagramLink(
            MapLink source,
            double sourceX,
            double sourceY,
            double targetX,
            double targetY,
            string label)
        {
            Source =
                source ??
                throw new ArgumentNullException(
                    nameof(source));

            SourceX = sourceX;
            SourceY = sourceY;
            TargetX = targetX;
            TargetY = targetY;
            Label = label;
        }

        public MapLink Source { get; }

        public double SourceX { get; }

        public double SourceY { get; }

        public double TargetX { get; }

        public double TargetY { get; }

        public string Label { get; }
    }

    public sealed class TopologyExportDiagram
    {
        internal TopologyExportDiagram(
            int pixelWidth,
            int pixelHeight,
            double scale,
            IEnumerable<TopologyExportDiagramNode> nodes,
            IEnumerable<TopologyExportDiagramLink> links,
            IEnumerable<TopologyExportDiagramLocation> locations)
        {
            if (pixelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pixelWidth));
            }

            if (pixelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pixelHeight));
            }

            if (double.IsNaN(scale) ||
                double.IsInfinity(scale) ||
                scale <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scale));
            }

            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            Scale = scale;

            Nodes =
                (nodes ??
                 throw new ArgumentNullException(
                     nameof(nodes)))
                .ToArray();

            Links =
                (links ??
                 throw new ArgumentNullException(
                     nameof(links)))
                .ToArray();

            Locations =
                (locations ??
                 throw new ArgumentNullException(
                     nameof(locations)))
                .ToArray();
        }

        public int PixelWidth { get; }

        public int PixelHeight { get; }

        public double Scale { get; }

        public IReadOnlyList<TopologyExportDiagramNode> Nodes { get; }

        public IReadOnlyList<TopologyExportDiagramLink> Links { get; }

        public IReadOnlyList<TopologyExportDiagramLocation> Locations { get; }
    }

    public sealed class TopologyExportDiagramBuilder
    {
        public TopologyExportDiagram Build(
            TopologyExportSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            var map =
                snapshot.Topology.MapSnapshot;

            var deviceLayoutById =
                snapshot.Layout.Devices
                    .ToDictionary(
                        item => item.DeviceId);

            var persistedLocationById =
                snapshot.Layout.Locations
                    .ToDictionary(
                        item => item.LocationId);

            var rawNodeByKey =
                new Dictionary<string, RawRectangle>(
                    StringComparer.Ordinal);

            foreach (var node in map.Nodes)
            {
                var x = node.X;
                var y = node.Y;

                if (node.DeviceId.HasValue)
                {
                    MapDeviceLayout layout;

                    if (deviceLayoutById.TryGetValue(
                            node.DeviceId.Value,
                            out layout))
                    {
                        x = layout.X;
                        y = layout.Y;
                    }
                }

                if (rawNodeByKey.ContainsKey(
                    node.Key))
                {
                    throw new InvalidOperationException(
                        "Export map contains duplicate node keys.");
                }

                rawNodeByKey.Add(
                    node.Key,
                    new RawRectangle(
                        x,
                        y,
                        TopologyExportDiagramPolicy.NodeWidth,
                        TopologyExportDiagramPolicy.NodeHeight));
            }

            var locationById =
                map.Locations
                    .ToDictionary(
                        item => item.Id);

            var locationOrder =
                map.Locations
                    .OrderBy(
                        item => item.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id)
                    .Select(
                        (item, index) =>
                            new
                            {
                                item.Id,
                                Index = index
                            })
                    .ToDictionary(
                        item => item.Id,
                        item => item.Index);

            var rawLocationById =
                new Dictionary<Guid, RawRectangle>();

            var resolving =
                new HashSet<Guid>();

            foreach (var location in
                map.Locations)
            {
                ResolveLocation(
                    location,
                    map.Nodes,
                    locationById,
                    locationOrder,
                    persistedLocationById,
                    rawNodeByKey,
                    rawLocationById,
                    resolving);
            }

            var content =
                new List<RawRectangle>();

            content.AddRange(
                rawNodeByKey.Values);

            content.AddRange(
                rawLocationById.Values);

            double minX;
            double minY;
            double maxX;
            double maxY;

            if (content.Count == 0)
            {
                minX = 0.0;
                minY = 0.0;
                maxX = 1.0;
                maxY = 1.0;
            }
            else
            {
                minX =
                    content.Min(
                        item => item.X);

                minY =
                    content.Min(
                        item => item.Y);

                maxX =
                    content.Max(
                        item => item.Right);

                maxY =
                    content.Max(
                        item => item.Bottom);
            }

            var logicalWidth =
                Math.Max(
                    1.0,
                    (maxX - minX) +
                    (2.0 *
                     TopologyExportDiagramPolicy.ContentMargin));

            var logicalHeight =
                Math.Max(
                    1.0,
                    (maxY - minY) +
                    (2.0 *
                     TopologyExportDiagramPolicy.ContentMargin));

            var scale =
                ComputeScale(
                    logicalWidth,
                    logicalHeight);

            var pixelWidth =
                Math.Max(
                    1,
                    Math.Min(
                        TopologyExportDiagramPolicy.MaxPixelDimension,
                        (int)Math.Floor(
                            logicalWidth *
                            scale)));

            var pixelHeight =
                Math.Max(
                    1,
                    Math.Min(
                        TopologyExportDiagramPolicy.MaxPixelDimension,
                        (int)Math.Floor(
                            logicalHeight *
                            scale)));

            scale =
                Math.Min(
                    scale,
                    Math.Min(
                        pixelWidth /
                        logicalWidth,
                        pixelHeight /
                        logicalHeight));

            var offsetX =
                -minX +
                TopologyExportDiagramPolicy.ContentMargin;

            var offsetY =
                -minY +
                TopologyExportDiagramPolicy.ContentMargin;

            var nodes =
                map.Nodes
                    .Select(
                        node =>
                        {
                            var raw =
                                rawNodeByKey[
                                    node.Key];

                            return
                                new TopologyExportDiagramNode(
                                    node,
                                    (raw.X + offsetX) *
                                        scale,
                                    (raw.Y + offsetY) *
                                        scale,
                                    raw.Width *
                                        scale,
                                    raw.Height *
                                        scale);
                        })
                    .ToArray();

            var nodeByKey =
                nodes.ToDictionary(
                    item => item.Source.Key,
                    StringComparer.Ordinal);

            var links =
                new List<TopologyExportDiagramLink>();

            foreach (var link in
                map.Links)
            {
                TopologyExportDiagramNode source;
                TopologyExportDiagramNode target;

                if (!nodeByKey.TryGetValue(
                        link.SourceNodeKey,
                        out source) ||
                    !nodeByKey.TryGetValue(
                        link.TargetNodeKey,
                        out target))
                {
                    continue;
                }

                links.Add(
                    new TopologyExportDiagramLink(
                        link,
                        source.X +
                            (source.Width / 2.0),
                        source.Y +
                            (source.Height / 2.0),
                        target.X +
                            (target.Width / 2.0),
                        target.Y +
                            (target.Height / 2.0),
                        LinkLabel(
                            link)));
            }

            var depths =
                new Dictionary<Guid, int>();

            var locations =
                map.Locations
                    .Select(
                        location =>
                        {
                            var raw =
                                rawLocationById[
                                    location.Id];

                            return
                                new TopologyExportDiagramLocation(
                                    location,
                                    LocationDepth(
                                        location,
                                        locationById,
                                        depths,
                                        new HashSet<Guid>()),
                                    (raw.X + offsetX) *
                                        scale,
                                    (raw.Y + offsetY) *
                                        scale,
                                    raw.Width *
                                        scale,
                                    raw.Height *
                                        scale);
                        })
                    .OrderBy(
                        item => item.Depth)
                    .ThenBy(
                        item => item.Source.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(
                        item => item.Source.Id)
                    .ToArray();

            return new TopologyExportDiagram(
                pixelWidth,
                pixelHeight,
                scale,
                nodes,
                links,
                locations);
        }

        private static double ComputeScale(
            double logicalWidth,
            double logicalHeight)
        {
            var maxDimension =
                Math.Max(
                    logicalWidth,
                    logicalHeight);

            var dimensionScale =
                maxDimension <=
                    TopologyExportDiagramPolicy.MaxPixelDimension
                    ? 1.0
                    : TopologyExportDiagramPolicy.MaxPixelDimension /
                      maxDimension;

            var logicalPixelCount =
                logicalWidth *
                logicalHeight;

            var pixelBudgetScale =
                logicalPixelCount <=
                    TopologyExportDiagramPolicy.MaxPixelCount
                    ? 1.0
                    : Math.Sqrt(
                        TopologyExportDiagramPolicy.MaxPixelCount /
                        logicalPixelCount);

            return
                Math.Min(
                    1.0,
                    Math.Min(
                        dimensionScale,
                        pixelBudgetScale));
        }

        private static RawRectangle ResolveLocation(
            MapLocation location,
            IReadOnlyList<MapNode> nodes,
            IReadOnlyDictionary<Guid, MapLocation> locationById,
            IReadOnlyDictionary<Guid, int> locationOrder,
            IReadOnlyDictionary<Guid, MapLocationLayout> persistedLocationById,
            IReadOnlyDictionary<string, RawRectangle> rawNodeByKey,
            IDictionary<Guid, RawRectangle> rawLocationById,
            ISet<Guid> resolving)
        {
            RawRectangle existing;

            if (rawLocationById.TryGetValue(
                    location.Id,
                    out existing))
            {
                return existing;
            }

            if (!resolving.Add(
                    location.Id))
            {
                throw new InvalidOperationException(
                    "Export location hierarchy contains a cycle.");
            }

            try
            {
                MapLocationLayout persisted;

                if (persistedLocationById.TryGetValue(
                        location.Id,
                        out persisted))
                {
                    var persistedBounds =
                        new RawRectangle(
                            persisted.X,
                            persisted.Y,
                            Math.Max(
                                TopologyExportDiagramPolicy.LocationMinWidth,
                                persisted.Width),
                            Math.Max(
                                TopologyExportDiagramPolicy.LocationMinHeight,
                                persisted.Height));

                    rawLocationById.Add(
                        location.Id,
                        persistedBounds);

                    return persistedBounds;
                }

                var content =
                    new List<RawRectangle>();

                foreach (var node in nodes)
                {
                    if (node.LocationId !=
                        location.Id)
                    {
                        continue;
                    }

                    RawRectangle nodeBounds;

                    if (rawNodeByKey.TryGetValue(
                            node.Key,
                            out nodeBounds))
                    {
                        content.Add(
                            nodeBounds);
                    }
                }

                foreach (var child in
                    locationById.Values
                        .Where(
                            item =>
                                item.ParentLocationId ==
                                location.Id))
                {
                    content.Add(
                        ResolveLocation(
                            child,
                            nodes,
                            locationById,
                            locationOrder,
                            persistedLocationById,
                            rawNodeByKey,
                            rawLocationById,
                            resolving));
                }

                RawRectangle result;

                if (content.Count > 0)
                {
                    var left =
                        content.Min(
                            item => item.X) -
                        TopologyExportDiagramPolicy
                            .LocationContentPadding;

                    var top =
                        content.Min(
                            item => item.Y) -
                        TopologyExportDiagramPolicy
                            .LocationContentPadding -
                        TopologyExportDiagramPolicy
                            .LocationHeaderHeight;

                    var right =
                        content.Max(
                            item => item.Right) +
                        TopologyExportDiagramPolicy
                            .LocationContentPadding;

                    var bottom =
                        content.Max(
                            item => item.Bottom) +
                        TopologyExportDiagramPolicy
                            .LocationContentPadding;

                    result =
                        new RawRectangle(
                            left,
                            top,
                            Math.Max(
                                TopologyExportDiagramPolicy.LocationMinWidth,
                                right - left),
                            Math.Max(
                                TopologyExportDiagramPolicy.LocationMinHeight,
                                bottom - top));
                }
                else
                {
                    RawRectangle parentPersistedBounds;

                    if (location.ParentLocationId.HasValue &&
                        TryGetPersistedLocationBounds(
                            location.ParentLocationId.Value,
                            persistedLocationById,
                            out parentPersistedBounds))
                    {
                        var availableWidth =
                            Math.Max(
                                TopologyExportDiagramPolicy.LocationMinWidth,
                                parentPersistedBounds.Width -
                                (2.0 *
                                 TopologyExportDiagramPolicy.LocationContentPadding));

                        var availableHeight =
                            Math.Max(
                                TopologyExportDiagramPolicy.LocationMinHeight,
                                parentPersistedBounds.Height -
                                TopologyExportDiagramPolicy.LocationHeaderHeight -
                                (2.0 *
                                 TopologyExportDiagramPolicy.LocationContentPadding));

                        result =
                            new RawRectangle(
                                parentPersistedBounds.X +
                                    TopologyExportDiagramPolicy.LocationContentPadding,
                                parentPersistedBounds.Y +
                                    TopologyExportDiagramPolicy.LocationHeaderHeight +
                                    TopologyExportDiagramPolicy.LocationContentPadding,
                                Math.Min(
                                    TopologyExportDiagramPolicy.LocationDefaultWidth,
                                    availableWidth),
                                Math.Min(
                                    TopologyExportDiagramPolicy.LocationDefaultHeight,
                                    availableHeight));
                    }
                    else
                    {
                        var order =
                            locationOrder[
                                location.Id];

                        var column =
                            order % 3;

                        var row =
                            order / 3;

                        result =
                            new RawRectangle(
                                (column *
                                    (TopologyExportDiagramPolicy.LocationDefaultWidth +
                                     56.0)) -
                                    (TopologyExportDiagramPolicy.LocationDefaultWidth /
                                     2.0),
                                (row *
                                    (TopologyExportDiagramPolicy.LocationDefaultHeight +
                                     56.0)) -
                                    (TopologyExportDiagramPolicy.LocationDefaultHeight /
                                     2.0),
                                TopologyExportDiagramPolicy.LocationDefaultWidth,
                                TopologyExportDiagramPolicy.LocationDefaultHeight);
                    }
                }

                rawLocationById.Add(
                    location.Id,
                    result);

                return result;
            }
            finally
            {
                resolving.Remove(
                    location.Id);
            }
        }

        private static bool TryGetPersistedLocationBounds(
            Guid locationId,
            IReadOnlyDictionary<Guid, MapLocationLayout> persistedLocationById,
            out RawRectangle bounds)
        {
            MapLocationLayout layout;

            if (!persistedLocationById.TryGetValue(
                    locationId,
                    out layout))
            {
                bounds = null;
                return false;
            }

            bounds =
                new RawRectangle(
                    layout.X,
                    layout.Y,
                    Math.Max(
                        TopologyExportDiagramPolicy.LocationMinWidth,
                        layout.Width),
                    Math.Max(
                        TopologyExportDiagramPolicy.LocationMinHeight,
                        layout.Height));

            return true;
        }

        private static int LocationDepth(
            MapLocation location,
            IReadOnlyDictionary<Guid, MapLocation> locationById,
            IDictionary<Guid, int> knownDepths,
            ISet<Guid> resolving)
        {
            int known;

            if (knownDepths.TryGetValue(
                    location.Id,
                    out known))
            {
                return known;
            }

            if (!resolving.Add(
                    location.Id))
            {
                throw new InvalidOperationException(
                    "Export location hierarchy contains a cycle.");
            }

            try
            {
                var depth = 0;

                if (location.ParentLocationId.HasValue)
                {
                    MapLocation parent;

                    if (locationById.TryGetValue(
                            location.ParentLocationId.Value,
                            out parent))
                    {
                        depth =
                            LocationDepth(
                                parent,
                                locationById,
                                knownDepths,
                                resolving) +
                            1;
                    }
                }

                knownDepths[
                    location.Id] =
                    depth;

                return depth;
            }
            finally
            {
                resolving.Remove(
                    location.Id);
            }
        }

        private static string LinkLabel(
            MapLink link)
        {
            var source =
                string.IsNullOrWhiteSpace(
                    link.SourcePortLabel)
                    ? null
                    : link.SourcePortLabel.Trim();

            var target =
                string.IsNullOrWhiteSpace(
                    link.TargetPortLabel)
                    ? null
                    : link.TargetPortLabel.Trim();

            if (source == null)
            {
                return target;
            }

            if (target == null)
            {
                return source;
            }

            return
                source +
                " \u2194 " +
                target;
        }

        private sealed class RawRectangle
        {
            public RawRectangle(
                double x,
                double y,
                double width,
                double height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public double X { get; }

            public double Y { get; }

            public double Width { get; }

            public double Height { get; }

            public double Right
            {
                get
                {
                    return X + Width;
                }
            }

            public double Bottom
            {
                get
                {
                    return Y + Height;
                }
            }
        }
    }
}
