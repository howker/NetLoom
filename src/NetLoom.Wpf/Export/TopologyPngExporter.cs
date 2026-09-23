using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetLoom.Application.Export;

namespace NetLoom.Wpf.Export
{
    public sealed class TopologyPngExporter
    {
        private static readonly Brush CanvasBrush =
            BrushFromRgb(
                0xED,
                0xF2,
                0xF6);

        private static readonly Brush SurfaceBrush =
            BrushFromRgb(
                0xFF,
                0xFF,
                0xFF);

        private static readonly Brush SurfaceMutedBrush =
            BrushFromRgb(
                0xF8,
                0xFA,
                0xFC);

        private static readonly Brush AccentSoftBrush =
            BrushFromRgb(
                0xEA,
                0xF1,
                0xFF);

        private static readonly Brush BorderBrush =
            BrushFromRgb(
                0x8F,
                0xA1,
                0xB2);

        private static readonly Brush TextPrimaryBrush =
            BrushFromRgb(
                0x17,
                0x21,
                0x2B);

        private static readonly Brush TextSecondaryBrush =
            BrushFromRgb(
                0x52,
                0x61,
                0x70);

        private static readonly Brush LinkBrush =
            BrushFromRgb(
                0x53,
                0x69,
                0x7E);

        private static readonly Brush AccentBrush =
            BrushFromRgb(
                0x2F,
                0x6F,
                0xED);

        private readonly TopologyExportDiagramBuilder
            _diagramBuilder;

        public TopologyPngExporter()
            : this(
                new TopologyExportDiagramBuilder())
        {
        }

        public TopologyPngExporter(
            TopologyExportDiagramBuilder diagramBuilder)
        {
            _diagramBuilder =
                diagramBuilder ??
                throw new ArgumentNullException(
                    nameof(diagramBuilder));
        }

        public byte[] Export(
            TopologyExportSnapshot snapshot)
        {
            return Export(
                _diagramBuilder.Build(
                    snapshot));
        }

        public byte[] Export(
            TopologyExportDiagram diagram)
        {
            if (diagram == null)
            {
                throw new ArgumentNullException(
                    nameof(diagram));
            }

            var visual =
                new DrawingVisual();

            using (var drawing =
                visual.RenderOpen())
            {
                drawing.DrawRectangle(
                    CanvasBrush,
                    null,
                    new Rect(
                        0.0,
                        0.0,
                        diagram.PixelWidth,
                        diagram.PixelHeight));

                DrawLocations(
                    drawing,
                    diagram);

                DrawLinks(
                    drawing,
                    diagram);

                DrawNodes(
                    drawing,
                    diagram);
            }

            var bitmap =
                new RenderTargetBitmap(
                    diagram.PixelWidth,
                    diagram.PixelHeight,
                    96.0,
                    96.0,
                    PixelFormats.Pbgra32);

            bitmap.Render(
                visual);

            var encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(
                    bitmap));

            using (var stream =
                new MemoryStream())
            {
                encoder.Save(
                    stream);

                return stream.ToArray();
            }
        }

        private static void DrawLocations(
            DrawingContext drawing,
            TopologyExportDiagram diagram)
        {
            foreach (var location in
                diagram.Locations
                    .OrderBy(item => item.Depth))
            {
                var rect =
                    new Rect(
                        location.X,
                        location.Y,
                        location.Width,
                        location.Height);

                drawing.DrawRoundedRectangle(
                    SurfaceMutedBrush,
                    new Pen(
                        BorderBrush,
                        1.0),
                    rect,
                    8.0,
                    8.0);

                var headerHeight =
                    Math.Min(
                        location.Height,
                        Math.Max(
                            18.0,
                            TopologyExportDiagramPolicy
                                .LocationHeaderHeight *
                            diagram.Scale));

                drawing.DrawRoundedRectangle(
                    AccentSoftBrush,
                    null,
                    new Rect(
                        location.X,
                        location.Y,
                        location.Width,
                        headerHeight),
                    8.0,
                    8.0);

                DrawText(
                    drawing,
                    location.Source.Name,
                    location.X + 10.0,
                    location.Y + 7.0,
                    Math.Max(
                        1.0,
                        location.Width - 20.0),
                    12.0,
                    TextPrimaryBrush);
            }
        }

        private static void DrawLinks(
            DrawingContext drawing,
            TopologyExportDiagram diagram)
        {
            var pen =
                new Pen(
                    LinkBrush,
                    2.0);

            foreach (var link in
                diagram.Links)
            {
                drawing.DrawLine(
                    pen,
                    new Point(
                        link.SourceX,
                        link.SourceY),
                    new Point(
                        link.TargetX,
                        link.TargetY));

                if (string.IsNullOrWhiteSpace(
                    link.Label))
                {
                    continue;
                }

                var text =
                    CreateText(
                        link.Label,
                        10.0,
                        TextPrimaryBrush);

                var x =
                    ((link.SourceX +
                      link.TargetX) /
                     2.0) -
                    (text.Width / 2.0);

                var y =
                    ((link.SourceY +
                      link.TargetY) /
                     2.0) -
                    (text.Height / 2.0);

                drawing.DrawRoundedRectangle(
                    SurfaceBrush,
                    new Pen(
                        BorderBrush,
                        1.0),
                    new Rect(
                        x - 4.0,
                        y - 2.0,
                        text.Width + 8.0,
                        text.Height + 4.0),
                    3.0,
                    3.0);

                drawing.DrawText(
                    text,
                    new Point(
                        x,
                        y));
            }
        }

        private static void DrawNodes(
            DrawingContext drawing,
            TopologyExportDiagram diagram)
        {
            foreach (var node in
                diagram.Nodes)
            {
                var rect =
                    new Rect(
                        node.X,
                        node.Y,
                        node.Width,
                        node.Height);

                drawing.DrawRoundedRectangle(
                    SurfaceBrush,
                    new Pen(
                        BorderBrush,
                        1.0),
                    rect,
                    6.0,
                    6.0);

                drawing.DrawRectangle(
                    AccentBrush,
                    null,
                    new Rect(
                        node.X,
                        node.Y,
                        Math.Min(
                            5.0,
                            node.Width),
                        node.Height));

                DrawText(
                    drawing,
                    node.Source.Label,
                    node.X + 12.0,
                    node.Y + 8.0,
                    Math.Max(
                        1.0,
                        node.Width - 20.0),
                    12.0,
                    TextPrimaryBrush);

                if (!string.IsNullOrWhiteSpace(
                    node.Source.SecondaryText))
                {
                    DrawText(
                        drawing,
                        node.Source.SecondaryText,
                        node.X + 12.0,
                        node.Y + 29.0,
                        Math.Max(
                            1.0,
                            node.Width - 20.0),
                        10.0,
                        TextSecondaryBrush);
                }
            }
        }

        private static void DrawText(
            DrawingContext drawing,
            string value,
            double x,
            double y,
            double maxWidth,
            double fontSize,
            Brush brush)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return;
            }

            var text =
                CreateText(
                    value,
                    fontSize,
                    brush);

            text.MaxTextWidth =
                Math.Max(
                    1.0,
                    maxWidth);

            text.Trimming =
                TextTrimming.CharacterEllipsis;

            drawing.DrawText(
                text,
                new Point(
                    x,
                    y));
        }

        private static FormattedText CreateText(
            string value,
            double fontSize,
            Brush brush)
        {
            return new FormattedText(
                value ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    "Segoe UI"),
                fontSize,
                brush);
        }

        private static Brush BrushFromRgb(
            byte red,
            byte green,
            byte blue)
        {
            var brush =
                new SolidColorBrush(
                    Color.FromRgb(
                        red,
                        green,
                        blue));

            brush.Freeze();

            return brush;
        }
    }
}
