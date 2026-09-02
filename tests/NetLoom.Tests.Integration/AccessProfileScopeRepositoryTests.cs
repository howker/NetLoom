using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.Repositories;
using NetLoom.Persistence.Sqlite.Security;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class AccessProfileScopeRepositoryTests
    {
        [TestMethod]
        public void ScopeAndCascadeDeleteWork()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "NetLoom.Tests",
                Guid.NewGuid().ToString("N"));

            var databasePath = Path.Combine(directory, "scope.db");

            try
            {
                var factory = new SqliteConnectionFactory(databasePath);
                new DatabaseInitializer(factory).Initialize();

                var profileId = Guid.NewGuid();
                var profiles = new AccessProfileRepository(factory);

                profiles.Save(
                    new AccessProfile(
                        profileId,
                        "Test profile",
                        true,
                        SnmpVersion.V2C,
                        null));

                var scope = new AccessProfileScopeRepository(factory);

                scope.AddTarget(
                    profileId,
                    AccessTargetKind.Cidr,
                    "192.168.10.0/24",
                    10);

                scope.AddExclusion(
                    profileId,
                    AccessTargetKind.IpAddress,
                    "192.168.10.50");

                scope.AddTcpPort(profileId, 22);
                scope.AddTcpPort(profileId, 443);

                var secrets = new SecretRepository(
                    factory,
                    new DpapiSecretProtector());

                secrets.SetSecret(
                    profileId,
                    AccessProfileSecretKind.SnmpCommunity,
                    Encoding.UTF8.GetBytes("test-community"));

                using (var connection = factory.OpenConnection())
                {
                    Assert.AreEqual(
                        1L,
                        Count(connection, "access_profile_targets"));

                    Assert.AreEqual(
                        1L,
                        Count(connection, "access_profile_exclusions"));

                    Assert.AreEqual(
                        2L,
                        Count(connection, "access_profile_tcp_ports"));

                    Assert.AreEqual(
                        1L,
                        Count(connection, "secrets"));
                }

                profiles.Delete(profileId);

                using (var connection = factory.OpenConnection())
                {
                    Assert.AreEqual(
                        0L,
                        Count(connection, "access_profiles"));

                    Assert.AreEqual(
                        0L,
                        Count(connection, "access_profile_targets"));

                    Assert.AreEqual(
                        0L,
                        Count(connection, "access_profile_exclusions"));

                    Assert.AreEqual(
                        0L,
                        Count(connection, "access_profile_tcp_ports"));

                    Assert.AreEqual(
                        0L,
                        Count(connection, "secrets"));
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

        [TestMethod]
        public void TcpPortRejectsInvalidValues()
        {
            var repository = new AccessProfileScopeRepository(
                new SqliteConnectionFactory(
                    Path.Combine(
                        Path.GetTempPath(),
                        Guid.NewGuid().ToString("N"),
                        "unused.db")));

            AssertInvalidPort(repository, 0);
            AssertInvalidPort(repository, 65536);
        }

        private static void AssertInvalidPort(
            AccessProfileScopeRepository repository,
            int port)
        {
            try
            {
                repository.AddTcpPort(Guid.NewGuid(), port);
                Assert.Fail("Invalid TCP port must be rejected.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private static long Count(
            System.Data.SQLite.SQLiteConnection connection,
            string table)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM " + table + ";";

                return Convert.ToInt64(command.ExecuteScalar());
            }
        }
    }
}
