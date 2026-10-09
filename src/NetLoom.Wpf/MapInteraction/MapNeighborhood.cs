using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Wpf.MapInteraction
{
    public sealed class MapNeighborhoodLink
    {
        public MapNeighborhoodLink(Guid id, Guid a, Guid b)
        {
            Id = id;
            A = a;
            B = b;
        }

        public Guid Id { get; }
        public Guid A { get; }
        public Guid B { get; }
    }

    // ADR-085: «вверх» — ближе к точке опроса Engine по известным физическим связям.
    // Расстояния считаются от точки опроса (null — точка не определена, направления нет).
    public static class MapNeighborhood
    {
        public static HashSet<Guid> Initial(Guid selected, IReadOnlyList<MapNeighborhoodLink> links)
        {
            var visible = new HashSet<Guid> { selected };
            foreach (var link in links)
            {
                if (link.A == selected) visible.Add(link.B);
                if (link.B == selected) visible.Add(link.A);
            }
            return visible;
        }

        // Поиск в ширину по неориентированному графу физических связей: число связей до точки опроса.
        // Устройства, не связанные с точкой опроса, в результат не попадают.
        public static Dictionary<Guid, int> Distances(Guid origin, IReadOnlyList<MapNeighborhoodLink> links)
        {
            var neighbors = new Dictionary<Guid, List<Guid>>();
            foreach (var link in links)
            {
                if (link.A == link.B) continue;
                List<Guid> list;
                if (!neighbors.TryGetValue(link.A, out list)) neighbors[link.A] = list = new List<Guid>();
                list.Add(link.B);
                if (!neighbors.TryGetValue(link.B, out list)) neighbors[link.B] = list = new List<Guid>();
                list.Add(link.A);
            }
            var distances = new Dictionary<Guid, int> { { origin, 0 } };
            var queue = new Queue<Guid>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                List<Guid> next;
                if (!neighbors.TryGetValue(current, out next)) continue;
                foreach (var device in next)
                {
                    if (distances.ContainsKey(device)) continue;
                    distances[device] = distances[current] + 1;
                    queue.Enqueue(device);
                }
            }
            return distances;
        }

        public static HashSet<Guid> ExpandUp(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, IReadOnlyDictionary<Guid, int> distances) =>
            Expand(visible, links, distances, NeighborhoodDirection.Up);

        public static HashSet<Guid> ExpandDown(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, IReadOnlyDictionary<Guid, int> distances) =>
            Expand(visible, links, distances, NeighborhoodDirection.Down);

        public static HashSet<Guid> ExpandUndirected(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, IReadOnlyDictionary<Guid, int> distances) =>
            Expand(visible, links, distances, NeighborhoodDirection.None);

        public static bool CanExpandUp(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, IReadOnlyDictionary<Guid, int> distances) =>
            Candidates(visible, links, distances, NeighborhoodDirection.Up).Any();

        public static bool CanExpandDown(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, IReadOnlyDictionary<Guid, int> distances) =>
            Candidates(visible, links, distances, NeighborhoodDirection.Down).Any();

        public static bool CanExpandUndirected(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, IReadOnlyDictionary<Guid, int> distances) =>
            Candidates(visible, links, distances, NeighborhoodDirection.None).Any();

        private enum NeighborhoodDirection
        {
            Up,
            Down,
            None
        }

        private static HashSet<Guid> Expand(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links,
            IReadOnlyDictionary<Guid, int> distances, NeighborhoodDirection direction)
        {
            // Проверяем исходную границу: одно действие добавляет ровно один шаг.
            var result = new HashSet<Guid>(visible);
            result.UnionWith(Candidates(visible, links, distances, direction));
            return result;
        }

        private static IEnumerable<Guid> Candidates(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links,
            IReadOnlyDictionary<Guid, int> distances, NeighborhoodDirection direction)
        {
            foreach (var link in links)
            {
                // Связь имеет направление только при известных расстояниях обоих концов и их неравенстве.
                // Без точки опроса или при равном расстоянии её сосед раскрывается лишь отдельным действием.
                foreach (var pair in new[] { Tuple.Create(link.A, link.B), Tuple.Create(link.B, link.A) })
                {
                    if (!visible.Contains(pair.Item1) || visible.Contains(pair.Item2)) continue;
                    var sign = Sign(distances, pair.Item1, pair.Item2);
                    var matches = direction == NeighborhoodDirection.Up ? sign < 0
                        : direction == NeighborhoodDirection.Down ? sign > 0
                        : sign == 0;
                    if (matches) yield return pair.Item2;
                }
            }
        }

        // Меньше нуля — сосед ближе к точке опроса («вверх»); больше нуля — дальше («вниз»).
        // Нуль — направления нет.
        private static int Sign(IReadOnlyDictionary<Guid, int> distances, Guid from, Guid to)
        {
            int fromDistance;
            int toDistance;
            if (distances == null || !distances.TryGetValue(from, out fromDistance) ||
                !distances.TryGetValue(to, out toDistance)) return 0;
            return toDistance.CompareTo(fromDistance);
        }
    }
}
