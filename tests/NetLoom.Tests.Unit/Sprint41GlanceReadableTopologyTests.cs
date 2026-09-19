using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint41GlanceReadableTopologyTests
    {
        [TestMethod]
        public void
            LongDeviceNamesRemainReadableAndDenseMetadataStaysOutOfTheCard()
        {
            RunOnSta(
                () =>
                {
                    var deviceId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        window.Show();

                        window.ShowMap(
                            new MapSnapshot(
                                DateTime.UtcNow,
                                new[]
                                {
                                    new MapNode(
                                        "device:central-a",
                                        "Центральный коммутатор A",
                                        "MOXA PT-7728",
                                        100.0,
                                        100.0,
                                        null,
                                        MapNodeOrigin.Automatic,
                                        MapMonitoringCapability.Unknown,
                                        MapNodeCategory.UnmanagedSwitch,
                                        deviceId,
                                        "192.0.2.11")
                                },
                                new MapLink[0]));

                        var card =
                            DeviceCard(
                                window,
                                deviceId);

                        Assert.IsNotNull(
                            card,
                            "Production map card was not created.");

                        card.Measure(
                            new Size(
                                card.Width,
                                double.PositiveInfinity));

                        var textBlocks =
                            Descendants<TextBlock>(
                                    card)
                                .Where(
                                    item =>
                                        item.Visibility ==
                                            Visibility.Visible &&
                                        !string.IsNullOrWhiteSpace(
                                            item.Text))
                                .ToArray();

                        var title =
                            textBlocks.Single(
                                item =>
                                    item.Text ==
                                    "Центральный коммутатор A");

                        Assert.AreEqual(
                            TextWrapping.Wrap,
                            title.TextWrapping,
                            "Long device names must wrap rather than being reduced to an ellipsis.");

                        Assert.AreEqual(
                            TextTrimming.None,
                            title.TextTrimming,
                            "The production title must not hide the distinguishing end of a long device name.");

                        CollectionAssert.AreEquivalent(
                            new[]
                            {
                                "Центральный коммутатор A",
                                "MOXA PT-7728"
                            },
                            textBlocks
                                .Select(
                                    item => item.Text)
                                .ToArray(),
                            "The glance-readable card must keep only the name and useful short secondary line visible.");

                        Assert.IsFalse(
                            textBlocks.Any(
                                item =>
                                    item.Text.Contains(
                                        "192.0.2.11")),
                            "Management details belong to diagnostics, not the glance-readable card.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void
            EveryKnownDeviceCategoryHasItsOwnVectorIconAndUnknownIsDistinct()
        {
            RunOnSta(
                () =>
                {
                    var fixtures =
                        new[]
                        {
                            CategoryFixture(
                                MapNodeCategory.Unknown,
                                "Unknown"),
                            CategoryFixture(
                                MapNodeCategory.MediaConverter,
                                "Media converter"),
                            CategoryFixture(
                                MapNodeCategory.UnmanagedSwitch,
                                "Unmanaged switch"),
                            CategoryFixture(
                                MapNodeCategory.OpticalConverter,
                                "Optical converter"),
                            CategoryFixture(
                                MapNodeCategory.PassiveNetworkEquipment,
                                "Passive equipment")
                        };

                    var window =
                        new MainWindow();

                    try
                    {
                        window.Show();

                        window.ShowMap(
                            new MapSnapshot(
                                DateTime.UtcNow,
                                fixtures
                                    .Select(
                                        (item, index) =>
                                            new MapNode(
                                                "device:" + index,
                                                item.Label,
                                                null,
                                                index * 220.0,
                                                100.0,
                                                null,
                                                MapNodeOrigin.Automatic,
                                                MapMonitoringCapability.Unknown,
                                                item.Category,
                                                item.DeviceId,
                                                null))
                                    .ToArray(),
                                new MapLink[0]));

                        var geometries =
                            new Dictionary<
                                MapNodeCategory,
                                string>();

                        foreach (var fixture in fixtures)
                        {
                            var card =
                                DeviceCard(
                                    window,
                                    fixture.DeviceId);

                            Assert.IsNotNull(
                                card);

                            var icon =
                                Descendants<Path>(
                                        card)
                                    .Single();

                            Assert.IsNotNull(
                                icon.Data,
                                "Every production category must render a vector geometry.");

                            geometries[fixture.Category] =
                                icon.Data.ToString();
                        }

                        Assert.AreEqual(
                            fixtures.Length,
                            geometries.Values
                                .Distinct(
                                    StringComparer.Ordinal)
                                .Count(),
                            "Unknown and each known OT category must be visually distinguishable by geometry.");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static CategoryTestFixture CategoryFixture(
            MapNodeCategory category,
            string label)
        {
            return new CategoryTestFixture(
                Guid.NewGuid(),
                category,
                label);
        }

        private static Border DeviceCard(
            MainWindow window,
            Guid deviceId)
        {
            var canvas =
                window.FindName(
                    "MapCanvas") as Canvas;

            Assert.IsNotNull(
                canvas,
                "Production MapCanvas was not found.");

            return canvas.Children
                .OfType<Border>()
                .SingleOrDefault(
                    item =>
                        item.Tag is Guid &&
                        (Guid)item.Tag ==
                            deviceId);
        }

        private static IEnumerable<T>
            Descendants<T>(
                DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            var count =
                VisualTreeHelper.GetChildrenCount(
                    root);

            for (var index = 0;
                 index < count;
                 index++)
            {
                var child =
                    VisualTreeHelper.GetChild(
                        root,
                        index);

                var match =
                    child as T;

                if (match != null)
                {
                    yield return match;
                }

                foreach (var nested in
                    Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static void RunOnSta(
            Action action)
        {
            Exception failure =
                null;

            var thread =
                new Thread(
                    () =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception error)
                        {
                            failure = error;
                        }
                    });

            thread.SetApartmentState(
                ApartmentState.STA);

            thread.Start();

            if (!thread.Join(
                    TimeSpan.FromSeconds(20)))
            {
                throw new AssertFailedException(
                    "STA WPF test did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        private sealed class CategoryTestFixture
        {
            public CategoryTestFixture(
                Guid deviceId,
                MapNodeCategory category,
                string label)
            {
                DeviceId = deviceId;
                Category = category;
                Label = label;
            }

            public Guid DeviceId { get; }

            public MapNodeCategory Category { get; }

            public string Label { get; }
        }
    }
}
