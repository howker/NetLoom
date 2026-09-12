using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.HostLogging;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class HostLoggingTests
    {
        [TestMethod]
        public void PersistentErrorIsWrittenToConfiguredHostFile()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom-HostLogging-" +
                    Guid.NewGuid().ToString("N"));

            try
            {
                var settings =
                    new HostLogSettings(
                        directory,
                        HostLogLevel.Info,
                        1024 * 1024,
                        2);

                string logFilePath;

                using (var logger =
                    HostLogManager.Create(
                        "engine",
                        settings))
                {
                    logFilePath =
                        logger.LogFilePath;

                    logger.Error(
                        new InvalidOperationException(
                            "SPRINT32B_TEST_EXCEPTION"),
                        "SPRINT32B_TEST_ERROR");

                    logger.Flush();
                }

                Assert.IsTrue(
                    File.Exists(
                        logFilePath),
                    "Persistent host log was not created.");

                var text =
                    File.ReadAllText(
                        logFilePath);

                StringAssert.Contains(
                    text,
                    "ERROR");

                StringAssert.Contains(
                    text,
                    "SPRINT32B_TEST_ERROR");

                StringAssert.Contains(
                    text,
                    "InvalidOperationException");

                StringAssert.Contains(
                    text,
                    "SPRINT32B_TEST_EXCEPTION");
            }
            finally
            {
                if (Directory.Exists(
                    directory))
                {
                    Directory.Delete(
                        directory,
                        true);
                }
            }
        }

        [TestMethod]
        public void WindowsDefaultUsesProgramData()
        {
            var result =
                HostLogPathResolver.ResolveDefault(
                    true,
                    @"C:\ProgramData",
                    null,
                    null);

            Assert.AreEqual(
                Path.Combine(
                    @"C:\ProgramData",
                    "NetLoom",
                    "logs"),
                result);
        }

        [TestMethod]
        public void PortableDefaultPrefersXdgStateHome()
        {
            var result =
                HostLogPathResolver.ResolveDefault(
                    false,
                    null,
                    "/var/lib/example-state",
                    "/home/example");

            Assert.AreEqual(
                Path.Combine(
                    "/var/lib/example-state",
                    "netloom",
                    "logs"),
                result);
        }

        [TestMethod]
        public void PortableDefaultFallsBackToHomeLocalState()
        {
            var result =
                HostLogPathResolver.ResolveDefault(
                    false,
                    null,
                    null,
                    "/home/example");

            Assert.AreEqual(
                Path.Combine(
                    "/home/example",
                    ".local",
                    "state",
                    "netloom",
                    "logs"),
                result);
        }
    }
}
