using System;
using NetLoom.Application.Monitoring;

namespace NetLoom.Engine
{
    internal static class Program
    {
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
            var databasePath =
                EngineDatabasePathResolver.Resolve(
                    options.DatabasePath);

            var credentials =
                EngineSnmpCredentialFactory.Create(
                    options.Version);

            var runtime =
                EngineMonitoringComposition.Create(
                    databasePath);

            var result =
                runtime.PollOnce(
                    new MonitoringPollRequest(
                        options.Address,
                        options.Port,
                        options.Version,
                        credentials,
                        options.TimeoutMilliseconds,
                        options.RetryCount,
                        options.MaxRepetitions,
                        options.Kinds));

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
    }
}
