using System;
using System.Text;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Engine
{
    internal static class EngineSnmpCredentialFactory
    {
        private const string Community =
            "NETLOOM_SNMP_COMMUNITY";

        private const string Username =
            "NETLOOM_SNMP_USERNAME";

        private const string AuthenticationProtocol =
            "NETLOOM_SNMP_AUTH_PROTOCOL";

        private const string AuthenticationPassword =
            "NETLOOM_SNMP_AUTH_PASSWORD";

        private const string PrivacyProtocol =
            "NETLOOM_SNMP_PRIVACY_PROTOCOL";

        private const string PrivacyPassword =
            "NETLOOM_SNMP_PRIVACY_PASSWORD";

        private const string Context =
            "NETLOOM_SNMP_CONTEXT";

        public static SnmpCredentials Create(
            SnmpVersion version)
        {
            if (version == SnmpVersion.V3)
            {
                return CreateV3();
            }

            var community =
                RequiredEnvironment(
                    Community);

            return new SnmpCommunityCredentials(
                Encoding.UTF8.GetBytes(
                    community));
        }

        private static SnmpCredentials CreateV3()
        {
            var username =
                RequiredEnvironment(
                    Username);

            var auth =
                ParseEnum<SnmpAuthenticationProtocol>(
                    Environment.GetEnvironmentVariable(
                        AuthenticationProtocol) ??
                    "None",
                    "INVALID_SNMP_AUTH_PROTOCOL");

            var privacy =
                ParseEnum<SnmpPrivacyProtocol>(
                    Environment.GetEnvironmentVariable(
                        PrivacyProtocol) ??
                    "None",
                    "INVALID_SNMP_PRIVACY_PROTOCOL");

            return new SnmpV3Credentials(
                username,
                auth,
                OptionalBytes(
                    AuthenticationPassword),
                privacy,
                OptionalBytes(
                    PrivacyPassword),
                Environment.GetEnvironmentVariable(
                    Context) ??
                string.Empty);
        }

        private static byte[] OptionalBytes(
            string variableName)
        {
            var value =
                Environment.GetEnvironmentVariable(
                    variableName);

            return string.IsNullOrEmpty(value)
                ? null
                : Encoding.UTF8.GetBytes(value);
        }

        private static string RequiredEnvironment(
            string variableName)
        {
            var value =
                Environment.GetEnvironmentVariable(
                    variableName);

            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    "MISSING_ENV_" +
                    variableName);
            }

            return value;
        }

        private static T ParseEnum<T>(
            string value,
            string errorCode)
            where T : struct
        {
            T parsed;

            if (!Enum.TryParse(
                value,
                true,
                out parsed))
            {
                throw new InvalidOperationException(
                    errorCode);
            }

            return parsed;
        }
    }
}
