using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapNeighborhoodTests
    {
        private static readonly Guid[] Devices = Enumerable.Range(1, 7)
            .Select(i => Guid.Parse("49494949-0007-0000-0000-" + i.ToString("D12"))).ToArray();

        private static MapNeighborhoodLink Link(int a, int b) =>
            new MapNeighborhoodLink(Guid.NewGuid(), Devices[a], Devices[b]);

        [TestMethod]
        public void InitialIncludesAllImmediateNeighborsButDoesNotTraverseThem()
        {
            var links = new[] { Link(0, 1), Link(1, 2), Link(1, 3), Link(3, 4) };
            CollectionAssert.AreEquivalent(new[] { Devices[0], Devices[1], Devices[2], Devices[3] },
                MapNeighborhood.Initial(Devices[1], links).ToArray());
        }

        [TestMethod]
        public void DistancesAreBreadthFirstOverUndirectedLinksAndSkipUnreachableDevices()
        {
            // Ориентация связи в списке не важна: граф неориентированный.
            var links = new[] { Link(1, 0), Link(1, 2), Link(3, 2), Link(5, 6) };
            var distances = MapNeighborhood.Distances(Devices[0], links);
            Assert.AreEqual(0, distances[Devices[0]]);
            Assert.AreEqual(1, distances[Devices[1]]);
            Assert.AreEqual(2, distances[Devices[2]]);
            Assert.AreEqual(3, distances[Devices[3]]);
            Assert.IsFalse(distances.ContainsKey(Devices[5]));
            Assert.IsFalse(distances.ContainsKey(Devices[6]));
        }

        [TestMethod]
        public void DirectionFollowsDistanceToPollingPointAndExpandsOneStep()
        {
            // Точка опроса — устройство 0; устройство 1 на расстоянии 1, 2 и 4 — на 2, 3 — на 3.
            var links = new[] { Link(2, 1), Link(0, 1), Link(2, 3), Link(1, 4), Link(5, 3) };
            var distances = MapNeighborhood.Distances(Devices[0], links);
            var visible = new HashSet<Guid> { Devices[2] };
            CollectionAssert.AreEquivalent(new[] { Devices[2], Devices[1] },
                MapNeighborhood.ExpandUp(visible, links, distances).ToArray());
            CollectionAssert.AreEquivalent(new[] { Devices[2], Devices[3] },
                MapNeighborhood.ExpandDown(visible, links, distances).ToArray());
            Assert.AreEqual(1, visible.Count, "Expansion must leave the input set unchanged.");
        }

        [TestMethod]
        public void EqualDistanceHasNoDirectionAndNeedsTheSeparateAction()
        {
            // Устройства 1 и 2 одинаково удалены от точки опроса 0: связь между ними без направления.
            var links = new[] { Link(0, 1), Link(0, 2), Link(1, 2) };
            var distances = MapNeighborhood.Distances(Devices[0], links);
            var visible = new HashSet<Guid> { Devices[1] };
            Assert.IsTrue(MapNeighborhood.CanExpandUp(visible, links, distances));
            Assert.IsFalse(MapNeighborhood.CanExpandDown(visible, links, distances));
            Assert.IsTrue(MapNeighborhood.CanExpandUndirected(visible, links, distances));
            CollectionAssert.AreEquivalent(new[] { Devices[1], Devices[2] },
                MapNeighborhood.ExpandUndirected(visible, links, distances).ToArray());
            CollectionAssert.AreEquivalent(new[] { Devices[1], Devices[0] },
                MapNeighborhood.ExpandUp(visible, links, distances).ToArray());
        }

        [TestMethod]
        public void UndefinedPollingPointDisablesDirectionsAndLeavesOnlyUndirectedExpansion()
        {
            var links = new[] { Link(0, 1), Link(1, 2) };
            var visible = new HashSet<Guid> { Devices[1] };
            Assert.IsFalse(MapNeighborhood.CanExpandUp(visible, links, null));
            Assert.IsFalse(MapNeighborhood.CanExpandDown(visible, links, null));
            Assert.IsTrue(MapNeighborhood.CanExpandUndirected(visible, links, null));
            Assert.AreEqual(1, MapNeighborhood.ExpandUp(visible, links, null).Count);
            Assert.AreEqual(1, MapNeighborhood.ExpandDown(visible, links, null).Count);
            var expanded = MapNeighborhood.ExpandUndirected(visible, links, null);
            CollectionAssert.AreEquivalent(new[] { Devices[0], Devices[1], Devices[2] }, expanded.ToArray());
            Assert.IsFalse(MapNeighborhood.CanExpandUndirected(expanded, links, null));
        }

        [TestMethod]
        public void DevicesOutsideThePollingPointComponentAreExpandedOnlyUndirected()
        {
            var links = new[] { Link(0, 1), Link(5, 6) };
            var distances = MapNeighborhood.Distances(Devices[0], links);
            var visible = new HashSet<Guid> { Devices[5] };
            Assert.IsFalse(MapNeighborhood.CanExpandUp(visible, links, distances));
            Assert.IsFalse(MapNeighborhood.CanExpandDown(visible, links, distances));
            Assert.IsTrue(MapNeighborhood.CanExpandUndirected(visible, links, distances));
        }

        [TestMethod]
        public void DirectedAvailabilityDisappearsAfterAllKnownDevicesAreShown()
        {
            var links = new[] { Link(0, 1) };
            var distances = MapNeighborhood.Distances(Devices[0], links);
            Assert.IsTrue(MapNeighborhood.CanExpandUp(new HashSet<Guid> { Devices[1] }, links, distances));
            Assert.IsTrue(MapNeighborhood.CanExpandDown(new HashSet<Guid> { Devices[0] }, links, distances));
            var all = new HashSet<Guid> { Devices[0], Devices[1] };
            Assert.IsFalse(MapNeighborhood.CanExpandUp(all, links, distances));
            Assert.IsFalse(MapNeighborhood.CanExpandDown(all, links, distances));
            Assert.IsFalse(MapNeighborhood.CanExpandUndirected(all, links, distances));
        }
    }
}
