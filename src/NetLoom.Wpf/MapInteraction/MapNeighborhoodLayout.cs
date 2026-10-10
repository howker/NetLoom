using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace NetLoom.Wpf.MapInteraction
{
    public sealed class MapNeighborhoodLayoutNode
    {
        public MapNeighborhoodLayoutNode(Guid deviceId, string identity, string name)
        {
            DeviceId = deviceId;
            Identity = identity;
            Name = name;
        }

        public Guid DeviceId { get; }
        public string Identity { get; }
        public string Name { get; }
    }

    // Временная геометрия представления: класс не знает о хранилище и рабочих координатах карты.
    public static class MapNeighborhoodLayout
    {
        public static IReadOnlyDictionary<string, Point> Arrange(
            IReadOnlyList<MapNeighborhoodLayoutNode> nodes, Guid selected,
            IReadOnlyList<MapNeighborhoodLink> links, IReadOnlyDictionary<Guid, int> pollingDistances,
            double nodeWidth, double nodeHeight, double columnGap, double rowGap, double availableWidth,
            double availableHeight, double columnGapMin, double rowGapMin, bool fullRowGapOnOverflow = false)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (links == null) throw new ArgumentNullException(nameof(links));
            if (nodeWidth <= 0 || nodeHeight <= 0 || columnGap < 0 || rowGap < 0 || availableWidth <= 0 ||
                availableHeight <= 0 || columnGapMin < 0 || rowGapMin < 0)
                throw new ArgumentOutOfRangeException(nameof(availableWidth));
            // Минимум не может превышать полный промежуток.
            columnGapMin = Math.Min(columnGapMin, columnGap);
            rowGapMin = Math.Min(rowGapMin, rowGap);
            var result = new Dictionary<string, Point>(StringComparer.Ordinal);
            if (nodes.Count == 0) return result;

            var shown = new HashSet<Guid>(nodes.Select(node => node.DeviceId));
            var distances = MapNeighborhood.Distances(selected,
                links.Where(link => shown.Contains(link.A) && shown.Contains(link.B)).ToArray());
            var disconnectedRow = distances.Values.Max() + 1;
            var known = pollingDistances == null ? new int[0]
                : nodes.Where(node => pollingDistances.ContainsKey(node.DeviceId))
                    .Select(node => pollingDistances[node.DeviceId]).ToArray();
            var firstDistance = known.Length == 0 ? 0 : known.Min();
            var unknownRow = known.Length == 0 ? 0 : known.Max() - firstDistance + 1;
            // Перенос ряда на новую строку — только если ряд не помещается и с минимальным промежутком.
            var columns = Math.Max(1, (int)Math.Min(nodes.Count,
                Math.Floor((availableWidth + columnGapMin) / (nodeWidth + columnGapMin))));
            var rows = nodes.GroupBy(node =>
            {
                int distance;
                if (pollingDistances != null && pollingDistances.TryGetValue(node.DeviceId, out distance))
                    return distance - firstDistance;
                return unknownRow + (distances.TryGetValue(node.DeviceId, out distance) ? distance : disconnectedRow);
            }).OrderBy(group => group.Key).ToArray();
            var widest = Math.Min(columns, rows.Max(row => row.Count()));
            // Промежуток между карточками сжимается от полного к минимальному, пока ряд помещается по ширине.
            var actualColumnGap = widest > 1
                ? Math.Max(columnGapMin, Math.Min(columnGap, (availableWidth - widest * nodeWidth) / (widest - 1)))
                : columnGap;
            var fullWidth = widest * nodeWidth + (widest - 1) * actualColumnGap;
            // Число строк с учётом переноса и пустых рядов между известными расстояниями.
            var lineCount = rows.Max(row => row.Key) + 1 +
                rows.Sum(row => (row.Count() + columns - 1) / columns - 1);
            var actualRowGap = lineCount > 1
                ? Math.Max(rowGapMin, Math.Min(rowGap, (availableHeight - lineCount * nodeHeight) / (lineCount - 1)))
                : rowGap;
            // Sprint 50 (прогноз отказа): если ряды не помещаются даже с минимальным промежутком, карта уйдёт
            // На дальний уровень с ярлыками имён над карточками — тогда нужен полный промежуток, иначе ярлык ложится на ряд выше.
            if (fullRowGapOnOverflow && lineCount > 1 &&
                lineCount * nodeHeight + (lineCount - 1) * rowGapMin > availableHeight)
            {
                // Число столбцов — по пропорциям окна: так раскладка вписывается крупнее, чем при ширине,
                // Рассчитанной на читаемый масштаб. Промежутки полные: ярлыки имён не ложатся на соседей.
                var maxRow = rows.Max(row => row.Count());
                var bestScale = 0.0;
                for (var candidate = 1; candidate <= maxRow; candidate++)
                {
                    var lines = rows.Max(row => row.Key) + 1 +
                        rows.Sum(row => (row.Count() + candidate - 1) / candidate - 1);
                    var candidateWidth = candidate * nodeWidth + (candidate - 1) * columnGap;
                    var candidateHeight = lines * nodeHeight + (lines - 1) * rowGap;
                    var scale = Math.Min(availableWidth / candidateWidth, availableHeight / candidateHeight);
                    if (scale <= bestScale) continue;
                    bestScale = scale;
                    columns = candidate;
                }
                widest = Math.Min(columns, maxRow);
                actualRowGap = rowGap;
                actualColumnGap = columnGap;
                fullWidth = widest * nodeWidth + (widest - 1) * actualColumnGap;
            }
            var wrappedRows = 0;
            foreach (var row in rows)
            {
                var ordered = row.OrderBy(node => node.Name, StringComparer.CurrentCulture)
                    .ThenBy(node => node.Identity, StringComparer.Ordinal).ToArray();
                for (var start = 0; start < ordered.Length; start += columns)
                {
                    var count = Math.Min(columns, ordered.Length - start);
                    var width = count * nodeWidth + (count - 1) * actualColumnGap;
                    var left = (fullWidth - width) / 2;
                    var top = (row.Key + wrappedRows) * (nodeHeight + actualRowGap);
                    for (var column = 0; column < count; column++)
                        result.Add(ordered[start + column].Identity,
                            new Point(left + column * (nodeWidth + actualColumnGap), top));
                    if (start + count < ordered.Length) wrappedRows++;
                }
            }
            return result;
        }
    }
}
