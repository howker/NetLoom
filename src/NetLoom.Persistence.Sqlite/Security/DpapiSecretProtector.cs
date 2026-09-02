using System;
using System.Security.Cryptography;
using NetLoom.Application.Security;

namespace NetLoom.Persistence.Sqlite.Security
{
    public sealed class DpapiSecretProtector : ISecretProtector
    {
        private static readonly byte[] Entropy =
        {
            0x4E, 0x65, 0x74, 0x4C, 0x6F, 0x6F, 0x6D
        };

        public byte[] Protect(byte[] plaintext)
        {
            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            return ProtectedData.Protect(
                plaintext,
                Entropy,
                DataProtectionScope.CurrentUser);
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            if (protectedData == null)
            {
                throw new ArgumentNullException(nameof(protectedData));
            }

            return ProtectedData.Unprotect(
                protectedData,
                Entropy,
                DataProtectionScope.CurrentUser);
        }
    }
}
