using System;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Desktop.Monitoring;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    // Sprint 47: доступность по ICMP и TCP проходит границу Engine → Desktop в строке завершения опроса.
    [TestClass]
    public sealed class Sprint47AvailabilityMarkerTests
    {
        private static readonly DateTime CompletedUtc =
            new DateTime(
                2026, 10, 7, 14, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void AvailabilityRoundTripsThroughEngineOutputAndDesktopParser()
        {
            var result =
                PollResult(false)
                    .WithAvailability(
                        new MonitoringAvailability(
                            true,
                            new[] { 443, 22, 80 },
                            new[] { 22 }));

            var marker =
                Parse(result);

            Assert.AreEqual(EngineMachineMarkerKind.PollCompleted, marker.Kind);
            Assert.AreEqual(false, marker.AnySucceeded, "ICMP does not make the SNMP poll successful.");
            Assert.IsNotNull(marker.Availability);
            Assert.AreEqual(true, marker.Availability.IcmpReachable);
            CollectionAssert.AreEqual(new[] { 22, 80, 443 }, marker.Availability.CheckedTcpPorts.ToArray());
            CollectionAssert.AreEqual(new[] { 22 }, marker.Availability.OpenTcpPorts.ToArray());

            var unknown =
                Parse(
                    PollResult(true)
                        .WithAvailability(
                            new MonitoringAvailability(
                                null,
                                new int[0],
                                new int[0])));

            Assert.IsNull(unknown.Availability.IcmpReachable, "A probe failure stays «not checked».");
            Assert.AreEqual(0, unknown.Availability.CheckedTcpPorts.Count);
        }

        [TestMethod]
        public void LineWithoutOrWithBrokenAvailabilityIsStillAPollResult()
        {
            var legacy =
                Parse(
                    PollResult(true));

            Assert.AreEqual(EngineMachineMarkerKind.PollCompleted, legacy.Kind);
            Assert.IsNull(legacy.Availability);

            EngineMachineMarker broken;

            Assert.IsTrue(
                EngineMachineMarkerParser.TryParse(
                    "NETLOOM_POLL state=completed completedUtc=2026-10-07T14:00:00.0000000Z success=1 failed=0 anySucceeded=true icmp=maybe tcpChecked=22 tcpOpen=22",
                    out broken));
            Assert.IsNull(broken.Availability);

            Assert.IsTrue(
                EngineMachineMarkerParser.TryParse(
                    "NETLOOM_POLL state=completed completedUtc=2026-10-07T14:00:00.0000000Z success=1 failed=0 anySucceeded=true icmp=yes tcpChecked=22 tcpOpen=80",
                    out broken));
            Assert.IsNull(broken.Availability, "An open port that was not checked is a broken line.");
        }

        private static MonitoringPollResult PollResult(
            bool succeeded)
        {
            return new MonitoringPollResult(
                IPAddress.Parse("192.0.2.70"),
                CompletedUtc.AddSeconds(-2),
                CompletedUtc,
                new[]
                {
                    new MonitoringPollStepResult(
                        MonitoringPollKind.Health,
                        succeeded,
                        succeeded ? null : "Timeout",
                        succeeded ? null : "No response")
                });
        }

        private static EngineMachineMarker Parse(
            MonitoringPollResult result)
        {
            var writer =
                new StringWriter();

            EngineMachineOutput.WritePollCompleted(
                writer,
                result);

            EngineMachineMarker marker;

            Assert.IsTrue(
                EngineMachineMarkerParser.TryParse(
                    writer.ToString().Trim(),
                    out marker),
                "Desktop must accept the Engine line: " + writer);

            return marker;
        }
    }
}
