using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint33AMapReconciliationTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                13,
                18,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void StableDeviceIdReusesBorderAndPreservesCanvasPosition()
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
                        window.ShowMap(
                            Snapshot(
                                Node(
                                    "presentation-a",
                                    "Before",
                                    100,
                                    120,
                                    deviceId)));

                        var canvas =
                            MapCanvas(window);

                        var first =
                            canvas.Children
                                .OfType<Border>()
                                .Single();

                        Canvas.SetLeft(
                            first,
                            333);

                        Canvas.SetTop(
                            first,
                            444);

                        window.ShowMap(
                            Snapshot(
                                Node(
                                    "presentation-b",
                                    "After",
                                    900,
                                    800,
                                    deviceId)));

                        var second =
                            canvas.Children
                                .OfType<Border>()
                                .Single();

                        Assert.AreSame(
                            first,
                            second);

                        Assert.AreEqual(
                            333,
                            Canvas.GetLeft(second));

                        Assert.AreEqual(
                            444,
                            Canvas.GetTop(second));

                        Assert.AreEqual(
                            "After",
                            NodeTitle(second).Text);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void StableLinkKeyReusesLineAndLabelAndUsesRetainedNodePositions()
        {
            RunOnSta(
                () =>
                {
                    var firstDeviceId =
                        Guid.NewGuid();

                    var secondDeviceId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        var firstA =
                            Node(
                                "node-a",
                                "A",
                                60,
                                60,
                                firstDeviceId);

                        var firstB =
                            Node(
                                "node-b",
                                "B",
                                300,
                                60,
                                secondDeviceId);

                        window.ShowMap(
                            Snapshot(
                                new[] { firstA, firstB },
                                new[]
                                {
                                    Link(
                                        "stable-link",
                                        firstA.Key,
                                        firstB.Key,
                                        "Gi1/0/1",
                                        "Gi1/0/2")
                                }));

                        var canvas =
                            MapCanvas(window);

                        var firstLine =
                            canvas.Children
                                .OfType<Line>()
                                .Single();

                        var firstLabel =
                            canvas.Children
                                .OfType<TextBlock>()
                                .Single();

                        var firstBorder =
                            BorderByTitle(
                                canvas,
                                "A");

                        var secondBorder =
                            BorderByTitle(
                                canvas,
                                "B");

                        Canvas.SetLeft(
                            firstBorder,
                            200);

                        Canvas.SetTop(
                            firstBorder,
                            250);

                        Canvas.SetLeft(
                            secondBorder,
                            500);

                        Canvas.SetTop(
                            secondBorder,
                            450);

                        var nextA =
                            Node(
                                "node-a-next",
                                "A2",
                                900,
                                800,
                                firstDeviceId);

                        var nextB =
                            Node(
                                "node-b-next",
                                "B2",
                                1100,
                                800,
                                secondDeviceId);

                        window.ShowMap(
                            Snapshot(
                                new[] { nextA, nextB },
                                new[]
                                {
                                    Link(
                                        "stable-link",
                                        nextA.Key,
                                        nextB.Key,
                                        "Gi1/0/10",
                                        "Gi1/0/20")
                                }));

                        var nextLine =
                            canvas.Children
                                .OfType<Line>()
                                .Single();

                        var nextLabel =
                            canvas.Children
                                .OfType<TextBlock>()
                                .Single();

                        Assert.AreSame(
                            firstLine,
                            nextLine);

                        Assert.AreSame(
                            firstLabel,
                            nextLabel);

                        Assert.AreEqual(
                            295,
                            nextLine.X1);

                        Assert.AreEqual(
                            595,
                            nextLine.X2);

                        StringAssert.Contains(
                            nextLabel.Text,
                            "Gi1/0/10");

                        StringAssert.Contains(
                            nextLabel.Text,
                            "Gi1/0/20");
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        [TestMethod]
        public void RemovingNodeKeepsNeighborVisualInstance()
        {
            RunOnSta(
                () =>
                {
                    var retainedDeviceId =
                        Guid.NewGuid();

                    var removedDeviceId =
                        Guid.NewGuid();

                    var window =
                        new MainWindow();

                    try
                    {
                        DisableMotion(
                            window);

                        var retained =
                            Node(
                                "retained",
                                "Retained",
                                60,
                                60,
                                retainedDeviceId);

                        var removed =
                            Node(
                                "removed",
                                "Removed",
                                300,
                                60,
                                removedDeviceId);

                        window.ShowMap(
                            Snapshot(
                                retained,
                                removed));

                        var canvas =
                            MapCanvas(window);

                        var retainedBorder =
                            BorderByTitle(
                                canvas,
                                "Retained");

                        var removedBorder =
                            BorderByTitle(
                                canvas,
                                "Removed");

                        window.ShowMap(
                            Snapshot(
                                Node(
                                    "retained-next",
                                    "Retained updated",
                                    900,
                                    900,
                                    retainedDeviceId)));

                        var remaining =
                            canvas.Children
                                .OfType<Border>()
                                .Single();

                        Assert.AreSame(
                            retainedBorder,
                            remaining);

                        Assert.IsFalse(
                            canvas.Children.Contains(
                                removedBorder));

                        Assert.AreEqual(
                            "Retained updated",
                            NodeTitle(remaining).Text);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
        }

        private static MapSnapshot Snapshot(
            params MapNode[] nodes)
        {
            return Snapshot(
                nodes,
                new MapLink[0]);
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
            string targetPortLabel)
        {
            return new MapLink(
                key,
                sourceNodeKey,
                targetNodeKey,
                sourcePortLabel,
                targetPortLabel,
                MapConfidence.High,
                MapFreshness.Fresh,
                new MapEvidenceItem[0]);
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

        private static Border BorderByTitle(
            Canvas canvas,
            string title)
        {
            return canvas.Children
                .OfType<Border>()
                .Single(
                    border =>
                        string.Equals(
                            NodeTitle(border).Text,
                            title,
                            StringComparison.Ordinal));
        }

        private static TextBlock NodeTitle(
            Border border)
        {
            var content =
                border.Child as StackPanel;

            Assert.IsNotNull(content);

            var title =
                content.Children[0]
                as TextBlock;

            Assert.IsNotNull(title);

            return title;
        }

        private static void DisableMotion(
            MainWindow window)
        {
            var motionButton =
                window.FindName(
                    "MapMotionModeButton")
                as Button;

            Assert.IsNotNull(
                motionButton);

            motionButton.RaiseEvent(
                new System.Windows.RoutedEventArgs(
                    Button.ClickEvent));

            motionButton.RaiseEvent(
                new System.Windows.RoutedEventArgs(
                    Button.ClickEvent));
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
