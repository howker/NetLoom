using System;
using System.Globalization;
using NetLoom.Application.Security;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Repositories
{
    public sealed class SecretRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly ISecretProtector _secretProtector;

        public SecretRepository(
            SqliteConnectionFactory connectionFactory,
            ISecretProtector secretProtector)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));

            _secretProtector = secretProtector ??
                throw new ArgumentNullException(nameof(secretProtector));
        }

        public void SetSecret(
            Guid accessProfileId,
            AccessProfileSecretKind secretKind,
            byte[] plaintext)
        {
            if (accessProfileId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Access profile id is required.",
                    nameof(accessProfileId));
            }

            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            var protectedValue = _secretProtector.Protect(plaintext);
            var now = DateTime.UtcNow.ToString(
                "o",
                CultureInfo.InvariantCulture);

            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO secrets
(
    secret_id,
    access_profile_id,
    secret_kind,
    protected_value,
    created_utc,
    updated_utc
)
VALUES
(
    @secretId,
    @accessProfileId,
    @secretKind,
    @protectedValue,
    @createdUtc,
    @updatedUtc
)
ON CONFLICT(access_profile_id, secret_kind) DO UPDATE SET
    protected_value = excluded.protected_value,
    updated_utc = excluded.updated_utc;";

                command.Parameters.AddWithValue(
                    "@secretId",
                    Guid.NewGuid().ToString("D"));
                command.Parameters.AddWithValue(
                    "@accessProfileId",
                    accessProfileId.ToString("D"));
                command.Parameters.AddWithValue(
                    "@secretKind",
                    secretKind.ToString());
                command.Parameters.AddWithValue(
                    "@protectedValue",
                    protectedValue);
                command.Parameters.AddWithValue("@createdUtc", now);
                command.Parameters.AddWithValue("@updatedUtc", now);

                command.ExecuteNonQuery();
            }
        }

        public byte[] GetSecret(
            Guid accessProfileId,
            AccessProfileSecretKind secretKind)
        {
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT protected_value
FROM secrets
WHERE access_profile_id = @accessProfileId
  AND secret_kind = @secretKind;";

                command.Parameters.AddWithValue(
                    "@accessProfileId",
                    accessProfileId.ToString("D"));
                command.Parameters.AddWithValue(
                    "@secretKind",
                    secretKind.ToString());

                var value = command.ExecuteScalar();

                if (value == null || value == DBNull.Value)
                {
                    return null;
                }

                return _secretProtector.Unprotect((byte[])value);
            }
        }
    }
}
