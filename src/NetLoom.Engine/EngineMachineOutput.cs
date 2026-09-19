using System;
using System.Globalization;
using System.IO;
using System.Text;
using NetLoom.Application.Discovery;
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

        public static void WriteDiscoveryControlReady(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_DISCOVERY_CONTROL state=ready");
        }

        public static void WriteDiscoveryControlStopAccepted(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_DISCOVERY_CONTROL command=STOP result=accepted");
        }

        public static void WriteDiscoveryControlIgnored(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_DISCOVERY_CONTROL command=UNKNOWN result=ignored");
        }

        public static void WriteDiscoveryControlEndOfInput(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_DISCOVERY_CONTROL event=EOF action=stop");
        }

        public static void WriteDiscoveryControlReadFailed(
            TextWriter writer)
        {
            WriteLine(
                writer,
                "NETLOOM_DISCOVERY_CONTROL event=READ_FAILED action=stop");
        }

        public static void WriteDiscoveryStarted(
            TextWriter writer,
            Guid accessProfileId,
            int totalAddresses,
            int interAddressDelayMilliseconds)
        {
            WriteLine(
                writer,
                "NETLOOM_DISCOVERY state=started accessProfileId=" +
                accessProfileId.ToString("D") +
                " total=" +
                totalAddresses.ToString(
                    CultureInfo.InvariantCulture) +
                " delayMs=" +
                interAddressDelayMilliseconds.ToString(
                    CultureInfo.InvariantCulture));
        }

        public static void WriteDiscoveryProgress(
            TextWriter writer,
            DiscoveryProgress progress,
            int foundCandidates)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(
                    nameof(progress));
            }

            WriteLine(
                writer,
                "NETLOOM_DISCOVERY state=progress processed=" +
                progress.ProcessedAddresses.ToString(
                    CultureInfo.InvariantCulture) +
                " total=" +
                progress.TotalAddresses.ToString(
                    CultureInfo.InvariantCulture) +
                " found=" +
                foundCandidates.ToString(
                    CultureInfo.InvariantCulture) +
                " address=" +
                progress.Address +
                " candidate=" +
                (progress.CandidateFound
                    ? "true"
                    : "false"));
        }

        public static void WriteDiscoveryCandidate(
            TextWriter writer,
            DiscoveryCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(
                    nameof(candidate));
            }

            var inventory =
                candidate.Inventory;

            WriteLine(
                writer,
                "NETLOOM_DISCOVERY_CANDIDATE address=" +
                candidate.Address +
                " accessProfileId=" +
                (candidate.AccessProfileId.HasValue
                    ? candidate.AccessProfileId.Value.ToString("D")
                    : "none") +
                " icmp=" +
                (candidate.IcmpReachable
                    ? "true"
                    : "false") +
                " snmp=" +
                (candidate.SnmpResponded
                    ? "true"
                    : "false") +
                " tcpPorts=" +
                FormatPorts(candidate) +
                " sysName64=" +
                EncodeText(
                    inventory == null
                        ? null
                        : inventory.SysName) +
                " sysDescription64=" +
                EncodeText(
                    inventory == null
                        ? null
                        : inventory.SysDescription) +
                " sysObjectId64=" +
                EncodeText(
                    inventory == null
                        ? null
                        : inventory.SysObjectId) +
                " sysLocation64=" +
                EncodeText(
                    inventory == null
                        ? null
                        : inventory.SysLocation) +
                " interfaces=" +
                (inventory == null ||
                 inventory.Interfaces == null
                    ? "0"
                    : inventory.Interfaces.Count.ToString(
                        CultureInfo.InvariantCulture)));
        }

        public static void WriteDiscoveryCompleted(
            TextWriter writer,
            int processedAddresses,
            int totalAddresses,
            int foundCandidates)
        {
            WriteDiscoveryTerminalState(
                writer,
                "completed",
                processedAddresses,
                totalAddresses,
                foundCandidates);
        }

        public static void WriteDiscoveryStopped(
            TextWriter writer,
            int processedAddresses,
            int totalAddresses,
            int foundCandidates)
        {
            WriteDiscoveryTerminalState(
                writer,
                "stopped",
                processedAddresses,
                totalAddresses,
                foundCandidates);
        }

        private static void WriteDiscoveryTerminalState(
            TextWriter writer,
            string state,
            int processedAddresses,
            int totalAddresses,
            int foundCandidates)
        {
            WriteLine(
                writer,
                "NETLOOM_DISCOVERY state=" +
                state +
                " processed=" +
                processedAddresses.ToString(
                    CultureInfo.InvariantCulture) +
                " total=" +
                totalAddresses.ToString(
                    CultureInfo.InvariantCulture) +
                " found=" +
                foundCandidates.ToString(
                    CultureInfo.InvariantCulture));
        }

        private static string FormatPorts(
            DiscoveryCandidate candidate)
        {
            if (candidate.OpenTcpPorts.Count == 0)
            {
                return "none";
            }

            var values =
                new string[candidate.OpenTcpPorts.Count];

            for (var index = 0;
                 index < candidate.OpenTcpPorts.Count;
                 index++)
            {
                values[index] =
                    candidate.OpenTcpPorts[index].ToString(
                        CultureInfo.InvariantCulture);
            }

            return string.Join(
                ",",
                values);
        }

        private static string EncodeText(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "-";
            }

            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value));
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
