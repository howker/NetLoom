using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Security;
using NetLoom.Desktop.Discovery;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Repositories;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint42DiscoveryProfileEnvironmentTests
    {
        [TestMethod]
        public void EnabledCommunityProfileProducesOnlyItsOwnRuntimeCredential()
        {
            WithDatabase(
                (profiles, secrets) =>
                {
                    var selected =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Selected",
                            true,
                            SnmpVersion.V2C,
                            null);

                    var other =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Other",
                            true,
                            SnmpVersion.V2C,
                            null);

                    profiles.Save(selected);
                    profiles.Save(other);

                    secrets.SetSecret(
                        selected.Id,
                        AccessProfileSecretKind.SnmpCommunity,
                        Encoding.UTF8.GetBytes(
                            "selected-community"));

                    secrets.SetSecret(
                        other.Id,
                        AccessProfileSecretKind.SnmpCommunity,
                        Encoding.UTF8.GetBytes(
                            "other-community"));

                    var provider =
                        new DesktopDiscoveryProcessEnvironmentProvider(
                            profiles,
                            secrets);

                    var environment =
                        provider.CreateEnvironment(
                            selected.Id,
                            SnmpVersion.V2C);

                    Assert.AreEqual(
                        "selected-community",
                        environment[
                            "NETLOOM_SNMP_COMMUNITY"]);
                    Assert.AreEqual(
                        string.Empty,
                        environment[
                            "NETLOOM_SNMP_USERNAME"]);
                    Assert.AreEqual(
                        "None",
                        environment[
                            "NETLOOM_SNMP_AUTH_PROTOCOL"]);
                });
        }

        [TestMethod]
        public void DisabledMissingAndVersionMismatchProfilesAreRejected()
        {
            WithDatabase(
                (profiles, secrets) =>
                {
                    var disabled =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Disabled",
                            false,
                            SnmpVersion.V2C,
                            null);

                    var enabled =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Enabled",
                            true,
                            SnmpVersion.V2C,
                            null);

                    profiles.Save(disabled);
                    profiles.Save(enabled);

                    var provider =
                        new DesktopDiscoveryProcessEnvironmentProvider(
                            profiles,
                            secrets);

                    AssertError(
                        "DISCOVERY_ACCESS_PROFILE_NOT_FOUND",
                        () =>
                            provider.CreateEnvironment(
                                Guid.NewGuid(),
                                SnmpVersion.V2C));

                    AssertError(
                        "DISCOVERY_ACCESS_PROFILE_DISABLED",
                        () =>
                            provider.CreateEnvironment(
                                disabled.Id,
                                SnmpVersion.V2C));

                    AssertError(
                        "DISCOVERY_ACCESS_PROFILE_VERSION_MISMATCH",
                        () =>
                            provider.CreateEnvironment(
                                enabled.Id,
                                SnmpVersion.V1));

                    AssertError(
                        "DISCOVERY_SNMP_COMMUNITY_REQUIRED",
                        () =>
                            provider.CreateEnvironment(
                                enabled.Id,
                                SnmpVersion.V2C));
                });
        }

        [TestMethod]
        public void V3ProfileIsLimitedToNoAuthNoPrivUntilProtocolsArePersisted()
        {
            WithDatabase(
                (profiles, secrets) =>
                {
                    var profile =
                        new AccessProfile(
                            Guid.NewGuid(),
                            "V3",
                            true,
                            SnmpVersion.V3,
                            "operator");

                    profiles.Save(profile);

                    var provider =
                        new DesktopDiscoveryProcessEnvironmentProvider(
                            profiles,
                            secrets);

                    var environment =
                        provider.CreateEnvironment(
                            profile.Id,
                            SnmpVersion.V3);

                    Assert.AreEqual(
                        "operator",
                        environment[
                            "NETLOOM_SNMP_USERNAME"]);
                    Assert.AreEqual(
                        "None",
                        environment[
                            "NETLOOM_SNMP_AUTH_PROTOCOL"]);
                    Assert.AreEqual(
                        "None",
                        environment[
                            "NETLOOM_SNMP_PRIVACY_PROTOCOL"]);

                    secrets.SetSecret(
                        profile.Id,
                        AccessProfileSecretKind.SnmpAuthenticationPassword,
                        Encoding.UTF8.GetBytes(
                            "synthetic-password"));

                    AssertError(
                        "DISCOVERY_SNMP_V3_SECURITY_PROTOCOLS_NOT_STORED",
                        () =>
                            provider.CreateEnvironment(
                                profile.Id,
                                SnmpVersion.V3));
                });
        }

        [TestMethod]
        public void EnabledProfilesAreReturnedInStableNameOrder()
        {
            WithDatabase(
                (profiles, secrets) =>
                {
                    profiles.Save(
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Zulu",
                            true,
                            SnmpVersion.V2C,
                            null));
                    profiles.Save(
                        new AccessProfile(
                            Guid.NewGuid(),
                            "alpha",
                            true,
                            SnmpVersion.V1,
                            null));
                    profiles.Save(
                        new AccessProfile(
                            Guid.NewGuid(),
                            "Hidden",
                            false,
                            SnmpVersion.V2C,
                            null));

                    var enabled =
                        profiles.GetEnabled();

                    Assert.AreEqual(
                        2,
                        enabled.Count);
                    Assert.AreEqual(
                        "alpha",
                        enabled[0].Name);
                    Assert.AreEqual(
                        "Zulu",
                        enabled[1].Name);
                });
        }

        private static void WithDatabase(
            Action<AccessProfileRepository, SecretRepository> action)
        {
            var path =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s42-profile-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        path);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                action(
                    new AccessProfileRepository(
                        connectionFactory),
                    new SecretRepository(
                        connectionFactory,
                        new PassThroughSecretProtector()));
            }
            finally
            {
                DeleteIfExists(path);
                DeleteIfExists(path + "-wal");
                DeleteIfExists(path + "-shm");
            }
        }

        private static void AssertError(
            string expected,
            Action action)
        {
            try
            {
                action();
                Assert.Fail(
                    "Expected InvalidOperationException.");
            }
            catch (InvalidOperationException exception)
            {
                Assert.AreEqual(
                    expected,
                    exception.Message);
            }
        }

        private static void DeleteIfExists(
            string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class PassThroughSecretProtector :
            ISecretProtector
        {
            public byte[] Protect(
                byte[] plaintext)
            {
                return (byte[])plaintext.Clone();
            }

            public byte[] Unprotect(
                byte[] protectedData)
            {
                return (byte[])protectedData.Clone();
            }
        }
    }
}
