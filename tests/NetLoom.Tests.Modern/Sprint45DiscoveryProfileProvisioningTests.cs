using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Security;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Repositories;

namespace NetLoom.Tests.Modern
{
    [TestClass]
    public sealed class Sprint45DiscoveryProfileProvisioningTests
    {
        [TestMethod]
        public void CreateCommunityProfilePersistsEnabledProfileAndSecret()
        {
            var databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s45-profile-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        databasePath);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                var protector =
                    new PrefixSecretProtector();

                var accessProfiles =
                    new AccessProfileRepository(
                        connectionFactory);

                var secrets =
                    new SecretRepository(
                        connectionFactory,
                        protector);

                var service =
                    new AccessProfileProvisioningService(
                        accessProfiles,
                        secrets);

                var community =
                    Encoding.UTF8.GetBytes(
                        "field-read-only");

                var created =
                    service.CreateCommunityProfile(
                        "Field v2c",
                        SnmpVersion.V2C,
                        community);

                Assert.IsNotNull(created);
                Assert.IsTrue(created.IsEnabled);
                Assert.AreEqual(
                    SnmpVersion.V2C,
                    created.SnmpVersion);

                var stored =
                    accessProfiles
                        .GetEnabled()
                        .Single(
                            profile =>
                                profile.Id ==
                                created.Id);

                Assert.AreEqual(
                    "Field v2c",
                    stored.Name);

                CollectionAssert.AreEqual(
                    community,
                    secrets.GetSecret(
                        created.Id,
                        AccessProfileSecretKind.SnmpCommunity));

                Assert.AreEqual(
                    1,
                    protector.ProtectCalls);
            }
            finally
            {
                DeleteDatabaseFamily(
                    databasePath);
            }
        }

        [TestMethod]
        public void CreateCommunityProfileRejectsV3WithoutPersistingProfile()
        {
            var databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    "netloom-s45-profile-" +
                    Guid.NewGuid().ToString("N") +
                    ".db");

            try
            {
                var connectionFactory =
                    new SqliteConnectionFactory(
                        databasePath);

                new DatabaseInitializer(
                    connectionFactory)
                    .Initialize();

                var accessProfiles =
                    new AccessProfileRepository(
                        connectionFactory);

                var service =
                    new AccessProfileProvisioningService(
                        accessProfiles,
                        new SecretRepository(
                            connectionFactory,
                            new PrefixSecretProtector()));

                try
                {
                    service.CreateCommunityProfile(
                        "Field v3",
                        SnmpVersion.V3,
                        Encoding.UTF8.GetBytes(
                            "unused"));

                    Assert.Fail(
                        "SNMPv3 community profile should be rejected.");
                }
                catch (InvalidOperationException error)
                {
                    Assert.AreEqual(
                        "DISCOVERY_PROFILE_VERSION_UNSUPPORTED",
                        error.Message);
                }

                Assert.AreEqual(
                    0,
                    accessProfiles.GetEnabled().Count);
            }
            finally
            {
                DeleteDatabaseFamily(
                    databasePath);
            }
        }

        private static void DeleteDatabaseFamily(
            string databasePath)
        {
            foreach (var path in new[]
            {
                databasePath,
                databasePath + "-wal",
                databasePath + "-shm"
            })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private sealed class PrefixSecretProtector :
            ISecretProtector
        {
            public int ProtectCalls { get; private set; }

            public byte[] Protect(
                byte[] plaintext)
            {
                ProtectCalls++;

                var result =
                    new byte[plaintext.Length + 1];

                result[0] =
                    0xA5;

                Buffer.BlockCopy(
                    plaintext,
                    0,
                    result,
                    1,
                    plaintext.Length);

                return result;
            }

            public byte[] Unprotect(
                byte[] protectedData)
            {
                if (protectedData == null ||
                    protectedData.Length == 0 ||
                    protectedData[0] != 0xA5)
                {
                    throw new InvalidOperationException(
                        "Unexpected protected payload.");
                }

                var result =
                    new byte[protectedData.Length - 1];

                Buffer.BlockCopy(
                    protectedData,
                    1,
                    result,
                    0,
                    result.Length);

                return result;
            }
        }
    }
}
