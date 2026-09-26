using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring;
using NetLoom.Application.MonitoringControl;
using NetLoom.Desktop.Discovery;
using NetLoom.Desktop.Monitoring;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint45MonitoringProfilePropagationTests
    {
        [TestMethod]
        public async Task ScheduleSetPassesSelectedProfileSecretViaEnvironmentOnly()
        {
            var profileId =
                Guid.Parse(
                    "51515151-5151-5151-5151-515151515151");

            var environmentProvider =
                new FakeEnvironmentProvider(
                    profileId,
                    SnmpVersion.V1,
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        {
                            "NETLOOM_SNMP_COMMUNITY",
                            "field-secret"
                        },
                        {
                            "NETLOOM_SNMP_USERNAME",
                            string.Empty
                        }
                    });

            var factory =
                new FakeEngineProcessFactory();

            using (var control =
                new DesktopEngineMonitoringControl(
                    "NetLoom.Engine.exe",
                    @"C:\Net Loom\netloom.db",
                    environmentProvider,
                    factory))
            {
                var policy =
                    new MonitoringSessionPolicy(
                        TimeSpan.FromSeconds(60),
                        SnmpVersion.V1,
                        161,
                        2000,
                        1,
                        25,
                        new[]
                        {
                            MonitoringPollKind.Lldp,
                            MonitoringPollKind.Interface
                        },
                        null,
                        null,
                        profileId);

                Assert.AreEqual(
                    profileId,
                    policy.AccessProfileId);

                var target =
                    new MonitoringTarget(
                        Guid.Parse(
                            "61616161-6161-6161-6161-616161616161"),
                        IPAddress.Parse(
                            "192.0.2.61"));

                var start =
                    control.StartSetAsync(
                        new[]
                        {
                            target
                        },
                        policy,
                        new MonitoringTargetSetPolicy(
                            1,
                            TimeSpan.Zero),
                        CancellationToken.None);

                Assert.AreEqual(
                    1,
                    environmentProvider.CallCount);

                Assert.AreEqual(
                    profileId,
                    environmentProvider.LastAccessProfileId);

                Assert.AreEqual(
                    SnmpVersion.V1,
                    environmentProvider.LastVersion);

                Assert.AreEqual(
                    "field-secret",
                    factory.LastRequest
                        .EnvironmentVariables[
                            "NETLOOM_SNMP_COMMUNITY"]);

                Assert.IsFalse(
                    factory.LastRequest.Arguments.Contains(
                        "field-secret"),
                    "SNMP secrets must never be placed on the command line.");

                factory.LastSession.EmitOutput(
                    "NETLOOM_SCHEDULE_SET state=started targets=1 intervalSeconds=60 maxConcurrency=1 startupJitterSeconds=0");

                await start;

                var stop =
                    control.StopAsync(
                        CancellationToken.None);

                factory.LastSession.Complete(
                    0);

                await stop;
            }
        }

        [TestMethod]
        public void PolicyRejectsEmptyAccessProfileId()
        {
            try
            {
                new MonitoringSessionPolicy(
                    TimeSpan.FromSeconds(60),
                    SnmpVersion.V2C,
                    161,
                    2000,
                    1,
                    25,
                    new[]
                    {
                        MonitoringPollKind.Interface
                    },
                    null,
                    null,
                    Guid.Empty);

                Assert.Fail(
                    "Empty monitoring access profile id should be rejected.");
            }
            catch (ArgumentException error)
            {
                Assert.AreEqual(
                    "accessProfileId",
                    error.ParamName);
            }
        }

        private sealed class FakeEnvironmentProvider :
            IDiscoveryProcessEnvironmentProvider
        {
            private readonly Guid _expectedAccessProfileId;
            private readonly SnmpVersion _expectedVersion;
            private readonly IReadOnlyDictionary<string, string>
                _environment;

            public FakeEnvironmentProvider(
                Guid expectedAccessProfileId,
                SnmpVersion expectedVersion,
                IReadOnlyDictionary<string, string> environment)
            {
                _expectedAccessProfileId =
                    expectedAccessProfileId;
                _expectedVersion =
                    expectedVersion;
                _environment =
                    environment;
            }

            public int CallCount { get; private set; }

            public Guid LastAccessProfileId { get; private set; }

            public SnmpVersion LastVersion { get; private set; }

            public IReadOnlyDictionary<string, string> CreateEnvironment(
                Guid accessProfileId,
                SnmpVersion version)
            {
                CallCount++;
                LastAccessProfileId =
                    accessProfileId;
                LastVersion =
                    version;

                if (accessProfileId !=
                        _expectedAccessProfileId ||
                    version !=
                        _expectedVersion)
                {
                    throw new InvalidOperationException(
                        "Unexpected profile environment request.");
                }

                return _environment;
            }
        }

        private sealed class FakeEngineProcessFactory :
            IEngineProcessFactory
        {
            public EngineProcessStartRequest LastRequest { get; private set; }

            public FakeEngineProcessSession LastSession { get; private set; }

            public IEngineProcessSession Start(
                EngineProcessStartRequest request)
            {
                LastRequest =
                    request;

                LastSession =
                    new FakeEngineProcessSession();

                return LastSession;
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeEngineProcessSession :
            IEngineProcessSession
        {
            private readonly TaskCompletionSource<int> _completion =
                new TaskCompletionSource<int>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public event Action<string> OutputLineReceived;

            public event Action<string> ErrorLineReceived;

            public Task<int> Completion =>
                _completion.Task;

            public void BeginRead()
            {
            }

            public void WriteLine(
                string line)
            {
            }

            public void Terminate()
            {
                _completion.TrySetResult(
                    -1);
            }

            public void EmitOutput(
                string line)
            {
                OutputLineReceived?.Invoke(
                    line);
            }

            public void Complete(
                int exitCode)
            {
                _completion.TrySetResult(
                    exitCode);
            }

            public void Dispose()
            {
            }
        }
    }
}
