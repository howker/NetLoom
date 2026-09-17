using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Topology;
using NetLoom.Domain.Observations;
using NetLoom.Domain.Topology;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint37ManualTopologyServiceTests
    {
        private static readonly DateTime Now =
            new DateTime(
                2026,
                9,
                17,
                10,
                0,
                0,
                DateTimeKind.Utc);

        [TestMethod]
        public void CreateDeviceAndPortUseManualSemanticsAndAuditBothActions()
        {
            var repository =
                new FakeRepository();

            var audit =
                new FakeAuditStore();

            var service =
                CreateService(
                    repository,
                    audit);

            var deviceId =
                service.CreateDevice(
                    "Converter",
                    ManualTopologyDeviceCategory.MediaConverter,
                    "Cabinet");

            var portId =
                service.CreatePort(
                    deviceId,
                    "FX",
                    "Fiber");

            var device =
                repository.GetDevice(
                    deviceId);

            var port =
                repository.FindInterface(
                    portId);

            Assert.AreEqual(
                DeviceDiscoveryOrigin.Manual,
                device.DiscoveryOrigin);

            Assert.AreEqual(
                MonitoringCapability.None,
                device.MonitoringCapability);

            Assert.AreEqual(
                DeviceCategory.MediaConverter,
                device.Category);

            Assert.IsTrue(
                port.IsManual);

            Assert.IsNull(
                port.IfIndex);

            Assert.AreEqual(
                "Fiber",
                port.MediaTypeOverride);

            Assert.AreEqual(
                2,
                audit.Observations.Count);

            foreach (var observation in
                audit.Observations)
            {
                Assert.AreEqual(
                    ObservationKind.Manual,
                    observation.Kind);

                Assert.AreEqual(
                    "User",
                    observation.SourceAddress);

                Assert.AreEqual(
                    Now,
                    observation.CapturedUtc);
            }
        }

        [TestMethod]
        public void CreatePortRejectsAutomaticDeviceWithoutAudit()
        {
            var repository =
                new FakeRepository();

            var automatic =
                CreateAutomaticDevice(
                    Guid.NewGuid(),
                    "Switch");

            repository.SaveDevice(
                automatic);

            var audit =
                new FakeAuditStore();

            var service =
                CreateService(
                    repository,
                    audit);

            AssertThrows<InvalidOperationException>(
                () =>
                    service.CreatePort(
                        automatic.Id,
                        "P1",
                        "Copper"));

            Assert.AreEqual(
                0,
                audit.Observations.Count);
        }

        [TestMethod]
        public void CreateLinkAllowsAutomaticToManualAndUsesManualStrength()
        {
            var repository =
                new FakeRepository();

            var automatic =
                CreateAutomaticDevice(
                    Guid.NewGuid(),
                    "Switch");

            repository.SaveDevice(
                automatic);

            var audit =
                new FakeAuditStore();

            var service =
                CreateService(
                    repository,
                    audit);

            var manualId =
                service.CreateDevice(
                    "Converter",
                    ManualTopologyDeviceCategory.MediaConverter,
                    null);

            var manualPortId =
                service.CreatePort(
                    manualId,
                    "FX",
                    "Fiber");

            var linkId =
                service.CreateLink(
                    automatic.Id,
                    null,
                    manualId,
                    manualPortId,
                    "Fiber",
                    "Manual cable");

            var link =
                repository.FindLink(
                    linkId);

            Assert.AreEqual(
                PhysicalLinkStrength.Manual,
                link.Strength);

            Assert.AreEqual(
                "Fiber",
                link.MediaTypeResolved);

            Assert.AreEqual(
                "Manual cable",
                link.Notes);
        }

        [TestMethod]
        public void ExistingCompatibleCableIsNotOverwrittenByManualCreate()
        {
            var repository =
                new FakeRepository();

            var first =
                CreateAutomaticDevice(
                    Guid.NewGuid(),
                    "First");

            var second =
                CreateAutomaticDevice(
                    Guid.NewGuid(),
                    "Second");

            repository.SaveDevice(
                first);

            repository.SaveDevice(
                second);

            var existing =
                new PhysicalLink(
                    Guid.NewGuid(),
                    first.Id,
                    null,
                    second.Id,
                    null,
                    PhysicalLinkStrength.Confirmed,
                    PhysicalLinkFreshness.Fresh,
                    "Copper",
                    null,
                    "LLDP",
                    Now,
                    Now,
                    Now,
                    null,
                    false,
                    false,
                    null);

            repository.SavePhysicalLink(
                existing);

            var audit =
                new FakeAuditStore();

            var service =
                CreateService(
                    repository,
                    audit);

            AssertThrows<InvalidOperationException>(
                () =>
                    service.CreateLink(
                        first.Id,
                        null,
                        second.Id,
                        null,
                        "Fiber",
                        null));

            Assert.AreEqual(
                PhysicalLinkStrength.Confirmed,
                repository.FindLink(
                    existing.Id).Strength);

            Assert.AreEqual(
                0,
                audit.Observations.Count);
        }

        [TestMethod]
        public void UpdateDeviceAndPortPreserveBackendEvidenceFields()
        {
            var repository =
                new FakeRepository();

            var deviceId =
                Guid.NewGuid();

            repository.SaveDevice(
                new TopologyDevice(
                    deviceId,
                    null,
                    "Old",
                    DeviceCategory.MediaConverter,
                    DeviceDiscoveryOrigin.Manual,
                    MonitoringCapability.None,
                    "Vendor",
                    "Model",
                    "Old note",
                    true,
                    false,
                    Now.AddDays(-2),
                    Now.AddDays(-1),
                    Now.AddDays(-1),
                    "Observed name",
                    "chassis"));

            var portId =
                Guid.NewGuid();

            repository.SaveInterface(
                new DeviceInterface(
                    portId,
                    deviceId,
                    null,
                    null,
                    "Observed descr",
                    null,
                    "Old port",
                    null,
                    null,
                    null,
                    null,
                    "Fiber",
                    "Old media",
                    true,
                    true,
                    Now.AddDays(-2),
                    Now.AddDays(-1),
                    "lldp-port",
                    "lldp-descr"));

            var service =
                CreateService(
                    repository,
                    new FakeAuditStore());

            service.UpdateDevice(
                deviceId,
                "New",
                ManualTopologyDeviceCategory.UnmanagedSwitch,
                "New note");

            service.UpdatePort(
                portId,
                "New port",
                "Copper");

            var device =
                repository.GetDevice(
                    deviceId);

            var port =
                repository.FindInterface(
                    portId);

            Assert.AreEqual(
                "Observed name",
                device.DiscoveredName);

            Assert.AreEqual(
                "chassis",
                device.LldpChassisId);

            Assert.AreEqual(
                "Vendor",
                device.VendorOverride);

            Assert.AreEqual(
                "Model",
                device.ModelOverride);

            Assert.IsTrue(
                device.IsHidden);

            Assert.AreEqual(
                "lldp-port",
                port.LldpPortId);

            Assert.AreEqual(
                "lldp-descr",
                port.LldpPortDescription);

            Assert.AreEqual(
                "Copper",
                port.MediaTypeOverride);

            Assert.IsTrue(
                port.IsHidden);
        }

        [TestMethod]
        public void UpdateManualLinkKeepsIdentityAndEndpoints()
        {
            var repository =
                new FakeRepository();

            var first =
                new ManualTopologyFactory()
                    .CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "First",
                        DeviceCategory.MediaConverter,
                        null);

            var second =
                new ManualTopologyFactory()
                    .CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "Second",
                        DeviceCategory.MediaConverter,
                        null);

            repository.SaveDevice(first);
            repository.SaveDevice(second);

            var link =
                new ManualTopologyFactory()
                    .CreateLink(
                        Guid.NewGuid(),
                        first.Id,
                        null,
                        second.Id,
                        null,
                        "Fiber",
                        "Old",
                        Now.AddHours(-1));

            repository.SavePhysicalLink(
                link);

            var service =
                CreateService(
                    repository,
                    new FakeAuditStore());

            service.UpdateLink(
                link.Id,
                "Copper",
                "New");

            var updated =
                repository.FindLink(
                    link.Id);

            Assert.AreEqual(
                link.Id,
                updated.Id);

            Assert.AreEqual(
                link.DeviceAId,
                updated.DeviceAId);

            Assert.AreEqual(
                link.DeviceBId,
                updated.DeviceBId);

            Assert.AreEqual(
                "Copper",
                updated.MediaTypeResolved);

            Assert.AreEqual(
                "New",
                updated.Notes);
        }

        [TestMethod]
        public void SnapshotMarksOnlyManualElementsEditable()
        {
            var repository =
                new FakeRepository();

            var automatic =
                CreateAutomaticDevice(
                    Guid.NewGuid(),
                    "Automatic");

            var manual =
                new ManualTopologyFactory()
                    .CreateDevice(
                        Guid.NewGuid(),
                        null,
                        "Manual",
                        DeviceCategory.MediaConverter,
                        null);

            repository.SaveDevice(automatic);
            repository.SaveDevice(manual);

            var manualPort =
                new ManualTopologyFactory()
                    .CreateInterface(
                        Guid.NewGuid(),
                        manual.Id,
                        "FX",
                        "Fiber");

            repository.SaveInterface(
                manualPort);

            var automaticPort =
                new DeviceInterface(
                    Guid.NewGuid(),
                    automatic.Id,
                    10,
                    "if10",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Copper",
                    null,
                    false,
                    false,
                    Now,
                    Now);

            repository.SaveInterface(
                automaticPort);

            var manualLink =
                new ManualTopologyFactory()
                    .CreateLink(
                        Guid.NewGuid(),
                        automatic.Id,
                        automaticPort.Id,
                        manual.Id,
                        manualPort.Id,
                        "Fiber",
                        null,
                        Now);

            repository.SavePhysicalLink(
                manualLink);

            var snapshot =
                CreateService(
                    repository,
                    new FakeAuditStore())
                    .GetSnapshot();

            Assert.AreEqual(
                2,
                snapshot.Devices.Count);

            Assert.AreEqual(
                2,
                snapshot.Ports.Count);

            Assert.AreEqual(
                1,
                snapshot.Links.Count);

            Assert.IsFalse(
                snapshot.Devices[0].CanEdit);

            Assert.IsTrue(
                snapshot.Devices[1].CanEdit);

            Assert.IsTrue(
                snapshot.Links[0].CanEdit);
        }

        private static ManualTopologyService
            CreateService(
                FakeRepository repository,
                FakeAuditStore audit)
        {
            return new ManualTopologyService(
                repository,
                audit,
                () => Now);
        }

        private static TopologyDevice
            CreateAutomaticDevice(
                Guid id,
                string name)
        {
            return new TopologyDevice(
                id,
                null,
                name,
                DeviceCategory.Unknown,
                DeviceDiscoveryOrigin.Automatic,
                MonitoringCapability.Unknown,
                null,
                null,
                null,
                false,
                false,
                Now,
                Now,
                Now);
        }

        private static void AssertThrows<T>(
            Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            Assert.Fail(
                "Expected exception: " +
                typeof(T).FullName);
        }

        private sealed class FakeAuditStore :
            IManualTopologyAuditStore
        {
            public readonly List<Observation>
                Observations =
                    new List<Observation>();

            public void Record(
                Observation observation)
            {
                Observations.Add(
                    observation);
            }
        }

        private sealed class FakeRepository :
            IMaterializedTopologyRepository
        {
            private readonly List<TopologyDevice>
                _devices =
                    new List<TopologyDevice>();

            private readonly List<DeviceInterface>
                _interfaces =
                    new List<DeviceInterface>();

            private readonly List<PhysicalLink>
                _links =
                    new List<PhysicalLink>();

            public void SaveDevice(
                TopologyDevice device)
            {
                Replace(
                    _devices,
                    device,
                    item => item.Id);
            }

            public void SaveInterface(
                DeviceInterface networkInterface)
            {
                Replace(
                    _interfaces,
                    networkInterface,
                    item => item.Id);
            }

            public PhysicalLink SavePhysicalLink(
                PhysicalLink link)
            {
                Replace(
                    _links,
                    link,
                    item => item.Id);

                return link;
            }

            public TopologyDevice GetDevice(
                Guid id)
            {
                foreach (var item in _devices)
                {
                    if (item.Id == id)
                    {
                        return item;
                    }
                }

                return null;
            }

            public IReadOnlyList<TopologyDevice>
                GetDevices()
            {
                return _devices.ToArray();
            }

            public IReadOnlyList<DeviceInterface>
                GetInterfaces()
            {
                return _interfaces.ToArray();
            }

            public IReadOnlyList<PhysicalLink>
                GetPhysicalLinks()
            {
                return _links.ToArray();
            }

            public void ReplacePhysicalLinkEvidence(
                Guid physicalLinkId,
                IEnumerable<PhysicalLinkEvidence> evidence)
            {
            }

            public IReadOnlyList<PhysicalLinkEvidence>
                GetPhysicalLinkEvidence()
            {
                return new PhysicalLinkEvidence[0];
            }

            public IReadOnlyList<PhysicalLinkEvidence>
                GetPhysicalLinkEvidence(
                    Guid physicalLinkId)
            {
                return new PhysicalLinkEvidence[0];
            }

            public void DeleteManualPhysicalLink(
                Guid id)
            {
                Remove(
                    _links,
                    id,
                    item => item.Id);
            }

            public void DeleteManualInterface(
                Guid id)
            {
                Remove(
                    _interfaces,
                    id,
                    item => item.Id);
            }

            public void DeleteManualDevice(
                Guid id)
            {
                Remove(
                    _devices,
                    id,
                    item => item.Id);
            }

            public DeviceInterface FindInterface(
                Guid id)
            {
                foreach (var item in _interfaces)
                {
                    if (item.Id == id)
                    {
                        return item;
                    }
                }

                return null;
            }

            public PhysicalLink FindLink(
                Guid id)
            {
                foreach (var item in _links)
                {
                    if (item.Id == id)
                    {
                        return item;
                    }
                }

                return null;
            }

            private static void Replace<T>(
                List<T> items,
                T value,
                Func<T, Guid> id)
            {
                for (var index = 0;
                    index < items.Count;
                    index++)
                {
                    if (id(items[index]) ==
                        id(value))
                    {
                        items[index] = value;
                        return;
                    }
                }

                items.Add(value);
            }

            private static void Remove<T>(
                List<T> items,
                Guid value,
                Func<T, Guid> id)
            {
                for (var index =
                    items.Count - 1;
                    index >= 0;
                    index--)
                {
                    if (id(items[index]) ==
                        value)
                    {
                        items.RemoveAt(index);
                    }
                }
            }
        }
    }
}
