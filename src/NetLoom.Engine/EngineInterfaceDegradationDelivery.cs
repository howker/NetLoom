using System;
using System.Globalization;
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

        private static IInterfaceDegradationDeliveryAdapter
            CreateAdapter()
        {
            var host =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_HOST");

            var from =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_FROM");

            var to =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_TO");

            var portText =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_PORT");

            var sslText =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_ENABLE_SSL");

            var username =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_USERNAME");

            var password =
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_PASSWORD");

            var anyConfigured =
                !string.IsNullOrWhiteSpace(host) ||
                !string.IsNullOrWhiteSpace(from) ||
                !string.IsNullOrWhiteSpace(to) ||
                !string.IsNullOrWhiteSpace(portText) ||
                !string.IsNullOrWhiteSpace(sslText) ||
                !string.IsNullOrWhiteSpace(username) ||
                !string.IsNullOrWhiteSpace(password);

            if (!anyConfigured)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(from) ||
                string.IsNullOrWhiteSpace(to))
            {
                throw new InvalidOperationException(
                    "SMTP_DELIVERY_REQUIRES_NETLOOM_SMTP_HOST_FROM_TO");
            }

            var port = 25;

            if (!string.IsNullOrWhiteSpace(portText) &&
                (!int.TryParse(
                    portText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out port) ||
                 port < 1 ||
                 port > 65535))
            {
                throw new InvalidOperationException(
                    "INVALID_NETLOOM_SMTP_PORT");
            }

            var enableSsl = false;

            if (!string.IsNullOrWhiteSpace(sslText) &&
                !bool.TryParse(
                    sslText,
                    out enableSsl))
            {
                throw new InvalidOperationException(
                    "INVALID_NETLOOM_SMTP_ENABLE_SSL");
            }

            if (string.IsNullOrEmpty(username) !=
                string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    "SMTP_USERNAME_AND_PASSWORD_MUST_BE_CONFIGURED_TOGETHER");
            }

            return new SmtpInterfaceDegradationDeliveryAdapter(
                host,
                port,
                enableSsl,
                from,
                to,
                username,
                password);
        }
    }
}
