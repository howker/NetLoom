using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint50PredictionWordingTests
    {
        [TestMethod]
        public void ImpactAndSinglePointWordingDescribesPrediction()
        {
            var russian = LoadResources("UiStrings.ru.resx");
            var english = LoadResources("UiStrings.resx");

            AssertForbiddenWords(russian, new[]
            {
                "недоступн", "упал", "упадут", "не работа", "отказал", "авари"
            });
            AssertForbiddenWords(english, new[]
            {
                "unavailable", "are down", "is down", "went down", "failed"
            });

            foreach (var key in new[] { "ImpactCutOff", "ImpactNoneAffected" })
            {
                AssertContains(russian, key, "по известной топологии");
                AssertContains(english, key, "known topology");
            }

            AssertContains(russian, "ImpactPredictionNote", "Прогноз");
            AssertContains(english, "ImpactPredictionNote", "prediction");
            AssertContains(russian, "SpofStrip", "прогноз");
            AssertContains(english, "SpofStrip", "predicted");
        }

        private static void AssertForbiddenWords(IDictionary<string, string> resources,
            IEnumerable<string> forbidden)
        {
            foreach (var pair in resources.Where(item =>
                item.Key.StartsWith("Impact", StringComparison.Ordinal) ||
                item.Key.StartsWith("Spof", StringComparison.Ordinal)))
            {
                foreach (var word in forbidden)
                    Assert.IsFalse(pair.Value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0,
                        pair.Key + " contains " + word + ": " + pair.Value);
            }
        }

        private static void AssertContains(IDictionary<string, string> resources, string key,
            string expected)
        {
            Assert.IsTrue(resources.ContainsKey(key), "Missing resource: " + key);
            Assert.IsTrue(resources[key].IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0,
                key + " must contain " + expected + ": " + resources[key]);
        }

        private static IDictionary<string, string> LoadResources(string fileName)
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                var path = Path.Combine(directory.FullName, "src", "NetLoom.Wpf",
                    "Resources", fileName);
                if (File.Exists(path))
                    return XDocument.Load(path, LoadOptions.PreserveWhitespace).Root.Elements("data")
                        .ToDictionary(element => (string)element.Attribute("name"),
                            element => (string)element.Element("value") ?? string.Empty,
                            StringComparer.Ordinal);
                directory = directory.Parent;
            }

            Assert.Fail("Repository file was not found: " + fileName);
            return null;
        }
    }
}
