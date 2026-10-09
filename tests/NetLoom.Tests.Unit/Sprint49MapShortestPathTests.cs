using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapShortestPathTests
    {
        // Различается только последний байт, поэтому порядок Guid совпадает с числовым.
        private static Guid Device(int number) => new Guid(0x49494949, 9, 0, 0, 0, 0, 0, 0, 0, 0, (byte)number);

        private static Guid Id(int number) => new Guid(0x49494949, 9, 1, 0, 0, 0, 0, 0, 0, 0, (byte)number);

        private static MapNeighborhoodLink Link(int id, int a, int b) =>
            new MapNeighborhoodLink(Id(id), Device(a), Device(b));

        [TestMethod]
        public void DirectNeighborIsOneLink()
        {
            var result = MapShortestPath.Find(Device(1), Device(2), new[] { Link(1, 1, 2), Link(2, 3, 4) });

            Assert.IsTrue(result.Found);
            CollectionAssert.AreEqual(new[] { Device(1), Device(2) }, result.DeviceIds.ToArray());
            CollectionAssert.AreEqual(new[] { Id(1) }, result.LinkIds.ToArray());
        }

        [TestMethod]
        public void PathThroughTwoHopsListsDevicesAndLinksInOrderAndIgnoresLinkOrientation()
        {
            // Связи заданы в разной ориентации и порядке: граф неориентированный.
            var links = new[] { Link(2, 3, 2), Link(1, 1, 2), Link(3, 3, 4), Link(9, 5, 6) };

            var result = MapShortestPath.Find(Device(1), Device(3), links);

            Assert.IsTrue(result.Found);
            CollectionAssert.AreEqual(new[] { Device(1), Device(2), Device(3) }, result.DeviceIds.ToArray());
            CollectionAssert.AreEqual(new[] { Id(1), Id(2) }, result.LinkIds.ToArray());

            var back = MapShortestPath.Find(Device(3), Device(1), links);
            CollectionAssert.AreEqual(new[] { Device(3), Device(2), Device(1) }, back.DeviceIds.ToArray());
            CollectionAssert.AreEqual(new[] { Id(2), Id(1) }, back.LinkIds.ToArray());
        }

        [TestMethod]
        public void EqualPathsGiveTheSameDeterministicChoiceRegardlessOfLinkOrder()
        {
            // Два пути одной длины: 1-2-4 и 1-3-4; выбирается путь через меньший идентификатор устройства.
            var links = new[] { Link(1, 1, 2), Link(2, 2, 4), Link(3, 1, 3), Link(4, 3, 4) };

            var forward = MapShortestPath.Find(Device(1), Device(4), links);
            var reversed = MapShortestPath.Find(Device(1), Device(4), links.Reverse().ToArray());

            CollectionAssert.AreEqual(new[] { Device(1), Device(2), Device(4) }, forward.DeviceIds.ToArray());
            CollectionAssert.AreEqual(new[] { Id(1), Id(2) }, forward.LinkIds.ToArray());
            CollectionAssert.AreEqual(forward.DeviceIds.ToArray(), reversed.DeviceIds.ToArray());
            CollectionAssert.AreEqual(forward.LinkIds.ToArray(), reversed.LinkIds.ToArray());
        }

        [TestMethod]
        public void ParallelLinksAreOneEdgeAndTheSmallestIdentifierIsShown()
        {
            var links = new[] { Link(7, 1, 2), Link(3, 2, 1), Link(5, 1, 2), Link(4, 2, 3) };

            var result = MapShortestPath.Find(Device(1), Device(3), links);
            var reversed = MapShortestPath.Find(Device(1), Device(3), links.Reverse().ToArray());

            Assert.AreEqual(2, result.LinkIds.Count);
            Assert.AreEqual(Id(3), result.LinkIds[0]);
            Assert.AreEqual(Id(4), result.LinkIds[1]);
            CollectionAssert.AreEqual(result.LinkIds.ToArray(), reversed.LinkIds.ToArray());
        }

        [TestMethod]
        public void NoPathIsNotAnErrorAndSelfPathIsTheSingleDevice()
        {
            var links = new[] { Link(1, 1, 2), Link(2, 3, 4) };

            var none = MapShortestPath.Find(Device(1), Device(4), links);
            Assert.IsFalse(none.Found);
            Assert.AreEqual(0, none.DeviceIds.Count);
            Assert.AreEqual(0, none.LinkIds.Count);

            // Устройство вне графа связей достижимо только само из себя.
            Assert.IsFalse(MapShortestPath.Find(Device(1), Device(9), links).Found);

            var self = MapShortestPath.Find(Device(1), Device(1), links);
            Assert.IsTrue(self.Found);
            CollectionAssert.AreEqual(new[] { Device(1) }, self.DeviceIds.ToArray());
            Assert.AreEqual(0, self.LinkIds.Count);
        }

        [TestMethod]
        public void LinksWithTheSameEndpointOnBothSidesAreIgnored()
        {
            var result = MapShortestPath.Find(Device(1), Device(2), new[] { Link(1, 1, 1), Link(2, 1, 2) });

            CollectionAssert.AreEqual(new[] { Id(2) }, result.LinkIds.ToArray());
        }

        [TestMethod]
        public void MissingLinkListIsRejected()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MapShortestPath.Find(Device(1), Device(2), null));
        }
    }
}
