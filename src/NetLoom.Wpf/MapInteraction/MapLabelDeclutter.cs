using System;
using System.Collections.Generic;
using System.Windows;

namespace NetLoom.Wpf.MapInteraction
{
    // Кандидат на показ подписи уровня «Издалека»: ключ, прямоугольник в экранных координатах холста
    // и приоритет (меньше — важнее).
    public sealed class MapLabelCandidate
    {
        public MapLabelCandidate(string key, Rect bounds, int priority)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Bounds = bounds;
            Priority = priority;
        }

        public string Key { get; }

        public Rect Bounds { get; }

        public int Priority { get; }
    }

    // Чистая логика снятия наложений: подписи берутся жадно по приоритету, при равном — по ключу (Ordinal).
    // Подпись показывается, если её прямоугольник, расширенный на поле, не пересекает уже показанные.
    public static class MapLabelDeclutter
    {
        public static HashSet<string> SelectVisible(IEnumerable<MapLabelCandidate> candidates, double gap)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var ordered = new List<MapLabelCandidate>(candidates);
            ordered.Sort((left, right) =>
            {
                var byPriority = left.Priority.CompareTo(right.Priority);
                return byPriority != 0 ? byPriority : string.CompareOrdinal(left.Key, right.Key);
            });
            var margin = Math.Max(0.0, gap);
            var shown = new List<Rect>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in ordered)
            {
                var expanded = new Rect(candidate.Bounds.Left - margin, candidate.Bounds.Top - margin,
                    candidate.Bounds.Width + 2 * margin, candidate.Bounds.Height + 2 * margin);
                var overlaps = false;
                foreach (var other in shown)
                {
                    if (Overlap(expanded, other))
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps) continue;
                shown.Add(candidate.Bounds);
                keys.Add(candidate.Key);
            }
            return keys;
        }

        // Пересечение с положительной площадью: касание краёв наложением не считается.
        private static bool Overlap(Rect a, Rect b)
        {
            return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
        }
    }
}
