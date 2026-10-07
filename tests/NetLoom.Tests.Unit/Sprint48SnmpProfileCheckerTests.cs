using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint48SnmpProfileCheckerTests
    {
        [TestMethod]
        public void CompleteAgentReturnsSixOkItemsWithCountsAndAvailabilityTime()
        {
            var transport = new ProfileTransport();
            var report = Check(transport);
            CollectionAssert.AreEqual(Enum.GetValues(typeof(SnmpProfileCheckKind)), report.Items.Select(item => item.Kind).ToArray());
            Assert.IsTrue(report.Items.All(item => item.Status == SnmpProfileCheckStatus.Ok));
            Assert.AreEqual(52, Item(report, SnmpProfileCheckKind.IfMib).Count);
            Assert.AreEqual(18, Item(report, SnmpProfileCheckKind.LldpMib).Count);
            Assert.IsTrue(Item(report, SnmpProfileCheckKind.Availability).Milliseconds >= 0);
            Assert.AreEqual(3, transport.WalkCalls);
        }

        [TestMethod]
        public void QBridgeWithoutNamesIsPartial()
        {
            var transport = new ProfileTransport();
            transport.WalkValues[ProfileTransport.VlanNames] = new SnmpVariable[0];
            Assert.AreEqual(SnmpProfileCheckStatus.Partial, Item(Check(transport), SnmpProfileCheckKind.QBridgeMib).Status);
        }

        [TestMethod]
        public void SystemAuthenticationFailureSkipsAllRemainingSnmpOperations()
        {
            var transport = new ProfileTransport { GetFailureOid = ProfileTransport.SysName };
            var report = Check(transport);
            var system = Item(report, SnmpProfileCheckKind.System);
            Assert.AreEqual(SnmpProfileCheckStatus.Failed, system.Status);
            Assert.AreEqual(SnmpTransportFailure.Authentication, system.Failure);
            Assert.IsTrue(report.Items.Skip(2).All(item => item.Status == SnmpProfileCheckStatus.NotChecked));
            Assert.AreEqual(1, transport.GetCalls);
            Assert.AreEqual(0, transport.WalkCalls);
        }

        [TestMethod]
        public void LldpWithoutNeighborsButWithLocalChassisIsOkWithZeroCount()
        {
            var transport = new ProfileTransport();
            transport.WalkValues[ProfileTransport.Neighbors] = new SnmpVariable[0];
            var item = Item(Check(transport), SnmpProfileCheckKind.LldpMib);
            Assert.AreEqual(SnmpProfileCheckStatus.Ok, item.Status);
            Assert.AreEqual(0, item.Count);
        }

        [TestMethod]
        public void AbsentBridgeIsReportedWithoutAffectingQBridge()
        {
            var transport = new ProfileTransport();
            transport.GetValues.Remove(ProfileTransport.BridgePorts);
            var report = Check(transport);
            Assert.AreEqual(SnmpProfileCheckStatus.Absent, Item(report, SnmpProfileCheckKind.BridgeMib).Status);
            Assert.AreEqual(SnmpProfileCheckStatus.Ok, Item(report, SnmpProfileCheckKind.QBridgeMib).Status);
        }

        [TestMethod]
        public void IcmpFailureDoesNotPreventSnmpChecks()
        {
            var report = Check(new ProfileTransport(), false);
            Assert.AreEqual(SnmpProfileCheckStatus.Failed, report.Items[0].Status);
            Assert.IsTrue(report.Items.Skip(1).All(item => item.Status == SnmpProfileCheckStatus.Ok));
        }

        [DataTestMethod]
        [DataRow(0x80)]
        [DataRow(0x81)]
        [DataRow(0x82)]
        public void SnmpExceptionValuesAreAbsentInGetAndWalk(int typeCode)
        {
            var transport = new ProfileTransport();
            transport.GetValues[ProfileTransport.BridgePorts] = new SnmpVariable(ProfileTransport.BridgePorts,
                typeCode, "exception-value", null);
            transport.WalkValues[ProfileTransport.Interfaces] = new[]
            {
                new SnmpVariable(ProfileTransport.Interfaces + ".1", typeCode, "exception-value", null)
            };
            var report = Check(transport);
            Assert.AreEqual(SnmpProfileCheckStatus.Absent, Item(report, SnmpProfileCheckKind.BridgeMib).Status);
            Assert.AreEqual(SnmpProfileCheckStatus.Absent, Item(report, SnmpProfileCheckKind.IfMib).Status);
        }

        [TestMethod]
        public void WalkFailureOnlyFailsItsItem()
        {
            var transport = new ProfileTransport { WalkFailureOid = ProfileTransport.Interfaces };
            var report = Check(transport);
            Assert.AreEqual(SnmpProfileCheckStatus.Failed, report.Items[2].Status);
            Assert.AreEqual(SnmpTransportFailure.Timeout, report.Items[2].Failure);
            Assert.IsTrue(report.Items.Skip(3).All(item => item.Status == SnmpProfileCheckStatus.Ok));
        }

        [TestMethod]
        public void OneSystemValueIsPartialAndAnEmptyAgentIsAbsent()
        {
            var transport = new ProfileTransport();
            transport.GetValues.Remove(ProfileTransport.SysObjectId);
            Assert.AreEqual(SnmpProfileCheckStatus.Partial, Check(transport).Items[1].Status);
            transport.GetValues.Clear();
            foreach (var key in transport.WalkValues.Keys.ToArray()) transport.WalkValues[key] = new SnmpVariable[0];
            Assert.IsTrue(Check(transport).Items.Skip(1).All(item => item.Status == SnmpProfileCheckStatus.Absent));
        }

        [TestMethod]
        public void CancellationBeforeCheckDoesNotContactAgent()
        {
            var transport = new ProfileTransport();
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                    new SnmpProfileChecker(transport, new ProfileProbe(true)).Check(Request(), source.Token));
            }
            Assert.AreEqual(0, transport.GetCalls);
            Assert.AreEqual(0, transport.WalkCalls);
        }

        private static SnmpProfileCheckReport Check(ProfileTransport transport, bool reachable = true)
        {
            return new SnmpProfileChecker(transport, new ProfileProbe(reachable)).Check(Request(), CancellationToken.None);
        }

        private static SnmpProfileCheckRequest Request()
        {
            return new SnmpProfileCheckRequest(IPAddress.Parse("192.0.2.1"), 161, SnmpVersion.V2C,
                new SnmpCommunityCredentials(Encoding.UTF8.GetBytes("profile-check-fixture-secret")), 750, 0, 10);
        }

        private static SnmpProfileCheckItem Item(SnmpProfileCheckReport report, SnmpProfileCheckKind kind)
        {
            return report.Items.Single(item => item.Kind == kind);
        }

        private sealed class ProfileProbe : INetworkDiscoveryProbe
        {
            private readonly bool _reachable;
            public ProfileProbe(bool reachable) { _reachable = reachable; }
            public bool IsIcmpReachable(IPAddress address, int timeoutMilliseconds, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Assert.AreEqual(500, timeoutMilliseconds);
                return _reachable;
            }
            public IReadOnlyList<int> FindOpenTcpPorts(IPAddress address, IReadOnlyList<int> ports,
                int timeoutMilliseconds, CancellationToken token) { throw new AssertFailedException("TCP is not part of profile check."); }
        }

        private sealed class ProfileTransport : ISnmpTransport
        {
            public const string SysName = "1.3.6.1.2.1.1.5.0";
            public const string SysObjectId = "1.3.6.1.2.1.1.2.0";
            public const string Interfaces = "1.3.6.1.2.1.2.2.1.1";
            public const string Neighbors = "1.0.8802.1.1.2.1.4.1.1.9";
            public const string BridgePorts = "1.3.6.1.2.1.17.1.2.0";
            public const string VlanNames = "1.3.6.1.2.1.17.7.1.4.3.1.1";
            public readonly Dictionary<string, SnmpVariable> GetValues = new Dictionary<string, SnmpVariable>();
            public readonly Dictionary<string, IReadOnlyList<SnmpVariable>> WalkValues = new Dictionary<string, IReadOnlyList<SnmpVariable>>();
            public string GetFailureOid;
            public string WalkFailureOid;
            public int GetCalls;
            public int WalkCalls;

            public ProfileTransport()
            {
                foreach (var oid in new[] { SysName, SysObjectId, BridgePorts, "1.0.8802.1.1.2.1.3.2.0", "1.3.6.1.2.1.17.7.1.1.4.0" })
                    GetValues[oid] = new SnmpVariable(oid, 4, "fixture-value", null);
                WalkValues[Interfaces] = Rows(Interfaces, 52);
                WalkValues[Neighbors] = Rows(Neighbors, 18);
                WalkValues[VlanNames] = Rows(VlanNames, 3);
            }

            public IReadOnlyList<SnmpVariable> Get(SnmpGetRequest request)
            {
                GetCalls++;
                if (request.Oids.Contains(GetFailureOid)) throw new SnmpTransportException(
                    SnmpTransportFailure.Authentication, "profile-check-fixture-secret", null);
                return request.Oids.Where(oid => GetValues.ContainsKey(oid)).Select(oid => GetValues[oid]).ToArray();
            }

            public IReadOnlyList<SnmpVariable> Walk(SnmpWalkRequest request)
            {
                WalkCalls++;
                if (request.RootOid == WalkFailureOid) throw new SnmpTransportException(
                    SnmpTransportFailure.Timeout, "profile-check-fixture-secret", null);
                return WalkValues[request.RootOid];
            }

            private static SnmpVariable[] Rows(string oid, int count)
            {
                return Enumerable.Range(1, count).Select(index => new SnmpVariable(oid + "." + index,
                    2, index.ToString(), null)).ToArray();
            }
        }
    }
}
