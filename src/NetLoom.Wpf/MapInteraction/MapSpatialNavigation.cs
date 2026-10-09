using System;
using System.Collections.Generic;
using System.Windows;

namespace NetLoom.Wpf.MapInteraction
{
    // Направление стрелки при переходе между элементами карты.
    public enum MapNavigationDirection
    {
        Left = 0,
        Right = 1,
        Up = 2,
        Down = 3
    }

    // Элемент карты, к которому можно перейти с клавиатуры: ключ и прямоугольник в координатах холста.
    public sealed class MapSpatialItem
    {
        public MapSpatialItem(string key, Rect bounds)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Bounds = bounds;
        }

        public string Key { get; }

        public Rect Bounds { get; }

        public Point Center
        {
            get { return new Point(Bounds.Left + Bounds.Width / 2.0, Bounds.Top + Bounds.Height / 2.0); }
        }
    }

    // Чистая логика перехода стрелками (UI_DESIGN_RULES §8, K4): без элементов WPF, только прямоугольники.
    // Кандидат — элемент, чей центр лежит в полуплоскости направления. Оценка (меньше — лучше):
    // Расстояние по оси направления плюс удвоенное поперечное отклонение центров.
    // При равной оценке выигрывает связанный с текущим элементом сосед, затем меньший ключ (Ordinal).
    public static class MapSpatialNavigation
    {
        // Вес поперечного отклонения: элемент «прямо по курсу» важнее ближайшего, но сбоку.
        private const double CrossWeight = 2.0;

        // Допуск сравнения: оценки в пределах допуска считаются равными.
        private const double ScoreTolerance = 0.001;

        public static MapSpatialItem Next(
            MapSpatialItem current,
            IEnumerable<MapSpatialItem> candidates,
            MapNavigationDirection direction,
            ISet<string> relatedKeys)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            var origin = current.Center;
            MapSpatialItem best = null;
            var bestScore = 0.0;

            foreach (var candidate in candidates)
            {
                if (candidate == null || string.Equals(candidate.Key, current.Key, StringComparison.Ordinal))
                {
                    continue;
                }

                var center = candidate.Center;
                double along;
                double cross;

                switch (direction)
                {
                    case MapNavigationDirection.Left:
                        along = origin.X - center.X;
                        cross = Math.Abs(center.Y - origin.Y);
                        break;
                    case MapNavigationDirection.Right:
                        along = center.X - origin.X;
                        cross = Math.Abs(center.Y - origin.Y);
                        break;
                    case MapNavigationDirection.Up:
                        along = origin.Y - center.Y;
                        cross = Math.Abs(center.X - origin.X);
                        break;
                    default:
                        along = center.Y - origin.Y;
                        cross = Math.Abs(center.X - origin.X);
                        break;
                }

                // Центр должен лежать строго в полуплоскости направления.
                if (along <= ScoreTolerance)
                {
                    continue;
                }

                var score = along + CrossWeight * cross;

                if (best == null || IsBetter(candidate, score, best, bestScore, relatedKeys))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static bool IsBetter(
            MapSpatialItem candidate,
            double score,
            MapSpatialItem best,
            double bestScore,
            ISet<string> relatedKeys)
        {
            if (score < bestScore - ScoreTolerance)
            {
                return true;
            }

            if (score > bestScore + ScoreTolerance)
            {
                return false;
            }

            var candidateRelated = relatedKeys != null && relatedKeys.Contains(candidate.Key);
            var bestRelated = relatedKeys != null && relatedKeys.Contains(best.Key);

            if (candidateRelated != bestRelated)
            {
                return candidateRelated;
            }

            return string.CompareOrdinal(candidate.Key, best.Key) < 0;
        }
    }
}
