using System;

namespace NetLoom.Wpf.MapInteraction
{
    public enum MapMotionMode
    {
        Normal = 0,
        Reduced = 1,
        Off = 2
    }

    public enum MapMotionKind
    {
        Appearance = 0,
        Disappearance = 1,
        FreshnessChange = 2,
        SearchFocus = 3,
        AlertPulse = 4
    }

    public static class MapMotionPolicy
    {
        public static TimeSpan Duration(
            MapMotionMode mode,
            MapMotionKind kind)
        {
            if (mode == MapMotionMode.Off)
            {
                return TimeSpan.Zero;
            }

            var reduced =
                mode == MapMotionMode.Reduced;

            switch (kind)
            {
                case MapMotionKind.Appearance:
                case MapMotionKind.Disappearance:
                    return TimeSpan.FromMilliseconds(
                        reduced ? 70.0 : 180.0);

                case MapMotionKind.FreshnessChange:
                    return TimeSpan.FromMilliseconds(
                        reduced ? 90.0 : 220.0);

                case MapMotionKind.SearchFocus:
                    return TimeSpan.FromMilliseconds(
                        reduced ? 100.0 : 260.0);

                case MapMotionKind.AlertPulse:
                    return TimeSpan.FromMilliseconds(
                        reduced ? 120.0 : 320.0);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind));
            }
        }

        public static double PulseOpacity(
            MapMotionMode mode)
        {
            switch (mode)
            {
                case MapMotionMode.Normal:
                    return 0.45;

                case MapMotionMode.Reduced:
                    return 0.72;

                case MapMotionMode.Off:
                    return 1.0;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode));
            }
        }
    }
}
