using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint39NodeDegradationVisualLanguageTests
    {
        [TestMethod]
        public void
            ConfirmedInterfaceDegradationDrivesNodeWarning()
        {
            Assert.AreEqual(
                "Degraded",
                Classify(
                    DiagnosticDegradationStatus.Healthy,
                    DiagnosticDegradationStatus.Degraded));

            Assert.AreEqual(
                "Degraded",
                Classify(
                    DiagnosticDegradationStatus.Unknown,
                    DiagnosticDegradationStatus.Degraded));

            Assert.AreEqual(
                "NetLoom.Brush.Warning",
                BrushForState(
                    "Degraded"));
        }

        [TestMethod]
        public void
            MissingOrUnknownEvidenceDoesNotInventNodeHealthOrFailure()
        {
            Assert.AreEqual(
                "Normal",
                Classify());

            Assert.AreEqual(
                "Normal",
                Classify(
                    DiagnosticDegradationStatus.Healthy));

            Assert.AreEqual(
                "Normal",
                Classify(
                    DiagnosticDegradationStatus.Unknown));

            Assert.AreEqual(
                "Normal",
                Classify(
                    (DiagnosticDegradationStatus)999));

            Assert.IsNull(
                BrushForState(
                    "Normal"));
        }

        [TestMethod]
        public void
            NodePresentationUsesDeviceDiagnosticsAndKeepsSelectionPrecedence()
        {
            var root =
                FindRepositoryRoot();

            var operational =
                File.ReadAllText(
                    Path.Combine(
                        root,
                        "src",
                        "NetLoom.Wpf",
                        "MainWindow.Map.Operational.cs"));

            var rendering =
                File.ReadAllText(
                    Path.Combine(
                        root,
                        "src",
                        "NetLoom.Wpf",
                        "MainWindow.Map.Rendering.cs"));

            StringAssert.Contains(
                operational,
                "_lastDiagnosticSnapshot.Devices");

            StringAssert.Contains(
                operational,
                "item.DegradationStatus");

            StringAssert.Contains(
                rendering,
                "if (isHighlighted)");

            StringAssert.Contains(
                rendering,
                "ApplyNodeDegradationPresentation");
        }

        private static string Classify(
            params DiagnosticDegradationStatus[] statuses)
        {
            var method =
                typeof(MainWindow).GetMethod(
                    "ClassifyNodeDegradationState",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(method);

            return method.Invoke(
                    null,
                    new object[]
                    {
                        (IEnumerable<DiagnosticDegradationStatus>)
                            statuses
                    })
                .ToString();
        }

        private static string BrushForState(
            string stateName)
        {
            var classifier =
                typeof(MainWindow).GetMethod(
                    "ClassifyNodeDegradationState",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(classifier);

            var state =
                Enum.Parse(
                    classifier.ReturnType,
                    stateName);

            var method =
                typeof(MainWindow).GetMethod(
                    "NodeDegradationBrushKey",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(method);

            return (string)method.Invoke(
                null,
                new[] { state });
        }

        private static string FindRepositoryRoot()
        {
            var directory =
                new DirectoryInfo(
                    AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "NetLoom.sln")))
                {
                    return directory.FullName;
                }

                directory =
                    directory.Parent;
            }

            Assert.Fail(
                "Repository root was not found.");

            return null;
        }
    }
}
