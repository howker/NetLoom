using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace NetLoom.Wpf.MapInteraction
{
    public sealed class LocationOverlapFrame
    {
        public LocationOverlapFrame(Guid id, Guid? parentId, string name, Rect bounds)
        {
            Id = id;
            ParentId = parentId;
            Name = name;
            Bounds = bounds;
        }
        public Guid Id { get; }
        public Guid? ParentId { get; }
        public string Name { get; }
        public Rect Bounds { get; }
    }

    public sealed class LocationOverlap
    {
        internal LocationOverlap(LocationOverlapFrame first, LocationOverlapFrame second, bool childOutsideParent = false)
        {
            First = first;
            Second = second;
            ChildOutsideParent = childOutsideParent;
        }
        public LocationOverlapFrame First { get; }
        public LocationOverlapFrame Second { get; }

        // Вложенное размещение (First) выходит за рамку своего родителя (Second).
        public bool ChildOutsideParent { get; }
    }

    public static class LocationOverlapProjection
    {
        public static IReadOnlyList<LocationOverlap> Build(IReadOnlyList<LocationOverlapFrame> frames)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            var ordered = frames.OrderBy(frame => frame.Id).ToArray();
            var byId = ordered.ToDictionary(frame => frame.Id);
            var overlaps = new List<LocationOverlap>();
            for (var i = 0; i < ordered.Length; i++)
                for (var j = i + 1; j < ordered.Length; j++)
                {
                    var first = ordered[i];
                    var second = ordered[j];
                    if (IsAncestor(first.Id, second, byId) || IsAncestor(second.Id, first, byId))
                        continue;
                    var intersection = Rect.Intersect(first.Bounds, second.Bounds);
                    if (!intersection.IsEmpty && intersection.Width * intersection.Height >= 1.0)
                        overlaps.Add(new LocationOverlap(first, second));
                }
            // Sprint 49: сохранённая рамка вложенного размещения за пределами родителя не исправляется молча,
            // А показывается оператору.
            foreach (var child in ordered)
            {
                LocationOverlapFrame parent;
                if (child.ParentId.HasValue && byId.TryGetValue(child.ParentId.Value, out parent) &&
                    !parent.Bounds.Contains(child.Bounds))
                    overlaps.Add(new LocationOverlap(child, parent, true));
            }
            return overlaps.AsReadOnly();
        }

        private static bool IsAncestor(Guid id, LocationOverlapFrame frame,
            IReadOnlyDictionary<Guid, LocationOverlapFrame> byId)
        {
            var visited = new HashSet<Guid>();
            var parentId = frame.ParentId;
            while (parentId.HasValue && visited.Add(parentId.Value))
            {
                if (parentId.Value == id) return true;
                LocationOverlapFrame parent;
                if (!byId.TryGetValue(parentId.Value, out parent)) break;
                parentId = parent.ParentId;
            }
            return false;
        }
    }
}
