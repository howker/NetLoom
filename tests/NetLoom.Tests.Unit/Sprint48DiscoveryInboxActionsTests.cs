using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Domain.Topology;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint48DiscoveryInboxActionsTests
    {
        [TestMethod]
        public void CanApplyCoversEveryGroupActionAndResolutionWithAndWithoutDevice()
        {
            var data = new Sprint48DiscoveryInboxFixture();
            foreach (DiscoveryResultGroup group in Enum.GetValues(typeof(DiscoveryResultGroup)))
            foreach (var hasDevice in new[] { false, true })
            foreach (DiscoveryResultResolution resolution in Enum.GetValues(typeof(DiscoveryResultResolution)))
            {
                var row = Sprint48InboxActionFixture.Copy(data.Result(group, "10.0.0.1"),
                    hasDevice ? (Guid?)Guid.NewGuid() : null, resolution);
                var pending = resolution == DiscoveryResultResolution.Pending && group != DiscoveryResultGroup.Excluded;
                var expected = new[]
                {
                    pending && (hasDevice || group == DiscoveryResultGroup.Missing),
                    pending && hasDevice,
                    pending && hasDevice && group != DiscoveryResultGroup.Missing,
                    pending && hasDevice
                };
                var actions = new[] { DiscoveryInboxAction.Accept, DiscoveryInboxAction.Ignore,
                    DiscoveryInboxAction.MarkUnmanaged, DiscoveryInboxAction.AssignPlacement };
                for (var index = 0; index < actions.Length; index++)
                    Assert.AreEqual(expected[index], DiscoveryInboxActions.CanApply(actions[index], row),
                        group + "/" + actions[index] + "/" + hasDevice + "/" + resolution);
            }
            foreach (DiscoveryInboxAction action in Enum.GetValues(typeof(DiscoveryInboxAction)))
                Assert.IsFalse(DiscoveryInboxActions.CanApply(action, null));
        }

        [TestMethod]
        public void ProjectionShowsResolutionsPlacementPathAndUndoInsteadOfRetry()
        {
            var data = new Sprint48DiscoveryInboxFixture();
            var resolved = data.Now;
            var rows = new[]
            {
                Sprint48InboxActionFixture.Copy(data.Result(DiscoveryResultGroup.New, "10.0.0.1"), Guid.NewGuid(), DiscoveryResultResolution.Accepted, resolved),
                Sprint48InboxActionFixture.Copy(data.Result(DiscoveryResultGroup.Changed, "10.0.0.2"), Guid.NewGuid(), DiscoveryResultResolution.Placed, resolved),
                Sprint48InboxActionFixture.Copy(data.Result(DiscoveryResultGroup.New, "10.0.0.3"), Guid.NewGuid(), DiscoveryResultResolution.Unmanaged, resolved),
                Sprint48InboxActionFixture.Copy(data.Result(DiscoveryResultGroup.New, "10.0.0.4"), Guid.NewGuid(), DiscoveryResultResolution.Ignored, resolved),
                data.Result(DiscoveryResultGroup.Excluded, "10.0.0.5", reason: DiscoveryResultReason.OperatorIgnored,
                    detail: resolved.ToString("o"), deviceId: Guid.NewGuid())
            };
            var projected = NetLoom.Wpf.Discovery.DiscoveryInboxProjection.Build(rows, data.Run,
                id => data.Profile.Name, data.Now, id => null, id => "АГПЗ / ГПП-1 / Серверная")
                .SelectMany(group => group.Rows).ToDictionary(row => row.Address);
            Assert.AreEqual(NetLoom.Wpf.Localization.UiText.Format("DiscoveryInboxResolutionPlaced", "АГПЗ / ГПП-1 / Серверная"),
                projected["10.0.0.2"].ResolutionText);
            Assert.IsTrue(projected.Values.All(row => !row.CanSelect));
            Assert.IsTrue(projected["10.0.0.4"].CanUndoIgnore);
            Assert.IsTrue(projected["10.0.0.5"].CanUndoIgnore);
            Assert.IsFalse(projected["10.0.0.5"].CanRetry);
            Assert.AreEqual(NetLoom.Wpf.Localization.UiText.Get("DiscoveryInboxUndoIgnore"), projected["10.0.0.5"].RetryText);
            Assert.AreEqual(NetLoom.Wpf.Localization.UiText.Format("DiscoveryInboxResolutionAccepted",
                resolved.ToLocalTime().ToString("HH:mm")), projected["10.0.0.1"].ResolutionText);
            Assert.AreEqual(NetLoom.Wpf.Localization.UiText.Format("DiscoveryInboxResolutionIgnored",
                resolved.ToLocalTime().ToString("d")), projected["10.0.0.4"].ResolutionText);
            Assert.AreEqual(NetLoom.Wpf.Localization.UiText.Get("DiscoveryInboxResolutionUnmanaged"), projected["10.0.0.3"].ResolutionText);
        }

        [TestMethod]
        public void InMemoryResolutionUpdatePreservesCandidateAndChanges()
        {
            var data = new Sprint48DiscoveryInboxFixture();
            var repository = data.Repository();
            var original = repository.GetResults(data.RunId).Single(row => row.Address == "10.48.228.20");
            repository.SetResolution(data.RunId, original.Address, DiscoveryResultResolution.Accepted, data.Now);
            var updated = repository.GetResults(data.RunId).Single(row => row.Address == original.Address);
            Assert.AreEqual(DiscoveryResultResolution.Accepted, updated.Resolution);
            Assert.AreEqual(data.Now, updated.ResolvedUtc);
            Assert.AreEqual(original.ObservedUtc, updated.ObservedUtc);
            Assert.AreEqual(original.Group, updated.Group);
            Assert.AreEqual(original.SysName, updated.SysName);
            Assert.AreEqual(original.SysDescription, updated.SysDescription);
            CollectionAssert.AreEqual(original.Changes.ToArray(), updated.Changes.ToArray());
            CollectionAssert.AreEqual(original.OpenTcpPorts.ToArray(), updated.OpenTcpPorts.ToArray());
            repository.SetResolution(data.RunId, original.Address, DiscoveryResultResolution.Pending, null);
            Assert.IsNull(repository.GetResults(data.RunId).Single(row => row.Address == original.Address).ResolvedUtc);
        }

        [TestMethod]
        public void IgnoredTimestampMustBeUtc()
        {
            var id = Guid.NewGuid();
            Assert.ThrowsExactly<ArgumentException>(() => new TopologyDevice(id, null, null,
                DeviceCategory.Unknown, DeviceDiscoveryOrigin.Automatic, MonitoringCapability.Unknown,
                null, null, null, false, false, null, null, null,
                ignoredUtc: new DateTime(2026, 10, 7, 9, 0, 0, DateTimeKind.Unspecified)));
        }
    }
}
