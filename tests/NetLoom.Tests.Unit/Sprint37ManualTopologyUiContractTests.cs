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
                        "ManualTopologyMapNodeHint",
                        "ManualTopologyConfirmDeleteDevice",
                        "ManualTopologyConfirmDeletePort",
                        "ManualTopologyConfirmDeleteLink",
                        "DiagnosticFieldConnections",
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

        [TestMethod]
        public void ManualTopologyMainMapOperatorInteractionContractIsPresent()
        {
            var mainCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/MainWindow.xaml.cs");

            var editorCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/ManualTopologyWindow.xaml.cs");

            StringAssert.Contains(
                mainCode,
                "CreateMapElementContextMenu");

            StringAssert.Contains(
                mainCode,
                "OnMapNodeMouseRightButtonDown");

            StringAssert.Contains(
                mainCode,
                "OnMapNodeKeyDown");

            StringAssert.Contains(
                mainCode,
                "e.ClickCount >= 2");

            StringAssert.Contains(
                mainCode,
                "DeleteManualDeviceFromMapAsync");

            StringAssert.Contains(
                mainCode,
                "DeleteManualLinkFromMapAsync");

            StringAssert.Contains(
                mainCode,
                "ManualTopologyConfirmDeleteDevice");

            StringAssert.Contains(
                mainCode,
                "ManualTopologyConfirmDeleteLink");

            StringAssert.Contains(
                editorCode,
                "Guid? initialDeviceId");

            StringAssert.Contains(
                editorCode,
                "Guid? initialLinkId");

            StringAssert.Contains(
                editorCode,
                "ManualTopologyTabs.SelectedItem");

            StringAssert.Contains(
                editorCode,
                "ManualPortsTab");

            var showDeviceStart =
                mainCode.IndexOf(
                    "private void ShowDeviceDiagnostic",
                    StringComparison.Ordinal);

            var showLinkStart =
                mainCode.IndexOf(
                    "private void ShowLinkDiagnostic",
                    StringComparison.Ordinal);

            Assert.IsTrue(
                showDeviceStart >= 0 &&
                showLinkStart > showDeviceStart,
                "Device diagnostic method boundary was not found.");

            var deviceDiagnostic =
                mainCode.Substring(
                    showDeviceStart,
                    showLinkStart - showDeviceStart);

            var connectionsTitle =
                deviceDiagnostic.IndexOf(
                    "DiagnosticConnectionsTitle",
                    StringComparison.Ordinal);

            var interfacesTitle =
                deviceDiagnostic.IndexOf(
                    "DiagnosticInterfacesTitle",
                    StringComparison.Ordinal);

            Assert.IsTrue(
                connectionsTitle >= 0 &&
                interfacesTitle > connectionsTitle,
                "Physical connections must be shown before the interface inventory in the selected-device panel.");
        }

        [TestMethod]
        public void
            Pass4UsesObservedInterfaceIdentityAndExplicitSavePrompt()
        {
            var mainCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/MainWindow.xaml.cs");

            var editorCode =
                ReadRepositoryFile(
                    "src/NetLoom.Wpf/ManualTopologyWindow.xaml.cs");

            StringAssert.Contains(
                mainCode,
                "item.IfName");

            StringAssert.Contains(
                mainCode,
                "item.IfAlias");

            StringAssert.Contains(
                mainCode,
                "item.IfType");

            Assert.IsFalse(
                mainCode.Contains("InterfacePrefix"),
                "UI must not synthesize vendor-specific port prefixes from speed or media.");

            Assert.IsFalse(
                mainCode.Contains("StandardInterfaceDisplayName"),
                "UI must render observed interface identity rather than invented names.");

            StringAssert.Contains(
                editorCode,
                "ManualTopologySaveBeforeLeavingDevice");

            StringAssert.Contains(
                editorCode,
                "MessageBoxButton.YesNo");
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
