using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint34HEngineCommandTests
    {
        [TestMethod]
        public void DeliveryStatusDoesNotRequirePollingAddress()
        {
            var parsed =
                EngineCommandLine.Parse(
                    new[]
                    {
                        "delivery-status",
                        "--database",
                        "operator.db",
                        "--limit",
                        "25"
                    });

            Assert.AreEqual(
                "delivery-status",
                parsed.Command);

            Assert.AreEqual(
                "operator.db",
                parsed.DatabasePath);

            Assert.AreEqual(
                25,
                parsed.DeliveryStatusLimit);
        }

        [TestMethod]
        public void DeliveryStatusRejectsPollingOnlyOption()
        {
            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "delivery-status",
                        "--address",
                        "127.0.0.1"
                    });

                Assert.Fail(
                    "Polling-only option must be rejected.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "UNKNOWN_OPTION_address",
                    exception.Message);
            }
        }

        [TestMethod]
        public void SmtpAcceptanceTakesNoArguments()
        {
            var parsed =
                EngineCommandLine.Parse(
                    new[]
                    {
                        "smtp-acceptance"
                    });

            Assert.AreEqual(
                "smtp-acceptance",
                parsed.Command);

            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "smtp-acceptance",
                        "--database",
                        "ignored.db"
                    });

                Assert.Fail(
                    "SMTP acceptance options must be rejected.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "SMTP_ACCEPTANCE_TAKES_NO_ARGUMENTS",
                    exception.Message);
            }
        }
    }
}
