using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.StpTree;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint39OperationalLinkVisualLanguageTests
    {
        [TestMethod]
        public void StpStateUsesStableOperationalPrecedence()
        {
            Assert.AreEqual(
                "Critical",
                StpState(
                    StpTreePortState.Broken,
                    StpTreePortState.Blocking));

            Assert.AreEqual(
                "Degraded",
                StpState(
                    StpTreePortState.Disabled,
                    StpTreePortState.Forwarding));

            Assert.AreEqual(
                "Blocked",
                StpState(
                    StpTreePortState.Blocking,
                    StpTreePortState.Forwarding));

            Assert.AreEqual(
                "Transition",
                StpState(
                    StpTreePortState.Listening,
                    StpTreePortState.Learning));

            Assert.AreEqual(
                "Forwarding",
                StpState(
                    StpTreePortState.Forwarding,
                    StpTreePortState.Unknown));
        }

        [TestMethod]
        public void AlertClassificationUsesExistingRiskSemantics()
        {
            Assert.AreEqual(
                "Critical",
                AlertState(
                    TopologyAlertKind.ForwardingCycle));

            Assert.AreEqual(
                "Degraded",
                AlertState(
                    TopologyAlertKind.RingProtectionDegraded));

            Assert.AreEqual(
                "Normal",
                AlertState(
                    (TopologyAlertKind)999));
        }

        [TestMethod]
        public void OperationalStatesUseExistingSemanticBrushes()
        {
            Assert.AreEqual(
                "NetLoom.Brush.Critical",
                BrushForState("Critical"));

            Assert.AreEqual(
                "NetLoom.Brush.Warning",
                BrushForState("Degraded"));

            Assert.AreEqual(
                "NetLoom.Brush.AccentHover",
                BrushForState("Transition"));

            Assert.AreEqual(
                "NetLoom.Brush.AccentPressed",
                BrushForState("Blocked"));

            Assert.AreEqual(
                "NetLoom.Brush.Success",
                BrushForState("Forwarding"));

            Assert.IsNull(
                BrushForState("Normal"));
        }

        private static string StpState(
            StpTreePortState stateA,
            StpTreePortState stateB)
        {
            return InvokeState(
                    "ClassifyStpOperationalState",
                    stateA,
                    stateB)
                .ToString();
        }

        private static string AlertState(
            TopologyAlertKind kind)
        {
            return InvokeState(
                    "ClassifyAlertOperationalState",
                    kind)
                .ToString();
        }

        private static string BrushForState(
            string stateName)
        {
            var classifier =
                typeof(MainWindow).GetMethod(
                    "ClassifyAlertOperationalState",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(classifier);

            var stateType =
                classifier.ReturnType;

            var state =
                Enum.Parse(
                    stateType,
                    stateName);

            var method =
                typeof(MainWindow).GetMethod(
                    "LinkOperationalBrushKey",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(method);

            return (string)method.Invoke(
                null,
                new[] { state });
        }

        private static object InvokeState(
            string methodName,
            params object[] arguments)
        {
            var method =
                typeof(MainWindow).GetMethod(
                    methodName,
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

            Assert.IsNotNull(
                method,
                "Expected private operational visual-language helper: " +
                methodName);

            return method.Invoke(
                null,
                arguments);
        }
    }
}
