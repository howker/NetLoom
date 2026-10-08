using System;
using System.Linq;
using System.Reflection;
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
    public sealed class Sprint49ParallelLinksMapTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Guid FirstDevice = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SecondDevice = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestMethod]
        public void OppositeDirectionLinksAreSeparatedAndLabelsDoNotIntersect()
        {
            RunOnSta(() =>
            {
                var window = new MainWindow();
                try
                {
                    DisableMotion(window);
                    SetReadableZoom(window);
                    var first = Link("a", "a", "b", "Gi1/0/1", "Gi1/0/2");
                    var second = Link("z", "b", "a", "Gi1/0/3", "Gi1/0/4");
                    window.ShowMap(Snapshot(first, second));
                    var canvas = MapCanvas(window);
                    var lines = canvas.Children.OfType<Line>().OrderBy(line => line.Y1).ToArray();
                    Assert.AreEqual(2, lines.Length);
                    Assert.AreEqual(Visibility.Visible, lines[0].Visibility);
                    Assert.AreEqual(Visibility.Visible, lines[1].Visibility);
                    Assert.AreEqual(lines[0].Y1, lines[0].Y2, 0.001);
                    Assert.AreEqual(lines[1].Y1, lines[1].Y2, 0.001);
                    Assert.AreEqual(lines[0].X1, lines[1].X2, 0.001);
                    Assert.AreEqual(lines[0].X2, lines[1].X1, 0.001);
                    Assert.AreEqual((double)window.FindResource("NetLoom.Map.ParallelLinkSpacing"),
                        lines[1].Y1 - lines[0].Y1, 0.001);

                    var labels = canvas.Children.OfType<TextBlock>().OrderBy(label => label.Text, StringComparer.Ordinal).ToArray();
                    Assert.AreEqual(2, labels.Length);
                    Assert.IsTrue(labels.All(label => label.Visibility == Visibility.Visible));
                    Assert.IsTrue(labels.All(label => label.DesiredSize.Width > 0 && label.DesiredSize.Height > 0));
                    var bounds = labels.Select(LabelBounds).ToArray();
                    Assert.IsFalse(bounds[0].IntersectsWith(bounds[1]));

                    // Перестановка снимка сохраняет визуальные объекты и расположение подписей.
                    window.ShowMap(Snapshot(second, first));
                    CollectionAssert.AreEquivalent(lines, canvas.Children.OfType<Line>().ToArray());
                    var updatedLabels = canvas.Children.OfType<TextBlock>().OrderBy(label => label.Text, StringComparer.Ordinal).ToArray();
                    for (var index = 0; index < labels.Length; index++)
                    {
                        Assert.AreSame(labels[index], updatedLabels[index]);
                        Assert.AreEqual(bounds[index], LabelBounds(updatedLabels[index]));
                    }

                    // Обновление на дальнем масштабе не теряет размеры будущих подписей.
                    typeof(MainWindow).GetField("_zoom", PrivateInstance).SetValue(window,
                        (double)window.FindResource("NetLoom.Map.ReadableZoomMin"));
                    window.ShowMap(Snapshot(second, first));
                    window.ShowMap(Snapshot(first, second));
                    SetReadableZoom(window);
                    var zoomedLabels = canvas.Children.OfType<TextBlock>().ToArray();
                    Assert.IsTrue(zoomedLabels.All(label => label.Visibility == Visibility.Visible));
                    Assert.IsFalse(LabelBounds(zoomedLabels[0]).IntersectsWith(LabelBounds(zoomedLabels[1])));

                    // После удаления второго кабеля оставшаяся линия возвращается на ось пары.
                    window.ShowMap(Snapshot(first));
                    Assert.AreSame(lines[0], canvas.Children.OfType<Line>().Single());
                    AssertJoinsCenters(window, canvas, canvas.Children.OfType<Line>().Single());
                }
                finally { window.Close(); }
            });
        }

        [TestMethod]
        public void SingleLinkJoinsNodeCenters()
        {
            RunOnSta(() =>
            {
                var window = new MainWindow();
                try
                {
                    DisableMotion(window);
                    window.ShowMap(Snapshot(Link("one", "a", "b", "Gi1/0/1", "Gi1/0/2")));
                    var canvas = MapCanvas(window);
                    AssertJoinsCenters(window, canvas, canvas.Children.OfType<Line>().Single());
                }
                finally { window.Close(); }
            });
        }

        private static void AssertJoinsCenters(MainWindow window, Canvas canvas, Line line)
        {
            var first = canvas.Children.OfType<Border>().Single(border => Equals(border.Tag, FirstDevice));
            var second = canvas.Children.OfType<Border>().Single(border => Equals(border.Tag, SecondDevice));
            Assert.AreEqual(Canvas.GetLeft(first) + first.Width / 2.0, line.X1, 0.001);
            var nodeHeight = (double)window.FindResource("NetLoom.Map.NodeHeight");
            Assert.AreEqual(Canvas.GetTop(first) + Math.Max(nodeHeight, first.DesiredSize.Height) / 2.0, line.Y1, 0.001);
            Assert.AreEqual(Canvas.GetLeft(second) + second.Width / 2.0, line.X2, 0.001);
            Assert.AreEqual(Canvas.GetTop(second) + Math.Max(nodeHeight, second.DesiredSize.Height) / 2.0, line.Y2, 0.001);
        }

        private static Rect LabelBounds(TextBlock label)
        {
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Rect(Canvas.GetLeft(label), Canvas.GetTop(label), label.DesiredSize.Width, label.DesiredSize.Height);
        }

        private static MapSnapshot Snapshot(params MapLink[] links)
        {
            return new MapSnapshot(Now, new[]
            {
                Node("a", "A", 60, 60, FirstDevice),
                Node("b", "B", 660, 60, SecondDevice)
            }, links);
        }

        private static MapNode Node(string key, string label, double x, double y, Guid deviceId)
        {
            return new MapNode(key, label, null, x, y, null, MapNodeOrigin.Automatic,
                MapMonitoringCapability.Unknown, MapNodeCategory.Unknown, deviceId);
        }

        private static MapLink Link(string key, string source, string target, string sourcePort, string targetPort)
        {
            return new MapLink(key, source, target, sourcePort, targetPort,
                MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0]);
        }

        private static Canvas MapCanvas(MainWindow window)
        {
            var canvas = window.FindName("MapCanvas") as Canvas;
            Assert.IsNotNull(canvas);
            return canvas;
        }

        private static void SetReadableZoom(MainWindow window)
        {
            var zoom = Math.Max(1.0, (double)window.FindResource("NetLoom.Map.LinkLabelMinZoom"));
            typeof(MainWindow).GetField("_zoom", PrivateInstance).SetValue(window, zoom);
            typeof(MainWindow).GetMethod("ApplyZoomTransform", PrivateInstance).Invoke(window, null);
        }

        private static void DisableMotion(MainWindow window)
        {
            var settings = (Button)window.FindName("MapSettingsButton");
            var motion = settings.ContextMenu.Items.OfType<MenuItem>()
                .Single(item => item.Items.OfType<MenuItem>().Count() == 3);
            motion.Items.OfType<MenuItem>().Last().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }

        private static void RunOnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception error) { failure = error; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(10)))
                Assert.Fail("STA WPF test did not complete.");
            if (failure != null) throw failure;
        }
    }
}
