using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Protocols.Snmp.Transport;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class SnmpTransportTests
    {
        [TestMethod]
        public void V3PrivacyRequiresAuthentication()
        {
            try
            {
                new SnmpV3Credentials(
                    "operator",
                    SnmpAuthenticationProtocol.None,
                    null,
                    SnmpPrivacyProtocol.Aes,
                    Encoding.UTF8.GetBytes("privacy123"),
                    null);

                Assert.Fail(
                    "Privacy without authentication must fail.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void CommunityCredentialsCloneSecret()
        {
            var source = Encoding.ASCII.GetBytes("public");
            var credentials =
                new SnmpCommunityCredentials(source);

            source[0] = 0;

            var restored = credentials.GetCommunityBytes();

            Assert.AreEqual(
                (byte)'p',
                restored[0]);

            restored[0] = 0;

            Assert.AreEqual(
                (byte)'p',
                credentials.GetCommunityBytes()[0]);
        }

        [TestMethod]
        public void TimeoutRetriesRequestedNumberOfTimes()
        {
            using (var receiver = new UdpClient(
                new IPEndPoint(
                    IPAddress.Loopback,
                    0)))
            {
                var endpoint =
                    (IPEndPoint)receiver.Client.LocalEndPoint;

                var received = 0;
                var stop = false;

                var thread = new Thread(
                    () =>
                    {
                        receiver.Client.ReceiveTimeout = 100;

                        while (!stop)
                        {
                            try
                            {
                                IPEndPoint remote = null;
                                receiver.Receive(ref remote);
                                Interlocked.Increment(
                                    ref received);
                            }
                            catch (SocketException)
                            {
                            }
                            catch (ObjectDisposedException)
                            {
                                return;
                            }
                        }
                    });

                thread.IsBackground = true;
                thread.Start();

                var transport = new SharpSnmpTransport();

                var request = new SnmpGetRequest(
                    IPAddress.Loopback,
                    endpoint.Port,
                    SnmpVersion.V2C,
                    new SnmpCommunityCredentials(
                        Encoding.ASCII.GetBytes("public")),
                    new[] { "1.3.6.1.2.1.1.1.0" },
                    150,
                    2);

                try
                {
                    transport.Get(request);
                    Assert.Fail(
                        "Request must time out.");
                }
                catch (SnmpTransportException exception)
                {
                    Assert.AreEqual(
                        SnmpTransportFailure.Timeout,
                        exception.Failure);
                }
                finally
                {
                    Thread.Sleep(100);
                    stop = true;
                    receiver.Close();
                    thread.Join(1000);
                }

                Assert.AreEqual(3, received);
            }
        }
    }
}
