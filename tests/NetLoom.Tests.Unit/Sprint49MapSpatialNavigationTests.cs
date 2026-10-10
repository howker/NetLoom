using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    // Sprint 49, K4: переход стрелками между элементами карты (чистая логика, без элементов WPF).
    [TestClass]
    public sealed class Sprint49MapSpatialNavigationTests
    {
        // Элемент 100×20 с левым верхним углом в (x, y); центр — (x + 50, y + 10).
        private static MapSpatialItem Item(string key, double x, double y) =>
            new MapSpatialItem(key, new Rect(x, y, 100, 20));

        private static string Next(
            MapSpatialItem current,
            IEnumerable<MapSpatialItem> candidates,
            MapNavigationDirection direction,
            ISet<string> related = null)
        {
            var next = MapSpatialNavigation.Next(current, candidates, direction, related);
            return next == null ? null : next.Key;
        }

        [TestMethod]
        public void EachDirectionPicksTheNearestElementInThatDirection()
        {
            var center = Item("center", 500, 500);
            var all = new[]
            {
                center,
                Item("left", 300, 500), Item("far-left", 100, 500),
                Item("right", 700, 500), Item("far-right", 900, 500),
                Item("up", 500, 300), Item("far-up", 500, 100),
                Item("down", 500, 700), Item("far-down", 500, 900)
            };

            Assert.AreEqual("left", Next(center, all, MapNavigationDirection.Left));
            Assert.AreEqual("right", Next(center, all, MapNavigationDirection.Right));
            Assert.AreEqual("up", Next(center, all, MapNavigationDirection.Up));
            Assert.AreEqual("down", Next(center, all, MapNavigationDirection.Down));
        }

        [TestMethod]
        public void NoCandidatesInDirectionKeepsTheCurrentElement()
        {
            var current = Item("current", 500, 500);

            Assert.IsNull(Next(current, new[] { current }, MapNavigationDirection.Right));
            Assert.IsNull(Next(current, new MapSpatialItem[0], MapNavigationDirection.Left));

            // Элементы левее и выше не годятся для стрелки вправо и вниз.
            var others = new[] { current, Item("left", 100, 500), Item("up", 500, 100) };
            Assert.IsNull(Next(current, others, MapNavigationDirection.Right));
            Assert.IsNull(Next(current, others, MapNavigationDirection.Down));
        }

        [TestMethod]
        public void ElementOnTheSameLineByCenterIsNotInTheHalfPlane()
        {
            var current = Item("current", 500, 500);

            // Центры совпадают по оси направления: это не «правее» и не «ниже».
            var beside = Item("beside", 500, 700);
            Assert.IsNull(Next(current, new[] { current, beside }, MapNavigationDirection.Right));
            Assert.AreEqual("beside", Next(current, new[] { current, beside }, MapNavigationDirection.Down));
        }

        [TestMethod]
        public void CrossDeviationCountsDoubleSoAnElementAheadBeatsACloserOneAside()
        {
            var current = Item("current", 0, 0);
            var ahead = Item("ahead", 300, 0);
            var aside = Item("aside", 150, 200);

            // Вправо: ahead — 300 по оси и 0 в сторону; aside — 150 по оси и 200 в сторону (оценка 550).
            Assert.AreEqual("ahead", Next(current, new[] { current, aside, ahead }, MapNavigationDirection.Right));
        }

        [TestMethod]
        public void RelatedNeighborWinsOnEqualScoreOtherwiseOrdinalKey()
        {
            var current = Item("current", 0, 0);

            // Оба кандидата получают оценку 200: b — 100 по оси и 50 в сторону, a — 200 по оси.
            var b = Item("node:b", 100, 50);
            var a = Item("node:a", 200, 0);
            var all = new[] { current, b, a };

            Assert.AreEqual("node:a", Next(current, all, MapNavigationDirection.Right));
            Assert.AreEqual("node:b", Next(current, all, MapNavigationDirection.Right,
                new HashSet<string> { "node:b" }));
            Assert.AreEqual("node:a", Next(current, all, MapNavigationDirection.Right,
                new HashSet<string> { "node:a" }));
            // Связь не отменяет более близкого соседа: приоритет действует только при равной оценке.
            var nearer = Item("node:near", 60, 0);
            Assert.AreEqual("node:near", Next(current, new[] { current, b, a, nearer }, MapNavigationDirection.Right,
                new HashSet<string> { "node:b" }));
        }
    }
}
