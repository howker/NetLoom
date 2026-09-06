using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Topology;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint15_1bPhysicalLinkIdentityTests
    {
        [TestMethod]
        public void ReverseEndpointsProduceSameCanonicalKey()
        {
            var deviceA = Guid.NewGuid();
            var deviceB = Guid.NewGuid();
            var interfaceA = Guid.NewGuid();
            var interfaceB = Guid.NewGuid();

            var forward =
                PhysicalLinkIdentity.BuildLinkKey(
                    deviceA,
                    interfaceA,
                    deviceB,
                    interfaceB);

            var reverse =
                PhysicalLinkIdentity.BuildLinkKey(
                    deviceB,
                    interfaceB,
                    deviceA,
                    interfaceA);

            Assert.AreEqual(
                forward,
                reverse);
        }

        [TestMethod]
        public void PhysicalLinkCanonicalizesEndpointOrder()
        {
            var deviceA = Guid.Parse(
                "ffffffff-ffff-ffff-ffff-ffffffffffff");

            var deviceB = Guid.Parse(
                "00000000-0000-0000-0000-000000000001");

            var interfaceA = Guid.NewGuid();
            var interfaceB = Guid.NewGuid();

            var link =
                CreateLink(
                    Guid.NewGuid(),
                    deviceA,
                    interfaceA,
                    deviceB,
                    interfaceB);

            Assert.AreEqual(
                deviceB,
                link.DeviceAId);

            Assert.AreEqual(
                interfaceB,
                link.InterfaceAId);

            Assert.AreEqual(
                deviceA,
                link.DeviceBId);

            Assert.AreEqual(
                interfaceA,
                link.InterfaceBId);
        }

        private static PhysicalLink CreateLink(
            Guid id,
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId)
        {
            var now =
                new DateTime(
                    2026,
                    1,
                    1,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc);

            return new PhysicalLink(
                id,
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId,
                PhysicalLinkStrength.Confirmed,
                PhysicalLinkFreshness.Fresh,
                null,
                null,
                "test",
                now,
                now,
                now,
                "test",
                false,
                false,
                null);
        }
    }
}
