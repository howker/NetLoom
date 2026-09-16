using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint34KSmtpConfigurationReadinessTests
    {
        [TestMethod]
        public void MissingRequiredFieldsAreReportedIndividually()
        {
            var configuration =
                Evaluate(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);

            Assert.AreEqual(
                EngineSmtpConfigurationState
                    .NotConfigured,
                configuration.State);

            CollectionAssert.AreEqual(
                new[]
                {
                    "MISSING_NETLOOM_SMTP_HOST",
                    "MISSING_NETLOOM_SMTP_FROM",
                    "MISSING_NETLOOM_SMTP_TO"
                },
                ToArray(
                    configuration.Reasons));
        }

        [TestMethod]
        public void PartialAuthenticationIsNotReady()
        {
            var configuration =
                Evaluate(
                    "smtp.example.invalid",
                    null,
                    null,
                    "sender@example.invalid",
                    "recipient@example.invalid",
                    "user-canary",
                    null);

            Assert.AreEqual(
                EngineSmtpConfigurationState
                    .NotReady,
                configuration.State);

            CollectionAssert.AreEqual(
                new[]
                {
                    "NETLOOM_SMTP_USERNAME_PASSWORD_PAIR_REQUIRED"
                },
                ToArray(
                    configuration.Reasons));

            Assert.AreEqual(
                "incomplete",
                configuration.AuthMode);
        }

        [TestMethod]
        public void InvalidPortAndSslAreReportedTogether()
        {
            var configuration =
                Evaluate(
                    "smtp.example.invalid",
                    "70000",
                    "sometimes",
                    "sender@example.invalid",
                    "recipient@example.invalid",
                    null,
                    null);

            CollectionAssert.AreEqual(
                new[]
                {
                    "INVALID_NETLOOM_SMTP_PORT",
                    "INVALID_NETLOOM_SMTP_ENABLE_SSL"
                },
                ToArray(
                    configuration.Reasons));

            Assert.AreEqual(
                "configured-invalid",
                configuration.PortMode);

            Assert.AreEqual(
                "configured-invalid",
                configuration.SslMode);
        }

        [TestMethod]
        public void DefaultsProduceReadyConfiguration()
        {
            var configuration =
                Evaluate(
                    "smtp.example.invalid",
                    null,
                    null,
                    "sender@example.invalid",
                    "recipient@example.invalid",
                    null,
                    null);

            Assert.AreEqual(
                EngineSmtpConfigurationState.Ready,
                configuration.State);

            Assert.AreEqual(
                25,
                configuration.Port);

            Assert.IsFalse(
                configuration.EnableSsl);

            Assert.AreEqual(
                "default-valid",
                configuration.PortMode);

            Assert.AreEqual(
                "default-valid",
                configuration.SslMode);

            Assert.AreEqual(
                "none",
                configuration.AuthMode);
        }

        [TestMethod]
        public void ExplicitValidTransportAndPairedCredentialsAreReady()
        {
            var configuration =
                Evaluate(
                    "smtp.example.invalid",
                    "587",
                    "true",
                    "sender@example.invalid",
                    "recipient@example.invalid",
                    "user-canary",
                    "password-canary");

            Assert.AreEqual(
                EngineSmtpConfigurationState.Ready,
                configuration.State);

            Assert.AreEqual(
                587,
                configuration.Port);

            Assert.IsTrue(
                configuration.EnableSsl);

            Assert.AreEqual(
                "configured-valid",
                configuration.PortMode);

            Assert.AreEqual(
                "configured-valid",
                configuration.SslMode);

            Assert.AreEqual(
                "paired",
                configuration.AuthMode);
        }

        [TestMethod]
        public void DiagnosticTextDoesNotContainConfiguredValues()
        {
            var configuration =
                Evaluate(
                    "host-canary.invalid",
                    "2525",
                    "true",
                    "from-canary@example.invalid",
                    "to-canary@example.invalid",
                    "user-canary",
                    "password-canary");

            var diagnostic =
                configuration.DiagnosticText;

            Assert.IsFalse(
                diagnostic.Contains(
                    "host-canary"));

            Assert.IsFalse(
                diagnostic.Contains(
                    "from-canary"));

            Assert.IsFalse(
                diagnostic.Contains(
                    "to-canary"));

            Assert.IsFalse(
                diagnostic.Contains(
                    "user-canary"));

            Assert.IsFalse(
                diagnostic.Contains(
                    "password-canary"));

            StringAssert.Contains(
                diagnostic,
                "state=Ready");

            StringAssert.Contains(
                diagnostic,
                "auth=paired");
        }

        [TestMethod]
        public void SmtpReadinessCommandTakesNoArguments()
        {
            var parsed =
                EngineCommandLine.Parse(
                    new[]
                    {
                        "smtp-readiness"
                    });

            Assert.AreEqual(
                "smtp-readiness",
                parsed.Command);

            try
            {
                EngineCommandLine.Parse(
                    new[]
                    {
                        "smtp-readiness",
                        "--database",
                        "ignored.db"
                    });

                Assert.Fail(
                    "SMTP readiness options must be rejected.");
            }
            catch (ArgumentException exception)
            {
                Assert.AreEqual(
                    "SMTP_READINESS_TAKES_NO_ARGUMENTS",
                    exception.Message);
            }
        }

        private static EngineSmtpConfiguration Evaluate(
            string host,
            string portText,
            string sslText,
            string from,
            string to,
            string username,
            string password)
        {
            return EngineSmtpConfiguration.Evaluate(
                host,
                portText,
                sslText,
                from,
                to,
                username,
                password);
        }

        private static string[] ToArray(
            System.Collections.Generic.IReadOnlyList<string>
                values)
        {
            var result =
                new string[values.Count];

            for (var index = 0;
                 index < values.Count;
                 index++)
            {
                result[index] =
                    values[index];
            }

            return result;
        }
    }
}
