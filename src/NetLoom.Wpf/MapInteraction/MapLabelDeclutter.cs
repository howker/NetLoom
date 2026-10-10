using System;
using System.Collections.Generic;
using System.Windows;

namespace NetLoom.Wpf.MapInteraction
{
    // Кандидат на показ подписи: упорядоченные положения в экранных координатах холста,
    // Приоритет (меньше — важнее) и признак обхода неподвижных препятствий.
    public sealed class MapLabelCandidate
    {
        public MapLabelCandidate(string key, Rect bounds, int priority)
            : this(key, bounds, priority, true)
        {
        }

        public MapLabelCandidate(string key, Rect bounds, int priority, bool avoidObstacles)
            : this(key, new[] { bounds }, priority, avoidObstacles)
        {
        }

        public MapLabelCandidate(string key, IEnumerable<Rect> boundsOptions, int priority,
            bool avoidObstacles = true)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            if (boundsOptions == null) throw new ArgumentNullException(nameof(boundsOptions));
            var options = new List<Rect>(boundsOptions);
            if (options.Count == 0) throw new ArgumentException("At least one position is required.", nameof(boundsOptions));
            BoundsOptions = options.AsReadOnly();
            Priority = priority;
            AvoidObstacles = avoidObstacles;
        }

        public string Key { get; }

        public Rect Bounds => BoundsOptions[0];

        public IReadOnlyList<Rect> BoundsOptions { get; }

        public int Priority { get; }

        public bool AvoidObstacles { get; }
    }

    // Чистая логика снятия наложений: подписи берутся жадно по приоритету, при равном — по ключу (Ordinal).
    // Для каждой подписи выбирается первое свободное положение; препятствия никогда не скрываются.
    public static class MapLabelDeclutter
    {
        public static HashSet<string> SelectVisible(IEnumerable<MapLabelCandidate> candidates, double gap)
        {
            return new HashSet<string>(SelectPlacements(candidates, gap).Keys, StringComparer.Ordinal);
        }

        public static IReadOnlyDictionary<string, Rect> SelectPlacements(
            IEnumerable<MapLabelCandidate> candidates, double gap, IEnumerable<Rect> obstacles = null)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var ordered = new List<MapLabelCandidate>(candidates);
            ordered.Sort((left, right) =>
            {
                var byPriority = left.Priority.CompareTo(right.Priority);
                return byPriority != 0 ? byPriority : string.CompareOrdinal(left.Key, right.Key);
            });
            var margin = Math.Max(0.0, gap);
            var fixedBounds = obstacles == null ? new List<Rect>() : new List<Rect>(obstacles);
            var shown = new Dictionary<string, Rect>(StringComparer.Ordinal);
            foreach (var candidate in ordered)
            {
                foreach (var bounds in candidate.BoundsOptions)
                {
                    if (bounds.IsEmpty) continue;
                    var expanded = new Rect(bounds.Left - margin, bounds.Top - margin,
                        bounds.Width + 2 * margin, bounds.Height + 2 * margin);
                    if (OverlapsAny(expanded, shown.Values) ||
                        (candidate.AvoidObstacles && OverlapsAny(expanded, fixedBounds))) continue;
                    shown.Add(candidate.Key, bounds);
                    break;
                }
            }
            return shown;
        }

        private static bool OverlapsAny(Rect bounds, IEnumerable<Rect> others)
        {
            foreach (var other in others)
            {
                // Пересечение с положительной площадью: касание краёв наложением не считается.
                if (bounds.Left < other.Right && other.Left < bounds.Right &&
                    bounds.Top < other.Bottom && other.Top < bounds.Bottom) return true;
            }
            return false;
        }
    }
}
