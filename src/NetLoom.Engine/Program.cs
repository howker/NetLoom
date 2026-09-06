using System;
using System.Globalization;
using System.Threading;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Observations;
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
            try
            {
                var options =
                    EngineCommandLine.Parse(
                        args);

                if (options.Command ==
                    "runtime-smoke")
                {
                    if (!EngineRuntimeSmoke.Run())
                    {
                        Console.Error.WriteLine(
                            "ERROR: RUNTIME_SMOKE_FAILED");

                        return 4;
                    }

                    Console.WriteLine(
                        "SUCCESS: MONITORING_RUNTIME_SMOKE");

                    return 0;
                }

                if (options.Command ==
                    "schedule")
                {
                    return RunScheduled(
                        options);
                }

                return PollOnce(options);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "ERROR: " +
                    exception.Message);

                return 2;
            }
        }

        private static int PollOnce(
            EngineCommandLine options)
        {
            var runtime =
                CreateRuntime(
                    options);

            var result =
                runtime.PollOnce(
                    CreateRequest(
                        options));

            WritePollResult(
                result);

            RunObservationRetention(
                options);

            if (!result.AnySucceeded)
            {
                Console.Error.WriteLine(
                    "ERROR: ALL_POLL_STEPS_FAILED");

                return 3;
            }

            Console.WriteLine(
                "SUCCESS: POLL_ONCE_COMPLETED");

            return 0;
        }

        private static int RunScheduled(
            EngineCommandLine options)
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
                                    pollResult);

                                RunObservationRetention(
                                    options);
                            });

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
            EngineCommandLine options)
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
                    Console.WriteLine(
                        "RETENTION: deletedObservations=" +
                        deleted);
                }
            }
            catch (Exception exception)
            {
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
            MonitoringPollResult result)
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

            Console.WriteLine(
                "POLL: success=" +
                succeeded +
                " failed=" +
                (result.Steps.Count - succeeded));
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
