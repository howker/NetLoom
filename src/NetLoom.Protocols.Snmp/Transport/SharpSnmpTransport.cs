using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using SharpSnmpTimeoutException =
    Lextm.SharpSnmpLib.Messaging.TimeoutException;

namespace NetLoom.Protocols.Snmp.Transport
{
    public sealed class SharpSnmpTransport : ISnmpTransport
    {
        public IReadOnlyList<SnmpVariable> Get(
            SnmpGetRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return ExecuteWithRetry(
                request.RetryCount,
                () => GetOnce(request));
        }

        public IReadOnlyList<SnmpVariable> Walk(
            SnmpWalkRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return ExecuteWithRetry(
                request.RetryCount,
                () => WalkOnce(request));
        }

        private static IReadOnlyList<SnmpVariable> ExecuteWithRetry(
            int retryCount,
            Func<IReadOnlyList<SnmpVariable>> action)
        {
            var attempts = retryCount + 1;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    return action();
                }
                catch (SharpSnmpTimeoutException exception)
                {
                    if (attempt < attempts)
                    {
                        continue;
                    }

                    throw new SnmpTransportException(
                        SnmpTransportFailure.Timeout,
                        "SNMP request timed out.",
                        exception);
                }
                catch (SocketException exception)
                {
                    throw new SnmpTransportException(
                        SnmpTransportFailure.Socket,
                        "SNMP socket error.",
                        exception);
                }
                catch (SnmpException exception)
                {
                    throw new SnmpTransportException(
                        SnmpTransportFailure.Protocol,
                        "SNMP protocol error.",
                        exception);
                }
            }

            throw new InvalidOperationException(
                "SNMP request loop finished unexpectedly.");
        }

        private static IReadOnlyList<SnmpVariable> GetOnce(
            SnmpGetRequest request)
        {
            var endpoint = new IPEndPoint(
                request.Address,
                request.Port);

            var variables = request.Oids
                .Select(
                    oid => new Variable(
                        new ObjectIdentifier(oid)))
                .ToList();

            if (request.Version == SnmpVersion.V3)
            {
                return GetV3(
                    request,
                    endpoint,
                    variables);
            }

            var credentials =
                request.Credentials as SnmpCommunityCredentials;

            if (credentials == null)
            {
                throw UnsupportedCredentials();
            }

            var version = request.Version == SnmpVersion.V1
                ? VersionCode.V1
                : VersionCode.V2;

            var result = Messenger.Get(
                version,
                endpoint,
                new OctetString(
                    credentials.GetCommunityBytes()),
                variables,
                request.TimeoutMilliseconds);

            return result.Select(ToVariable).ToArray();
        }

        private static IReadOnlyList<SnmpVariable> WalkOnce(
            SnmpWalkRequest request)
        {
            var endpoint = new IPEndPoint(
                request.Address,
                request.Port);

            var root = new ObjectIdentifier(request.RootOid);
            var result = new List<Variable>();

            if (request.Version == SnmpVersion.V3)
            {
                WalkV3(request, endpoint, root, result);

                return result
                    .Select(ToVariable)
                    .ToArray();
            }

            var credentials =
                request.Credentials as SnmpCommunityCredentials;

            if (credentials == null)
            {
                throw UnsupportedCredentials();
            }

            var community = new OctetString(
                credentials.GetCommunityBytes());

            if (request.Version == SnmpVersion.V1)
            {
                Messenger.Walk(
                    VersionCode.V1,
                    endpoint,
                    community,
                    root,
                    result,
                    request.TimeoutMilliseconds,
                    WalkMode.WithinSubtree);
            }
            else
            {
                Messenger.BulkWalk(
                    VersionCode.V2,
                    endpoint,
                    community,
                    new OctetString(string.Empty),
                    root,
                    result,
                    request.TimeoutMilliseconds,
                    request.MaxRepetitions,
                    WalkMode.WithinSubtree,
                    null,
                    null);
            }

            return result
                .Select(ToVariable)
                .ToArray();
        }

        private static IReadOnlyList<SnmpVariable> GetV3(
            SnmpGetRequest request,
            IPEndPoint endpoint,
            IList<Variable> variables)
        {
            var credentials =
                request.Credentials as SnmpV3Credentials;

            if (credentials == null)
            {
                throw UnsupportedCredentials();
            }

            var authentication =
                CreateAuthenticationProvider(credentials);

            var privacy =
                CreatePrivacyProvider(
                    credentials,
                    authentication);

            var discovery =
                Messenger.GetNextDiscovery(
                    SnmpType.GetRequestPdu);

            var report = discovery.GetResponse(
                request.TimeoutMilliseconds,
                endpoint);

            var message = CreateV3Request(
                credentials,
                variables,
                privacy,
                report);

            var reply = message.GetResponse(
                request.TimeoutMilliseconds,
                endpoint);

            if (reply is ReportMessage &&
                reply.Pdu().Variables.Count > 0 &&
                reply.Pdu().Variables[0].Id ==
                Messenger.NotInTimeWindow)
            {
                message = CreateV3Request(
                    credentials,
                    variables,
                    privacy,
                    (ReportMessage)reply);

                reply = message.GetResponse(
                    request.TimeoutMilliseconds,
                    endpoint);
            }

            if (reply is ReportMessage)
            {
                throw new SnmpTransportException(
                    SnmpTransportFailure.Protocol,
                    "SNMP v3 agent returned a report message.",
                    null);
            }

            var pdu = reply.Pdu();

            if (pdu.ErrorStatus.ToInt32() != 0)
            {
                throw new SnmpTransportException(
                    SnmpTransportFailure.Protocol,
                    "SNMP agent returned an error response.",
                    null);
            }

            return pdu.Variables
                .Select(ToVariable)
                .ToArray();
        }

        private static void WalkV3(
            SnmpWalkRequest request,
            IPEndPoint endpoint,
            ObjectIdentifier root,
            IList<Variable> result)
        {
            var credentials =
                request.Credentials as SnmpV3Credentials;

            if (credentials == null)
            {
                throw UnsupportedCredentials();
            }

            var authentication =
                CreateAuthenticationProvider(credentials);

            var privacy =
                CreatePrivacyProvider(
                    credentials,
                    authentication);

            var discovery =
                Messenger.GetNextDiscovery(
                    SnmpType.GetBulkRequestPdu);

            var report = discovery.GetResponse(
                request.TimeoutMilliseconds,
                endpoint);

            Messenger.BulkWalk(
                VersionCode.V3,
                endpoint,
                new OctetString(credentials.Username),
                new OctetString(credentials.ContextName),
                root,
                result,
                request.TimeoutMilliseconds,
                request.MaxRepetitions,
                WalkMode.WithinSubtree,
                privacy,
                report);
        }

        private static GetRequestMessage CreateV3Request(
            SnmpV3Credentials credentials,
            IList<Variable> variables,
            IPrivacyProvider privacy,
            ReportMessage report)
        {
            return new GetRequestMessage(
                VersionCode.V3,
                Messenger.NextMessageId,
                Messenger.NextRequestId,
                new OctetString(credentials.Username),
                new OctetString(credentials.ContextName),
                variables,
                privacy,
                Messenger.MaxMessageSize,
                report);
        }

        private static IAuthenticationProvider
            CreateAuthenticationProvider(
                SnmpV3Credentials credentials)
        {
            switch (credentials.AuthenticationProtocol)
            {
                case SnmpAuthenticationProtocol.None:
                    return DefaultAuthenticationProvider.Instance;

                case SnmpAuthenticationProtocol.Md5:
#pragma warning disable 0618
                    return new MD5AuthenticationProvider(
                        new OctetString(
                            credentials.GetAuthenticationPassword()));
#pragma warning restore 0618

                case SnmpAuthenticationProtocol.Sha1:
#pragma warning disable 0618
                    return new SHA1AuthenticationProvider(
                        new OctetString(
                            credentials.GetAuthenticationPassword()));
#pragma warning restore 0618

                case SnmpAuthenticationProtocol.Sha256:
                    return new SHA256AuthenticationProvider(
                        new OctetString(
                            credentials.GetAuthenticationPassword()));

                case SnmpAuthenticationProtocol.Sha384:
                    return new SHA384AuthenticationProvider(
                        new OctetString(
                            credentials.GetAuthenticationPassword()));

                case SnmpAuthenticationProtocol.Sha512:
                    return new SHA512AuthenticationProvider(
                        new OctetString(
                            credentials.GetAuthenticationPassword()));

                default:
                    throw UnsupportedCredentials();
            }
        }

        private static IPrivacyProvider CreatePrivacyProvider(
            SnmpV3Credentials credentials,
            IAuthenticationProvider authentication)
        {
            switch (credentials.PrivacyProtocol)
            {
                case SnmpPrivacyProtocol.None:
                    return new DefaultPrivacyProvider(
                        authentication);

                case SnmpPrivacyProtocol.Des:
#pragma warning disable 0618
                    return new DESPrivacyProvider(
                        new OctetString(
                            credentials.GetPrivacyPassword()),
                        authentication);
#pragma warning restore 0618

                case SnmpPrivacyProtocol.Aes:
                    return new AESPrivacyProvider(
                        new OctetString(
                            credentials.GetPrivacyPassword()),
                        authentication);

                case SnmpPrivacyProtocol.Aes192:
                    return new AES192PrivacyProvider(
                        new OctetString(
                            credentials.GetPrivacyPassword()),
                        authentication);

                case SnmpPrivacyProtocol.Aes256:
                    return new AES256PrivacyProvider(
                        new OctetString(
                            credentials.GetPrivacyPassword()),
                        authentication);

                default:
                    throw UnsupportedCredentials();
            }
        }

        private static SnmpVariable ToVariable(
            Variable variable)
        {
            return new SnmpVariable(
                variable.Id.ToString(),
                (int)variable.Data.TypeCode,
                variable.Data.ToString(),
                variable.Data.ToBytes());
        }

        private static SnmpTransportException
            UnsupportedCredentials()
        {
            return new SnmpTransportException(
                SnmpTransportFailure.UnsupportedCredentials,
                "Unsupported SNMP credentials.",
                null);
        }
    }
}
