using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class UiDesignFoundationTests
    {
        private static readonly XNamespace
            XamlNamespace =
                "http://schemas.microsoft.com/winfx/2006/xaml";

        [TestMethod]
        public void
            LightAndDarkPaletteResourceIntegrityUsesTheSameSemanticKeys()
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
            DesignTokenResourceIntegrityCoversTypographySpacingAndMapGeometry()
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
                    "NetLoom.Thickness.InlineGap",
                    "NetLoom.Radius.Control",
                    "NetLoom.Radius.Panel",
                    "NetLoom.Radius.MapNode",
                    "NetLoom.Map.NodeWidth",
                    "NetLoom.Map.NodeHeight",
                    "NetLoom.Map.ZoomMin",
                    "NetLoom.Map.ZoomMax",
                    "NetLoom.Map.ZoomStep",
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
            MapNodeCardMeasuresProductionContentWithoutVerticalClipping()
        {
            Exception failure = null;

            var thread =
                new Thread(
                    () =>
                    {
                        try
                        {
                            var window =
                                new MainWindow();

                            window.ShowMap(
                                new MapSnapshot(
                                    DateTime.UtcNow,
                                    new[]
                                    {
                                        new MapNode(
                                            "device:layout-probe",
                                            "Коммутатор доступа с длинным именем",
                                            "Автоматическое устройство",
                                            0.0,
                                            0.0,
                                            null,
                                            MapNodeOrigin.Automatic,
                                            MapMonitoringCapability.None,
                                            MapNodeCategory.UnmanagedSwitch,
                                            Guid.NewGuid(),
                                            "192.0.2.10")
                                    },
                                    new MapLink[0]));

                            var canvas =
                                window.FindName("MapCanvas")
                                    as Canvas;

                            Assert.IsNotNull(
                                canvas,
                                "Production MapCanvas was not found.");

                            var card =
                                canvas.Children
                                    .OfType<Border>()
                                    .Single();

                            var content =
                                card.Child as FrameworkElement;

                            Assert.IsNotNull(
                                content,
                                "Production map-node content was not found.");

                            var contentWidth =
                                card.Width -
                                card.Padding.Left -
                                card.Padding.Right -
                                card.BorderThickness.Left -
                                card.BorderThickness.Right;

                            content.Measure(
                                new Size(
                                    contentWidth,
                                    double.PositiveInfinity));

                            var naturalContentHeight =
                                content.DesiredSize.Height;

                            card.Measure(
                                new Size(
                                    card.Width,
                                    double.PositiveInfinity));

                            var availableContentHeight =
                                card.DesiredSize.Height -
                                card.Padding.Top -
                                card.Padding.Bottom -
                                card.BorderThickness.Top -
                                card.BorderThickness.Bottom;

                            Assert.IsTrue(
                                naturalContentHeight <=
                                    availableContentHeight + 0.1,
                                string.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "Production map-node content ({0:F1}) does not fit measured card content height ({1:F1}).",
                                    naturalContentHeight,
                                    availableContentHeight));

                            Assert.IsTrue(
                                double.IsNaN(card.Height),
                                "Map-node card must not use a fixed Height; content must be allowed to grow.");

                            window.Close();
                        }
                        catch (Exception exception)
                        {
                            failure = exception;
                        }
                    });

            thread.SetApartmentState(
                ApartmentState.STA);

            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new AssertFailedException(
                    "Production map-node layout measurement failed: " +
                    failure);
            }
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
