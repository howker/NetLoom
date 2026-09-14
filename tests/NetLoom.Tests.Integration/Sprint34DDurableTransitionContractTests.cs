using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34DDurableTransitionContractTests
    {
        [TestMethod]
        public void DurableDegradationTransitionTypesExist()
        {
            var application =
                typeof(MonitoringRuntime).Assembly;

            var persistence =
                typeof(SqliteConnectionFactory).Assembly;

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.IInterfaceDegradationStateStore"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationState"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationTransition"));

            Assert.IsNotNull(
                application.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceDegradationTransitionTracker"));

            Assert.IsNotNull(
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceDegradationStateStore"));

            Assert.IsNotNull(
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Migrations.Migration015InterfaceDegradationStates"));
        }
    }
}
