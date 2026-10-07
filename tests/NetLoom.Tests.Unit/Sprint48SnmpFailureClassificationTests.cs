using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Snmp;
using NetLoom.Protocols.Snmp.Transport;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint48SnmpFailureClassificationTests
    {
        [TestMethod]
        [DataRow(".1.3.6.1.6.3.15.1.1.5.0", SnmpTransportFailure.Authentication)]
        [DataRow("1.3.6.1.6.3.15.1.1.3.0", SnmpTransportFailure.Authentication)]
        [DataRow("1.3.6.1.6.3.15.1.1.1.0", SnmpTransportFailure.Authentication)]
        [DataRow("1.3.6.1.6.3.15.1.1.6.0", SnmpTransportFailure.Authentication)]
        [DataRow("1.3.6.1.6.3.15.1.1.2.0", SnmpTransportFailure.Protocol)]
        public void ReportsClassifyAuthenticationCounters(
            string oid,
            SnmpTransportFailure expected)
        {
            Assert.AreEqual(expected, SnmpFailureClassifier.ClassifyReport(oid));
        }

        [TestMethod]
        [DataRow(16, SnmpTransportFailure.Authentication)]
        [DataRow(6, SnmpTransportFailure.Authentication)]
        [DataRow(5, SnmpTransportFailure.Protocol)]
        public void ErrorStatusesClassifyAccessFailures(
            int errorStatus,
            SnmpTransportFailure expected)
        {
            Assert.AreEqual(expected, SnmpFailureClassifier.ClassifyErrorStatus(errorStatus));
        }
    }
}
