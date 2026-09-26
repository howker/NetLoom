using System;
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
            byte[] communityUtf8)
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
                    null);

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
    }
}
