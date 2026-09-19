using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint39OperatorVisualRemediationTests
    {
        private static readonly string[] FocusResourceKeys =
        {
            "MapOperationalFocusSettings",
            "MapOperationalFocusAllProblems",
            "MapOperationalFocusCriticalLinks",
            "MapOperationalFocusDegradedLinks",
            "MapOperationalFocusBlockedLinks",
            "MapOperationalFocusTransitionLinks",
            "MapOperationalFocusDegradedNodes",
            "MapOperationalFocusClear"
        };

        [TestMethod]
        public void OperationalStatesUseCompactMonotonicStrokeScales()
        {
            Assert.AreEqual(1.0, StrokeScale("Normal"));
            Assert.AreEqual(1.0, StrokeScale("Forwarding"));
            Assert.AreEqual(1.10, StrokeScale("Transition"));
            Assert.AreEqual(1.20, StrokeScale("Blocked"));
            Assert.AreEqual(1.35, StrokeScale("Degraded"));
            Assert.AreEqual(1.55, StrokeScale("Critical"));

            Assert.IsTrue(
                StrokeScale("Critical") < 2.0,
                "Operational lines must remain readable without dominating the map.");
        }

        [TestMethod]
        public void OperationalStatesUseDistinctExistingSemanticBrushes()
        {
            Assert.IsNull(BrushKey("Normal"));
            Assert.AreEqual("NetLoom.Brush.Success", BrushKey("Forwarding"));
            Assert.AreEqual("NetLoom.Brush.AccentHover", BrushKey("Transition"));
            Assert.AreEqual("NetLoom.Brush.AccentPressed", BrushKey("Blocked"));
            Assert.AreEqual("NetLoom.Brush.Warning", BrushKey("Degraded"));
            Assert.AreEqual("NetLoom.Brush.Critical", BrushKey("Critical"));
        }

        [TestMethod]
        public void OperationalFocusSeparatesLinkAndNodeProblemClasses()
        {
            Assert.IsTrue(FocusMatchesLink("AllProblems", "Critical"));
            Assert.IsTrue(FocusMatchesLink("AllProblems", "Degraded"));
            Assert.IsTrue(FocusMatchesLink("AllProblems", "Blocked"));
            Assert.IsTrue(FocusMatchesLink("AllProblems", "Transition"));
            Assert.IsFalse(FocusMatchesLink("AllProblems", "Forwarding"));
            Assert.IsFalse(FocusMatchesLink("AllProblems", "Normal"));

            Assert.IsTrue(FocusMatchesLink("CriticalLinks", "Critical"));
            Assert.IsFalse(FocusMatchesLink("CriticalLinks", "Degraded"));

            Assert.IsTrue(FocusMatchesLink("DegradedLinks", "Degraded"));
            Assert.IsFalse(FocusMatchesLink("DegradedLinks", "Critical"));

            Assert.IsTrue(FocusMatchesLink("BlockedLinks", "Blocked"));
            Assert.IsTrue(FocusMatchesLink("TransitionLinks", "Transition"));

            Assert.IsTrue(FocusMatchesNode("AllProblems", "Degraded"));
            Assert.IsTrue(FocusMatchesNode("DegradedNodes", "Degraded"));
            Assert.IsFalse(FocusMatchesNode("DegradedNodes", "Normal"));
            Assert.IsFalse(FocusMatchesLink("DegradedNodes", "Degraded"));
        }

        [TestMethod]
        public void OperationalFocusResourceIntegrityUsesNeutralAndRussianResources()
        {
            AssertResourceKeys(
                FindRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Resources",
                        "UiStrings.resx")));

            AssertResourceKeys(
                FindRepositoryFile(
                    Path.Combine(
                        "src",
                        "NetLoom.Wpf",
                        "Resources",
                        "UiStrings.ru.resx")));
        }

        private static double StrokeScale(
            string stateName)
        {
            var method =
                typeof(MainWindow).GetMethod(
                    "LinkOperationalStrokeScale",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(method);

            var state =
                Enum.Parse(
                    method.GetParameters()[0].ParameterType,
                    stateName);

            return (double)method.Invoke(
                null,
                new[] { state });
        }

        private static string BrushKey(
            string stateName)
        {
            var method =
                typeof(MainWindow).GetMethod(
                    "LinkOperationalBrushKey",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(method);

            var state =
                Enum.Parse(
                    method.GetParameters()[0].ParameterType,
                    stateName);

            return (string)method.Invoke(
                null,
                new[] { state });
        }

        private static bool FocusMatchesLink(
            string modeName,
            string stateName)
        {
            var method =
                typeof(MainWindow).GetMethod(
                    "OperationalFocusMatchesLink",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(method);

            var parameters =
                method.GetParameters();

            var mode =
                Enum.Parse(
                    parameters[0].ParameterType,
                    modeName);

            var state =
                Enum.Parse(
                    parameters[1].ParameterType,
                    stateName);

            return (bool)method.Invoke(
                null,
                new[] { mode, state });
        }

        private static bool FocusMatchesNode(
            string modeName,
            string stateName)
        {
            var method =
                typeof(MainWindow).GetMethod(
                    "OperationalFocusMatchesNode",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(method);

            var parameters =
                method.GetParameters();

            var mode =
                Enum.Parse(
                    parameters[0].ParameterType,
                    modeName);

            var state =
                Enum.Parse(
                    parameters[1].ParameterType,
                    stateName);

            return (bool)method.Invoke(
                null,
                new[] { mode, state });
        }

        private static void AssertResourceKeys(
            string path)
        {
            var document =
                XDocument.Load(
                    path,
                    LoadOptions.PreserveWhitespace);

            var keys =
                new HashSet<string>(
                    document
                        .Descendants("data")
                        .Select(
                            item =>
                                (string)item.Attribute("name"))
                        .Where(
                            item =>
                                !string.IsNullOrWhiteSpace(item)),
                    StringComparer.Ordinal);

            foreach (var resourceKey in
                FocusResourceKeys)
            {
                Assert.IsTrue(
                    keys.Contains(resourceKey),
                    "Missing resource key: " +
                    resourceKey +
                    " in " +
                    path);
            }
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

            Assert.Fail(
                "Repository file not found: " +
                relativePath);

            return null;
        }
    }
}
