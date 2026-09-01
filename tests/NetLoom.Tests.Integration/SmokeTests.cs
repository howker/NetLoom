using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class SmokeTests
    {
        [TestMethod]
        public void TestInfrastructureIsAvailable()
        {
            Assert.IsNotNull(typeof(SmokeTests).Assembly);
        }
    }
}