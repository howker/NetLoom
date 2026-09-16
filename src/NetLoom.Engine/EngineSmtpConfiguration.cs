using System;
using System.Collections.Generic;
using System.Globalization;

namespace NetLoom.Engine
{
    internal enum EngineSmtpConfigurationState
    {
        NotConfigured = 0,
        NotReady = 1,
        Ready = 2
    }

    internal sealed class EngineSmtpConfiguration
    {
        private EngineSmtpConfiguration(
            EngineSmtpConfigurationState state,
            IReadOnlyList<string> reasons,
            string host,
            int port,
            bool enableSsl,
            string from,
            string to,
            string username,
            string password,
            string portMode,
            string sslMode,
            string authMode)
        {
            State = state;
            Reasons = reasons;
            Host = host;
            Port = port;
            EnableSsl = enableSsl;
            From = from;
            To = to;
            Username = username;
            Password = password;
            PortMode = portMode;
            SslMode = sslMode;
            AuthMode = authMode;
        }

        public EngineSmtpConfigurationState State { get; }

        public IReadOnlyList<string> Reasons { get; }

        public bool IsReady
        {
            get
            {
                return State ==
                    EngineSmtpConfigurationState.Ready;
            }
        }

        public bool IsAnyConfigured
        {
            get
            {
                return State !=
                    EngineSmtpConfigurationState.NotConfigured;
            }
        }

        public string Host { get; }

        public int Port { get; }

        public bool EnableSsl { get; }

        public string From { get; }

        public string To { get; }

        public string Username { get; }

        public string Password { get; }

        public string PortMode { get; }

        public string SslMode { get; }

        public string AuthMode { get; }

        public string ReasonSummary
        {
            get
            {
                return Reasons.Count == 0
                    ? "NONE"
                    : string.Join(
                        ",",
                        Reasons);
            }
        }

        public string DiagnosticText
        {
            get
            {
                return
                    "state=" +
                    State +
                    " reasons=" +
                    ReasonSummary +
                    " port=" +
                    PortMode +
                    " ssl=" +
                    SslMode +
                    " auth=" +
                    AuthMode;
            }
        }

        public static EngineSmtpConfiguration
            ReadFromEnvironment()
        {
            return Evaluate(
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_HOST"),
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_PORT"),
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_ENABLE_SSL"),
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_FROM"),
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_TO"),
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_USERNAME"),
                Environment.GetEnvironmentVariable(
                    "NETLOOM_SMTP_PASSWORD"));
        }

        internal static EngineSmtpConfiguration Evaluate(
            string host,
            string portText,
            string sslText,
            string from,
            string to,
            string username,
            string password)
        {
            var anyConfigured =
                !string.IsNullOrWhiteSpace(host) ||
                !string.IsNullOrWhiteSpace(portText) ||
                !string.IsNullOrWhiteSpace(sslText) ||
                !string.IsNullOrWhiteSpace(from) ||
                !string.IsNullOrWhiteSpace(to) ||
                !string.IsNullOrWhiteSpace(username) ||
                !string.IsNullOrWhiteSpace(password);

            var reasons =
                new List<string>();

            if (string.IsNullOrWhiteSpace(host))
            {
                reasons.Add(
                    "MISSING_NETLOOM_SMTP_HOST");
            }

            if (string.IsNullOrWhiteSpace(from))
            {
                reasons.Add(
                    "MISSING_NETLOOM_SMTP_FROM");
            }

            if (string.IsNullOrWhiteSpace(to))
            {
                reasons.Add(
                    "MISSING_NETLOOM_SMTP_TO");
            }

            var port = 25;
            var portMode = "default-valid";

            if (!string.IsNullOrWhiteSpace(portText))
            {
                if (!int.TryParse(
                        portText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out port) ||
                    port < 1 ||
                    port > 65535)
                {
                    reasons.Add(
                        "INVALID_NETLOOM_SMTP_PORT");

                    port = 25;
                    portMode = "configured-invalid";
                }
                else
                {
                    portMode = "configured-valid";
                }
            }

            var enableSsl = false;
            var sslMode = "default-valid";

            if (!string.IsNullOrWhiteSpace(sslText))
            {
                if (!bool.TryParse(
                    sslText,
                    out enableSsl))
                {
                    reasons.Add(
                        "INVALID_NETLOOM_SMTP_ENABLE_SSL");

                    enableSsl = false;
                    sslMode = "configured-invalid";
                }
                else
                {
                    sslMode = "configured-valid";
                }
            }

            string authMode;

            if (string.IsNullOrEmpty(username) &&
                string.IsNullOrEmpty(password))
            {
                authMode = "none";
            }
            else if (string.IsNullOrEmpty(username) !=
                string.IsNullOrEmpty(password))
            {
                reasons.Add(
                    "NETLOOM_SMTP_USERNAME_PASSWORD_PAIR_REQUIRED");

                authMode = "incomplete";
            }
            else
            {
                authMode = "paired";
            }

            EngineSmtpConfigurationState state;

            if (!anyConfigured)
            {
                state =
                    EngineSmtpConfigurationState
                        .NotConfigured;
            }
            else if (reasons.Count > 0)
            {
                state =
                    EngineSmtpConfigurationState
                        .NotReady;
            }
            else
            {
                state =
                    EngineSmtpConfigurationState
                        .Ready;
            }

            return new EngineSmtpConfiguration(
                state,
                reasons.AsReadOnly(),
                host,
                port,
                enableSsl,
                from,
                to,
                username,
                password,
                portMode,
                sslMode,
                authMode);
        }
    }
}
