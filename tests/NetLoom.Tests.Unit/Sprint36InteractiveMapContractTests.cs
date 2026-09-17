using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint36InteractiveMapContractTests
    {
        [TestMethod]
        public void InteractivePersistentMapContractIsPresent()
        {
            var migration =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Persistence.Sqlite",
                        "Migrations",
                        "Migration019MapLayout.cs"));

            var layoutModels =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Application",
                        "MapLayout",
                        "MapLayoutModels.cs"));

            var layoutStore =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Persistence.Sqlite",
                        "MapLayout",
                        "SqliteMapLayoutStore.cs"));

            var xaml =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "MainWindow.xaml"));

            var code =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "MainWindow.xaml.cs"));

            var motion =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "MapInteraction",
                        "MapMotionPolicy.cs"));

            var virtualWorkspace =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "MapInteraction",
                        "MapVirtualWorkspace.cs"));

            var designTokens =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Themes",
                        "DesignTokens.xaml"));

            var controls =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Themes",
                        "Controls.xaml"));

            var russianStrings =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Resources",
                        "UiStrings.ru.resx"));

            var desktop =
                ReadRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Desktop",
                        "Program.cs"));

            StringAssert.Contains(
                migration,
                "CREATE TABLE maps");

            StringAssert.Contains(
                migration,
                "CREATE TABLE map_device_layout");

            StringAssert.Contains(
                layoutModels,
                "interface IMapLayoutStore");

            StringAssert.Contains(
                layoutStore,
                "ON CONFLICT(map_id, device_id)");

            StringAssert.Contains(
                xaml,
                "OnMapPreviewMouseWheel");

            StringAssert.Contains(
                xaml,
                "OnMapPreviewMouseDown");

            StringAssert.Contains(
                code,
                "OnMapNodeMouseMove");

            StringAssert.Contains(
                code,
                "TrySaveDeviceLayout");

            StringAssert.Contains(
                code,
                "TrySaveViewportLayout");

            StringAssert.Contains(
                motion,
                "MapMotionMode.Off");

            StringAssert.Contains(
                motion,
                "MapMotionKind.AlertPulse");

            StringAssert.Contains(
                code,
                "MapNodeLockedBadge");

            StringAssert.Contains(
                code,
                "MapMotionNormalHint");

            StringAssert.Contains(
                code,
                "MapVirtualWorkspace");

            StringAssert.Contains(
                code,
                "ToCanvasCoordinate");

            StringAssert.Contains(
                code,
                "ToLogicalPan");

            StringAssert.Contains(
                code,
                "UpdateNodeLockPresentation");

            Assert.IsFalse(
                code.Contains(
                    "private static void UpdateNodeLockPresentation(\r\n" +
                    "        MapNodeVisual visual)\r\n" +
                    "    {\r\n" +
                    "        UpdateNodeLockPresentation("),
                "Lock presentation must not recursively call itself.");

            StringAssert.Contains(
                virtualWorkspace,
                "ToScrollOffset");

            StringAssert.Contains(
                designTokens,
                "NetLoom.Map.VirtualOriginX");

            StringAssert.Contains(
                designTokens,
                "1000000");

            StringAssert.Contains(
                designTokens,
                "NetLoom.Map.ZoomMin\">0.01");

            StringAssert.Contains(
                designTokens,
                "NetLoom.Map.ZoomMax\">5.0");

            StringAssert.Contains(
                xaml,
                "MapFitAllButton");

            StringAssert.Contains(
                xaml,
                "MapHelpButton");

            StringAssert.Contains(
                xaml,
                "MapSettingsButton");

            Assert.IsFalse(
                xaml.Contains(
                    "MapMotionModeButton"),
                "Change animation is a map setting, not a primary toolbar action.");

            StringAssert.Contains(
                code,
                "FitTopologyToViewport");

            StringAssert.Contains(
                code,
                "MapHelpBody");

            StringAssert.Contains(
                code,
                "InitializeMapSettingsMenu");

            StringAssert.Contains(
                controls,
                "NetLoom.Style.MapNodeLockBadge");

            StringAssert.Contains(
                russianStrings,
                "MapNodeLockedHint");

            StringAssert.Contains(
                russianStrings,
                "MapMotionReducedHint");

            StringAssert.Contains(
                desktop,
                "new SqliteMapLayoutStore");

            Assert.IsFalse(
                code.Contains(
                    "NetLoom.Persistence.Sqlite"),
                "WPF must not depend on SQLite persistence directly.");
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
                    AppDomain.CurrentDomain.BaseDirectory);

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

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "Repository file was not found: " +
                relativePath);
        }
    }
}
