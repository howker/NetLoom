using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using NetLoom.Application.Monitoring;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Observations;
using NetLoom.HostLogging;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Monitoring;
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

        private static readonly TimeSpan
            MultiTargetMaintenanceInterval =
                TimeSpan.FromSeconds(30);

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
                "smtp-readiness")
            {
                return RunSmtpReadiness();
            }

            if (options.Command ==
                "smtp-acceptance")
            {
                return RunSmtpAcceptance(
                    hostLog);
            }

            if (options.Command ==
                "delivery-status")
            {
                return RunDeliveryStatus(
                    options);
            }

            if (options.Command ==
                "schedule-set")
            {
                return RunScheduledSet(
                    options,
                    hostLog);
            }

            if (options.Command ==
                "schedule")
            {
                return RunScheduled(
                    options,
                    hostLog);
            }

            if (options.Command ==
                "discover")
            {
                return RunDiscovery(
                    options,
                    hostLog);
            }

            return PollOnce(
                options,
                hostLog);
        }

        private static int RunSmtpReadiness()
        {
            var configuration =
                EngineSmtpConfiguration
                    .ReadFromEnvironment();

            Console.WriteLine(
                "SMTP-READINESS: " +
                configuration.DiagnosticText);

            return configuration.IsReady
                ? 0
                : 8;
        }

        private static int RunSmtpAcceptance(
            HostLogManager hostLog)
        {
            var configuration =
                EngineSmtpConfiguration
                    .ReadFromEnvironment();

            if (!configuration.IsReady)
            {
                hostLog.Error(
                    "SMTP_ACCEPTANCE_CONFIG_NOT_READY reasons=" +
                    configuration.ReasonSummary);

                Console.Error.WriteLine(
                    "ERROR: SMTP_ACCEPTANCE_CONFIG_NOT_READY reasons=" +
                    configuration.ReasonSummary);

                return 5;
            }

            try
            {
                EngineInterfaceDegradationDelivery
                    .SendAcceptanceProbe(
                        configuration);

                hostLog.Info(
                    "SMTP_ACCEPTANCE_SUCCESS");

                Console.WriteLine(
                    "SUCCESS: SMTP_ACCEPTANCE");

                return 0;
            }
            catch (Exception exception)
            {
                hostLog.Error(
                    exception,
                    "SMTP_ACCEPTANCE_FAILED type=" +
                    exception.GetType().Name);

                Console.Error.WriteLine(
                    "ERROR: SMTP_ACCEPTANCE_FAILED type=" +
                    exception.GetType().Name);

                return 5;
            }
        }

        private static int RunDeliveryStatus(
            EngineCommandLine options)
        {
            var databasePath =
                EngineDatabasePathResolver.Resolve(
                    options.DatabasePath);

            if (!File.Exists(
                databasePath))
            {
                Console.Error.WriteLine(
                    "ERROR: DELIVERY_STATUS_DATABASE_NOT_FOUND");

                return 6;
            }

            var connectionFactory =
                new SqliteConnectionFactory(
                    databasePath);

            if (!new SqliteInterfaceDegradationDeliveryStatusSchemaProbe(
                connectionFactory)
                .IsCompatible())
            {
                Console.Error.WriteLine(
                    "ERROR: DELIVERY_STATUS_SCHEMA_UNSUPPORTED");

                return 7;
            }

            var nowUtc =
                DateTime.UtcNow;

            var reader =
                new SqliteInterfaceDegradationEventOutbox(
                    connectionFactory);

            var statuses =
                reader.ReadStatus(
                    options.DeliveryStatusLimit,
                    nowUtc);

            var ready = 0;
            var deferred = 0;
            var delivered = 0;

            foreach (var status in statuses)
            {
                switch (status.Kind)
                {
                    case InterfaceDegradationDeliveryStatusKind
                        .Ready:
                        ready++;
                        break;

                    case InterfaceDegradationDeliveryStatusKind
                        .Deferred:
                        deferred++;
                        break;

                    case InterfaceDegradationDeliveryStatusKind
                        .Delivered:
                        delivered++;
                        break;
                }

                WriteDeliveryStatus(
                    status);
            }

            Console.WriteLine(
                "DELIVERY-STATUS-SUMMARY: total=" +
                statuses.Count +
                " ready=" +
                ready +
                " deferred=" +
                deferred +
                " delivered=" +
                delivered +
                " nowUtc=" +
                nowUtc.ToString(
                    "o",
                    CultureInfo.InvariantCulture));

            return 0;
        }

        private static void WriteDeliveryStatus(
            InterfaceDegradationDeliveryStatus status)
        {
            Console.WriteLine(
                "DELIVERY-STATUS: state=" +
                status.Kind +
                " failures=" +
                status.FailureCount +
                " capturedUtc=" +
                status.Event.CapturedUtc.ToString(
                    "o",
                    CultureInfo.InvariantCulture) +
                " lastFailureUtc=" +
                FormatUtc(
                    status.LastFailureUtc) +
                " nextAttemptUtc=" +
                FormatUtc(
                    status.NextAttemptUtc) +
                " deliveredUtc=" +
                FormatUtc(
                    status.DeliveredUtc) +
                " transition=" +
                status.Event.TransitionKind +
                " ifIndex=" +
                status.Event.IfIndex +
                " deviceId=" +
                status.Event.DeviceId.ToString("D") +
                " eventKey=" +
                status.Event.EventKey);
        }

        private static string FormatUtc(
            DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString(
                    "o",
                    CultureInfo.InvariantCulture)
                : "none";
        }

        private static int RunDiscovery(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            var engine =
                EngineDiscoveryComposition
                    .CreateEngine();

            var request =
                EngineDiscoveryComposition
                    .CreateRequest(
                        options);

            using (var cancellation =
                new CancellationTokenSource())
            {
                if (options.ControlStdin)
                {
                    var control =
                        new EngineDiscoveryStdinControl(
                            cancellation,
                            Console.In,
                            Console.Out);

                    EngineMachineOutput
                        .WriteDiscoveryControlReady(
                            Console.Out);

                    control.StartReading();
                }

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
                        string.IsNullOrWhiteSpace(
                            options.DiscoveryCidr)
                            ? "DISCOVERY_STARTED range=" +
                                options.DiscoveryStartAddress +
                                "-" +
                                options.DiscoveryEndAddress +
                                " mask=" +
                                options.DiscoverySubnetMask
                            : "DISCOVERY_STARTED cidr=" +
                                options.DiscoveryCidr);

                    var completed =
                        EngineDiscoveryRunner.Run(
                            engine,
                            request,
                            cancellation.Token,
                            Console.Out);

                    hostLog.Info(
                        completed
                            ? "DISCOVERY_COMPLETED"
                            : "DISCOVERY_STOPPED");

                    return 0;
                }
                finally
                {
                    Console.CancelKeyPress -=
                        handler;
                }
            }
        }

        private static int PollOnce(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            var runtime =
                CreateRuntime(
                    options);

            EngineMachineOutput
                .WritePollStarted(
                    Console.Out);

            var result =
                runtime.PollOnce(
                    CreateRequest(
                        options));

            WritePollResult(
                result,
                hostLog);

            EngineMachineOutput
                .WritePollCompleted(
                    Console.Out,
                    result);

            RunObservationRetention(
                options,
                hostLog);

            RunInterfaceDegradationDelivery(
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

        private static int RunScheduledSet(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            var targets =
                EngineMonitoringTargetSetFile.Read(
                    options.TargetsFilePath,
                    (deviceId, address) =>
                        CreateRequest(
                            options,
                            deviceId,
                            address),
                    TimeSpan.FromSeconds(
                        options.IntervalSeconds),
                    TimeSpan.FromSeconds(
                        options.StartupJitterSeconds));

            var databasePath =
                EngineDatabasePathResolver.Resolve(
                    options.DatabasePath);

            EngineMonitoringComposition
                .InitializeDatabase(
                    databasePath);

            var interfaceDegradationPolicy =
                CreateInterfaceDegradationPolicy(
                    options);

            var runtimes =
                new ConcurrentDictionary<Guid, MonitoringRuntime>();

            using (var cancellation =
                new CancellationTokenSource())
            {
                EngineMultiTargetScheduleStdinControl control = null;

                if (options.ControlStdin)
                {
                    control =
                        new EngineMultiTargetScheduleStdinControl(
                            cancellation,
                            Console.In,
                            Console.Out);

                    EngineMachineOutput
                        .WriteControlReady(
                            Console.Out);

                    control.StartReading();
                }

                Func<MonitoringPollRequest, MonitoringPollResult> poll =
                    request =>
                    {
                        var deviceId =
                            request.DeviceId.Value;

                        var runtime =
                            runtimes.GetOrAdd(
                                deviceId,
                                ignored =>
                                    EngineMonitoringComposition
                                        .CreateForInitializedDatabase(
                                            databasePath,
                                            interfaceDegradationPolicy));

                        return runtime.PollOnce(
                            request);
                    };

                var runner =
                    control == null
                        ? new EngineMultiTargetMonitoringRunner(
                            poll)
                        : new EngineMultiTargetMonitoringRunner(
                            poll,
                            control.WaitAsync);

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
                        "SCHEDULER_SET_STARTED targets=" +
                        targets.Count +
                        " intervalSeconds=" +
                        options.IntervalSeconds +
                        " maxConcurrency=" +
                        options.MaxConcurrentPolls +
                        " startupJitterSeconds=" +
                        options.StartupJitterSeconds);

                    Console.WriteLine(
                        "SCHEDULER-SET: started targets=" +
                        targets.Count +
                        " intervalSeconds=" +
                        options.IntervalSeconds +
                        " maxConcurrency=" +
                        options.MaxConcurrentPolls);

                    EngineMachineOutput
                        .WriteScheduleSetStarted(
                            Console.Out,
                            targets.Count,
                            options.IntervalSeconds,
                            options.MaxConcurrentPolls,
                            options.StartupJitterSeconds);

                    var result =
                        runner.Run(
                            targets,
                            new MonitoringConcurrencyPolicy(
                                options.MaxConcurrentPolls),
                            cancellation.Token,
                            Console.Out,
                            (target, pollResult) =>
                            {
                                WritePollResult(
                                    pollResult,
                                    hostLog);
                            },
                            target =>
                            {
                                hostLog.Info(
                                    "SCHEDULER_BACKPRESSURE_SKIPPED deviceId=" +
                                    target.DeviceId.ToString("D"));
                            },
                            () =>
                            {
                                RunObservationRetention(
                                    options,
                                    hostLog);

                                RunInterfaceDegradationDelivery(
                                    options,
                                    hostLog);
                            },
                            MultiTargetMaintenanceInterval);

                    hostLog.Info(
                        "SCHEDULER_SET_STOPPED completedPolls=" +
                        result.CompletedPolls +
                        " backpressureSkips=" +
                        result.BackpressureSkips);

                    Console.WriteLine(
                        "SCHEDULER-SET: stopped polls=" +
                        result.CompletedPolls +
                        " skipped=" +
                        result.BackpressureSkips);

                    EngineMachineOutput
                        .WriteScheduleSetStopped(
                            Console.Out,
                            result.CompletedPolls,
                            result.BackpressureSkips);

                    return 0;
                }
                finally
                {
                    Console.CancelKeyPress -=
                        handler;
                }
            }
        }

        private static int RunScheduled(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            var runtime =
                CreateRuntime(
                    options);

            var request =
                CreateRequest(
                    options);

            using (var cancellation =
                new CancellationTokenSource())
            {
                EngineScheduleStdinControl control = null;

                if (options.ControlStdin)
                {
                    control =
                        new EngineScheduleStdinControl(
                            cancellation,
                            Console.In,
                            Console.Out);

                    EngineMachineOutput
                        .WriteControlReady(
                            Console.Out);

                    control.StartReading();
                }

                var scheduler =
                    options.ControlStdin
                        ? new MonitoringScheduler(
                            runtime,
                            (interval, token) =>
                            {
                                control.Wait(
                                    interval,
                                    token);
                            })
                        : new MonitoringScheduler(
                            runtime);

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

                    if (options.ControlStdin)
                    {
                        EngineMachineOutput
                            .WriteScheduleStarted(
                                Console.Out,
                                options.IntervalSeconds);
                    }

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

                                if (options.ControlStdin)
                                {
                                    EngineMachineOutput
                                        .WritePollCompleted(
                                            Console.Out,
                                            pollResult);
                                }

                                RunObservationRetention(
                                    options,
                                    hostLog);

                                RunInterfaceDegradationDelivery(
                                    options,
                                    hostLog);
                            },
                            () =>
                            {
                                if (options.ControlStdin)
                                {
                                    EngineMachineOutput
                                        .WritePollStarted(
                                            Console.Out);
                                }
                            });

                    hostLog.Info(
                        "SCHEDULER_STOPPED completedCycles=" +
                        result.CompletedCycles);

                    Console.WriteLine(
                        "SCHEDULER: stopped cycles=" +
                        result.CompletedCycles);

                    if (options.ControlStdin)
                    {
                        EngineMachineOutput
                            .WriteScheduleStopped(
                                Console.Out,
                                result.CompletedCycles);
                    }

                    return 0;
                }
                finally
                {
                    Console.CancelKeyPress -=
                        handler;
                }
            }
        }

        private static void RunInterfaceDegradationDelivery(
            EngineCommandLine options,
            HostLogManager hostLog)
        {
            try
            {
                var databasePath =
                    EngineDatabasePathResolver.Resolve(
                        options.DatabasePath);

                EngineInterfaceDegradationDelivery.Drain(
                    databasePath,
                    hostLog);
            }
            catch (Exception exception)
            {
                hostLog.Error(
                    exception,
                    "INTERFACE_DEGRADATION_DELIVERY_CONFIGURATION_FAILED");

                Console.Error.WriteLine(
                    "INTERFACE-DEGRADATION-DELIVERY: CONFIG FAIL " +
                    exception.Message);
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
                databasePath,
                CreateInterfaceDegradationPolicy(
                    options));
        }

        private static InterfaceDegradationPolicy
            CreateInterfaceDegradationPolicy(
                EngineCommandLine options)
        {
            if (!options
                    .InterfaceErrorRatePerMinuteThreshold
                    .HasValue &&
                !options
                    .InterfaceDiscardRatePerMinuteThreshold
                    .HasValue)
            {
                return null;
            }

            return new InterfaceDegradationPolicy(
                options
                    .InterfaceErrorRatePerMinuteThreshold,
                options
                    .InterfaceDiscardRatePerMinuteThreshold);
        }

        private static MonitoringPollRequest CreateRequest(
            EngineCommandLine options)
        {
            return CreateRequest(
                options,
                options.DeviceId,
                options.Address);
        }

        private static MonitoringPollRequest CreateRequest(
            EngineCommandLine options,
            Guid? deviceId,
            System.Net.IPAddress address)
        {
            var credentials =
                EngineSnmpCredentialFactory.Create(
                    options.Version);

            return new MonitoringPollRequest(
                address,
                options.Port,
                options.Version,
                credentials,
                options.TimeoutMilliseconds,
                options.RetryCount,
                options.MaxRepetitions,
                options.Kinds,
                deviceId);
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

                foreach (var classification in
                    step.InterfaceDegradationClassifications)
                {
                    WriteInterfaceDegradation(
                        classification);
                }

                foreach (var transition in
                    step.InterfaceDegradationTransitions)
                {
                    if (transition.HasStateChange)
                    {
                        WriteInterfaceDegradationTransition(
                            transition);
                    }
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

        private static void WriteInterfaceDegradation(
            InterfaceDegradationClassification classification)
        {
            Console.WriteLine(
                "INTERFACE-DEGRADATION: ifIndex=" +
                classification.IfIndex +
                " status=" +
                classification.Status +
                " errorRatePerMinute=" +
                FormatRate(
                    classification.ErrorRatePerMinute) +
                " discardRatePerMinute=" +
                FormatRate(
                    classification.DiscardRatePerMinute) +
                " reasons=" +
                (classification.Reasons.Count == 0
                    ? "none"
                    : string.Join(
                        ",",
                        classification.Reasons)) +
                " deviceId=" +
                classification.DeviceId.ToString("D"));
        }

        private static void
            WriteInterfaceDegradationTransition(
                InterfaceDegradationTransition transition)
        {
            Console.WriteLine(
                "INTERFACE-DEGRADATION-TRANSITION: ifIndex=" +
                transition.Classification.IfIndex +
                " kind=" +
                transition.Kind +
                " previous=" +
                (transition.PreviousState == null
                    ? "none"
                    : transition.PreviousState.Status.ToString()) +
                " current=" +
                transition.Classification.Status +
                " reasons=" +
                (transition.Classification.Reasons.Count == 0
                    ? "none"
                    : string.Join(
                        ",",
                        transition.Classification.Reasons)) +
                " deviceId=" +
                transition.Classification.DeviceId.ToString("D"));
        }

        private static string FormatRate(
            double? value)
        {
            return value.HasValue
                ? value.Value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                : "unknown";
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
