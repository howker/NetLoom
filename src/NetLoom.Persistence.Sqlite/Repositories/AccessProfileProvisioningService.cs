using System;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Persistence.Sqlite.Repositories
{
    public sealed class AccessProfileProvisioningService
    {
        private readonly AccessProfileRepository _accessProfiles;
        private readonly SecretRepository _secrets;

        public AccessProfileProvisioningService(
            AccessProfileRepository accessProfiles,
            SecretRepository secrets)
        {
            _accessProfiles = accessProfiles ??
                throw new ArgumentNullException(nameof(accessProfiles));
            _secrets = secrets ??
                throw new ArgumentNullException(nameof(secrets));
        }

        public AccessProfile CreateCommunityProfile(
            string name,
            SnmpVersion snmpVersion,
            byte[] communityUtf8,
            int? timeoutMs = null,
            int? retries = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Access profile name is required.",
                    nameof(name));
            }

            if (snmpVersion != SnmpVersion.V1 &&
                snmpVersion != SnmpVersion.V2C)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_PROFILE_VERSION_UNSUPPORTED");
            }

            if (communityUtf8 == null ||
                communityUtf8.Length == 0)
            {
                throw new ArgumentException(
                    "SNMP community is required.",
                    nameof(communityUtf8));
            }

            var profile =
                new AccessProfile(
                    Guid.NewGuid(),
                    name.Trim(),
                    true,
                    snmpVersion,
                    null, null, null, timeoutMs, retries);

            _accessProfiles.Save(profile);

            try
            {
                _secrets.SetSecret(
                    profile.Id,
                    AccessProfileSecretKind.SnmpCommunity,
                    communityUtf8);

                return profile;
            }
            catch
            {
                try
                {
                    _accessProfiles.Delete(profile.Id);
                }
                catch
                {
                }

                throw;
            }
        }

        public AccessProfile UpdateCommunityProfile(
            Guid profileId,
            string name,
            SnmpVersion snmpVersion,
            byte[] communityUtf8,
            int? timeoutMs = null,
            int? retries = null)
        {
            if (profileId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Access profile id is required.",
                    nameof(profileId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Access profile name is required.",
                    nameof(name));
            }

            if (snmpVersion != SnmpVersion.V1 &&
                snmpVersion != SnmpVersion.V2C)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_PROFILE_VERSION_UNSUPPORTED");
            }

            if (communityUtf8 != null &&
                communityUtf8.Length == 0)
            {
                throw new ArgumentException(
                    "SNMP community cannot be empty when supplied.",
                    nameof(communityUtf8));
            }

            var previous =
                _accessProfiles.Get(
                    profileId);

            if (previous == null)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_PROFILE_NOT_FOUND");
            }

            var updated =
                new AccessProfile(
                    previous.Id,
                    name.Trim(),
                    previous.IsEnabled,
                    snmpVersion,
                    previous.SnmpUsername,
                    null, null, timeoutMs, retries);

            _accessProfiles.Save(
                updated);

            if (communityUtf8 == null)
            {
                return updated;
            }

            try
            {
                _secrets.SetSecret(
                    profileId,
                    AccessProfileSecretKind.SnmpCommunity,
                    communityUtf8);

                return updated;
            }
            catch
            {
                try
                {
                    _accessProfiles.Save(
                        previous);
                }
                catch
                {
                }

                throw;
            }
        }

        public AccessProfile CreateV3Profile(string name, string username,
            string authProtocol, byte[] authPasswordUtf8,
            string privacyProtocol, byte[] privacyPasswordUtf8,
            int? timeoutMs = null, int? retries = null)
        {
            ValidateV3(name, username, authProtocol, authPasswordUtf8,
                privacyProtocol, privacyPasswordUtf8, false, Guid.Empty);
            var profile = new AccessProfile(Guid.NewGuid(), name.Trim(), true,
                SnmpVersion.V3, username.Trim(), authProtocol, privacyProtocol, timeoutMs, retries);
            _accessProfiles.Save(profile);
            try
            {
                SaveV3Secrets(profile.Id, authPasswordUtf8, privacyPasswordUtf8);
                return profile;
            }
            catch
            {
                _accessProfiles.Delete(profile.Id);
                throw;
            }
        }

        public AccessProfile UpdateV3Profile(Guid id, string name, string username,
            string authProtocol, byte[] authPasswordUtf8,
            string privacyProtocol, byte[] privacyPasswordUtf8,
            int? timeoutMs = null, int? retries = null)
        {
            var previous = _accessProfiles.Get(id) ??
                throw new InvalidOperationException("DISCOVERY_PROFILE_NOT_FOUND");
            ValidateV3(name, username, authProtocol, authPasswordUtf8,
                privacyProtocol, privacyPasswordUtf8, true, id);
            var updated = new AccessProfile(id, name.Trim(), previous.IsEnabled,
                SnmpVersion.V3, username.Trim(), authProtocol, privacyProtocol, timeoutMs, retries);
            _accessProfiles.Save(updated);
            try
            {
                SaveV3Secrets(id, authPasswordUtf8, privacyPasswordUtf8);
                return updated;
            }
            catch
            {
                _accessProfiles.Save(previous);
                throw;
            }
        }

        private void ValidateV3(string name, string username, string authProtocol,
            byte[] authPassword, string privacyProtocol, byte[] privacyPassword,
            bool updating, Guid id)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(nameof(name));
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException(nameof(username));
            if (!Enum.TryParse(authProtocol, out SnmpAuthenticationProtocol auth) ||
                !Enum.IsDefined(typeof(SnmpAuthenticationProtocol), auth))
                throw new ArgumentException(nameof(authProtocol));
            if (!Enum.TryParse(privacyProtocol, out SnmpPrivacyProtocol privacy) ||
                !Enum.IsDefined(typeof(SnmpPrivacyProtocol), privacy))
                throw new ArgumentException(nameof(privacyProtocol));
            if (privacy != SnmpPrivacyProtocol.None && auth == SnmpAuthenticationProtocol.None)
                throw new ArgumentException("Privacy requires authentication.");
            ValidatePassword(auth != SnmpAuthenticationProtocol.None, authPassword,
                updating, id, AccessProfileSecretKind.SnmpAuthenticationPassword);
            ValidatePassword(privacy != SnmpPrivacyProtocol.None, privacyPassword,
                updating, id, AccessProfileSecretKind.SnmpPrivacyPassword);
        }

        private void ValidatePassword(bool required, byte[] supplied, bool updating,
            Guid id, AccessProfileSecretKind kind)
        {
            if (!required) return;
            if (supplied != null && supplied.Length >= 8) return;
            if (updating && (supplied == null || supplied.Length == 0))
            {
                var previous = _secrets.GetSecret(id, kind);
                try { if (previous != null && previous.Length >= 8) return; }
                finally { if (previous != null) Array.Clear(previous, 0, previous.Length); }
            }
            throw new ArgumentException("SNMP password must contain at least 8 bytes.");
        }

        private void SaveV3Secrets(Guid id, byte[] authPassword, byte[] privacyPassword)
        {
            if (authPassword != null && authPassword.Length > 0)
                _secrets.SetSecret(id, AccessProfileSecretKind.SnmpAuthenticationPassword, authPassword);
            if (privacyPassword != null && privacyPassword.Length > 0)
                _secrets.SetSecret(id, AccessProfileSecretKind.SnmpPrivacyPassword, privacyPassword);
        }

        public void DeleteProfile(
            Guid profileId)
        {
            if (profileId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Access profile id is required.",
                    nameof(profileId));
            }

            if (_accessProfiles.Get(
                    profileId) == null)
            {
                throw new InvalidOperationException(
                    "DISCOVERY_PROFILE_NOT_FOUND");
            }

            _accessProfiles.Delete(
                profileId);
        }
    }
}
