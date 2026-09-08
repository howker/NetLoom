using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                    "EvidenceCount"
                };

        [TestMethod]
        public void
            RussianUiValuesDoNotStartWithUnexpectedLowercaseCyrillic()
        {
            var path =
                FindRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Resources",
                        "UiStrings.resx"));

            var document =
                XDocument.Load(
                    path,
                    LoadOptions.PreserveWhitespace);

            var offenders =
                document
                    .Root
                    .Elements("data")
                    .Select(
                        element =>
                            new
                            {
                                Name =
                                    (string)element.Attribute(
                                        "name"),
                                Value =
                                    (string)element.Element(
                                        "value")
                            })
                    .Where(
                        item =>
                            !string.IsNullOrEmpty(
                                item.Name) &&
                            !AllowedLowercaseStarts.Contains(
                                item.Name) &&
                            StartsWithLowercaseCyrillic(
                                item.Value))
                    .Select(
                        item =>
                            item.Name +
                            " = \"" +
                            item.Value +
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

        private static bool StartsWithLowercaseCyrillic(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var first =
                value[0];

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
