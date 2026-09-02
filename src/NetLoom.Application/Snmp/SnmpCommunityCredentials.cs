using System;

namespace NetLoom.Application.Snmp
{
    public sealed class SnmpCommunityCredentials : SnmpCredentials
    {
        private readonly byte[] _community;

        public SnmpCommunityCredentials(byte[] community)
        {
            if (community == null)
            {
                throw new ArgumentNullException(nameof(community));
            }

            if (community.Length == 0)
            {
                throw new ArgumentException(
                    "Community is required.",
                    nameof(community));
            }

            _community = (byte[])community.Clone();
        }

        public byte[] GetCommunityBytes()
        {
            return (byte[])_community.Clone();
        }
    }
}
