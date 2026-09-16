using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint35DiagnosticPanelContractTests
    {
        [TestMethod]
        public void DiagnosticPanelContractIsPresentAcrossRefreshAndWpf()
        {
            var contractPath =
                FindRepositoryPath(
                    "src",
                    "NetLoom.Contracts",
                    "Diagnostics",
                    "NetworkDiagnosticSnapshot.cs");

            Assert.IsTrue(
                File.Exists(contractPath),
                "Sprint 35 diagnostic contract is missing.");

            var readSet =
                ReadRepositoryFile(
                    "src",
                    "NetLoom.Application",
                    "Topology",
                    "MaterializedTopologyReadSet.cs");

            StringAssert.Contains(
                readSet,
                "InterfaceDegradationStates");

            StringAssert.Contains(
                readSet,
                "PhysicalLinkEvidenceExplanations");

            var refresh =
                ReadRepositoryFile(
                    "src",
                    "NetLoom.Application",
                    "TopologyRefresh",
                    "TopologyRefreshSnapshot.cs");

            StringAssert.Contains(
                refresh,
                "DiagnosticSnapshot");

            var mainWindow =
                ReadRepositoryFile(
                    "src",
                    "NetLoom.Wpf",
                    "MainWindow.xaml.cs");

            StringAssert.Contains(
                mainWindow,
                "OnMapNodeMouseLeftButtonDown");

            StringAssert.Contains(
                mainWindow,
                "OnMapLinkMouseLeftButtonDown");

            StringAssert.Contains(
                mainWindow,
                "ShowSelectedDiagnostic");

            var xaml =
                ReadRepositoryFile(
                    "src",
                    "NetLoom.Wpf",
                    "MainWindow.xaml");

            StringAssert.Contains(
                xaml,
                "DiagnosticFieldsList");

            StringAssert.Contains(
                xaml,
                "DiagnosticSecondaryList");

            StringAssert.Contains(
                xaml,
                "DiagnosticTertiaryList");

            foreach (var fileName in
                new[]
                {
                    "UiStrings.resx",
                    "UiStrings.ru.resx"
                })
            {
                var document =
                    XDocument.Load(
                        FindRepositoryPath(
                            "src",
                            "NetLoom.Wpf",
                            "Resources",
                            fileName));

                var names =
                    document
                        .Descendants("data")
                        .Select(
                            element =>
                                (string)element.Attribute("name"))
                        .ToArray();

                foreach (var required in
                    new[]
                    {
                        "DiagnosticTitle",
                        "DiagnosticInterfacesTitle",
                        "DiagnosticConnectionsTitle",
                        "DiagnosticImpactTitle",
                        "DiagnosticEvidenceTitle",
                        "DiagnosticRawExpired"
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
        }

        private static string ReadRepositoryFile(
            params string[] parts)
        {
            return File.ReadAllText(
                FindRepositoryPath(parts));
        }

        private static string FindRepositoryPath(
            params string[] parts)
        {
            var directory =
                new DirectoryInfo(
                    AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null)
            {
                var candidate =
                    parts.Aggregate(
                        directory.FullName,
                        Path.Combine);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return parts.Aggregate(
                Path.GetPathRoot(
                    AppDomain.CurrentDomain.BaseDirectory),
                Path.Combine);
        }
    }
}
