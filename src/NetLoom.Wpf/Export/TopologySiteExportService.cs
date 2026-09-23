using System;
using System.IO;
using NetLoom.Application.Export;

namespace NetLoom.Wpf.Export
{
    public sealed class TopologySiteExportResult
    {
        public TopologySiteExportResult(
            string pngPath,
            string csvPath)
        {
            if (string.IsNullOrWhiteSpace(
                pngPath))
            {
                throw new ArgumentException(
                    "PNG path is required.",
                    nameof(pngPath));
            }

            if (string.IsNullOrWhiteSpace(
                csvPath))
            {
                throw new ArgumentException(
                    "CSV path is required.",
                    nameof(csvPath));
            }

            PngPath = pngPath;
            CsvPath = csvPath;
        }

        public string PngPath { get; }

        public string CsvPath { get; }
    }

    public sealed class TopologySiteExportService
    {
        private readonly ITopologyExportSnapshotProvider
            _snapshotProvider;

        private readonly TopologyPngExporter
            _pngExporter;

        private readonly TopologyInventoryCsvExporter
            _csvExporter;

        public TopologySiteExportService(
            ITopologyExportSnapshotProvider snapshotProvider)
            : this(
                snapshotProvider,
                new TopologyPngExporter(),
                new TopologyInventoryCsvExporter())
        {
        }

        public TopologySiteExportService(
            ITopologyExportSnapshotProvider snapshotProvider,
            TopologyPngExporter pngExporter,
            TopologyInventoryCsvExporter csvExporter)
        {
            _snapshotProvider =
                snapshotProvider ??
                throw new ArgumentNullException(
                    nameof(snapshotProvider));

            _pngExporter =
                pngExporter ??
                throw new ArgumentNullException(
                    nameof(pngExporter));

            _csvExporter =
                csvExporter ??
                throw new ArgumentNullException(
                    nameof(csvExporter));
        }

        public TopologySiteExportResult Export(
            string pngPath)
        {
            if (string.IsNullOrWhiteSpace(
                pngPath))
            {
                throw new ArgumentException(
                    "PNG export path is required.",
                    nameof(pngPath));
            }

            var fullPngPath =
                Path.GetFullPath(
                    pngPath);

            if (!string.Equals(
                    Path.GetExtension(
                        fullPngPath),
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Site export path must use the .png extension.",
                    nameof(pngPath));
            }

            var directory =
                Path.GetDirectoryName(
                    fullPngPath);

            if (string.IsNullOrWhiteSpace(
                    directory) ||
                !Directory.Exists(
                    directory))
            {
                throw new DirectoryNotFoundException(
                    "Site export directory does not exist.");
            }

            var csvPath =
                Path.ChangeExtension(
                    fullPngPath,
                    ".csv");

            var snapshot =
                _snapshotProvider
                    .GetSnapshot();

            var pngBytes =
                _pngExporter.Export(
                    snapshot);

            var csvBytes =
                _csvExporter.Export(
                    snapshot);

            File.WriteAllBytes(
                fullPngPath,
                pngBytes);

            File.WriteAllBytes(
                csvPath,
                csvBytes);

            return new TopologySiteExportResult(
                fullPngPath,
                csvPath);
        }
    }
}
