using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Protocols.Snmp.Interfaces;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint34AInterfaceCounterDeltaTests
    {
        private static readonly Guid DeviceId =
            new Guid(
                "11111111-2222-3333-4444-555555555555");

        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 5, 12, 0, 0,
                DateTimeKind.Utc);

        private static readonly DateTime T2 =
            T1.AddSeconds(30);

        [TestMethod]
        public void CollectorCapturesErrorDiscardCountersAndDiscontinuity()
        {
            var collector =
                new SnmpInterfaceStatusCollector(
                    new CounterTransport(),
                    () => T2);

            var snapshots =
                collector.Collect(
                    Request());

            Assert.AreEqual(
                1,
                snapshots.Count);

            var snapshot =
                snapshots[0];

            Assert.AreEqual(
                (uint)20,
                Property<uint?>(
                    snapshot,
                    "InErrors"));

            Assert.AreEqual(
                (uint)40,
                Property<uint?>(
                    snapshot,
                    "OutErrors"));

            Assert.AreEqual(
                (uint)10,
                Property<uint?>(
                    snapshot,
                    "InDiscards"));

            Assert.AreEqual(
                (uint)30,
                Property<uint?>(
                    snapshot,
                    "OutDiscards"));

            Assert.AreEqual(
                (uint)500,
                Property<uint?>(
                    snapshot,
                    "CounterDiscontinuityTimeTicks"));
        }

        [TestMethod]
        public void FirstSampleDoesNotInventDelta()
        {
            var result =
                Evaluate(
                    null,
                    Snapshot(
                        T2,
                        10,
                        20,
                        30,
                        40,
                        500));

            Assert.AreEqual(
                "NoBaseline",
                Status(result));

            Assert.IsNull(
                Property<ulong?>(
                    result,
                    "InErrors"));
        }

        [TestMethod]
        public void StableMarkerComputesCounterDeltas()
        {
            var result =
                Evaluate(
                    Snapshot(
                        T1,
                        10,
                        20,
                        30,
                        40,
                        500),
                    Snapshot(
                        T2,
                        13,
                        27,
                        35,
                        49,
                        500));

            Assert.AreEqual(
                "Valid",
                Status(result));

            Assert.AreEqual(
                (ulong)3,
                Property<ulong?>(
                    result,
                    "InErrors"));

            Assert.AreEqual(
                (ulong)7,
                Property<ulong?>(
                    result,
                    "OutErrors"));

            Assert.AreEqual(
                (ulong)5,
                Property<ulong?>(
                    result,
                    "InDiscards"));

            Assert.AreEqual(
                (ulong)9,
                Property<ulong?>(
                    result,
                    "OutDiscards"));
        }

        [TestMethod]
        public void StableMarkerTreatsDecreaseAsCounter32Wrap()
        {
            var result =
                Evaluate(
                    Snapshot(
                        T1,
                        uint.MaxValue - 2,
                        null,
                        null,
                        null,
                        500),
                    Snapshot(
                        T2,
                        4,
                        null,
                        null,
                        null,
                        500));

            Assert.AreEqual(
                "Valid",
                Status(result));

            Assert.AreEqual(
                (ulong)7,
                Property<ulong?>(
                    result,
                    "InErrors"));
        }

        [TestMethod]
        public void ChangedMarkerRejectsCounterReset()
        {
            var result =
                Evaluate(
                    Snapshot(
                        T1,
                        4000,
                        5000,
                        6000,
                        7000,
                        500),
                    Snapshot(
                        T2,
                        2,
                        3,
                        4,
                        5,
                        800));

            Assert.AreEqual(
                "Discontinuity",
                Status(result));

            Assert.IsNull(
                Property<ulong?>(
                    result,
                    "InErrors"));

            Assert.IsNull(
                Property<ulong?>(
                    result,
                    "OutErrors"));
        }

        [TestMethod]
        public void MissingMarkerDoesNotInventDelta()
        {
            var result =
                Evaluate(
                    Snapshot(
                        T1,
                        10,
                        20,
                        30,
                        40,
                        null),
                    Snapshot(
                        T2,
                        11,
                        21,
                        31,
                        41,
                        null));

            Assert.AreEqual(
                "NoBaseline",
                Status(result));

            Assert.IsNull(
                Property<ulong?>(
                    result,
                    "InDiscards"));
        }

        private static InterfaceCollectionRequest Request()
        {
            return new InterfaceCollectionRequest(
                DeviceId,
                IPAddress.Parse(
                    "192.0.2.70"),
                161,
                SnmpVersion.V2C,
                new SnmpCommunityCredentials(
                    new byte[]
                    {
                        1,
                        2,
                        3
                    }),
                1000,
                1,
                10);
        }

        private static InterfaceMonitoringSnapshot Snapshot(
            DateTime capturedUtc,
            uint? inErrors,
            uint? outErrors,
            uint? inDiscards,
            uint? outDiscards,
            uint? discontinuity)
        {
            var constructor =
                typeof(InterfaceMonitoringSnapshot)
                    .GetConstructor(
                        new[]
                        {
                            typeof(Guid?),
                            typeof(int),
                            typeof(int?),
                            typeof(int?),
                            typeof(DateTime),
                            typeof(uint?),
                            typeof(uint?),
                            typeof(uint?),
                            typeof(uint?),
                            typeof(uint?)
                        });

            Assert.IsNotNull(
                constructor,
                "Sprint 34A extended interface snapshot constructor is missing.");

            return
                (InterfaceMonitoringSnapshot)
                    constructor.Invoke(
                        new object[]
                        {
                            DeviceId,
                            7,
                            1,
                            1,
                            capturedUtc,
                            inErrors,
                            outErrors,
                            inDiscards,
                            outDiscards,
                            discontinuity
                        });
        }

        private static object Evaluate(
            InterfaceMonitoringSnapshot previous,
            InterfaceMonitoringSnapshot current)
        {
            var evaluatorType =
                typeof(InterfaceMonitoringSnapshot)
                    .Assembly
                    .GetType(
                        "NetLoom.Application.Monitoring.Interfaces.InterfaceCounterDeltaEvaluator");

            Assert.IsNotNull(
                evaluatorType,
                "Sprint 34A counter delta evaluator is missing.");

            var evaluator =
                Activator.CreateInstance(
                    evaluatorType);

            var method =
                evaluatorType.GetMethod(
                    "Evaluate",
                    BindingFlags.Instance |
                    BindingFlags.Public);

            Assert.IsNotNull(
                method,
                "Sprint 34A counter delta evaluator method is missing.");

            return method.Invoke(
                evaluator,
                new object[]
                {
                    previous,
                    current
                });
        }

        private static string Status(
            object result)
        {
            var status =
                result.GetType()
                    .GetProperty(
                        "Status")
                    .GetValue(
                        result,
                        null);

            return status.ToString();
        }

        private static T Property<T>(
            object value,
            string name)
        {
            var property =
                value.GetType()
                    .GetProperty(
                        name,
                        BindingFlags.Instance |
                        BindingFlags.Public);

            Assert.IsNotNull(
                property,
                "Expected property is missing: " +
                name);

            var propertyValue =
                property.GetValue(
                    value,
                    null);

            if (propertyValue == null)
            {
                return default(T);
            }

            return (T)propertyValue;
        }

        private sealed class CounterTransport :
            ISnmpTransport
        {
            public IReadOnlyList<SnmpVariable> Get(
                SnmpGetRequest request)
            {
                return new SnmpVariable[0];
            }

            public IReadOnlyList<SnmpVariable> Walk(
                SnmpWalkRequest request)
            {
                switch (request.RootOid)
                {
                    case "1.3.6.1.2.1.2.2.1.7":
                        return One(
                            request.RootOid,
                            2,
                            "1");

                    case "1.3.6.1.2.1.2.2.1.8":
                        return One(
                            request.RootOid,
                            2,
                            "1");

                    case "1.3.6.1.2.1.2.2.1.13":
                        return One(
                            request.RootOid,
                            65,
                            "10");

                    case "1.3.6.1.2.1.2.2.1.14":
                        return One(
                            request.RootOid,
                            65,
                            "20");

                    case "1.3.6.1.2.1.2.2.1.19":
                        return One(
                            request.RootOid,
                            65,
                            "30");

                    case "1.3.6.1.2.1.2.2.1.20":
                        return One(
                            request.RootOid,
                            65,
                            "40");

                    case "1.3.6.1.2.1.31.1.1.1.19":
                        return One(
                            request.RootOid,
                            67,
                            "(500) 0:00:05.00");

                    default:
                        return new SnmpVariable[0];
                }
            }

            private static IReadOnlyList<SnmpVariable> One(
                string rootOid,
                int typeCode,
                string displayValue)
            {
                return new[]
                {
                    new SnmpVariable(
                        rootOid + ".7",
                        typeCode,
                        displayValue,
                        new byte[0])
                };
            }
        }
    }
}
