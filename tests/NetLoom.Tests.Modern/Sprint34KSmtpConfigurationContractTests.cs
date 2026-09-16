using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint34KSmtpConfigurationContractTests
    {
        [TestMethod]
        public void SmtpReadinessConfigurationTypeExists()
        {
            var engineAssembly =
                typeof(EngineCommandLine)
                    .Assembly;

            var configurationType =
                engineAssembly.GetType(
                    "NetLoom.Engine.EngineSmtpConfiguration");

            Assert.IsNotNull(
                configurationType);

            Assert.IsNotNull(
                configurationType.GetMethod(
                    "ReadFromEnvironment"));

            Assert.IsNotNull(
                configurationType.GetProperty(
                    "DiagnosticText"));
        }
    }
}
