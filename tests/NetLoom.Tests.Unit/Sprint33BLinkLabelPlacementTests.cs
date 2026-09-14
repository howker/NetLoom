using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint33BLinkLabelPlacementTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                14,
                4,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void LongHorizontalLinkLabelAvoidsEndpointNodeCards()
        {
            RunOnSta(
                () =>
                {
                    var window =
                        new MainWindow();

                    try
                    {
                        var left =
                            Node(
                                "left",
                                "Left",
                                58,
                                168,
                                Guid.NewGuid());

                        var right =
                            Node(
                                "right",
                                "Right",
                                298,
                                168,
                                Guid.NewGuid());

                        window.ShowMap(
                            Snapshot(
                                new[]
                                {
                                    left,
                                    right
                                },
                                new[]
                                {
                                    Link(
                                        "physical-link",
                                        left.Key,
                                        right.Key,
                                        "Gi1/0/1",
                                        "Gi1/0/24",
                                        Guid.NewGuid())
                                }));

                        AssertLabelAvoidsAllNodeCards(
                            MapCanvas(window));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void RetainedLinkLabelAvoidsUnrelatedNodeCardAfterRefresh()
        {
            RunOnSta(
                () =>
                {
                    var sourceId =
                        Guid.NewGuid();

                    var targetId =
                        Guid.NewGuid();

                    var obstacleId =
                        Guid.NewGuid();

                    var physicalLinkId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        var source =
                            Node(
                                "source",
                                "Source",
                                80,
                                350,
                                sourceId);

                        var target =
                            Node(
                                "target",
                                "Target",
                                900,
                                350,
                                targetId);

                        var obstacle =
                            Node(
                                "obstacle",
                                "Obstacle",
                                450,
                                330,
                                obstacleId);

                        window.ShowMap(
                            Snapshot(
                                new[]
                                {
                                    source,
                                    target,
                                    obstacle
                                },
                                new[]
                                {
                                    Link(
                                        "physical-link-a",
                                        source.Key,
                                        target.Key,
                                        "Gi1/0/10",
                                        "Gi1/0/20",
                                        physicalLinkId)
                                }));

                        var canvas =
                            MapCanvas(window);

                        var firstLabel =
                            canvas.Children
                                .OfType<TextBlock>()
                                .Single();

                        AssertLabelAvoidsAllNodeCards(
                            canvas);

                        window.ShowMap(
                            Snapshot(
                                new[]
                                {
                                    Node(
                                        "source-next",
                                        "Source",
                                        180,
                                        650,
                                        sourceId),
                                    Node(
                                        "target-next",
                                        "Target",
                                        1100,
                                        650,
                                        targetId),
                                    Node(
                                        "obstacle-next",
                                        "Obstacle",
                                        650,
                                        620,
                                        obstacleId)
                                },
                                new[]
                                {
                                    Link(
                                        "physical-link-b",
                                        "source-next",
                                        "target-next",
                                        "TenGigabitEthernet1/0/10",
                                        "TenGigabitEthernet1/0/20",
                                        physicalLinkId)
                                }));

                        var nextLabel =
                            canvas.Children
                                .OfType<TextBlock>()
                                .Single();

                        Assert.AreSame(
                            firstLabel,
                            nextLabel);

                        AssertLabelAvoidsAllNodeCards(
                            canvas);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static void AssertLabelAvoidsAllNodeCards(
            Canvas canvas)
        {
            var label =
                canvas.Children
                    .OfType<TextBlock>()
                    .Single();

            label.Measure(
                new Size(
                    double.PositiveInfinity,
                    double.PositiveInfinity));

            var labelBounds =
                new Rect(
                    Canvas.GetLeft(label),
                    Canvas.GetTop(label),
                    label.DesiredSize.Width,
                    label.DesiredSize.Height);

            foreach (var border in
                canvas.Children.OfType<Border>())
            {
                var nodeBounds =
                    new Rect(
                        Canvas.GetLeft(border),
                        Canvas.GetTop(border),
                        border.Width,
                        border.Height);

                Assert.IsFalse(
                    nodeBounds.IntersectsWith(
                        labelBounds),
                    "Link label overlaps a node card.");
            }
        }

        private static MapSnapshot Snapshot(
            MapNode[] nodes,
            MapLink[] links)
        {
            return new MapSnapshot(
                Now,
                nodes,
                links);
        }

        private static MapNode Node(
            string key,
            string label,
            double x,
            double y,
            Guid deviceId)
        {
            return new MapNode(
                key,
                label,
                null,
                x,
                y,
                null,
                MapNodeOrigin.Automatic,
                MapMonitoringCapability.Unknown,
                MapNodeCategory.Unknown,
                deviceId);
        }

        private static MapLink Link(
            string key,
            string sourceNodeKey,
            string targetNodeKey,
            string sourcePortLabel,
            string targetPortLabel,
            Guid physicalLinkId)
        {
            return new MapLink(
                key,
                sourceNodeKey,
                targetNodeKey,
                sourcePortLabel,
                targetPortLabel,
                MapConfidence.High,
                MapFreshness.Fresh,
                new MapEvidenceItem[0],
                physicalLinkId);
        }

        private static Canvas MapCanvas(
            MainWindow window)
        {
            var canvas =
                window.FindName(
                    "MapCanvas") as Canvas;

            Assert.IsNotNull(canvas);

            return canvas;
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
                TimeSpan.FromSeconds(10)))
            {
                Assert.Fail(
                    "STA WPF test did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }
    }
}
