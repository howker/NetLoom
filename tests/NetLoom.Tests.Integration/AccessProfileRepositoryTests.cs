using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Repositories;
using NetLoom.Persistence.Sqlite.Security;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class AccessProfileRepositoryTests
    {
        [TestMethod]
        public void ProfileAndSecretRoundTripWithoutPlaintextStorage()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Tests",
                Guid.NewGuid().ToString("N"));

            var databasePath = Path.Combine(directory, "access.db");

            try
            {
                var factory = new SqliteConnectionFactory(databasePath);
                new DatabaseInitializer(factory).Initialize();

                var profileId = Guid.NewGuid();
                var profile = new AccessProfile(
                    profileId,
                    "Core switches",
                    true,
                    SnmpVersion.V2C,
                    null);

                var profiles = new AccessProfileRepository(factory);
                profiles.Save(profile);

                var loaded = profiles.Get(profileId);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(profile.Id, loaded.Id);
                Assert.AreEqual(profile.Name, loaded.Name);
                Assert.AreEqual(profile.IsEnabled, loaded.IsEnabled);
                Assert.AreEqual(profile.SnmpVersion, loaded.SnmpVersion);
                Assert.IsNull(loaded.SnmpUsername);

                var plaintext = Encoding.UTF8.GetBytes(
                    "integration-community");

                var secrets = new SecretRepository(
                    factory,
                    new DpapiSecretProtector());

                secrets.SetSecret(
                    profileId,
                    AccessProfileSecretKind.SnmpCommunity,
                    plaintext);

                var restored = secrets.GetSecret(
                    profileId,
                    AccessProfileSecretKind.SnmpCommunity);

                CollectionAssert.AreEqual(plaintext, restored);

                using (var connection = factory.OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT protected_value
FROM secrets
WHERE access_profile_id = @accessProfileId
  AND secret_kind = @secretKind;";

                    command.Parameters.AddWithValue(
                        "@accessProfileId",
                        profileId.ToString("D"));
                    command.Parameters.AddWithValue(
                        "@secretKind",
                        AccessProfileSecretKind.SnmpCommunity.ToString());

                    var stored = (byte[])command.ExecuteScalar();

                    Assert.IsFalse(plaintext.SequenceEqual(stored));
                    Assert.IsFalse(
                        Encoding.UTF8.GetString(stored)
                            .Contains("integration-community"));
                }
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
