using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using NetLoom.Application.Locations;
using NetLoom.Domain.Locations;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Locations
{
    public sealed class SqliteLocationRepository :
        ILocationRepository
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteLocationRepository(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public void Save(Location location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var transaction =
                connection.BeginTransaction())
            {
                ValidateParent(
                    connection,
                    transaction,
                    location.Id,
                    location.ParentLocationId);

                var now =
                    DateTime.UtcNow.ToString(
                        "o",
                        CultureInfo.InvariantCulture);

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
INSERT INTO locations
(
    location_id,
    parent_location_id,
    name,
    description,
    created_utc,
    updated_utc
)
VALUES
(
    @id,
    @parentId,
    @name,
    @description,
    @createdUtc,
    @updatedUtc
)
ON CONFLICT(location_id) DO UPDATE SET
    parent_location_id = excluded.parent_location_id,
    name = excluded.name,
    description = excluded.description,
    updated_utc = excluded.updated_utc;";

                    command.Parameters.AddWithValue(
                        "@id",
                        location.Id.ToString("D"));

                    command.Parameters.AddWithValue(
                        "@parentId",
                        location.ParentLocationId.HasValue
                            ? (object)location.ParentLocationId
                                .Value.ToString("D")
                            : DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@name",
                        location.Name);

                    command.Parameters.AddWithValue(
                        "@description",
                        (object)location.Description ??
                        DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@createdUtc",
                        now);

                    command.Parameters.AddWithValue(
                        "@updatedUtc",
                        now);

                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        public Location Get(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Location id is required.",
                    nameof(id));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    parent_location_id,
    name,
    description
FROM locations
WHERE location_id = @id;";

                command.Parameters.AddWithValue(
                    "@id",
                    id.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return ReadLocation(
                        id,
                        reader);
                }
            }
        }

        public IReadOnlyList<Location> GetAll()
        {
            var result =
                new List<Location>();

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    location_id,
    parent_location_id,
    name,
    description
FROM locations
ORDER BY name, location_id;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new Location(
                                Guid.Parse(
                                    reader.GetString(0)),
                                reader.IsDBNull(1)
                                    ? (Guid?)null
                                    : Guid.Parse(
                                        reader.GetString(1)),
                                reader.GetString(2),
                                reader.IsDBNull(3)
                                    ? null
                                    : reader.GetString(3)));
                    }
                }
            }

            return result;
        }

        public void Delete(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Location id is required.",
                    nameof(id));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var transaction =
                connection.BeginTransaction())
            {
                if (HasChildren(
                    connection,
                    transaction,
                    id))
                {
                    throw new InvalidOperationException(
                        "Location with child locations cannot be deleted.");
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
DELETE FROM locations
WHERE location_id = @id;";

                    command.Parameters.AddWithValue(
                        "@id",
                        id.ToString("D"));

                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        private static Location ReadLocation(
            Guid id,
            SQLiteDataReader reader)
        {
            return new Location(
                id,
                reader.IsDBNull(0)
                    ? (Guid?)null
                    : Guid.Parse(
                        reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2)
                    ? null
                    : reader.GetString(2));
        }

        private static bool HasChildren(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            Guid id)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
SELECT COUNT(*)
FROM locations
WHERE parent_location_id = @id;";

                command.Parameters.AddWithValue(
                    "@id",
                    id.ToString("D"));

                return Convert.ToInt64(
                    command.ExecuteScalar()) > 0;
            }
        }

        private static void ValidateParent(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            Guid locationId,
            Guid? parentLocationId)
        {
            if (!parentLocationId.HasValue)
            {
                return;
            }

            if (locationId == parentLocationId.Value)
            {
                throw new InvalidOperationException(
                    "Location cannot be its own parent.");
            }

            var current =
                parentLocationId;

            var visited =
                new HashSet<Guid>();

            while (current.HasValue)
            {
                if (current.Value == locationId)
                {
                    throw new InvalidOperationException(
                        "Location hierarchy cycle is not allowed.");
                }

                if (!visited.Add(current.Value))
                {
                    throw new InvalidOperationException(
                        "Existing location hierarchy contains a cycle.");
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
SELECT parent_location_id
FROM locations
WHERE location_id = @id;";

                    command.Parameters.AddWithValue(
                        "@id",
                        current.Value.ToString("D"));

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            throw new InvalidOperationException(
                                "Parent location does not exist.");
                        }

                        current =
                            reader.IsDBNull(0)
                                ? (Guid?)null
                                : Guid.Parse(
                                    reader.GetString(0));
                    }
                }
            }
        }
    }
}
