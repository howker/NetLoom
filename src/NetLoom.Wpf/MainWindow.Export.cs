using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using NetLoom.Application.Export;
using NetLoom.Wpf.Export;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private readonly ITopologyExportSnapshotProvider
        _topologyExportSnapshotProvider;

    private void OnSiteExportClick(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = ".png",
                Filter =
                    ExportUiText.Get(
                        "SiteExportPngFilter"),
                FileName =
                    "netloom-site-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss",
                        CultureInfo.InvariantCulture) +
                    ".png",
                OverwritePrompt = true,
                Title =
                    ExportUiText.Get(
                        "SiteExportDialogTitle")
            };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var csvPath =
            Path.ChangeExtension(
                Path.GetFullPath(
                    dialog.FileName),
                ".csv");

        if (File.Exists(
            csvPath))
        {
            var overwrite =
                MessageBox.Show(
                    this,
                    ExportUiText.Format(
                        "SiteExportCsvOverwritePrompt",
                        csvPath),
                    ExportUiText.Get(
                        "SiteExportCsvOverwriteTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

            if (overwrite !=
                MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            var result =
                new TopologySiteExportService(
                    _topologyExportSnapshotProvider)
                    .Export(
                        dialog.FileName);

            MessageBox.Show(
                this,
                ExportUiText.Format(
                    "SiteExportSuccessBody",
                    result.PngPath,
                    result.CsvPath),
                ExportUiText.Get(
                    "SiteExportSuccessTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception error)
        {
            Trace.TraceError(
                error.ToString());

            MessageBox.Show(
                this,
                ExportUiText.Get(
                    "SiteExportFailedBody"),
                ExportUiText.Get(
                    "SiteExportFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
