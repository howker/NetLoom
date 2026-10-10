using System;
using System.Collections.Generic;
using System.Windows;

namespace NetLoom.Wpf.MapInteraction
{
    public static class MapFreePlacement
    {
        public static Point FindFreeSpot(Rect desired, IReadOnlyList<Rect> occupied, double step, double margin)
        {
            if (desired.IsEmpty) throw new ArgumentException("A placement rectangle is required.", nameof(desired));
            if (occupied == null) throw new ArgumentNullException(nameof(occupied));
            if (double.IsNaN(step) || double.IsInfinity(step) || step <= 0)
                throw new ArgumentOutOfRangeException(nameof(step));
            if (double.IsNaN(margin) || double.IsInfinity(margin) || margin < 0)
                throw new ArgumentOutOfRangeException(nameof(margin));
            if (IsFree(desired, occupied, margin)) return desired.TopLeft;

            // Квадратная спираль: вправо, вниз, влево, вверх; не более 400 проверок.
            var x = 0;
            var y = 0;
            var direction = 0;
            var length = 1;
            var checkedSteps = 0;
            var dx = new[] { 1, 0, -1, 0 };
            var dy = new[] { 0, 1, 0, -1 };
            while (checkedSteps < 400)
            {
                for (var side = 0; side < 2 && checkedSteps < 400; side++)
                {
                    for (var offset = 0; offset < length && checkedSteps < 400; offset++)
                    {
                        x += dx[direction];
                        y += dy[direction];
                        checkedSteps++;
                        var candidate = new Rect(desired.X + x * step, desired.Y + y * step,
                            desired.Width, desired.Height);
                        if (IsFree(candidate, occupied, margin)) return candidate.TopLeft;
                    }
                    direction = (direction + 1) % 4;
                }
                length++;
            }
            return desired.TopLeft;
        }

        private static bool IsFree(Rect candidate, IReadOnlyList<Rect> occupied, double margin)
        {
            candidate.Inflate(margin, margin);
            foreach (var rectangle in occupied)
            {
                var intersection = Rect.Intersect(candidate, rectangle);
                if (!intersection.IsEmpty && intersection.Width > 0 && intersection.Height > 0)
                    return false;
            }
            return true;
        }
    }
}
