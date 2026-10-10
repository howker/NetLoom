using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Wpf.MapInteraction;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint49EnginePollingPointTests
    {
        private const string Mac = "02:00:5E:10:49:01";

        private static MacIpLookupCandidate Candidate(
            Guid? deviceId, Guid? interfaceId,
            MacIpLookupCandidateStatus status = MacIpLookupCandidateStatus.ResolvedInterface) =>
            new MacIpLookupCandidate(null, Mac, deviceId, interfaceId, null, null, null, null, null,
                null, null, null, status);

        [TestMethod]
        public void NoResolvedCandidateMeansNotFound()
        {
            var result = EnginePollingPoint.Resolve(new[]
            {
                Candidate(null, null, MacIpLookupCandidateStatus.FdbNotObserved),
                Candidate(Guid.NewGuid(), null, MacIpLookupCandidateStatus.BridgePortUnresolved)
            }, new HashSet<Guid>());
            Assert.AreEqual(EnginePollingPointStatus.NotFound, result.Status);
            Assert.IsNull(result.DeviceId);
            Assert.AreEqual(EnginePollingPointStatus.NotFound,
                EnginePollingPoint.Resolve(new MacIpLookupCandidate[0], new HashSet<Guid>()).Status);
        }

        [TestMethod]
        public void SingleAccessPortDeterminesTheDeviceEvenIfOtherPortsAreLinkPorts()
        {
            var device = Guid.NewGuid();
            var accessPort = Guid.NewGuid();
            var linkPort = Guid.NewGuid();
            var result = EnginePollingPoint.Resolve(new[]
            {
                Candidate(device, accessPort),
                Candidate(Guid.NewGuid(), linkPort)
            }, new HashSet<Guid> { linkPort });
            Assert.AreEqual(EnginePollingPointStatus.Determined, result.Status);
            Assert.AreEqual(device, result.DeviceId);
        }

        [TestMethod]
        public void SeveralPortsOfOneDeviceStayDeterminedButSeveralDevicesAreAmbiguous()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var same = EnginePollingPoint.Resolve(new[]
            {
                Candidate(first, Guid.NewGuid()),
                Candidate(first, Guid.NewGuid())
            }, new HashSet<Guid>());
            Assert.AreEqual(EnginePollingPointStatus.Determined, same.Status);
            var ambiguous = EnginePollingPoint.Resolve(new[]
            {
                Candidate(first, Guid.NewGuid()),
                Candidate(second, Guid.NewGuid())
            }, new HashSet<Guid>());
            Assert.AreEqual(EnginePollingPointStatus.Ambiguous, ambiguous.Status);
            Assert.IsNull(ambiguous.DeviceId);
        }

        [TestMethod]
        public void MacSeenOnlyOnLinkPortsHasNoAccessPort()
        {
            var linkPorts = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var result = EnginePollingPoint.Resolve(
                linkPorts.Select(port => Candidate(Guid.NewGuid(), port)).ToArray(),
                new HashSet<Guid>(linkPorts));
            Assert.AreEqual(EnginePollingPointStatus.NoAccessPort, result.Status);
            Assert.IsNull(result.DeviceId);
        }
    }
}
