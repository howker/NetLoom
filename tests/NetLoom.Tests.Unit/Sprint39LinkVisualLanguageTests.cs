using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint39LinkVisualLanguageTests
    {
        [TestMethod]
        public void ConfidenceUsesStableDashGrammar()
        {
            Assert.IsNull(
                DashPattern(MapConfidence.High));

            CollectionAssert.AreEqual(
                new[] { 6.0, 3.0 },
                DashPattern(MapConfidence.Medium));

            CollectionAssert.AreEqual(
                new[] { 2.0, 2.0 },
                DashPattern(MapConfidence.Low));
        }

        [TestMethod]
        public void FreshnessUsesStableOpacityGrammar()
        {
            Assert.AreEqual(
                1.0,
                FreshnessOpacity(MapFreshness.Fresh));

            Assert.AreEqual(
                0.72,
                FreshnessOpacity(MapFreshness.Aging));

            Assert.AreEqual(
                0.45,
                FreshnessOpacity(MapFreshness.Stale));
        }

        [TestMethod]
        public void UnknownEnumValuesFailSafeToLowestTrust()
        {
            CollectionAssert.AreEqual(
                new[] { 2.0, 2.0 },
                DashPattern((MapConfidence)999));

            Assert.AreEqual(
                0.45,
                FreshnessOpacity((MapFreshness)999));
        }

        private static double[] DashPattern(
            MapConfidence confidence)
        {
            return (double[])InvokeStatic(
                "LinkConfidenceDashPattern",
                confidence);
        }

        private static double FreshnessOpacity(
            MapFreshness freshness)
        {
            return (double)InvokeStatic(
                "LinkFreshnessOpacity",
                freshness);
        }

        private static object InvokeStatic(
            string name,
            object argument)
        {
            var method =
                typeof(MainWindow).GetMethod(
                    name,
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(
                method,
                "Expected private visual-language helper: " +
                name);

            return method.Invoke(
                null,
                new[] { argument });
        }
    }
}
