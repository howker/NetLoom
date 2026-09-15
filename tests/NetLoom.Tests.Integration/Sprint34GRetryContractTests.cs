using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34GRetryContractTests
    {
        [TestMethod]
        public void DurableDeliveryRetryTypesExist()
        {
            var application =
                typeof(MonitoringRuntime).Assembly;

            var persistence =
                typeof(SqliteConnectionFactory).Assembly;

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationPendingDelivery"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationDeliveryRetryPolicy"));

            Assert.IsNotNull(
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Migrations.Migration018InterfaceDegradationDeliveryRetry"));

            var outboxType =
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceDegradationEventOutbox");

            Assert.IsNotNull(
                outboxType.GetMethod(
                    "ReadReady"));

            Assert.IsNotNull(
                outboxType.GetMethod(
                    "MarkDeliveryFailed"));
        }
    }
}
