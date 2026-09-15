using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Engine;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint34HSmtpAcceptanceTests
    {
        [TestMethod]
        public void SmtpAdapterDeliversSyntheticCanaryToControlledRelay()
        {
            using (var relay =
                new ControlledSmtpRelay())
            {
                var capturedUtc =
                    new DateTime(
                        2026, 1, 16, 12, 0, 0,
                        DateTimeKind.Utc);

                var item =
                    new InterfaceDegradationOutboxEvent(
                        new Guid(
                            "eeeeeeee-ffff-0000-0000-000000000001"),
                        17,
                        capturedUtc,
                        InterfaceDegradationTransitionKind
                            .FirstAppearance,
                        null,
                        string.Empty,
                        InterfaceDegradationStatus
                            .Degraded,
                        "4",
                        2.0,
                        null,
                        new[]
                        {
                            InterfaceDegradationReason
                                .ErrorRateThresholdExceeded
                        });

                new SmtpInterfaceDegradationDeliveryAdapter(
                    "127.0.0.1",
                    relay.Port,
                    false,
                    "netloom@example.invalid",
                    "operator@example.invalid")
                    .Deliver(
                        item);

                var message =
                    relay.WaitForMessage();

                var body =
                    DecodeBody(
                        message);

                StringAssert.Contains(
                    body,
                    item.EventKey);

                StringAssert.Contains(
                    body,
                    "currentStatus=Degraded");

                StringAssert.Contains(
                    message,
                    "Subject: [NetLoom] Interface degradation");
            }
        }

        private static string DecodeBody(
            string message)
        {
            var separator =
                "\r\n\r\n";

            var separatorIndex =
                message.IndexOf(
                    separator,
                    StringComparison.Ordinal);

            Assert.IsTrue(
                separatorIndex >= 0,
                "SMTP message must contain a MIME header/body separator.");

            var headers =
                message.Substring(
                    0,
                    separatorIndex);

            StringAssert.Contains(
                headers,
                "Content-Transfer-Encoding: base64");

            var encodedBody =
                message.Substring(
                        separatorIndex +
                        separator.Length)
                    .Replace(
                        "\r",
                        string.Empty)
                    .Replace(
                        "\n",
                        string.Empty)
                    .Trim();

            return Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    encodedBody));
        }

        private sealed class ControlledSmtpRelay :
            IDisposable
        {
            private readonly TcpListener
                _listener;

            private readonly Task<string>
                _messageTask;

            public ControlledSmtpRelay()
            {
                _listener =
                    new TcpListener(
                        IPAddress.Loopback,
                        0);

                _listener.Start();

                Port =
                    ((IPEndPoint)
                        _listener.LocalEndpoint)
                        .Port;

                _messageTask =
                    Task.Run(
                        ReceiveMessage);
            }

            public int Port { get; }

            public string WaitForMessage()
            {
                if (!_messageTask.Wait(
                    TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException(
                        "Controlled SMTP relay did not receive a message.");
                }

                return _messageTask
                    .GetAwaiter()
                    .GetResult();
            }

            public void Dispose()
            {
                _listener.Stop();
            }

            private string ReceiveMessage()
            {
                using (var client =
                    _listener.AcceptTcpClient())
                using (var stream =
                    client.GetStream())
                using (var reader =
                    new StreamReader(
                        stream,
                        Encoding.ASCII,
                        false,
                        1024,
                        true))
                using (var writer =
                    new StreamWriter(
                        stream,
                        Encoding.ASCII,
                        1024,
                        true))
                {
                    stream.ReadTimeout = 10000;
                    stream.WriteTimeout = 10000;

                    writer.NewLine = "\r\n";
                    writer.AutoFlush = true;

                    writer.WriteLine(
                        "220 localhost NetLoom controlled relay");

                    var message =
                        new StringBuilder();

                    var readingData = false;

                    while (true)
                    {
                        var line =
                            reader.ReadLine();

                        if (line == null)
                        {
                            break;
                        }

                        if (readingData)
                        {
                            if (line == ".")
                            {
                                readingData = false;

                                writer.WriteLine(
                                    "250 2.0.0 accepted");

                                continue;
                            }

                            message.AppendLine(
                                line);

                            continue;
                        }

                        if (line.StartsWith(
                            "EHLO ",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine(
                                "250-localhost");

                            writer.WriteLine(
                                "250 8BITMIME");

                            continue;
                        }

                        if (line.StartsWith(
                            "HELO ",
                            StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith(
                                "MAIL FROM:",
                                StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith(
                                "RCPT TO:",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine(
                                "250 2.0.0 OK");

                            continue;
                        }

                        if (string.Equals(
                            line,
                            "DATA",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine(
                                "354 End data with <CR><LF>.<CR><LF>");

                            readingData = true;
                            continue;
                        }

                        if (string.Equals(
                            line,
                            "QUIT",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine(
                                "221 2.0.0 bye");

                            break;
                        }

                        writer.WriteLine(
                            "250 2.0.0 OK");
                    }

                    return message.ToString();
                }
            }
        }
    }
}
