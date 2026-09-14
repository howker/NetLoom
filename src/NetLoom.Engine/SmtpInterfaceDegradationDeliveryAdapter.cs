using System;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using NetLoom.Application.Monitoring.Interfaces;

namespace NetLoom.Engine
{
    public sealed class SmtpInterfaceDegradationDeliveryAdapter :
        IInterfaceDegradationDeliveryAdapter
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _enableSsl;
        private readonly string _from;
        private readonly string _to;
        private readonly string _username;
        private readonly string _password;

        public SmtpInterfaceDegradationDeliveryAdapter(
            string host,
            int port,
            bool enableSsl,
            string from,
            string to,
            string username = null,
            string password = null)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException(
                    "SMTP_HOST_REQUIRED",
                    nameof(host));
            }

            if (port < 1 ||
                port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port));
            }

            if (string.IsNullOrWhiteSpace(from))
            {
                throw new ArgumentException(
                    "SMTP_FROM_REQUIRED",
                    nameof(from));
            }

            if (string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentException(
                    "SMTP_TO_REQUIRED",
                    nameof(to));
            }

            if (string.IsNullOrEmpty(username) !=
                string.IsNullOrEmpty(password))
            {
                throw new ArgumentException(
                    "SMTP_USERNAME_AND_PASSWORD_MUST_BE_CONFIGURED_TOGETHER");
            }

            _host = host;
            _port = port;
            _enableSsl = enableSsl;
            _from = from;
            _to = to;
            _username = username;
            _password = password;
        }

        public void Deliver(
            InterfaceDegradationOutboxEvent item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(
                    nameof(item));
            }

            using (var message =
                new MailMessage(
                    _from,
                    _to))
            {
                message.Subject =
                    BuildSubject(
                        item);

                message.Body =
                    BuildBody(
                        item);

                message.BodyEncoding =
                    Encoding.UTF8;

                message.SubjectEncoding =
                    Encoding.UTF8;

                using (var client =
                    new SmtpClient(
                        _host,
                        _port))
                {
                    client.EnableSsl =
                        _enableSsl;

                    if (!string.IsNullOrEmpty(
                        _username))
                    {
                        client.UseDefaultCredentials =
                            false;

                        client.Credentials =
                            new NetworkCredential(
                                _username,
                                _password);
                    }

                    client.Send(
                        message);
                }
            }
        }

        internal static string BuildSubject(
            InterfaceDegradationOutboxEvent item)
        {
            return
                "[NetLoom] Interface degradation " +
                item.TransitionKind +
                " " +
                item.DeviceId.ToString("D") +
                "/" +
                item.IfIndex.ToString(
                    CultureInfo.InvariantCulture);
        }

        internal static string BuildBody(
            InterfaceDegradationOutboxEvent item)
        {
            var reasons =
                item.Reasons.Count == 0
                    ? "none"
                    : string.Join(
                        ",",
                        item.Reasons);

            return
                "eventKey=" + item.EventKey + Environment.NewLine +
                "deviceId=" + item.DeviceId.ToString("D") + Environment.NewLine +
                "ifIndex=" + item.IfIndex.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "capturedUtc=" + item.CapturedUtc.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine +
                "transition=" + item.TransitionKind + Environment.NewLine +
                "previousStatus=" + (item.PreviousStatus.HasValue ? item.PreviousStatus.Value.ToString() : "none") + Environment.NewLine +
                "currentStatus=" + item.CurrentStatus + Environment.NewLine +
                "errorRatePerMinute=" + FormatRate(item.ErrorRatePerMinute) + Environment.NewLine +
                "discardRatePerMinute=" + FormatRate(item.DiscardRatePerMinute) + Environment.NewLine +
                "reasons=" + reasons;
        }

        private static string FormatRate(
            double? value)
        {
            return value.HasValue
                ? value.Value.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                : "unknown";
        }
    }
}
