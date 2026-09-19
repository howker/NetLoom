using System;
using System.Globalization;
using System.IO;
using NetLoom.Application.Monitoring;

namespace NetLoom.Engine
{
    internal static class EngineMachineOutput
    {
        public static void WriteControlReady(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_CONTROL state=ready");
        }

        public static void WriteControlPollNowAccepted(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_CONTROL command=POLL_NOW result=accepted");
        }

        public static void WriteControlPollNowNoActiveDelay(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_CONTROL command=POLL_NOW result=no-active-delay");
        }

        public static void WriteControlStopAccepted(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_CONTROL command=STOP result=accepted");
        }

        public static void WriteControlIgnored(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_CONTROL command=UNKNOWN result=ignored");
        }

        public static void WriteControlEndOfInput(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_CONTROL event=EOF action=stop");
        }

        public static void WriteControlReadFailed(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_CONTROL event=READ_FAILED action=stop");
        }

        public static void WriteScheduleStarted(
            TextWriter writer,
            int intervalSeconds)
        {
            WriteLine(
                writer,
                "NETLOOM_SCHEDULE state=started intervalSeconds=" +
                intervalSeconds.ToString(
                    CultureInfo.InvariantCulture));
        }

        public static void WriteScheduleStopped(
            TextWriter writer,
            int completedCycles)
        {
            WriteLine(
                writer,
                "NETLOOM_SCHEDULE state=stopped cycles=" +
                completedCycles.ToString(
                    CultureInfo.InvariantCulture));
        }

        public static void WritePollStarted(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_POLL state=started");
        }

        public static void WritePollCompleted(
            TextWriter writer,
            MonitoringPollResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            var succeeded = 0;

            foreach (var step in result.Steps)
            {
                if (step.Succeeded)
                {
                    succeeded++;
                }
            }

            var failed =
                result.Steps.Count -
                succeeded;

            WriteLine(
                writer,
                "NETLOOM_POLL state=completed completedUtc=" +
                result.CompletedUtc.ToString(
                    "o",
                    CultureInfo.InvariantCulture) +
                " success=" +
                succeeded.ToString(
                    CultureInfo.InvariantCulture) +
                " failed=" +
                failed.ToString(
                    CultureInfo.InvariantCulture) +
                " anySucceeded=" +
                (result.AnySucceeded
                    ? "true"
                    : "false"));
        }

        private static void WriteLine(
            TextWriter writer,
            string line)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(
                    nameof(writer));
            }

            writer.WriteLine(
                line);

            writer.Flush();
        }
    }
}
