using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.HostLogging;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class HostLogTraceListenerTests
    {
        [TestMethod]
        public void ErrorTraceIsPersistedAtErrorLevel()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom-TraceLogging-" +
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
                        "desktop",
                        settings))
                using (var listener =
                    new HostLogTraceListener(
                        logger))
                {
                    logFilePath =
                        logger.LogFilePath;

                    listener.TraceEvent(
                        null,
                        "NetLoom.Wpf",
                        TraceEventType.Error,
                        17,
                        "SPRINT32B_TRACE_ERROR");

                    logger.Flush();
                }

                var text =
                    File.ReadAllText(
                        logFilePath);

                StringAssert.Contains(
                    text,
                    "|ERROR|");

                StringAssert.Contains(
                    text,
                    "eventType=Error");

                StringAssert.Contains(
                    text,
                    "source=NetLoom.Wpf");

                StringAssert.Contains(
                    text,
                    "id=17");

                StringAssert.Contains(
                    text,
                    "SPRINT32B_TRACE_ERROR");
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
        public void WarningTraceIsPersistedAtWarningLevel()
        {
            var directory =
                Path.Combine(
                    Path.GetTempPath(),
                    "NetLoom-TraceLogging-" +
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
                        "desktop",
                        settings))
                using (var listener =
                    new HostLogTraceListener(
                        logger))
                {
                    logFilePath =
                        logger.LogFilePath;

                    listener.TraceEvent(
                        null,
                        "NetLoom.Wpf",
                        TraceEventType.Warning,
                        3,
                        "SPRINT32B_TRACE_WARNING");

                    logger.Flush();
                }

                var text =
                    File.ReadAllText(
                        logFilePath);

                StringAssert.Contains(
                    text,
                    "|WARN|");

                StringAssert.Contains(
                    text,
                    "SPRINT32B_TRACE_WARNING");
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
    }
}
