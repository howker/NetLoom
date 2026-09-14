using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34FDeliveryContractTests
    {
        [TestMethod]
        public void DeliveryAcknowledgementTypesExist()
        {
            var application =
                typeof(MonitoringRuntime).Assembly;

            var persistence =
                typeof(SqliteConnectionFactory).Assembly;

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.IInterfaceDegradationDeliveryAdapter"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationOutboxDispatcher"));

            Assert.IsNotNull(
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Migrations.Migration017InterfaceDegradationDelivery"));

            var outboxType =
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceDegradationEventOutbox");

            Assert.IsNotNull(
                outboxType.GetMethod(
                    "MarkDelivered"));
        }
    }
}
