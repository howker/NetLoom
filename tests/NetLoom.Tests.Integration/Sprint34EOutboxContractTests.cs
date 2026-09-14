using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34EOutboxContractTests
    {
        [TestMethod]
        public void DurableDegradationOutboxTypesExist()
        {
            var application =
                typeof(MonitoringRuntime).Assembly;

            var persistence =
                typeof(SqliteConnectionFactory).Assembly;

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.IInterfaceDegradationTransitionProcessor"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationTransitionEvaluator"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationOutboxEvent"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.IInterfaceDegradationEventOutbox"));

            Assert.IsNotNull(
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceDegradationTransitionProcessor"));

            Assert.IsNotNull(
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceDegradationEventOutbox"));

            Assert.IsNotNull(
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Migrations.Migration016InterfaceDegradationOutbox"));
        }
    }
}
