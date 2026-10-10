using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Security;
using NetLoom.Application.PollingPolicies;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Migrations;
using NetLoom.Persistence.Sqlite.Repositories;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class Sprint51AccessProfileParametersTests
    {
        [TestMethod]
        public void MigrationReadsLegacyProfileAndV3ParametersRoundTrip()
        {
            var path = Path.Combine(Path.GetTempPath(), "netloom-s51-profile-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var factory = new SqliteConnectionFactory(path);
                var legacyId = Guid.NewGuid();
                var migrations = typeof(Migration001Initial).Assembly.GetTypes()
                    .Where(type => typeof(IMigration).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                    .Where(type => type.Name.StartsWith("Migration", StringComparison.Ordinal))
                    .Select(type => (IMigration)Activator.CreateInstance(type))
                    .Where(migration => migration.Version <= 28).ToArray();
                using (var connection = factory.OpenConnection())
                {
                    new MigrationRunner(migrations).ApplyPending(connection);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO access_profiles (access_profile_id, name, is_enabled, snmp_version, created_utc, updated_utc) VALUES (@id, 'Legacy', 1, 'V2C', '2026-10-10', '2026-10-10');";
                        command.Parameters.AddWithValue("@id", legacyId.ToString("D"));
                        command.ExecuteNonQuery();
                    }
                }
                new DatabaseInitializer(factory).Initialize();
                var profiles = new AccessProfileRepository(factory);
                var legacy = profiles.Get(legacyId);
                Assert.IsNull(legacy.SnmpAuthenticationProtocol);
                Assert.IsNull(legacy.SnmpPrivacyProtocol);
                Assert.IsNull(legacy.SnmpTimeoutMilliseconds);
                Assert.IsNull(legacy.SnmpRetryCount);
                var secrets = new SecretRepository(factory, new PassThroughProtector());
                var service = new AccessProfileProvisioningService(profiles, secrets);
                var template = PollingTemplates.SecureSnmpV3;
                Assert.ThrowsExactly<ArgumentException>(() => service.CreateV3Profile(
                    "Missing passwords", "operator", template.AuthenticationProtocol.ToString(), null,
                    template.PrivacyProtocol.ToString(), null,
                    template.TimeoutMilliseconds, template.RetryCount));
                Assert.ThrowsExactly<ArgumentException>(() => service.CreateV3Profile(
                    "Invalid", "operator", "Sha256", Encoding.UTF8.GetBytes("short"),
                    "Aes", Encoding.UTF8.GetBytes("longpassword")));
                var v3 = service.CreateV3Profile("V3", "operator", "Sha256",
                    Encoding.UTF8.GetBytes("authpassword"), "Aes",
                    Encoding.UTF8.GetBytes("privpassword"), 3000, 1);
                var read = profiles.Get(v3.Id);
                Assert.AreEqual("Sha256", read.SnmpAuthenticationProtocol);
                Assert.AreEqual("Aes", read.SnmpPrivacyProtocol);
                Assert.AreEqual(3000, read.SnmpTimeoutMilliseconds);
                Assert.AreEqual(1, read.SnmpRetryCount);
                service.UpdateV3Profile(v3.Id, "V3 updated", "operator", "Sha256",
                    null, "Aes", null, 5000, 2);
                Assert.AreEqual(5000, profiles.Get(v3.Id).SnmpTimeoutMilliseconds);
                var savedPassword = secrets.GetSecret(v3.Id, AccessProfileSecretKind.SnmpAuthenticationPassword);
                CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("authpassword"), savedPassword);
                Array.Clear(savedPassword, 0, savedPassword.Length);
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }

        private sealed class PassThroughProtector : ISecretProtector
        {
            public byte[] Protect(byte[] value) => (byte[])value.Clone();
            public byte[] Unprotect(byte[] value) => (byte[])value.Clone();
        }
    }
}
