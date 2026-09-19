using System;
using System.Collections.Generic;
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
            NodeStripeSeparatesProvenHealthyFromUnknownEvidence()
        {
            Assert.AreEqual(
                "Unknown",
                Classify());

            Assert.AreEqual(
                "Normal",
                Classify(
                    DiagnosticDegradationStatus.Healthy));

            Assert.AreEqual(
                "Unknown",
                Classify(
                    DiagnosticDegradationStatus.Unknown));

            Assert.AreEqual(
                "Unknown",
                Classify(
                    DiagnosticDegradationStatus.Healthy,
                    DiagnosticDegradationStatus.Unknown));

            Assert.AreEqual(
                "Unknown",
                Classify(
                    (DiagnosticDegradationStatus)999));

            Assert.AreEqual(
                "NetLoom.Brush.Success",
                BrushForState(
                    "Normal"));

            Assert.AreEqual(
                "NetLoom.Brush.TextDisabled",
                BrushForState(
                    "Unknown"));
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

    }
}
