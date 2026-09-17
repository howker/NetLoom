using System;

namespace NetLoom.Wpf.MapInteraction
{
    public static class MapVirtualWorkspace
    {
        public static double ToCanvasCoordinate(
            double logicalCoordinate,
            double virtualOrigin)
        {
            ValidateFinite(
                logicalCoordinate,
                nameof(logicalCoordinate));

            ValidateFinite(
                virtualOrigin,
                nameof(virtualOrigin));

            return
                virtualOrigin +
                logicalCoordinate;
        }

        public static double ToLogicalCoordinate(
            double canvasCoordinate,
            double virtualOrigin)
        {
            ValidateFinite(
                canvasCoordinate,
                nameof(canvasCoordinate));

            ValidateFinite(
                virtualOrigin,
                nameof(virtualOrigin));

            return
                canvasCoordinate -
                virtualOrigin;
        }

        public static double ToScrollOffset(
            double logicalPan,
            double virtualOrigin,
            double zoom)
        {
            ValidateFinite(
                logicalPan,
                nameof(logicalPan));

            ValidateFinite(
                virtualOrigin,
                nameof(virtualOrigin));

            ValidateZoom(
                zoom);

            return
                (virtualOrigin * zoom) +
                logicalPan;
        }

        public static double ToLogicalPan(
            double scrollOffset,
            double virtualOrigin,
            double zoom)
        {
            ValidateFinite(
                scrollOffset,
                nameof(scrollOffset));

            ValidateFinite(
                virtualOrigin,
                nameof(virtualOrigin));

            ValidateZoom(
                zoom);

            return
                scrollOffset -
                (virtualOrigin * zoom);
        }

        private static void ValidateZoom(
            double zoom)
        {
            ValidateFinite(
                zoom,
                nameof(zoom));

            if (zoom <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(zoom));
            }
        }

        private static void ValidateFinite(
            double value,
            string parameterName)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
        }
    }
}
