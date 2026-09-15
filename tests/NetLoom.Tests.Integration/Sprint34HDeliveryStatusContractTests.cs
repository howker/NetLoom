using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34HDeliveryStatusContractTests
    {
        [TestMethod]
        public void OperatorDeliveryStatusTypesExist()
        {
            var application =
                typeof(MonitoringRuntime).Assembly;

            var persistence =
                typeof(SqliteConnectionFactory).Assembly;

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.IInterfaceDegradationDeliveryStatusReader"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationDeliveryStatus"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationDeliveryStatusKind"));

            var outboxType =
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceDegradationEventOutbox");

            Assert.IsNotNull(
                outboxType.GetMethod(
                    "ReadStatus"));
        }
    }
}
