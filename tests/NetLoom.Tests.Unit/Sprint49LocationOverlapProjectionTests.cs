using System;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49LocationOverlapProjectionTests
    {
        private static readonly Guid First = Guid.Parse("49494949-0005-0000-0000-000000000001");
        private static readonly Guid Second = Guid.Parse("49494949-0005-0000-0000-000000000002");
        private static readonly Guid Parent = Guid.Parse("49494949-0005-0000-0000-000000000003");

        [TestMethod]
        public void IntersectingSiblingsProduceOneStablePair()
        {
            var a = new LocationOverlapFrame(First, Parent, "А", new Rect(0, 0, 100, 100));
            var b = new LocationOverlapFrame(Second, Parent, "Б", new Rect(50, 50, 100, 100));
            var pairs = LocationOverlapProjection.Build(new[] { b, a });
            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual(First, pairs[0].First.Id);
            Assert.AreEqual(Second, pairs[0].Second.Id);
        }

        [TestMethod]
        public void AncestorsAreExcludedAcrossSeveralLevels()
        {
            var bounds = new Rect(0, 0, 100, 100);
            var pairs = LocationOverlapProjection.Build(new[]
            {
                new LocationOverlapFrame(Parent, null, "Площадка", bounds),
                new LocationOverlapFrame(First, Parent, "Корпус", bounds),
                new LocationOverlapFrame(Second, First, "Комната", bounds)
            });
            Assert.AreEqual(0, pairs.Count);
        }

        [TestMethod]
        [DataRow(100.0, 0)]
        [DataRow(99.995, 0)]
        [DataRow(99.99, 1)]
        public void TouchingAndSubPixelAreaDoNotProduceGaps(double left, int expected)
        {
            Assert.AreEqual(expected, LocationOverlapProjection.Build(new[]
            {
                new LocationOverlapFrame(First, null, "А", new Rect(0, 0, 100, 100)),
                new LocationOverlapFrame(Second, null, "Б", new Rect(left, 0, 100, 100))
            }).Count);
        }

        [TestMethod]
        public void OverlapCanBeReportedWithoutDevicesOrDiagnostics()
        {
            var overlaps = LocationOverlapProjection.Build(new[]
            {
                new LocationOverlapFrame(First, null, "А", new Rect(0, 0, 100, 100)),
                new LocationOverlapFrame(Second, null, "Б", new Rect(20, 20, 100, 100))
            });
            var report = TopologyQualityProjection.Build(null, null, overlaps: overlaps);
            var item = report.Items.Single();
            Assert.AreEqual(TopologyQualityGapKind.LocationOverlap, item.Reasons.Single().Kind);
            Assert.AreEqual(First, item.LocationId);
            Assert.AreEqual(Second, item.OtherLocationId);
            Assert.AreEqual(1, report.Count);
        }
    }
}
