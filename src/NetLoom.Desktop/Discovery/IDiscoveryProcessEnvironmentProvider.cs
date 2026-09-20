using System;
using System.Collections.Generic;
using NetLoom.Domain.Access;

namespace NetLoom.Desktop.Discovery
{
    internal interface IDiscoveryProcessEnvironmentProvider
    {
        IReadOnlyDictionary<string, string> CreateEnvironment(
            Guid accessProfileId,
            SnmpVersion version);
    }

    internal sealed class InheritedDiscoveryProcessEnvironmentProvider :
        IDiscoveryProcessEnvironmentProvider
    {
        public static readonly InheritedDiscoveryProcessEnvironmentProvider
            Instance =
                new InheritedDiscoveryProcessEnvironmentProvider();

        private InheritedDiscoveryProcessEnvironmentProvider()
        {
        }

        public IReadOnlyDictionary<string, string> CreateEnvironment(
            Guid accessProfileId,
            SnmpVersion version)
        {
            return new Dictionary<string, string>();
        }
    }
}
