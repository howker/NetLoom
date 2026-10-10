using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetLoom.Domain.Access;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.PollingPolicies;
using NetLoom.Application.Snmp;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint51PollingScheduleTests
    {
        private static readonly MonitoringPollKind[] Kinds = { MonitoringPollKind.Health, MonitoringPollKind.Interface, MonitoringPollKind.Lldp };
        private static readonly DateTime Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void BuildGroupsUsesModesAndMergesEqualPeriods()
        {
            var general = TimeSpan.FromSeconds(60);
            var defaults = PollingPolicySchedule.BuildGroups(PollingPolicy.CreateDefault(), general, Kinds);
            Assert.AreEqual(1, defaults.Count);
            CollectionAssert.AreEqual(Kinds, defaults[0].Kinds.ToArray());
            Assert.AreEqual(general, defaults[0].Cadence);
            Assert.IsFalse(defaults[0].PollsOnce);

            var split = Policy(true, PollingSchedule.General, PollingSchedule.Every(3600));
            var groups = PollingPolicySchedule.BuildGroups(split, general, Kinds);
            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEqual(new[] { MonitoringPollKind.Health, MonitoringPollKind.Interface }, groups[0].Kinds.ToArray());
            Assert.AreEqual(TimeSpan.FromSeconds(3600), groups[1].Cadence);
            Assert.AreEqual(1, PollingPolicySchedule.BuildGroups(split, general, new[] { MonitoringPollKind.Lldp }).Count);

            var once = PollingPolicySchedule.BuildGroups(Policy(true, PollingSchedule.Once, PollingSchedule.Off), general, Kinds);
            Assert.AreEqual(1, once.Count);
            Assert.IsTrue(once[0].PollsOnce);
            CollectionAssert.AreEqual(new[] { MonitoringPollKind.Health, MonitoringPollKind.Interface }, once[0].Kinds.ToArray());
            Assert.AreEqual(0, PollingPolicySchedule.BuildGroups(Policy(false, PollingSchedule.General, PollingSchedule.General), general, Kinds).Count);
            Assert.AreEqual(1, PollingPolicySchedule.BuildGroups(Policy(true, PollingSchedule.Every(60), PollingSchedule.General), general, Kinds).Count);
        }

        [TestMethod]
        public void SchedulerRespectsDueGroupsAndOriginalRequest()
        {
            var request = Request();
            var seen = new List<MonitoringPollKind[]>();
            var sameRequest = false;
            using (var cancellation = new CancellationTokenSource())
            {
                var groups = new[]
                {
                    new MonitoringScheduleGroup(new[] { MonitoringPollKind.Health, MonitoringPollKind.Interface }, TimeSpan.FromSeconds(60), false),
                    new MonitoringScheduleGroup(new[] { MonitoringPollKind.Lldp }, TimeSpan.FromSeconds(180), false)
                };
                var scheduler = new MultiTargetMonitoringScheduler(poll =>
                {
                    sameRequest |= ReferenceEquals(request, poll);
                    seen.Add(poll.Kinds.ToArray());
                    if (seen.Count == 4) cancellation.Cancel();
                    return Result(poll);
                }, (delay, token) => Task.CompletedTask);
                scheduler.Run(new[] { new MonitoringScheduleTarget(request, groups, TimeSpan.Zero) },
                    new MonitoringConcurrencyPolicy(1), cancellation.Token);
            }
            Assert.IsTrue(sameRequest);
            Assert.AreEqual(4, seen.Count);
            CollectionAssert.AreEqual(Kinds, seen[0]);
            CollectionAssert.AreEqual(Kinds.Take(2).ToArray(), seen[1]);
            CollectionAssert.AreEqual(Kinds.Take(2).ToArray(), seen[2]);
            CollectionAssert.AreEqual(Kinds, seen[3]);
        }

        [TestMethod]
        public void OnceGroupsFinishWithoutCancellationAndSingleGroupReusesRequest()
        {
            var request = Request();
            var calls = 0;
            var scheduler = new MultiTargetMonitoringScheduler(poll =>
            {
                Assert.AreSame(request, poll);
                calls++;
                return Result(poll);
            }, (delay, token) => Task.CompletedTask);
            var target = new MonitoringScheduleTarget(request,
                new[] { new MonitoringScheduleGroup(Kinds, TimeSpan.FromSeconds(60), true) }, TimeSpan.Zero);
            scheduler.Run(new[] { target }, new MonitoringConcurrencyPolicy(1), CancellationToken.None);
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public void OnceGroupIsRemovedWhilePeriodicGroupContinues()
        {
            var seen = new List<MonitoringPollKind[]>();
            using (var cancellation = new CancellationTokenSource())
            {
                var scheduler = new MultiTargetMonitoringScheduler(poll =>
                {
                    seen.Add(poll.Kinds.ToArray());
                    if (seen.Count == 3) cancellation.Cancel();
                    return Result(poll);
                }, (delay, token) => Task.CompletedTask);
                var groups = new[]
                {
                    new MonitoringScheduleGroup(new[] { MonitoringPollKind.Health, MonitoringPollKind.Interface }, TimeSpan.FromSeconds(60), false),
                    new MonitoringScheduleGroup(new[] { MonitoringPollKind.Lldp }, TimeSpan.FromSeconds(60), true)
                };
                scheduler.Run(new[] { new MonitoringScheduleTarget(Request(), groups, TimeSpan.Zero) },
                    new MonitoringConcurrencyPolicy(1), cancellation.Token);
            }
            Assert.AreEqual(3, seen.Count);
            CollectionAssert.AreEqual(Kinds, seen[0]);
            CollectionAssert.AreEqual(Kinds.Take(2).ToArray(), seen[1]);
            CollectionAssert.AreEqual(Kinds.Take(2).ToArray(), seen[2]);
        }

        [TestMethod]
        public void OnceTargetDoesNotHoldLaterCycles()
        {
            var once = Guid.NewGuid();
            var repeated = Guid.NewGuid();
            var tracker = new MonitoringCycleTracker(new[]
            {
                new MonitoringTarget(once, IPAddress.Parse("192.0.2.1"), true),
                new MonitoringTarget(repeated, IPAddress.Parse("192.0.2.2"))
            });
            Assert.IsFalse(tracker.TargetSkipped(once, Now));
            Assert.IsTrue(tracker.TargetCompleted(repeated, Now, true));
            Assert.IsTrue(tracker.TargetCompleted(repeated, Now.AddMinutes(1), true));
            Assert.AreEqual(2, tracker.LastCompleted.CycleNumber);
        }

        private static PollingPolicy Policy(bool active, PollingSchedule state, PollingSchedule topology) =>
            new PollingPolicy(Guid.NewGuid(), "Test", false, active, state, topology, new[] { 22 });

        private static MonitoringPollRequest Request() => new MonitoringPollRequest(IPAddress.Parse("192.0.2.3"), 161,
            SnmpVersion.V2C, new SnmpCommunityCredentials(new byte[] { 4, 5, 6 }), 1000, 0, 10, Kinds, Guid.NewGuid());

        private static MonitoringPollResult Result(MonitoringPollRequest request) => new MonitoringPollResult(request.Address,
            Now, Now, request.Kinds.Select(kind => new MonitoringPollStepResult(kind, true, null, null)));
    }
}
