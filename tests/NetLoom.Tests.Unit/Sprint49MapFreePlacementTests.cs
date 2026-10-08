using System;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapFreePlacementTests
    {
        [TestMethod]
        public void FreeRectangleKeepsOriginalPosition()
        {
            var desired = new Rect(-30, 50, 24, 24);
            Assert.AreEqual(desired.TopLeft,
                MapFreePlacement.FindFreeSpot(desired, new[] { new Rect(100, 100, 24, 24) }, 24, 0));
        }

        [TestMethod]
        public void OccupiedRectangleUsesFirstFreeSpiralStep()
        {
            var desired = new Rect(0, 0, 24, 24);
            Assert.AreEqual(new Point(24, 0), MapFreePlacement.FindFreeSpot(desired, new[] { desired }, 24, 0));
        }

        [TestMethod]
        public void MarginAndOccupiedOrderGiveDeterministicClearance()
        {
            var desired = new Rect(0, 0, 24, 24);
            var second = new Rect(24, 0, 24, 24);
            var point = MapFreePlacement.FindFreeSpot(desired, new[] { desired, second }, 24, 4);
            Assert.AreEqual(point, MapFreePlacement.FindFreeSpot(desired, new[] { second, desired }, 24, 4));
            var placed = new Rect(point, desired.Size);
            placed.Inflate(4, 4);
            foreach (var occupied in new[] { desired, second })
            {
                var intersection = Rect.Intersect(placed, occupied);
                Assert.IsTrue(intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0);
            }
        }

        [TestMethod]
        public void ExhaustedSearchKeepsOriginalPosition()
        {
            var desired = new Rect(10, 20, 24, 24);
            Assert.AreEqual(desired.TopLeft, MapFreePlacement.FindFreeSpot(desired,
                new[] { new Rect(-100000, -100000, 200000, 200000) }, 24, 4));
        }

        [TestMethod]
        public void InvalidStepIsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                MapFreePlacement.FindFreeSpot(new Rect(0, 0, 24, 24), new Rect[0], 0, 0));
        }
    }
}
