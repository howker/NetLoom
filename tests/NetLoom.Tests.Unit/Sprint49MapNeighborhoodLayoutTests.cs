using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapNeighborhoodLayoutTests
    {
        private static readonly Guid[] Devices = Enumerable.Range(1, 7)
            .Select(index => Guid.Parse("49494949-0010-0000-0000-" + index.ToString("D12"))).ToArray();

        [TestMethod]
        public void PollingDistanceDefinesRowsAndUnknownDevicesFollowTheKnownRows()
        {
            var nodes = Nodes("anchor", "upstream", "downstream", "unknown", "further");
            var links = Links(0, 1, 0, 2, 0, 3, 3, 4);
            var distances = new Dictionary<Guid, int>
            {
                [Devices[0]] = 6, [Devices[1]] = 5, [Devices[2]] = 8
            };
            var layout = Arrange(nodes, links, distances);
            Assert.AreEqual(0, layout["1"].Y);
            Assert.AreEqual(160, layout["0"].Y);
            Assert.AreEqual(480, layout["2"].Y);
            Assert.IsTrue(layout["3"].Y > layout["2"].Y);
            Assert.AreEqual(160, layout["4"].Y - layout["3"].Y);
        }

        [TestMethod]
        public void WithoutPollingPointRowsUseOnlyLinksInsideTheShownNeighborhood()
        {
            var nodes = Nodes("anchor", "neighbor", "second", "disconnected");
            // Через скрытое устройство 4 нельзя сократить путь к устройству 2 или связать устройство 3.
            var links = Links(0, 1, 1, 2, 0, 4, 4, 2, 4, 3);
            var layout = Arrange(nodes, links);
            Assert.AreEqual(0, layout["0"].Y);
            Assert.AreEqual(160, layout["1"].Y);
            Assert.AreEqual(320, layout["2"].Y);
            Assert.AreEqual(480, layout["3"].Y);
        }

        [TestMethod]
        public void RowUsesCurrentCultureNamesThenIdentityAndCentersNarrowRows()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                var nodes = Nodes("anchor", "Б", "А", "А");
                var layout = Arrange(nodes, Links(0, 1, 0, 2, 0, 3));
                Assert.IsTrue(layout["2"].X < layout["3"].X);
                Assert.IsTrue(layout["3"].X < layout["1"].X);
                Assert.AreEqual((layout["2"].X + layout["1"].X) / 2, layout["0"].X);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestMethod]
        public void LongRowsWrapWithoutOverlappingTheFollowingDistanceRow()
        {
            var nodes = Nodes("anchor", "a", "b", "c", "d", "e", "next");
            var links = Links(0, 1, 0, 2, 0, 3, 0, 4, 0, 5, 5, 6);
            var layout = Arrange(nodes, links, availableWidth: 540);
            Assert.AreEqual(layout["1"].Y, layout["2"].Y);
            Assert.AreEqual(layout["1"].Y + 160, layout["3"].Y);
            Assert.AreEqual(layout["3"].Y + 160, layout["5"].Y);
            Assert.AreEqual(layout["5"].Y + 160, layout["6"].Y);
            Assert.AreEqual(180, layout["5"].X);
            Assert.IsTrue(layout.Values.All(point => point.X >= 0 && point.X + 180 <= 540));
        }

        [TestMethod]
        public void InputOrderAndLinkDirectionDoNotChangePositions()
        {
            var nodes = Nodes("anchor", "c", "a", "b", "tail");
            var links = Links(0, 1, 0, 2, 0, 3, 3, 4);
            var expected = Arrange(nodes, links, availableWidth: 540);
            var actual = Arrange(nodes.Reverse().ToArray(), links.Reverse()
                .Select(link => new MapNeighborhoodLink(link.Id, link.B, link.A)).ToArray(), availableWidth: 540);
            foreach (var pair in expected) Assert.AreEqual(pair.Value, actual[pair.Key]);
        }

        [TestMethod]
        public void SmallHeightShrinksRowGapAndLayoutFitsAvailableHeight()
        {
            var nodes = Nodes("anchor", "a", "b", "c");
            var links = Links(0, 1, 1, 2, 2, 3);
            // Четыре ряда: при высоте 400 промежуток (400 − 4 × 64) / 3 = 48, между полным 96 и минимумом 32.
            var layout = Arrange(nodes, links, availableHeight: 400);
            Assert.AreEqual(64 + 48, layout["1"].Y - layout["0"].Y, 0.001);
            Assert.AreEqual(64 + 48, layout["3"].Y - layout["2"].Y, 0.001);
            Assert.IsTrue(layout.Values.Max(point => point.Y) + 64 <= 400 + 0.001);
        }

        [TestMethod]
        public void RowGapNeverDropsBelowTheMinimum()
        {
            var nodes = Nodes("anchor", "a", "b", "c");
            var links = Links(0, 1, 1, 2, 2, 3);
            var layout = Arrange(nodes, links, availableHeight: 100);
            Assert.AreEqual(64 + 32, layout["1"].Y - layout["0"].Y, 0.001);
            Assert.AreEqual(64 + 32, layout["3"].Y - layout["2"].Y, 0.001);
        }

        [TestMethod]
        public void EnoughHeightKeepsTheFullRowGap()
        {
            var nodes = Nodes("anchor", "a", "b");
            var links = Links(0, 1, 1, 2);
            var layout = Arrange(nodes, links, availableHeight: 5000);
            Assert.AreEqual(160, layout["1"].Y - layout["0"].Y, 0.001);
            Assert.AreEqual(160, layout["2"].Y - layout["1"].Y, 0.001);
        }

        [TestMethod]
        public void NarrowWidthShrinksColumnGapBeforeWrapping()
        {
            var nodes = Nodes("anchor", "a", "b");
            var links = Links(0, 1, 0, 2);
            // Две карточки по 180 помещаются в 450 только с промежутком 90 (не меньше минимума 32): переноса нет.
            var layout = MapNeighborhoodLayout.Arrange(nodes, Devices[0], links, null, 180, 64, 180, 96, 450,
                2000, 32, 32);
            Assert.AreEqual(layout["1"].Y, layout["2"].Y);
            Assert.AreEqual(90, layout["2"].X - layout["1"].X - 180, 0.001);
        }

        private static MapNeighborhoodLayoutNode[] Nodes(params string[] names) => names
            .Select((name, index) => new MapNeighborhoodLayoutNode(Devices[index], index.ToString(), name)).ToArray();

        private static MapNeighborhoodLink[] Links(params int[] pairs) => Enumerable.Range(0, pairs.Length / 2)
            .Select(index => new MapNeighborhoodLink(Guid.Empty, Devices[pairs[index * 2]], Devices[pairs[index * 2 + 1]]))
            .ToArray();

        private static IReadOnlyDictionary<string, Point> Arrange(
            MapNeighborhoodLayoutNode[] nodes, MapNeighborhoodLink[] links,
            IReadOnlyDictionary<Guid, int> distances = null, double availableWidth = 2000,
            double availableHeight = 5000) =>
            MapNeighborhoodLayout.Arrange(nodes, Devices[0], links, distances, 180, 64, 180, 96, availableWidth,
                availableHeight, 180, 32);
    }
}
