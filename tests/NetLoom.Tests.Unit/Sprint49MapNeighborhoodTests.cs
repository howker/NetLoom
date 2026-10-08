using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapNeighborhoodTests
    {
        private static readonly Guid[] Devices = Enumerable.Range(1, 7)
            .Select(i => Guid.Parse("49494949-0007-0000-0000-" + i.ToString("D12"))).ToArray();

        private static MapNeighborhoodLink Link(int a, int b, DiagnosticStpUplink direction) =>
            new MapNeighborhoodLink(Guid.NewGuid(), Devices[a], Devices[b], direction);

        [TestMethod]
        public void InitialIncludesAllImmediateNeighborsButDoesNotTraverseThem()
        {
            var links = new[]
            {
                Link(0, 1, DiagnosticStpUplink.SideAIsUpstream),
                Link(1, 2, DiagnosticStpUplink.Unknown),
                Link(1, 3, DiagnosticStpUplink.SideBIsUpstream),
                Link(3, 4, DiagnosticStpUplink.SideBIsUpstream)
            };
            CollectionAssert.AreEquivalent(new[] { Devices[0], Devices[1], Devices[2], Devices[3] },
                MapNeighborhood.Initial(Devices[1], links).ToArray());
        }

        [TestMethod]
        public void DirectionUsesStpRatherThanEndpointOrderAndExpandsOneStep()
        {
            var links = new[]
            {
                Link(0, 1, DiagnosticStpUplink.SideAIsUpstream),
                Link(2, 0, DiagnosticStpUplink.SideBIsUpstream),
                Link(1, 3, DiagnosticStpUplink.SideAIsUpstream),
                Link(4, 3, DiagnosticStpUplink.SideBIsUpstream),
                Link(1, 5, DiagnosticStpUplink.Unknown),
                Link(0, 6, DiagnosticStpUplink.SideBIsUpstream)
            };
            var visible = new HashSet<Guid> { Devices[1] };
            CollectionAssert.AreEquivalent(new[] { Devices[1], Devices[0] },
                MapNeighborhood.ExpandUp(visible, links).ToArray());
            CollectionAssert.AreEquivalent(new[] { Devices[1], Devices[3] },
                MapNeighborhood.ExpandDown(visible, links).ToArray());
            Assert.AreEqual(1, visible.Count, "Expansion must leave the input set unchanged.");
        }

        [TestMethod]
        public void UnknownDirectionRequiresSeparateActionAndAvailabilityTracksBoundary()
        {
            var links = new[]
            {
                Link(0, 1, DiagnosticStpUplink.Unknown),
                Link(1, 2, DiagnosticStpUplink.Unknown)
            };
            var visible = new HashSet<Guid> { Devices[0] };
            Assert.IsFalse(MapNeighborhood.CanExpandUp(visible, links));
            Assert.IsFalse(MapNeighborhood.CanExpandDown(visible, links));
            Assert.IsTrue(MapNeighborhood.CanExpandUndirected(visible, links));
            Assert.AreEqual(1, MapNeighborhood.ExpandUp(visible, links).Count);
            Assert.AreEqual(1, MapNeighborhood.ExpandDown(visible, links).Count);
            var expanded = MapNeighborhood.ExpandUndirected(visible, links);
            CollectionAssert.AreEquivalent(new[] { Devices[0], Devices[1] }, expanded.ToArray());
            expanded = MapNeighborhood.ExpandUndirected(expanded, links);
            Assert.IsFalse(MapNeighborhood.CanExpandUndirected(expanded, links));
        }

        [TestMethod]
        public void DirectedAvailabilityDisappearsAfterAllKnownDevicesAreShown()
        {
            var links = new[] { Link(0, 1, DiagnosticStpUplink.SideAIsUpstream) };
            Assert.IsTrue(MapNeighborhood.CanExpandUp(new HashSet<Guid> { Devices[1] }, links));
            Assert.IsTrue(MapNeighborhood.CanExpandDown(new HashSet<Guid> { Devices[0] }, links));
            var all = new HashSet<Guid> { Devices[0], Devices[1] };
            Assert.IsFalse(MapNeighborhood.CanExpandUp(all, links));
            Assert.IsFalse(MapNeighborhood.CanExpandDown(all, links));
            Assert.IsFalse(MapNeighborhood.CanExpandUndirected(all, links));
        }
    }
}
