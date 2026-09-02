using System;

namespace NetLoom.Application.Snmp
{
    public sealed class SnmpV3Credentials : SnmpCredentials
    {
        private readonly byte[] _authenticationPassword;
        private readonly byte[] _privacyPassword;

        public SnmpV3Credentials(
            string username,
            SnmpAuthenticationProtocol authenticationProtocol,
            byte[] authenticationPassword,
            SnmpPrivacyProtocol privacyProtocol,
            byte[] privacyPassword,
            string contextName)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(
                    "SNMP v3 username is required.",
                    nameof(username));
            }

            if (privacyProtocol != SnmpPrivacyProtocol.None &&
                authenticationProtocol == SnmpAuthenticationProtocol.None)
            {
                throw new ArgumentException(
                    "SNMP privacy requires authentication.",
                    nameof(privacyProtocol));
            }

            if (authenticationProtocol != SnmpAuthenticationProtocol.None &&
                (authenticationPassword == null ||
                 authenticationPassword.Length == 0))
            {
                throw new ArgumentException(
                    "Authentication password is required.",
                    nameof(authenticationPassword));
            }

            if (privacyProtocol != SnmpPrivacyProtocol.None &&
                (privacyPassword == null ||
                 privacyPassword.Length == 0))
            {
                throw new ArgumentException(
                    "Privacy password is required.",
                    nameof(privacyPassword));
            }

            Username = username;
            AuthenticationProtocol = authenticationProtocol;
            PrivacyProtocol = privacyProtocol;
            ContextName = contextName ?? string.Empty;

            _authenticationPassword =
                authenticationPassword == null
                    ? null
                    : (byte[])authenticationPassword.Clone();

            _privacyPassword =
                privacyPassword == null
                    ? null
                    : (byte[])privacyPassword.Clone();
        }

        public string Username { get; }

        public SnmpAuthenticationProtocol AuthenticationProtocol { get; }

        public SnmpPrivacyProtocol PrivacyProtocol { get; }

        public string ContextName { get; }

        public byte[] GetAuthenticationPassword()
        {
            return _authenticationPassword == null
                ? null
                : (byte[])_authenticationPassword.Clone();
        }

        public byte[] GetPrivacyPassword()
        {
            return _privacyPassword == null
                ? null
                : (byte[])_privacyPassword.Clone();
        }
    }
}
