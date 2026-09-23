using System;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Export;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Export;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint44TopologyPngExporterTests
    {
        [TestMethod]
        public void
            WritesPngWithBoundedPlannedDimensions()
        {
            RunSta(
                () =>
                {
                    var snapshot =
                        CreateSnapshot();

                    var diagram =
                        new TopologyExportDiagramBuilder()
                            .Build(
                                snapshot);

                    var bytes =
                        new TopologyPngExporter()
                            .Export(
                                diagram);

                    Assert.IsTrue(
                        bytes.Length > 8);

                    Assert.AreEqual(
                        (byte)0x89,
                        bytes[0]);

                    Assert.AreEqual(
                        (byte)0x50,
                        bytes[1]);

                    Assert.AreEqual(
                        (byte)0x4E,
                        bytes[2]);

                    Assert.AreEqual(
                        (byte)0x47,
                        bytes[3]);

                    using (var stream =
                        new MemoryStream(
                            bytes))
                    {
                        var decoder =
                            new PngBitmapDecoder(
                                stream,
                                BitmapCreateOptions.PreservePixelFormat,
                                BitmapCacheOption.OnLoad);

                        Assert.AreEqual(
                            1,
                            decoder.Frames.Count);

                        Assert.AreEqual(
                            diagram.PixelWidth,
                            decoder.Frames[0].PixelWidth);

                        Assert.AreEqual(
                            diagram.PixelHeight,
                            decoder.Frames[0].PixelHeight);
                    }
                });
        }

        [TestMethod]
        public void
            RendersNonUniformDiagramWithoutUsingLiveCanvas()
        {
            RunSta(
                () =>
                {
                    var bytes =
                        new TopologyPngExporter()
                            .Export(
                                CreateSnapshot());

                    using (var stream =
                        new MemoryStream(
                            bytes))
                    {
                        var decoder =
                            new PngBitmapDecoder(
                                stream,
                                BitmapCreateOptions.PreservePixelFormat,
                                BitmapCacheOption.OnLoad);

                        var frame =
                            decoder.Frames[0];

                        var converted =
                            new FormatConvertedBitmap(
                                frame,
                                System.Windows.Media.PixelFormats.Bgra32,
                                null,
                                0.0);

                        var stride =
                            converted.PixelWidth * 4;

                        var pixels =
                            new byte[
                                stride *
                                converted.PixelHeight];

                        converted.CopyPixels(
                            pixels,
                            stride,
                            0);

                        var firstBlue =
                            pixels[0];

                        var firstGreen =
                            pixels[1];

                        var firstRed =
                            pixels[2];

                        var foundDifferent =
                            false;

                        for (var index = 4;
                             index + 2 < pixels.Length;
                             index += 4)
                        {
                            if (pixels[index] != firstBlue ||
                                pixels[index + 1] != firstGreen ||
                                pixels[index + 2] != firstRed)
                            {
                                foundDifferent = true;
                                break;
                            }
                        }

                        Assert.IsTrue(
                            foundDifferent,
                            "The PNG must contain rendered topology, not a flat canvas capture.");
                    }
                });
        }

        private static TopologyExportSnapshot
            CreateSnapshot()
        {
            var generatedUtc =
                new DateTime(
                    2026,
                    9,
                    23,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc);

            var locationId =
                new Guid(
                    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            var map =
                new MapSnapshot(
                    generatedUtc,
                    new[]
                    {
                        new MapNode(
                            "a",
                            "Core",
                            "10.0.0.1",
                            40.0,
                            80.0,
                            locationId),
                        new MapNode(
                            "b",
                            "Access",
                            "10.0.0.2",
                            420.0,
                            220.0,
                            locationId)
                    },
                    new[]
                    {
                        new MapLink(
                            "a-b",
                            "a",
                            "b",
                            "Gi1/0/1",
                            "Gi1/0/2",
                            MapConfidence.High,
                            MapFreshness.Fresh,
                            new MapEvidenceItem[0])
                    },
                    new[]
                    {
                        new MapLocation(
                            locationId,
                            null,
                            "Server room",
                            "Main rack")
                    });

            return new TopologyExportSnapshot(
                new TopologyRefreshSnapshot(
                    map,
                    new TopologyAlertSnapshot(
                        generatedUtc,
                        "cist",
                        new TopologyAlert[0])),
                new TopologyExportLayoutSnapshot(
                    MapLayoutScope.PhysicalTopologyMapId,
                    new MapDeviceLayout[0],
                    new MapLocationLayout[0]));
        }

        private static void RunSta(
            Action action)
        {
            Exception failure = null;

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
            thread.Join();

            if (failure != null)
            {
                throw failure;
            }
        }
    }
}
