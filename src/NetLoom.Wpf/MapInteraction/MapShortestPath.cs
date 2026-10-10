using System;
using System.Collections.Generic;
using System.Linq;

namespace NetLoom.Wpf.MapInteraction
{
    // Результат поиска кратчайшего известного физического пути между двумя устройствами.
    public sealed class MapShortestPathResult
    {
        private static readonly Guid[] NoIds = new Guid[0];

        public MapShortestPathResult(IReadOnlyList<Guid> deviceIds, IReadOnlyList<Guid> linkIds)
        {
            DeviceIds = deviceIds ?? NoIds;
            LinkIds = linkIds ?? NoIds;
        }

        // Пути нет: список устройств пуст.
        public static MapShortestPathResult NotFound { get; } = new MapShortestPathResult(NoIds, NoIds);

        public bool Found
        {
            get { return DeviceIds.Count > 0; }
        }

        // Устройства пути от начала к концу.
        public IReadOnlyList<Guid> DeviceIds { get; }

        // Связи пути в том же порядке: между соседними устройствами по одной.
        public IReadOnlyList<Guid> LinkIds { get; }
    }

    // Поиск в ширину по неориентированному графу физических связей.
    // Параллельные связи пары — одно ребро (для показа берётся связь с наименьшим идентификатором).
    // При нескольких равных по длине путях выбор детерминирован: соседи обходятся по возрастанию идентификатора.
    public static class MapShortestPath
    {
        public static MapShortestPathResult Find(Guid origin, Guid target, IReadOnlyList<MapNeighborhoodLink> links)
        {
            if (links == null) throw new ArgumentNullException(nameof(links));
            if (origin == target) return new MapShortestPathResult(new[] { origin }, new Guid[0]);

            var edges = new Dictionary<Tuple<Guid, Guid>, Guid>();
            foreach (var link in links)
            {
                if (link.A == link.B) continue;
                var key = Key(link.A, link.B);
                Guid existing;
                if (!edges.TryGetValue(key, out existing) || link.Id.CompareTo(existing) < 0) edges[key] = link.Id;
            }

            var neighbors = new Dictionary<Guid, List<Guid>>();
            foreach (var key in edges.Keys)
            {
                Add(neighbors, key.Item1, key.Item2);
                Add(neighbors, key.Item2, key.Item1);
            }
            foreach (var list in neighbors.Values) list.Sort();

            var parents = new Dictionary<Guid, Guid>();
            var queue = new Queue<Guid>();
            queue.Enqueue(origin);
            parents[origin] = origin;
            while (queue.Count > 0 && !parents.ContainsKey(target))
            {
                var current = queue.Dequeue();
                List<Guid> next;
                if (!neighbors.TryGetValue(current, out next)) continue;
                foreach (var device in next)
                {
                    if (parents.ContainsKey(device)) continue;
                    parents[device] = current;
                    queue.Enqueue(device);
                }
            }
            if (!parents.ContainsKey(target)) return MapShortestPathResult.NotFound;

            var devices = new List<Guid> { target };
            var step = target;
            while (step != origin)
            {
                step = parents[step];
                devices.Add(step);
            }
            devices.Reverse();
            var linkIds = new List<Guid>();
            for (var i = 0; i < devices.Count - 1; i++) linkIds.Add(edges[Key(devices[i], devices[i + 1])]);
            return new MapShortestPathResult(devices, linkIds);
        }

        private static Tuple<Guid, Guid> Key(Guid a, Guid b)
        {
            return a.CompareTo(b) <= 0 ? Tuple.Create(a, b) : Tuple.Create(b, a);
        }

        private static void Add(Dictionary<Guid, List<Guid>> neighbors, Guid device, Guid neighbor)
        {
            List<Guid> list;
            if (!neighbors.TryGetValue(device, out list)) neighbors[device] = list = new List<Guid>();
            list.Add(neighbor);
        }
    }
}
