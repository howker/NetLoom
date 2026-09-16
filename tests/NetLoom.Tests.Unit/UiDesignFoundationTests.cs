using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class UiDesignFoundationTests
    {
        private static readonly XNamespace
            XamlNamespace =
                "http://schemas.microsoft.com/winfx/2006/xaml";

        private static readonly Regex
            ResourceReferenceRegex =
                new Regex(
                    @"\{(?:Dynamic|Static)Resource\s+(?<Key>NetLoom\.[^}\s]+)\}",
                    RegexOptions.Compiled);

        private static readonly Regex
            LocalStyleLiteralRegex =
                new Regex(
                    @"\b(?:FontSize|Margin|Padding|BorderThickness)\s*=\s*""(?!\{)[^""]+""",
                    RegexOptions.Compiled);

        [TestMethod]
        public void
            LightAndDarkPalettesExposeTheSameSemanticKeys()
        {
            var light =
                LoadKeys(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Themes",
                        "Light.xaml"));

            var dark =
                LoadKeys(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Themes",
                        "Dark.xaml"));

            CollectionAssert.AreEquivalent(
                light,
                dark);

            var required =
                new[]
                {
                    "NetLoom.Brush.Window",
                    "NetLoom.Brush.Surface",
                    "NetLoom.Brush.Canvas",
                    "NetLoom.Brush.Border",
                    "NetLoom.Brush.TextPrimary",
                    "NetLoom.Brush.TextSecondary",
                    "NetLoom.Brush.Accent",
                    "NetLoom.Brush.Success",
                    "NetLoom.Brush.Warning",
                    "NetLoom.Brush.Critical",
                    "NetLoom.Brush.Link",
                    "NetLoom.Brush.Selection"
                };

            foreach (var key in required)
            {
                Assert.IsTrue(
                    light.Contains(
                        key,
                        StringComparer.Ordinal),
                    "Light/Dark palette is missing semantic key: " +
                    key);
            }
        }

        [TestMethod]
        public void
            DesignTokensCoverTypographySpacingAndMapGeometry()
        {
            var keys =
                LoadKeys(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Themes",
                        "DesignTokens.xaml"));

            var required =
                new[]
                {
                    "NetLoom.FontFamily.Ui",
                    "NetLoom.FontFamily.Mono",
                    "NetLoom.FontSize.Caption",
                    "NetLoom.FontSize.Body",
                    "NetLoom.FontSize.SectionTitle",
                    "NetLoom.FontSize.WindowTitle",
                    "NetLoom.Spacing.Xs",
                    "NetLoom.Spacing.Sm",
                    "NetLoom.Spacing.Md",
                    "NetLoom.Spacing.Lg",
                    "NetLoom.Spacing.Xl",
                    "NetLoom.Thickness.PanelPadding",
                    "NetLoom.Radius.Control",
                    "NetLoom.Radius.Panel",
                    "NetLoom.Radius.MapNode",
                    "NetLoom.Map.NodeWidth",
                    "NetLoom.Map.NodeHeight",
                    "NetLoom.Map.LinkLabelPlacementStep",
                    "NetLoom.Map.LinkLabelCollisionMargin"
                };

            foreach (var key in required)
            {
                Assert.IsTrue(
                    keys.Contains(
                        key,
                        StringComparer.Ordinal),
                    "Design token is missing: " +
                    key);
            }
        }

        [TestMethod]
        public void
            MainWindowUsesSharedStylesInsteadOfLocalBrushAndSpacingLiterals()
        {
            var mainWindowXaml =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "MainWindow.xaml"));

            var mainWindowCode =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "MainWindow.xaml.cs"));

            StringAssert.Contains(
                mainWindowXaml,
                "Themes/DesignTokens.xaml");

            StringAssert.Contains(
                mainWindowXaml,
                "Themes/Light.xaml");

            StringAssert.Contains(
                mainWindowXaml,
                "Themes/Controls.xaml");

            StringAssert.Contains(
                mainWindowCode,
                "NetLoom.Style.MapNodeCard");

            StringAssert.Contains(
                mainWindowCode,
                "NetLoom.Style.MapLink");

            Assert.IsFalse(
                LocalStyleLiteralRegex.IsMatch(
                    mainWindowXaml),
                "MainWindow.xaml still contains local font-size/spacing/border literals.");

            Assert.IsFalse(
                mainWindowCode.Contains(
                    "SystemColors."),
                "MainWindow code-behind still owns system color literals.");

            Assert.IsFalse(
                mainWindowCode.Contains(
                    "Brushes."),
                "MainWindow code-behind still owns WPF brush literals.");
        }

        [TestMethod]
        public void
            MainWindowAndControlStylesOnlyReferenceDefinedFoundationKeys()
        {
            var designTokensPath =
                Path.Combine(
                    "src",
                    "NetLoom.Wpf",
                    "Themes",
                    "DesignTokens.xaml");

            var lightPath =
                Path.Combine(
                    "src",
                    "NetLoom.Wpf",
                    "Themes",
                    "Light.xaml");

            var controlsPath =
                Path.Combine(
                    "src",
                    "NetLoom.Wpf",
                    "Themes",
                    "Controls.xaml");

            var mainWindowPath =
                Path.Combine(
                    "src",
                    "NetLoom.Wpf",
                    "MainWindow.xaml");

            var defined =
                new HashSet<string>(
                    LoadKeys(designTokensPath)
                        .Concat(
                            LoadKeys(lightPath))
                        .Concat(
                            LoadKeys(controlsPath)),
                    StringComparer.Ordinal);

            var references =
                ResourceReferences(
                    ReadRepositoryFile(
                        controlsPath))
                    .Concat(
                        ResourceReferences(
                            ReadRepositoryFile(
                                mainWindowPath)))
                    .Distinct(
                        StringComparer.Ordinal)
                    .OrderBy(
                        key => key,
                        StringComparer.Ordinal)
                    .ToArray();

            var unresolved =
                references
                    .Where(
                        key =>
                            !defined.Contains(
                                key))
                    .ToArray();

            Assert.AreEqual(
                0,
                unresolved.Length,
                unresolved.Length == 0
                    ? null
                    : "Unresolved WPF design resources:" +
                      Environment.NewLine +
                      string.Join(
                          Environment.NewLine,
                          unresolved));
        }

        private static string[] LoadKeys(
            string relativePath)
        {
            var path =
                FindRepositoryFile(
                    relativePath);

            var document =
                XDocument.Load(
                    path,
                    LoadOptions.PreserveWhitespace);

            return document
                .Descendants()
                .Attributes(
                    XamlNamespace +
                    "Key")
                .Select(
                    attribute =>
                        attribute.Value)
                .OrderBy(
                    key => key,
                    StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ResourceReferences(
            string text)
        {
            return ResourceReferenceRegex
                .Matches(
                    text ?? string.Empty)
                .Cast<Match>()
                .Select(
                    match =>
                        match.Groups["Key"]
                            .Value)
                .ToArray();
        }

        private static string ReadRepositoryFile(
            string relativePath)
        {
            return File.ReadAllText(
                FindRepositoryFile(
                    relativePath));
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
