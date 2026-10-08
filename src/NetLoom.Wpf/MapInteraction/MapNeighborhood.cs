using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Contracts.Diagnostics;

namespace NetLoom.Wpf.MapInteraction
{
    public sealed class MapNeighborhoodLink
    {
        public MapNeighborhoodLink(Guid id, Guid a, Guid b, DiagnosticStpUplink stpUplink)
        {
            Id = id;
            A = a;
            B = b;
            StpUplink = stpUplink;
        }

        public Guid Id { get; }
        public Guid A { get; }
        public Guid B { get; }
        public DiagnosticStpUplink StpUplink { get; }
    }

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

        public static HashSet<Guid> ExpandUp(ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links) =>
            Expand(visible, links, 1);

        public static HashSet<Guid> ExpandDown(ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links) =>
            Expand(visible, links, -1);

        public static HashSet<Guid> ExpandUndirected(ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links) =>
            Expand(visible, links, 0);

        public static bool CanExpandUp(ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links) =>
            Candidates(visible, links, 1).Any();

        public static bool CanExpandDown(ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links) =>
            Candidates(visible, links, -1).Any();

        public static bool CanExpandUndirected(ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links) =>
            Candidates(visible, links, 0).Any();

        private static HashSet<Guid> Expand(ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, int direction)
        {
            // Проверяем исходную границу: одно действие добавляет ровно один шаг.
            var result = new HashSet<Guid>(visible);
            result.UnionWith(Candidates(visible, links, direction));
            return result;
        }

        private static IEnumerable<Guid> Candidates(
            ISet<Guid> visible, IReadOnlyList<MapNeighborhoodLink> links, int direction)
        {
            foreach (var link in links)
            {
                // Без данных STP связь не получает направления даже при известной геометрии.
                if (direction == 0)
                {
                    if (link.StpUplink != DiagnosticStpUplink.Unknown) continue;
                    if (visible.Contains(link.A) && !visible.Contains(link.B)) yield return link.B;
                    if (visible.Contains(link.B) && !visible.Contains(link.A)) yield return link.A;
                    continue;
                }
                if (link.StpUplink == DiagnosticStpUplink.Unknown) continue;
                var upstream = link.StpUplink == DiagnosticStpUplink.SideAIsUpstream ? link.A : link.B;
                var downstream = upstream == link.A ? link.B : link.A;
                var from = direction > 0 ? downstream : upstream;
                var to = direction > 0 ? upstream : downstream;
                if (visible.Contains(from) && !visible.Contains(to)) yield return to;
            }
        }
    }
}
