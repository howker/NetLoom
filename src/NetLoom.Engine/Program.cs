using System;
using System.Globalization;
using System.Threading;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Observations;
using NetLoom.HostLogging;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Observations;

namespace NetLoom.Engine
{
    internal static class Program
    {
        private static readonly TimeSpan
            RawObservationRetentionWindow =
                TimeSpan.FromHours(24);

        private const int
            RawObservationRetentionBatchSize = 8;

        private static int Main(
            string[] args)
        {
            HostLogManager hostLog = null;

            try
            {
                hostLog =
                    HostLogManager.Create(
                        "engine");

                hostLog.Info(
                    "HOST_STARTED");

                return Run(
                    args,
                    hostLog);
            }
            catch (Exception exception)
            {
                if (hostLog != null)
                {
                    hostLog.Error(
                        exception,
                        "HOST_FATAL");

                    hostLog.Flush();
                }

                Console.Error.WriteLine(
                    "ERROR: " +
                    exception.Message);

                return 2;
            }
            finally
            {
                if (hostLog != null)
                {
                    hostLog.Dispose();
                }
            }
        }

        private static int Run(
            string[] args,
            HostLogManager hostLog)
        {
            var options =
                EngineCommandLine.Parse(
                    args);

            if (options.Command ==
                "runtime-smoke")
            {
                if (!EngineRuntimeSmoke.Run())
                {
                    hostLog.Error(
                        "RUNTIME_SMOKE_FAILED");

                    Console.Error.WriteLine(
                        "ERROR: RUNTIME_SMOKE_FAILED");

                    return 4;
                }

                hostLog.Info(
                    "MONITORING_RUNTIME_SMOKE_SUCCESS");

                Console.WriteLine(
                    "SUCCESS: MONITORING_RUNTIME_SMOKE");

                return 0;
            }

            if (options.Command ==
                "schedule")
            {
                return RunScheduled(
                    options,
                    hostLog);
            }

            return PollOnce(
                options,
                hostLog);
        }

        private static int PollOnce(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            var runtime =
                CreateRuntime(
                    options);

            var result =
                runtime.PollOnce(
                    CreateRequest(
                        options));

            WritePollResult(
                result,
                hostLog);

            RunObservationRetention(
                options,
                hostLog);

            if (!result.AnySucceeded)
            {
                hostLog.Error(
                    "ALL_POLL_STEPS_FAILED");

                Console.Error.WriteLine(
                    "ERROR: ALL_POLL_STEPS_FAILED");

                return 3;
            }

            hostLog.Info(
                "POLL_ONCE_COMPLETED");

            Console.WriteLine(
                "SUCCESS: POLL_ONCE_COMPLETED");

            return 0;
        }

        private static int RunScheduled(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            var runtime =
                CreateRuntime(
                    options);

            var scheduler =
                new MonitoringScheduler(
                    runtime);

            var request =
                CreateRequest(
                    options);

            using (var cancellation =
                new CancellationTokenSource())
            {
                ConsoleCancelEventHandler handler =
                    (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        cancellation.Cancel();
                    };

                Console.CancelKeyPress +=
                    handler;

                try
                {
                    hostLog.Info(
                        "SCHEDULER_STARTED intervalSeconds=" +
                        options.IntervalSeconds);

                    Console.WriteLine(
                        "SCHEDULER: started intervalSeconds=" +
                        options.IntervalSeconds);

                    var result =
                        scheduler.Run(
                            request,
                            TimeSpan.FromSeconds(
                                options.IntervalSeconds),
                            cancellation.Token,
                            pollResult =>
                            {
                                WritePollResult(
                                    pollResult,
                                    hostLog);

                                RunObservationRetention(
                                    options,
                                    hostLog);
                            });

                    hostLog.Info(
                        "SCHEDULER_STOPPED completedCycles=" +
                        result.CompletedCycles);

                    Console.WriteLine(
                        "SCHEDULER: stopped cycles=" +
                        result.CompletedCycles);

                    return 0;
                }
                finally
                {
                    Console.CancelKeyPress -=
                        handler;
                }
            }
        }

        private static void RunObservationRetention(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            try
            {
                var databasePath =
                    EngineDatabasePathResolver.Resolve(
                        options.DatabasePath);

                IObservationRetentionStore retentionStore =
                    new SqliteObservationRetentionStore(
                        new SqliteConnectionFactory(
                            databasePath));

                var cutoffUtc =
                    DateTime.UtcNow.Subtract(
                        RawObservationRetentionWindow);

                var deleted =
                    retentionStore.DeleteOlderThan(
                        cutoffUtc,
                        RawObservationRetentionBatchSize);

                if (deleted > 0)
                {
                    hostLog.Info(
                        "RETENTION_DELETED observations=" +
                        deleted);

                    Console.WriteLine(
                        "RETENTION: deletedObservations=" +
                        deleted);
                }
            }
            catch (Exception exception)
            {
                hostLog.Error(
                    exception,
                    "RETENTION_FAILED");

                Console.Error.WriteLine(
                    "RETENTION: FAIL " +
                    exception.GetType().Name +
                    " " +
                    exception.Message);
            }
        }

        private static MonitoringRuntime CreateRuntime(
            EngineCommandLine options)
        {
            var databasePath =
                EngineDatabasePathResolver.Resolve(
                    options.DatabasePath);

            return EngineMonitoringComposition.Create(
                databasePath);
        }

        private static MonitoringPollRequest CreateRequest(
            EngineCommandLine options)
        {
            var credentials =
                EngineSnmpCredentialFactory.Create(
                    options.Version);

            return new MonitoringPollRequest(
                options.Address,
                options.Port,
                options.Version,
                credentials,
                options.TimeoutMilliseconds,
                options.RetryCount,
                options.MaxRepetitions,
                options.Kinds,
                options.DeviceId);
        }

        private static void WritePollResult(
            MonitoringPollResult result,
            HostLogManager hostLog)
        {
            foreach (var step in result.Steps)
            {
                if (step.Succeeded)
                {
                    Console.WriteLine(
                        "STEP: " +
                        step.Kind +
                        " OK");
                }
                else
                {
                    hostLog.Error(
                        "POLL_STEP_FAILED kind=" +
                        step.Kind +
                        " errorType=" +
                        (string.IsNullOrWhiteSpace(
                            step.ErrorType)
                            ? "Unknown"
                            : step.ErrorType));

                    Console.WriteLine(
                        "STEP: " +
                        step.Kind +
                        " FAIL " +
                        step.ErrorType);
                }

                if (step.HealthSnapshot != null)
                {
                    WriteHealth(
                        step.HealthSnapshot);
                }

                foreach (var snapshot in
                    step.InterfaceSnapshots)
                {
                    WriteInterface(
                        snapshot);
                }
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

            hostLog.Info(
                "POLL_COMPLETED success=" +
                succeeded +
                " failed=" +
                failed);

            Console.WriteLine(
                "POLL: success=" +
                succeeded +
                " failed=" +
                failed);
        }

        private static void WriteInterface(
            Application.Monitoring.Interfaces.InterfaceMonitoringSnapshot snapshot)
        {
            var device =
                snapshot.DeviceId.HasValue
                    ? snapshot.DeviceId.Value.ToString("D")
                    : "unbound";

            Console.WriteLine(
                "INTERFACE: ifIndex=" +
                snapshot.IfIndex +
                " admin=" +
                (snapshot.AdminStatus.HasValue
                    ? snapshot.AdminStatus.Value.ToString(
                        CultureInfo.InvariantCulture)
                    : "unknown") +
                " oper=" +
                (snapshot.OperStatus.HasValue
                    ? snapshot.OperStatus.Value.ToString(
                        CultureInfo.InvariantCulture)
                    : "unknown") +
                " deviceId=" +
                device);
        }

        private static void WriteHealth(
            Application.Monitoring.Health.HealthSnapshot snapshot)
        {
            var uptime =
                snapshot.Uptime.HasValue
                    ? snapshot.Uptime.Value.TotalSeconds.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    : "unknown";

            var device =
                snapshot.DeviceId.HasValue
                    ? snapshot.DeviceId.Value.ToString("D")
                    : "unbound";

            Console.WriteLine(
                "HEALTH: status=" +
                snapshot.Status +
                " uptimeSeconds=" +
                uptime +
                " deviceId=" +
                device);
        }
    }
}
