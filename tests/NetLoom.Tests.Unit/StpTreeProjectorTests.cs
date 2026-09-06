using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Contracts.StpTree;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Observations.Stp;
using NetLoom.Domain.Topology;
using NetLoom.Topology.Stp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class StpTreeProjectorTests
    {
        private static readonly DateTime CapturedUtc =
            new DateTime(
                2026,
                1,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void ProjectsCistRootPortAndStableInterfaceBinding()
        {
            var deviceId =
                Guid.NewGuid();

            var otherDeviceId =
                Guid.NewGuid();

            var interface101Id =
                Guid.NewGuid();

            var interface102Id =
                Guid.NewGuid();

            var observation =
                Observation(
                    5,
                    101,
                    new[]
                    {
                        Port(6, 102, 2, 20000),
                        Port(5, 101, 5, 10000)
                    });

            var result =
                new StpTreeProjector().Project(
                    deviceId,
                    new[]
                    {
                        Interface(
                            interface102Id,
                            deviceId,
                            102,
                            "Gi1/0/2"),
                        Interface(
                            Guid.NewGuid(),
                            otherDeviceId,
                            101,
                            "other-device-port"),
                        Interface(
                            interface101Id,
                            deviceId,
                            101,
                            "Gi1/0/1")
                    },
                    observation);

            Assert.AreEqual(
                deviceId,
                result.DeviceId);
            Assert.AreEqual(
                "cist",
                result.InstanceId);
            Assert.AreEqual(
                observation.Observation.Id,
                result.ObservationId);
            Assert.AreEqual(
                interface101Id,
                result.RootInterfaceId);
            Assert.AreEqual(
                2,
                result.Ports.Count);

            var rootPort =
                result.Ports[0];

            Assert.AreEqual(
                5,
                rootPort.BridgePortIndex);
            Assert.AreEqual(
                101,
                rootPort.IfIndex);
            Assert.AreEqual(
                interface101Id,
                rootPort.InterfaceId);
            Assert.AreEqual(
                "Gi1/0/1",
                rootPort.InterfaceLabel);
            Assert.AreEqual(
                StpTreePortState.Forwarding,
                rootPort.State);
            Assert.IsTrue(
                rootPort.IsRootPort);

            var blockedPort =
                result.Ports[1];

            Assert.AreEqual(
                6,
                blockedPort.BridgePortIndex);
            Assert.AreEqual(
                102,
                blockedPort.IfIndex);
            Assert.AreEqual(
                interface102Id,
                blockedPort.InterfaceId);
            Assert.AreEqual(
                StpTreePortState.Blocking,
                blockedPort.State);
            Assert.IsFalse(
                blockedPort.IsRootPort);
        }

        [TestMethod]
        public void DoesNotBindIfIndexAcrossDevicesOrWhenAmbiguous()
        {
            var deviceId =
                Guid.NewGuid();

            var observation =
                Observation(
                    7,
                    201,
                    new[]
                    {
                        Port(7, 201, 5, 100)
                    });

            var crossDevice =
                new StpTreeProjector().Project(
                    deviceId,
                    new[]
                    {
                        Interface(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            201,
                            "foreign-port")
                    },
                    observation);

            Assert.IsNull(
                crossDevice.RootInterfaceId);
            Assert.IsNull(
                crossDevice.Ports[0].InterfaceId);

            var ambiguous =
                new StpTreeProjector().Project(
                    deviceId,
                    new[]
                    {
                        Interface(
                            Guid.NewGuid(),
                            deviceId,
                            201,
                            "first"),
                        Interface(
                            Guid.NewGuid(),
                            deviceId,
                            201,
                            "second")
                    },
                    observation);

            Assert.IsNull(
                ambiguous.RootInterfaceId);
            Assert.IsNull(
                ambiguous.Ports[0].InterfaceId);
        }

        [TestMethod]
        public void ProjectionIsDeterministicAndPreservesUnresolvedPort()
        {
            var deviceId =
                Guid.NewGuid();

            var observation =
                Observation(
                    0,
                    null,
                    new[]
                    {
                        Port(9, null, null, null),
                        Port(3, null, 4, 30)
                    });

            var first =
                new StpTreeProjector().Project(
                    deviceId,
                    new DeviceInterface[0],
                    observation);

            var second =
                new StpTreeProjector().Project(
                    deviceId,
                    new DeviceInterface[0],
                    observation);

            CollectionAssert.AreEqual(
                new[] { 3, 9 },
                first.Ports
                    .Select(
                        item => item.BridgePortIndex)
                    .ToArray());

            CollectionAssert.AreEqual(
                first.Ports
                    .Select(
                        item => item.BridgePortIndex)
                    .ToArray(),
                second.Ports
                    .Select(
                        item => item.BridgePortIndex)
                    .ToArray());

            Assert.IsNull(
                first.Ports[1].IfIndex);
            Assert.IsNull(
                first.Ports[1].InterfaceId);
            Assert.AreEqual(
                StpTreePortState.Unknown,
                first.Ports[1].State);
        }

        private static StpObservation Observation(
            int? rootBridgePort,
            int? rootIfIndex,
            StpPortState[] ports)
        {
            return new StpObservation(
                new Observation(
                    Guid.NewGuid(),
                    ObservationKind.Stp,
                    "192.0.2.50",
                    CapturedUtc),
                "cist",
                2,
                "8000.001122334455",
                0,
                rootBridgePort,
                rootIfIndex,
                ports);
        }

        private static StpPortState Port(
            int bridgePortIndex,
            int? ifIndex,
            int? state,
            long? pathCost)
        {
            return new StpPortState(
                bridgePortIndex,
                ifIndex,
                128,
                state,
                1,
                pathCost,
                "8000.001122334455",
                0,
                "8000.00AABBCCDDEE",
                "128.1",
                0);
        }

        private static DeviceInterface Interface(
            Guid id,
            Guid deviceId,
            int ifIndex,
            string ifName)
        {
            var constructor =
                typeof(DeviceInterface)
                    .GetConstructors()
                    .Single();

            var parameters =
                constructor.GetParameters();

            var arguments =
                new object[parameters.Length];

            for (var index = 0;
                 index < parameters.Length;
                 index++)
            {
                var parameter =
                    parameters[index];

                switch (parameter.Name)
                {
                    case "id":
                        arguments[index] = id;
                        break;

                    case "deviceId":
                        arguments[index] = deviceId;
                        break;

                    case "ifIndex":
                        arguments[index] = ifIndex;
                        break;

                    case "ifName":
                        arguments[index] = ifName;
                        break;

                    case "firstSeenUtc":
                    case "lastSeenUtc":
                        arguments[index] = CapturedUtc;
                        break;

                    default:
                        arguments[index] =
                            DefaultArgument(parameter);
                        break;
                }
            }

            return
                (DeviceInterface)
                    constructor.Invoke(arguments);
        }

        private static object DefaultArgument(
            ParameterInfo parameter)
        {
            if (parameter.HasDefaultValue)
            {
                return parameter.DefaultValue;
            }

            var type =
                parameter.ParameterType;

            if (!type.IsValueType ||
                Nullable.GetUnderlyingType(type) != null)
            {
                return null;
            }

            if (type == typeof(bool))
            {
                return false;
            }

            if (type == typeof(DateTime))
            {
                return CapturedUtc;
            }

            if (type == typeof(Guid))
            {
                return Guid.NewGuid();
            }

            return Activator.CreateInstance(type);
        }
    }
}
