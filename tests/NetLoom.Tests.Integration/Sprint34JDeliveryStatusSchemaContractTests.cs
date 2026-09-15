using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint34JDeliveryStatusSchemaContractTests
    {
        [TestMethod]
        public void ReadOnlyDeliveryStatusCompatibilityBoundaryExists()
        {
            var persistence =
                typeof(SqliteConnectionFactory).Assembly;

            Assert.IsNotNull(
                typeof(SqliteConnectionFactory)
                    .GetMethod(
                        "OpenReadOnlyConnection"));

            var probeType =
                persistence.GetType(
                    "NetLoom.Persistence.Sqlite.Monitoring.SqliteInterfaceDegradationDeliveryStatusSchemaProbe");

            Assert.IsNotNull(
                probeType);

            Assert.IsNotNull(
                probeType.GetMethod(
                    "IsCompatible"));
        }
    }
}
