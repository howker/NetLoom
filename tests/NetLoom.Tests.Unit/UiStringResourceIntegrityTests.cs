using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class UiStringResourceIntegrityTests
    {
        private static readonly HashSet<string>
            AllowedLowercaseStarts =
                new HashSet<string>(
                    StringComparer.Ordinal)
                {
                    "EvidenceCountOne",
                    "EvidenceCountFew",
                    "EvidenceCountMany",
                    "EvidenceCountOther"
                };

        private static readonly Regex
            PlaceholderRegex =
                new Regex(
                    @"\{(?<Index>[0-9]+)(?:[^}]*)\}",
                    RegexOptions.Compiled);

        [TestMethod]
        public void
            NeutralResourceLanguageIsEnglishAndTemplateExampleNamesAreNotResourceKeys()
        {
            var attribute =
                typeof(UiText)
                    .Assembly
                    .GetCustomAttributes(
                        typeof(NeutralResourcesLanguageAttribute),
                        false)
                    .OfType<NeutralResourcesLanguageAttribute>()
                    .SingleOrDefault();

            Assert.IsNotNull(
                attribute,
                "NetLoom.Wpf must declare NeutralResourcesLanguage.");

            Assert.AreEqual(
                "en",
                attribute.CultureName);

            var templateExampleNames =
                new[]
                {
                    "Name1",
                    "Icon1",
                    "Bitmap1"
                };

            foreach (var fileName in
                new[]
                {
                    "UiStrings.resx",
                    "UiStrings.ru.resx"
                })
            {
                var resources =
                    LoadResources(
                        fileName);

                foreach (var name in
                    templateExampleNames)
                {
                    Assert.IsFalse(
                        resources.ContainsKey(
                            name),
                        fileName +
                        " contains template resource data key " +
                        name +
                        ". ResX schema comments do not count as data.");
                }
            }
        }

        [TestMethod]
        public void
            NeutralAndRussianResourcesHaveIdenticalKeysAndPlaceholders()
        {
            var neutral =
                LoadResources(
                    "UiStrings.resx");

            var russian =
                LoadResources(
                    "UiStrings.ru.resx");

            CollectionAssert.AreEquivalent(
                neutral.Keys.ToArray(),
                russian.Keys.ToArray());

            foreach (var key in neutral.Keys)
            {
                CollectionAssert.AreEquivalent(
                    PlaceholderIndices(
                        neutral[key]),
                    PlaceholderIndices(
                        russian[key]),
                    "Placeholder mismatch for " +
                    key);
            }
        }

        [TestMethod]
        public void
            NeutralResourcesContainNoCyrillicText()
        {
            var neutral =
                LoadResources(
                    "UiStrings.resx");

            var offenders =
                neutral
                    .Where(
                        pair =>
                            ContainsCyrillic(
                                pair.Value))
                    .Select(
                        pair =>
                            pair.Key +
                            " = \"" +
                            pair.Value +
                            "\"")
                    .OrderBy(
                        value => value,
                        StringComparer.Ordinal)
                    .ToArray();

            Assert.AreEqual(
                0,
                offenders.Length,
                offenders.Length == 0
                    ? null
                    : "Neutral resources contain Cyrillic:" +
                      Environment.NewLine +
                      string.Join(
                          Environment.NewLine,
                          offenders));
        }

        [TestMethod]
        public void
            RussianUiValuesDoNotStartWithUnexpectedLowercaseCyrillic()
        {
            var russian =
                LoadResources(
                    "UiStrings.ru.resx");

            var offenders =
                russian
                    .Where(
                        pair =>
                            IsSuspicious(
                                pair.Key,
                                pair.Value))
                    .Select(
                        pair =>
                            pair.Key +
                            " = \"" +
                            pair.Value +
                            "\"")
                    .OrderBy(
                        value => value,
                        StringComparer.Ordinal)
                    .ToArray();

            Assert.AreEqual(
                0,
                offenders.Length,
                offenders.Length == 0
                    ? null
                    : "Unexpected lowercase Cyrillic resource starts:" +
                      Environment.NewLine +
                      string.Join(
                          Environment.NewLine,
                          offenders));
        }

        [TestMethod]
        public void
            DroppedCapitalExampleIsRejectedButExplicitLowercaseIsAllowed()
        {
            Assert.IsTrue(
                IsSuspicious(
                    "MapTitle",
                    "изическая топология"));

            Assert.IsFalse(
                IsSuspicious(
                    "MapTitle",
                    "Физическая топология"));

            Assert.IsFalse(
                IsSuspicious(
                    "EvidenceCountMany",
                    "подтверждений: {0}"));
        }

        private static IDictionary<string, string>
            LoadResources(
                string fileName)
        {
            var path =
                FindRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Resources",
                        fileName));

            var document =
                XDocument.Load(
                    path,
                    LoadOptions.PreserveWhitespace);

            return document
                .Root
                .Elements("data")
                .ToDictionary(
                    element =>
                        (string)element.Attribute(
                            "name"),
                    element =>
                        (string)element.Element(
                            "value") ??
                        string.Empty,
                    StringComparer.Ordinal);
        }

        private static string[] PlaceholderIndices(
            string value)
        {
            return PlaceholderRegex
                .Matches(
                    value ??
                    string.Empty)
                .Cast<Match>()
                .Select(
                    match =>
                        match.Groups["Index"].Value)
                .Distinct(
                    StringComparer.Ordinal)
                .OrderBy(
                    item => item,
                    StringComparer.Ordinal)
                .ToArray();
        }

        private static bool ContainsCyrillic(
            string value)
        {
            if (string.IsNullOrEmpty(
                value))
            {
                return false;
            }

            return value.Any(
                character =>
                    character >= '\u0400' &&
                    character <= '\u04FF');
        }

        private static bool IsSuspicious(
            string name,
            string value)
        {
            if (string.IsNullOrEmpty(name) ||
                AllowedLowercaseStarts.Contains(name))
            {
                return false;
            }

            return StartsWithLowercaseCyrillic(
                value);
        }

        private static bool StartsWithLowercaseCyrillic(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var first =
                value
                    .TrimStart()[0];

            return
                (first >= 'а' &&
                 first <= 'я') ||
                first == 'ё';
        }

        private static string FindRepositoryFile(
            string relativePath)
        {
            var directory =
                new DirectoryInfo(
                    AppDomain.CurrentDomain
                        .BaseDirectory);

            while (directory != null)
            {
                var candidate =
                    Path.Combine(
                        directory.FullName,
                        relativePath);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory =
                    directory.Parent;
            }

            Assert.Fail(
                "Repository file was not found: " +
                relativePath);

            return null;
        }

    }
}
