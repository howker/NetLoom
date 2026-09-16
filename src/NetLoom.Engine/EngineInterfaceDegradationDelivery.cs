using System;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.HostLogging;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Monitoring;

namespace NetLoom.Engine
{
    internal static class EngineInterfaceDegradationDelivery
    {
        private const int BatchSize = 32;

        private static readonly
            InterfaceDegradationDeliveryRetryPolicy
                RetryPolicy =
                    new InterfaceDegradationDeliveryRetryPolicy(
                        TimeSpan.FromMinutes(1),
                        TimeSpan.FromHours(1));

        public static void Drain(
            string databasePath,
            HostLogManager hostLog)
        {
            if (hostLog == null)
            {
                throw new ArgumentNullException(
                    nameof(hostLog));
            }

            var adapter =
                CreateAdapter();

            if (adapter == null)
            {
                return;
            }

            var factory =
                new SqliteConnectionFactory(
                    databasePath);

            var dispatcher =
                new InterfaceDegradationOutboxDispatcher(
                    new SqliteInterfaceDegradationEventOutbox(
                        factory),
                    adapter,
                    RetryPolicy);

            try
            {
                var delivered =
                    dispatcher.DispatchPending(
                        BatchSize,
                        () => DateTime.UtcNow);

                if (delivered > 0)
                {
                    hostLog.Info(
                        "INTERFACE_DEGRADATION_DELIVERY_COMPLETED count=" +
                        delivered);

                    Console.WriteLine(
                        "INTERFACE-DEGRADATION-DELIVERY: delivered=" +
                        delivered);
                }
            }
            catch (Exception exception)
            {
                hostLog.Error(
                    exception,
                    "INTERFACE_DEGRADATION_DELIVERY_FAILED");

                Console.Error.WriteLine(
                    "INTERFACE-DEGRADATION-DELIVERY: FAIL " +
                    exception.GetType().Name +
                    " " +
                    exception.Message);
            }
        }

        public static void SendAcceptanceProbe(
            EngineSmtpConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(
                    nameof(configuration));
            }

            var adapter =
                CreateAdapter(
                    configuration);

            if (adapter == null)
            {
                throw new InvalidOperationException(
                    "SMTP_DELIVERY_NOT_CONFIGURED");
            }

            adapter.Deliver(
                new InterfaceDegradationOutboxEvent(
                    new Guid(
                        "00000000-0000-0000-0000-000000000034"),
                    1,
                    DateTime.UtcNow,
                    InterfaceDegradationTransitionKind
                        .FirstAppearance,
                    null,
                    string.Empty,
                    InterfaceDegradationStatus.Degraded,
                    "4",
                    1.0,
                    null,
                    new[]
                    {
                        InterfaceDegradationReason
                            .ErrorRateThresholdExceeded
                    }));
        }

        private static IInterfaceDegradationDeliveryAdapter
            CreateAdapter()
        {
            return CreateAdapter(
                EngineSmtpConfiguration
                    .ReadFromEnvironment());
        }

        private static IInterfaceDegradationDeliveryAdapter
            CreateAdapter(
                EngineSmtpConfiguration configuration)
        {
            if (!configuration.IsAnyConfigured)
            {
                return null;
            }

            if (!configuration.IsReady)
            {
                throw new InvalidOperationException(
                    "SMTP_CONFIGURATION_NOT_READY reasons=" +
                    configuration.ReasonSummary);
            }

            return new SmtpInterfaceDegradationDeliveryAdapter(
                configuration.Host,
                configuration.Port,
                configuration.EnableSsl,
                configuration.From,
                configuration.To,
                configuration.Username,
                configuration.Password);
        }

    }
}
