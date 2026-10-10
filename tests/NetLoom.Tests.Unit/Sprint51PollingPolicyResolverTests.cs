using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.PollingPolicies;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint51PollingPolicyResolverTests
    {
        private static PollingPolicy Policy(string name) => new PollingPolicy(Guid.NewGuid(), name, false, true,
            PollingSchedule.General, PollingSchedule.Once, new[] { 443 });

        [TestMethod]
        public void DefaultAndOwnDevicePolicy()
        {
            var device = Guid.NewGuid();
            var location = Guid.NewGuid();
            var devicePolicy = Policy("Device");
            var locationPolicy = Policy("Location");
            var parents = new Dictionary<Guid, Guid?> { [location] = null };
            var devices = new Dictionary<Guid, Guid?> { [device] = location };
            var empty = new PollingPolicyResolver(new PollingPolicy[0], new PollingPolicyAssignment[0], parents, devices);
            Assert.AreEqual(PollingPolicySource.Default, empty.ResolveDevice(device).Source);
            Assert.AreEqual(PollingPolicy.DefaultPolicyId, empty.Default.Id);
            Assert.AreEqual(PollingPolicySource.Default, empty.ResolveLocation(Guid.NewGuid()).Source);
            var resolver = new PollingPolicyResolver(new[] { devicePolicy, locationPolicy }, new[]
            {
                new PollingPolicyAssignment(PollingPolicySubjectKind.Device, device, devicePolicy.Id),
                new PollingPolicyAssignment(PollingPolicySubjectKind.Location, location, locationPolicy.Id)
            }, parents, devices);
            Assert.AreEqual(devicePolicy.Id, resolver.ResolveDevice(device).Policy.Id);
            Assert.AreEqual(PollingPolicySource.Device, resolver.ResolveDevice(device).Source);
            Assert.IsNull(resolver.ResolveDevice(device).SourceLocationId);
            Assert.AreEqual(locationPolicy.Id, resolver.ResolveLocation(location).Policy.Id);
            Assert.AreEqual(location, resolver.ResolveLocation(location).SourceLocationId);
        }

        [TestMethod]
        public void ClosestAssignedAncestorWinsAcrossTwoParents()
        {
            var root = Guid.NewGuid();
            var middle = Guid.NewGuid();
            var leaf = Guid.NewGuid();
            var device = Guid.NewGuid();
            var policy = Policy("Root");
            var resolver = new PollingPolicyResolver(new[] { policy }, new[]
            {
                new PollingPolicyAssignment(PollingPolicySubjectKind.Location, root, policy.Id)
            }, new Dictionary<Guid, Guid?> { [root] = null, [middle] = root, [leaf] = middle },
                new Dictionary<Guid, Guid?> { [device] = leaf });
            var effective = resolver.ResolveDevice(device);
            Assert.AreEqual(policy.Id, effective.Policy.Id);
            Assert.AreEqual(PollingPolicySource.Location, effective.Source);
            Assert.AreEqual(root, effective.SourceLocationId);
        }

        [TestMethod]
        public void MissingPolicyCycleAndDeviceWithoutLocationFallBack()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var device = Guid.NewGuid();
            var unplaced = Guid.NewGuid();
            var resolver = new PollingPolicyResolver(new PollingPolicy[0], new[]
            {
                new PollingPolicyAssignment(PollingPolicySubjectKind.Location, first, Guid.NewGuid())
            }, new Dictionary<Guid, Guid?> { [first] = second, [second] = first },
                new Dictionary<Guid, Guid?> { [device] = first, [unplaced] = null });
            Assert.AreEqual(PollingPolicySource.Default, resolver.ResolveDevice(device).Source);
            Assert.AreEqual(PollingPolicySource.Default, resolver.ResolveLocation(first).Source);
            Assert.AreEqual(PollingPolicySource.Default, resolver.ResolveDevice(unplaced).Source);
            Assert.AreEqual(PollingPolicySource.Default, resolver.ResolveDevice(Guid.NewGuid()).Source);
        }

        [TestMethod]
        public void PolicyValidationAndDefaultValues()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new PollingPolicy(Guid.NewGuid(), "Off", false, true,
                PollingSchedule.Off, PollingSchedule.Off, new int[0]));
            var disabled = new PollingPolicy(Guid.NewGuid(), " Disabled ", false, false,
                PollingSchedule.Off, PollingSchedule.Off, new[] { 443, 22, 443 });
            Assert.AreEqual("Disabled", disabled.Name);
            CollectionAssert.AreEqual(new[] { 22, 443 }, disabled.TcpPorts.ToArray());
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PollingPolicy(Guid.NewGuid(), "Bad", false, false,
                PollingSchedule.Off, PollingSchedule.Off, new[] { 0 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PollingPolicy(Guid.NewGuid(), "Bad", false, false,
                PollingSchedule.Off, PollingSchedule.Off, new[] { 65536 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PollingSchedule.Every(5));
            Assert.AreEqual(PollingSchedule.Every(10), PollingSchedule.Every(10));
            Assert.AreEqual(PollingSchedule.Every(10).GetHashCode(), PollingSchedule.Every(10).GetHashCode());
            var fallback = PollingPolicy.CreateDefault();
            Assert.IsTrue(fallback.ActivePolling);
            Assert.AreEqual(PollingScheduleMode.General, fallback.StateSchedule.Mode);
            Assert.AreEqual(PollingScheduleMode.General, fallback.TopologySchedule.Mode);
            CollectionAssert.AreEqual(new[] { 22, 80, 443 }, fallback.TcpPorts.ToArray());
        }
    }
}
