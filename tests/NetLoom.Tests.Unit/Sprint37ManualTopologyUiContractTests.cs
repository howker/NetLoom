using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint37ManualTopologyUiContractTests
    {
        [TestMethod]
        public void ManualTopologyUiUsesApplicationBoundaryAndDesktopComposition()
        {
            var mainXaml =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/MainWindow.xaml");

            var mainCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/MainWindow.xaml.cs");

            var editorXaml =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/ManualTopologyWindow.xaml");

            var editorCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/ManualTopologyWindow.xaml.cs");

            var desktop =
                ReadRepositoryFile(
                    "src/NetLoom.Desktop/Program.cs");

            StringAssert.Contains(
                mainXaml,
                "ManualTopologyButton");

            StringAssert.Contains(
                mainCode,
                "IManualTopologyService");

            StringAssert.Contains(
                mainCode,
                "new ManualTopologyWindow");

            StringAssert.Contains(
                editorXaml,
                "ManualDevicesTab");

            StringAssert.Contains(
                editorXaml,
                "ManualPortsTab");

            StringAssert.Contains(
                editorXaml,
                "ManualLinksTab");

            StringAssert.Contains(
                editorCode,
                "_service.CreateDevice");

            StringAssert.Contains(
                editorCode,
                "_service.CreatePort");

            StringAssert.Contains(
                editorCode,
                "_service.CreateLink");

            StringAssert.Contains(
                editorCode,
                "_service.UpdateDevice");

            StringAssert.Contains(
                editorCode,
                "_service.UpdatePort");

            StringAssert.Contains(
                editorCode,
                "_service.UpdateLink");

            StringAssert.Contains(
                editorCode,
                "_service.DeleteDevice");

            StringAssert.Contains(
                editorCode,
                "_service.DeletePort");

            StringAssert.Contains(
                editorCode,
                "_service.DeleteLink");

            StringAssert.Contains(
                desktop,
                "new ManualTopologyService");

            StringAssert.Contains(
                desktop,
                "new SqliteManualTopologyAuditStore");

            Assert.IsFalse(
                mainCode.Contains(
                    "NetLoom.Persistence.Sqlite"),
                "MainWindow must not depend on SQLite.");

            Assert.IsFalse(
                editorCode.Contains(
                    "NetLoom.Persistence.Sqlite"),
                "Manual topology WPF editor must not depend on SQLite.");

            Assert.IsFalse(
                editorCode.Contains(
                    "NetLoom.Domain.Topology"),
                "Manual topology WPF editor must use Application editor models rather than Domain topology state.");
        }

        [TestMethod]
        public void ManualTopologyOperatorStringsExistInNeutralAndRussianResources()
        {
            foreach (var fileName in
                new[]
                {
                    "UiStrings.resx",
                    "UiStrings.ru.resx"
                })
            {
                var document =
                    XDocument.Load(
                        FindRepositoryFile(
                            Path.Combine(
                                "src",
                                "NetLoom.Wpf",
                                "Resources",
                                fileName)));

                var names =
                    document
                        .Descendants("data")
                        .Select(
                            element =>
                                (string)element.Attribute(
                                    "name"))
                        .ToArray();

                foreach (var required in
                    new[]
                    {
                        "ManualTopologyAction",
                        "ManualTopologyIntro",
                        "ManualTopologyDevicesTab",
                        "ManualTopologyPortsTab",
                        "ManualTopologyLinksTab",
                        "ManualTopologyValidationDifferentDevices",
                        "ManualTopologyDeleteConnectedDevice",
                        "ManualTopologyLinkConflict",
                        "ManualTopologyMediaFiber",
                        "ManualTopologyInteractionHint",
                        "ManualTopologyConfirmDeleteDevice",
                        "ManualTopologyConfirmDeletePort",
                        "ManualTopologyConfirmDeleteLink",
                        "DiagnosticInterfaceIdentityIfIndex",
                        "DiagnosticDeviceLevelEndpoint",
                        "DiagnosticInterfaceNoTelemetry"
                    })
                {
                    Assert.IsTrue(
                        names.Contains(
                            required,
                            StringComparer.Ordinal),
                        fileName +
                        " is missing resource key " +
                        required);
                }
            }

            var russian =
                XDocument.Load(
                    FindRepositoryFile(
                        Path.Combine(
                            "src",
                            "NetLoom.Wpf",
                            "Resources",
                            "UiStrings.ru.resx")));

            var action =
                russian
                    .Descendants("data")
                    .Single(
                        element =>
                            string.Equals(
                                (string)element.Attribute(
                                    "name"),
                                "ManualTopologyAction",
                                StringComparison.Ordinal))
                    .Element("value")
                    .Value;

            Assert.AreEqual(
                "Ручная топология",
                action);
        }

        [TestMethod]
        public void ManualTopologyOperatorRemediationContractIsPresent()
        {
            var editorXaml =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/ManualTopologyWindow.xaml");

            var editorCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/ManualTopologyWindow.xaml.cs");

            var mainCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/MainWindow.xaml.cs");

            StringAssert.Contains(
                editorXaml,
                "MouseDoubleClick=\"OnDeviceListDoubleClick\"");

            StringAssert.Contains(
                editorXaml,
                "PreviewMouseRightButtonDown=\"OnPortListPreviewMouseRightButtonDown\"");

            StringAssert.Contains(
                editorXaml,
                "KeyDown=\"OnLinkListKeyDown\"");

            StringAssert.Contains(
                editorCode,
                "Key.Delete");

            StringAssert.Contains(
                editorCode,
                "ManualTopologyConfirmDeleteDevice");

            StringAssert.Contains(
                editorCode,
                "MessageBoxButton.YesNo");

            StringAssert.Contains(
                editorCode,
                "ManualTopologyDeleteConnectedDevice");

            StringAssert.Contains(
                editorCode,
                "ManualPortMediaComboBox.IsEnabled");

            StringAssert.Contains(
                mainCode,
                "DisplayLinkEndpointInterface");

            StringAssert.Contains(
                mainCode,
                "DiagnosticInterfaceIdentityIfIndex");

            StringAssert.Contains(
                mainCode,
                "DiagnosticDeviceLevelEndpoint");

            Assert.IsFalse(
                mainCode.Contains(
                    "\"DiagnosticInterfaceRow\""),
                "The device panel must not render the old all-fields-in-one-line interface row.");
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

                directory =
                    directory.Parent;
            }

            throw new FileNotFoundException(
                "Repository file was not found: " +
                relativePath);
        }
    }
}
