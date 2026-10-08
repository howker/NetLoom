using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49MapSemanticLevelTests
    {
        [TestMethod]
        [DataRow(0.01, MapSemanticLevel.Far)]
        [DataRow(0.74, MapSemanticLevel.Far)]
        [DataRow(0.75, MapSemanticLevel.Medium)]
        [DataRow(0.949, MapSemanticLevel.Medium)]
        [DataRow(0.95, MapSemanticLevel.Close)]
        [DataRow(1.599, MapSemanticLevel.Close)]
        [DataRow(1.6, MapSemanticLevel.Detailed)]
        [DataRow(5.0, MapSemanticLevel.Detailed)]
        public void BoundariesSelectTheNextLevel(double zoom, MapSemanticLevel expected)
        {
            Assert.AreEqual(expected, MapSemanticLevels.For(zoom, 0.75, 0.95, 1.6));
        }

        [TestMethod]
        public void UsesSuppliedThresholds()
        {
            Assert.AreEqual(MapSemanticLevel.Far, MapSemanticLevels.For(0.75, 1, 2, 3));
            Assert.AreEqual(MapSemanticLevel.Medium, MapSemanticLevels.For(1, 1, 2, 3));
            Assert.AreEqual(MapSemanticLevel.Close, MapSemanticLevels.For(2, 1, 2, 3));
            Assert.AreEqual(MapSemanticLevel.Detailed, MapSemanticLevels.For(3, 1, 2, 3));
        }
    }
}
