using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34BInterfaceCounterBaselineContractTests
    {
        [TestMethod]
        public void DurableCounterBaselineTypesExist()
        {
            Assert.IsNotNull(
                typeof(MonitoringRuntime).Assembly.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.IInterfaceCounterBaselineStore"));

            Assert.IsNotNull(
                typeof(MonitoringRuntime).Assembly.GetType(
                    "NetLoom.Application.Monitoring.Interfaces.InterfaceCounterEvaluation"));

            Assert.IsNotNull(
                typeof(SqliteConnectionFactory).Assembly.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceCounterBaselineStore"));

            Assert.IsNotNull(
                typeof(SqliteConnectionFactory).Assembly.GetType(
                    "NetLoom.Persistence.Sqlite.Migrations.Migration014InterfaceCounterBaselines"));
        }
    }
}
