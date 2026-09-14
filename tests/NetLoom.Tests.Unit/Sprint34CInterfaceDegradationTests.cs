using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint34CInterfaceDegradationTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "44444444-5555-6666-7777-888888888888");

        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 8, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void NoBaselineIsIndeterminateAndNeverDegraded()
        {
            var result =
                Classify(
                    Evaluation(
                        null,
                        Snapshot(
                            T1,
                            10,
                            20,
                            30,
                            40,
                            500)),
                    1.0,
                    1.0);

            Assert.AreEqual(
                "Indeterminate",
                Property(
                    result,
                    "Status").ToString());

            CollectionAssert.AreEqual(
                new[]
                {
                    "NoBaseline"
                },
                Reasons(result));
        }

        [TestMethod]
        public void DiscontinuityIsIndeterminateAndNeverDegraded()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            4000,
                            5000,
                            6000,
                            7000,
                            500),
                        Snapshot(
                            T1.AddMinutes(1),
                            2,
                            3,
                            4,
                            5,
                            800)),
                    1.0,
                    1.0);

            Assert.AreEqual(
                "Indeterminate",
                Property(
                    result,
                    "Status").ToString());

            CollectionAssert.AreEqual(
                new[]
                {
                    "CounterDiscontinuity"
                },
                Reasons(result));
        }

        [TestMethod]
        public void ValidRatesBelowThresholdsAreHealthy()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            10,
                            20,
                            30,
                            40,
                            500),
                        Snapshot(
                            T1.AddMinutes(1),
                            12,
                            23,
                            31,
                            41,
                            500)),
                    6.0,
                    3.0);

            Assert.AreEqual(
                "Healthy",
                Property(
                    result,
                    "Status").ToString());

            Assert.AreEqual(
                5.0,
                NullableDouble(
                    result,
                    "ErrorRatePerMinute"),
                0.000001);

            Assert.AreEqual(
                2.0,
                NullableDouble(
                    result,
                    "DiscardRatePerMinute"),
                0.000001);

            Assert.AreEqual(
                0,
                Reasons(result).Length);
        }

        [TestMethod]
        public void ErrorRateAtThresholdIsDegraded()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            10,
                            20,
                            30,
                            40,
                            500),
                        Snapshot(
                            T1.AddMinutes(1),
                            12,
                            23,
                            30,
                            40,
                            500)),
                    5.0,
                    null);

            Assert.AreEqual(
                "Degraded",
                Property(
                    result,
                    "Status").ToString());

            CollectionAssert.Contains(
                Reasons(result),
                "ErrorRateThresholdExceeded");
        }

        [TestMethod]
        public void DiscardRateAtThresholdIsDegraded()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            10,
                            20,
                            30,
                            40,
                            500),
                        Snapshot(
                            T1.AddMinutes(1),
                            10,
                            20,
                            31,
                            41,
                            500)),
                    null,
                    2.0);

            Assert.AreEqual(
                "Degraded",
                Property(
                    result,
                    "Status").ToString());

            CollectionAssert.Contains(
                Reasons(result),
                "DiscardRateThresholdExceeded");
        }

        [TestMethod]
        public void RateIsNormalizedPerMinute()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            10,
                            20,
                            30,
                            40,
                            500),
                        Snapshot(
                            T1.AddSeconds(30),
                            12,
                            23,
                            30,
                            40,
                            500)),
                    11.0,
                    null);

            Assert.AreEqual(
                "Healthy",
                Property(
                    result,
                    "Status").ToString());

            Assert.AreEqual(
                10.0,
                NullableDouble(
                    result,
                    "ErrorRatePerMinute"),
                0.000001);
        }

        [TestMethod]
        public void MissingEnabledCountersAreIndeterminate()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            10,
                            null,
                            30,
                            40,
                            500),
                        Snapshot(
                            T1.AddMinutes(1),
                            11,
                            null,
                            30,
                            40,
                            500)),
                    1.0,
                    null);

            Assert.AreEqual(
                "Indeterminate",
                Property(
                    result,
                    "Status").ToString());

            CollectionAssert.Contains(
                Reasons(result),
                "IncompleteCounterData");
        }

        [TestMethod]
        public void DisabledCategoryDoesNotBlockHealthyClassification()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            10,
                            20,
                            null,
                            null,
                            500),
                        Snapshot(
                            T1.AddMinutes(1),
                            11,
                            21,
                            null,
                            null,
                            500)),
                    3.0,
                    null);

            Assert.AreEqual(
                "Healthy",
                Property(
                    result,
                    "Status").ToString());

            Assert.IsNull(
                Property(
                    result,
                    "DiscardRatePerMinute"));
        }

        [TestMethod]
        public void KnownThresholdBreachWinsOverOtherIncompleteEvidence()
        {
            var result =
                Classify(
                    Evaluation(
                        Snapshot(
                            T1,
                            10,
                            20,
                            30,
                            null,
                            500),
                        Snapshot(
                            T1.AddMinutes(1),
                            15,
                            25,
                            31,
                            null,
                            500)),
                    5.0,
                    1.0);

            Assert.AreEqual(
                "Degraded",
                Property(
                    result,
                    "Status").ToString());

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "IncompleteCounterData",
                    "ErrorRateThresholdExceeded"
                },
                Reasons(result));
        }

        [TestMethod]
        public void PolicyRequiresFinitePositiveConfiguredThreshold()
        {
            var policyType =
                RequireType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationPolicy");

            AssertConstructorFailure(
                policyType,
                null,
                null,
                typeof(ArgumentException));

            AssertConstructorFailure(
                policyType,
                0.0,
                null,
                typeof(ArgumentOutOfRangeException));

            AssertConstructorFailure(
                policyType,
                double.PositiveInfinity,
                null,
                typeof(ArgumentOutOfRangeException));
        }

        private static InterfaceCounterEvaluation Evaluation(
            InterfaceMonitoringSnapshot previous,
            InterfaceMonitoringSnapshot current)
        {
            return new InterfaceCounterEvaluation(
                current,
                new InterfaceCounterDeltaEvaluator()
                    .Evaluate(
                        previous,
                        current));
        }

        private static InterfaceMonitoringSnapshot Snapshot(
            DateTime capturedUtc,
            uint? inErrors,
            uint? outErrors,
            uint? inDiscards,
            uint? outDiscards,
            uint? discontinuity)
        {
            return new InterfaceMonitoringSnapshot(
                DeviceId,
                7,
                1,
                1,
                capturedUtc,
                inErrors,
                outErrors,
                inDiscards,
                outDiscards,
                discontinuity);
        }

        private static object Classify(
            InterfaceCounterEvaluation evaluation,
            double? errorThreshold,
            double? discardThreshold)
        {
            var policyType =
                RequireType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationPolicy");

            var classifierType =
                RequireType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationClassifier");

            var policy =
                Activator.CreateInstance(
                    policyType,
                    new object[]
                    {
                        errorThreshold,
                        discardThreshold
                    });

            var classifier =
                Activator.CreateInstance(
                    classifierType);

            var method =
                classifierType.GetMethod(
                    "Classify",
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.IsNotNull(
                method);

            return method.Invoke(
                classifier,
                new[]
                {
                    (object)evaluation,
                    policy
                });
        }

        private static Type RequireType(
            string fullName)
        {
            var type =
                typeof(InterfaceCounterEvaluation)
                    .Assembly
                    .GetType(
                        fullName);

            Assert.IsNotNull(
                type,
                "Missing Sprint 34C type: " +
                fullName);

            return type;
        }

        private static object Property(
            object instance,
            string propertyName)
        {
            Assert.IsNotNull(
                instance);

            var property =
                instance.GetType()
                    .GetProperty(
                        propertyName,
                        BindingFlags.Public |
                        BindingFlags.Instance);

            Assert.IsNotNull(
                property,
                "Missing property: " +
                propertyName);

            return property.GetValue(
                instance);
        }

        private static double NullableDouble(
            object instance,
            string propertyName)
        {
            var value =
                Property(
                    instance,
                    propertyName);

            Assert.IsNotNull(
                value);

            return (double)value;
        }

        private static string[] Reasons(
            object instance)
        {
            var enumerable =
                Property(
                    instance,
                    "Reasons") as IEnumerable;

            Assert.IsNotNull(
                enumerable);

            var values =
                new List<string>();

            foreach (var value in enumerable)
            {
                values.Add(
                    value.ToString());
            }

            return values.ToArray();
        }

        private static void AssertConstructorFailure(
            Type type,
            double? errorThreshold,
            double? discardThreshold,
            Type expectedExceptionType)
        {
            try
            {
                Activator.CreateInstance(
                    type,
                    new object[]
                    {
                        errorThreshold,
                        discardThreshold
                    });

                Assert.Fail(
                    "Expected constructor failure.");
            }
            catch (TargetInvocationException exception)
            {
                Assert.IsInstanceOfType(
                    exception.InnerException,
                    expectedExceptionType);
            }
        }
    }
}
