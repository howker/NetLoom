using System;
using System.Collections.Generic;
using System.Text;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Repositories;

namespace NetLoom.Desktop.Discovery
{
    internal sealed class DesktopDiscoveryProcessEnvironmentProvider :
        IDiscoveryProcessEnvironmentProvider
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

        private readonly AccessProfileRepository _profileRepository;
        private readonly SecretRepository _secretRepository;

        public DesktopDiscoveryProcessEnvironmentProvider(
            AccessProfileRepository profileRepository,
            SecretRepository secretRepository)
        {
            _profileRepository =
                profileRepository ??
                throw new ArgumentNullException(
                    nameof(profileRepository));

            _secretRepository =
                secretRepository ??
                throw new ArgumentNullException(
                    nameof(secretRepository));
        }

        public IReadOnlyDictionary<string, string> CreateEnvironment(
            Guid accessProfileId,
            SnmpVersion version)
        {
            if (accessProfileId == Guid.Empty)
            {
                throw new ArgumentException(
                    "DISCOVERY_ACCESS_PROFILE_ID_REQUIRED",
                    nameof(accessProfileId));
            }

            var profile =
                _profileRepository.Get(
                    accessProfileId);

            if (profile == null)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_ACCESS_PROFILE_NOT_FOUND");
            }

            if (!profile.IsEnabled)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_ACCESS_PROFILE_DISABLED");
            }

            if (profile.SnmpVersion != version)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_ACCESS_PROFILE_VERSION_MISMATCH");
            }

            if (version == SnmpVersion.V3)
            {
                return CreateV3Environment(
                    profile);
            }

            return CreateCommunityEnvironment(
                profile.Id);
        }

        private IReadOnlyDictionary<string, string>
            CreateCommunityEnvironment(
                Guid accessProfileId)
        {
            var communityBytes =
                _secretRepository.GetSecret(
                    accessProfileId,
                    AccessProfileSecretKind.SnmpCommunity);

            try
            {
                var community =
                    DecodeRequiredSecret(
                        communityBytes,
                        "DISCOVERY_SNMP_COMMUNITY_REQUIRED");

                return BaseEnvironment(
                    community,
                    string.Empty);
            }
            finally
            {
                Clear(
                    communityBytes);
            }
        }

        private IReadOnlyDictionary<string, string>
            CreateV3Environment(
                AccessProfile profile)
        {
            if (string.IsNullOrWhiteSpace(
                    profile.SnmpUsername))
            {
                throw new InvalidOperationException(
                    "DISCOVERY_SNMP_V3_USERNAME_REQUIRED");
            }

            var authenticationPassword =
                _secretRepository.GetSecret(
                    profile.Id,
                    AccessProfileSecretKind.SnmpAuthenticationPassword);

            var privacyPassword =
                _secretRepository.GetSecret(
                    profile.Id,
                    AccessProfileSecretKind.SnmpPrivacyPassword);

            try
            {
                if (HasSecret(authenticationPassword) ||
                    HasSecret(privacyPassword))
                {
                    throw new InvalidOperationException(
                        "DISCOVERY_SNMP_V3_SECURITY_PROTOCOLS_NOT_STORED");
                }

                var environment =
                    BaseEnvironment(
                        string.Empty,
                        profile.SnmpUsername.Trim());

                environment[AuthenticationProtocol] =
                    "None";
                environment[PrivacyProtocol] =
                    "None";

                return environment;
            }
            finally
            {
                Clear(
                    authenticationPassword);
                Clear(
                    privacyPassword);
            }
        }

        private static Dictionary<string, string> BaseEnvironment(
            string community,
            string username)
        {
            return new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                { Community, community ?? string.Empty },
                { Username, username ?? string.Empty },
                { AuthenticationProtocol, "None" },
                { AuthenticationPassword, string.Empty },
                { PrivacyProtocol, "None" },
                { PrivacyPassword, string.Empty },
                { Context, string.Empty }
            };
        }

        private static string DecodeRequiredSecret(
            byte[] value,
            string errorCode)
        {
            if (!HasSecret(value))
            {
                throw new InvalidOperationException(
                    errorCode);
            }

            var decoded =
                Encoding.UTF8.GetString(
                    value);

            if (string.IsNullOrWhiteSpace(decoded))
            {
                throw new InvalidOperationException(
                    errorCode);
            }

            return decoded;
        }

        private static bool HasSecret(
            byte[] value)
        {
            return value != null &&
                value.Length > 0;
        }

        private static void Clear(
            byte[] value)
        {
            if (value != null)
            {
                Array.Clear(
                    value,
                    0,
                    value.Length);
            }
        }
    }
}
