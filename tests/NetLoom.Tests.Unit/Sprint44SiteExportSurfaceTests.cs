using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Export;
using NetLoom.Application.MapLayout;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.Export;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint44SiteExportSurfaceTests
    {
        [TestMethod]
        public void
            WritesPngAndCsvFromOneCoherentExportSnapshot()
        {
            RunSta(
                () =>
                {
                    var provider =
                        new RecordingExportSnapshotProvider(
                            CreateSnapshot());

                    var service =
                        new TopologySiteExportService(
                            provider);

                    var directory =
                        Path.Combine(
                            Path.GetTempPath(),
                            "NetLoom-Sprint44-" +
                            Guid.NewGuid().ToString("N"));

                    Directory.CreateDirectory(
                        directory);

                    try
                    {
                        var pngPath =
                            Path.Combine(
                                directory,
                                "assessment.png");

                        var result =
                            service.Export(
                                pngPath);

                        Assert.AreEqual(
                            1,
                            provider.CallCount,
                            "PNG and CSV must come from one coherent export snapshot.");

                        Assert.AreEqual(
                            Path.GetFullPath(
                                pngPath),
                            result.PngPath);

                        Assert.AreEqual(
                            Path.ChangeExtension(
                                Path.GetFullPath(
                                    pngPath),
                                ".csv"),
                            result.CsvPath);

                        var png =
                            File.ReadAllBytes(
                                result.PngPath);

                        Assert.IsTrue(
                            png.Length > 8);

                        Assert.AreEqual(
                            (byte)0x89,
                            png[0]);

                        Assert.AreEqual(
                            (byte)0x50,
                            png[1]);

                        var csv =
                            File.ReadAllBytes(
                                result.CsvPath);

                        Assert.IsTrue(
                            csv.Length > 3);

                        Assert.AreEqual(
                            (byte)0xEF,
                            csv[0]);

                        Assert.AreEqual(
                            (byte)0xBB,
                            csv[1]);

                        Assert.AreEqual(
                            (byte)0xBF,
                            csv[2]);

                        var csvText =
                            Encoding.UTF8.GetString(
                                csv,
                                3,
                                csv.Length - 3);

                        StringAssert.Contains(
                            csvText,
                            "192.0.2.10");
                    }
                    finally
                    {
                        Directory.Delete(
                            directory,
                            true);
                    }
                });
        }

        [TestMethod]
        public void
            ExistingMapSettingsMenuOffersSiteExportAction()
        {
            RunSta(
                () =>
                {
                    var previousCulture =
                        Thread.CurrentThread
                            .CurrentUICulture;

                    try
                    {
                        Thread.CurrentThread
                            .CurrentUICulture =
                            CultureInfo.GetCultureInfo(
                                "en-US");

                        var window =
                            new MainWindow();

                        var settingsButton =
                            window.FindName(
                                "MapSettingsButton")
                            as Button;

                        Assert.IsNotNull(
                            settingsButton);

                        Assert.IsNotNull(
                            settingsButton.ContextMenu);

                        var matches =
                            settingsButton
                                .ContextMenu
                                .Items
                                .OfType<MenuItem>()
                                .Where(
                                    item =>
                                        string.Equals(
                                            item.Header as string,
                                            "Export site diagram and inventory...",
                                            StringComparison.Ordinal))
                                .ToArray();

                        Assert.AreEqual(
                            1,
                            matches.Length,
                            "Export must be exposed through the existing map settings surface.");
                    }
                    finally
                    {
                        Thread.CurrentThread
                            .CurrentUICulture =
                            previousCulture;
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

            var deviceId =
                new Guid(
                    "11111111-1111-1111-1111-111111111111");

            var map =
                new MapSnapshot(
                    generatedUtc,
                    new[]
                    {
                        new MapNode(
                            "device-1",
                            "Core",
                            "192.0.2.10",
                            100.0,
                            120.0,
                            deviceId: deviceId)
                    },
                    new MapLink[0]);

            var diagnostics =
                new NetworkDiagnosticSnapshot(
                    generatedUtc,
                    new[]
                    {
                        new DeviceDiagnostic(
                            deviceId,
                            "Core",
                            "primary",
                            "Server room",
                            generatedUtc,
                            generatedUtc,
                            new InterfaceDiagnostic[0],
                            "192.0.2.10")
                    },
                    new PhysicalLinkDiagnostic[0]);

            var topology =
                new TopologyRefreshSnapshot(
                    map,
                    new TopologyAlertSnapshot(
                        generatedUtc,
                        "cist",
                        new TopologyAlert[0]),
                    diagnostics);

            return new TopologyExportSnapshot(
                topology,
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

        private sealed class
            RecordingExportSnapshotProvider :
            ITopologyExportSnapshotProvider
        {
            private readonly TopologyExportSnapshot
                _snapshot;

            public RecordingExportSnapshotProvider(
                TopologyExportSnapshot snapshot)
            {
                _snapshot =
                    snapshot ??
                    throw new ArgumentNullException(
                        nameof(snapshot));
            }

            public int CallCount { get; private set; }

            public TopologyExportSnapshot
                GetSnapshot()
            {
                CallCount++;

                return _snapshot;
            }
        }
    }
}
