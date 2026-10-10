using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapLabelDeclutterTests
    {
        private static MapLabelCandidate Label(string key, double x, double y, int priority) =>
            new MapLabelCandidate(key, new Rect(x, y, 100, 20), priority);

        [TestMethod]
        public void LabelsWithoutIntersectionsAreAllShown()
        {
            var shown = MapLabelDeclutter.SelectVisible(new[]
            {
                Label("a", 0, 0, 5), Label("b", 200, 0, 5), Label("c", 0, 100, 3)
            }, 4);
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, new List<string>(shown));
        }

        [TestMethod]
        public void IntersectingLabelsKeepTheMoreImportantOne()
        {
            var shown = MapLabelDeclutter.SelectVisible(new[]
            {
                Label("a", 0, 0, 5), Label("b", 50, 5, 2)
            }, 4);
            CollectionAssert.AreEquivalent(new[] { "b" }, new List<string>(shown));
        }

        [TestMethod]
        public void EqualPriorityIsResolvedByOrdinalKey()
        {
            var shown = MapLabelDeclutter.SelectVisible(new[]
            {
                Label("b", 50, 5, 2), Label("a", 0, 0, 2), Label("B", 20, 0, 2)
            }, 4);
            // Ordinal: «B» меньше «a» и «b»; «B» пересекает остальные.
            CollectionAssert.AreEquivalent(new[] { "B" }, new List<string>(shown));
        }

        [TestMethod]
        public void GapBetweenLabelsIsTakenIntoAccount()
        {
            // Между подписями 3 px: при поле 4 они считаются наложенными, при поле 2 — нет.
            var candidates = new[] { Label("a", 0, 0, 1), Label("b", 103, 0, 2) };
            CollectionAssert.AreEquivalent(new[] { "a" },
                new List<string>(MapLabelDeclutter.SelectVisible(candidates, 4)));
            CollectionAssert.AreEquivalent(new[] { "a", "b" },
                new List<string>(MapLabelDeclutter.SelectVisible(candidates, 2)));
        }

        [TestMethod]
        public void HiddenLabelDoesNotBlockLessImportantOnes()
        {
            // «b» проигрывает «a», поэтому «c», пересекающая только «b», остаётся видимой.
            var shown = MapLabelDeclutter.SelectVisible(new[]
            {
                Label("a", 0, 0, 1), Label("b", 90, 0, 2), Label("c", 180, 0, 3)
            }, 0);
            CollectionAssert.AreEquivalent(new[] { "a", "c" }, new List<string>(shown));
        }

        [TestMethod]
        public void FirstFreeAlternativeIsSelectedAndOccupiesItsChosenPosition()
        {
            var main = new Rect(0, 0, 100, 20);
            var alternative = new Rect(0, 24, 100, 20);
            var shown = MapLabelDeclutter.SelectPlacements(new[]
            {
                Label("parent", 0, 0, 2),
                new MapLabelCandidate("child", new[] { main, alternative, new Rect(0, 48, 100, 20) }, 3),
                Label("link", 0, 24, 4)
            }, 4);
            Assert.AreEqual(alternative, shown["child"]);
            Assert.IsFalse(shown.ContainsKey("link"));
        }

        [TestMethod]
        public void HigherPriorityWinsEvenWhenLowerPriorityHasAnEarlierKey()
        {
            var shown = MapLabelDeclutter.SelectPlacements(new[]
            {
                new MapLabelCandidate("a-child", new[] { new Rect(0, 0, 100, 20), new Rect(0, 24, 100, 20) }, 3),
                Label("z-selected", 0, 0, 0)
            }, 4);
            Assert.AreEqual(new Rect(0, 0, 100, 20), shown["z-selected"]);
            Assert.AreEqual(new Rect(0, 24, 100, 20), shown["a-child"]);
        }

        [TestMethod]
        public void FixedObstaclesForceAnAlternativeAndAreNeverRemoved()
        {
            var shown = MapLabelDeclutter.SelectPlacements(new[]
            {
                new MapLabelCandidate("location", new[] { new Rect(0, 0, 100, 20), new Rect(0, 24, 100, 20) }, 2),
                Label("blocked", 0, 0, 3)
            }, 4, new[] { new Rect(50, 0, 16, 16) });
            Assert.AreEqual(new Rect(0, 24, 100, 20), shown["location"]);
            Assert.IsFalse(shown.ContainsKey("blocked"));
        }

        [TestMethod]
        public void AlternativeSelectionIsDeterministicRegardlessOfInputOrder()
        {
            var candidates = new[]
            {
                new MapLabelCandidate("a", new[] { new Rect(0, 0, 100, 20), new Rect(0, 24, 100, 20) }, 2),
                new MapLabelCandidate("b", new[] { new Rect(0, 0, 100, 20), new Rect(0, 24, 100, 20) }, 2),
                Label("c", 0, 48, 3)
            };
            var expected = MapLabelDeclutter.SelectPlacements(candidates, 4);
            System.Array.Reverse(candidates);
            var actual = MapLabelDeclutter.SelectPlacements(candidates, 4);
            Assert.AreEqual(expected.Count, actual.Count);
            foreach (var pair in expected) Assert.AreEqual(pair.Value, actual[pair.Key]);
        }

        [TestMethod]
        public void ImportantDeviceLabelCanIgnoreObstaclesReservedForLocationTabs()
        {
            var shown = MapLabelDeclutter.SelectPlacements(new[]
            {
                new MapLabelCandidate("selected", new Rect(0, 0, 100, 20), 0, avoidObstacles: false),
                Label("location", 200, 0, 2)
            }, 4, new[] { new Rect(0, 0, 300, 20) });
            Assert.IsTrue(shown.ContainsKey("selected"));
            Assert.IsFalse(shown.ContainsKey("location"));
        }
    }
}
