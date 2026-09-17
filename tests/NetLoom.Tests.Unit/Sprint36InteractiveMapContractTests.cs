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
