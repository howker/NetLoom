using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.MapLayout;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.MapLayout
{
    public sealed class SqliteMapLayoutStore :
        IMapLayoutStore,
        IMapLocationLayoutStore
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteMapLayoutStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public MapLayoutSnapshot Load(
            Guid mapId)
        {
            ValidateMapId(mapId);

            using (var connection =
                _connectionFactory.OpenReadOnlyConnection())
            {
                var viewport =
                    LoadViewport(
                        connection,
                        mapId);

                var devices =
                    LoadDevices(
                        connection,
                        mapId);

                var locations =
                    LoadLocations(
                        connection,
                        mapId);

                return new MapLayoutSnapshot(
                    mapId,
                    viewport,
                    devices,
                    locations);
            }
        }

        public void SaveViewport(
            Guid mapId,
            MapViewportLayout viewport)
        {
            ValidateMapId(mapId);

            if (viewport == null)
            {
                throw new ArgumentNullException(
                    nameof(viewport));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        EnsureMap(
                            connection,
                            mapId);

                        using (var command =
                            connection.CreateCommand())
                        {
                            command.CommandText = @"
UPDATE maps
SET
    default_zoom = @zoom,
    default_pan_x = @panX,
    default_pan_y = @panY,
    updated_at_utc = @updatedUtc
WHERE id = @mapId;";

                            command.Parameters.AddWithValue(
                                "@zoom",
                                viewport.Zoom);

                            command.Parameters.AddWithValue(
                                "@panX",
                                viewport.PanX);

                            command.Parameters.AddWithValue(
                                "@panY",
                                viewport.PanY);

                            command.Parameters.AddWithValue(
                                "@updatedUtc",
                                UtcNowText());

                            command.Parameters.AddWithValue(
                                "@mapId",
                                mapId.ToString("D"));

                            command.ExecuteNonQuery();
                        }
                    });
            }
        }

        public void SaveDevice(
            Guid mapId,
            MapDeviceLayout deviceLayout)
        {
            ValidateMapId(mapId);

            if (deviceLayout == null)
            {
                throw new ArgumentNullException(
                    nameof(deviceLayout));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        EnsureMap(
                            connection,
                            mapId);

                        using (var command =
                            connection.CreateCommand())
                        {
                            command.CommandText = @"
INSERT INTO map_device_layout
(
    map_id,
    device_id,
    x,
    y,
    is_locked
)
VALUES
(
    @mapId,
    @deviceId,
    @x,
    @y,
    @isLocked
)
ON CONFLICT(map_id, device_id) DO UPDATE SET
    x = excluded.x,
    y = excluded.y,
    is_locked = excluded.is_locked;";

                            command.Parameters.AddWithValue(
                                "@mapId",
                                mapId.ToString("D"));

                            command.Parameters.AddWithValue(
                                "@deviceId",
                                deviceLayout.DeviceId
                                    .ToString("D"));

                            command.Parameters.AddWithValue(
                                "@x",
                                deviceLayout.X);

                            command.Parameters.AddWithValue(
                                "@y",
                                deviceLayout.Y);

                            command.Parameters.AddWithValue(
                                "@isLocked",
                                deviceLayout.IsLocked
                                    ? 1
                                    : 0);

                            command.ExecuteNonQuery();
                        }
                    });
            }
        }

        public void SaveLocation(
            Guid mapId,
            MapLocationLayout locationLayout)
        {
            ValidateMapId(mapId);

            if (locationLayout == null)
            {
                throw new ArgumentNullException(
                    nameof(locationLayout));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        EnsureMap(
                            connection,
                            mapId);

                        using (var command =
                            connection.CreateCommand())
                        {
                            command.CommandText = @"
INSERT INTO map_location_layout
(
    map_id,
    location_id,
    x,
    y,
    width,
    height,
    is_collapsed,
    is_locked
)
VALUES
(
    @mapId,
    @locationId,
    @x,
    @y,
    @width,
    @height,
    @isCollapsed,
    @isLocked
)
ON CONFLICT(map_id, location_id) DO UPDATE SET
    x = excluded.x,
    y = excluded.y,
    width = excluded.width,
    height = excluded.height,
    is_collapsed = excluded.is_collapsed,
    is_locked = excluded.is_locked;";

                            command.Parameters.AddWithValue(
                                "@mapId",
                                mapId.ToString("D"));

                            command.Parameters.AddWithValue(
                                "@locationId",
                                locationLayout.LocationId
                                    .ToString("D"));

                            command.Parameters.AddWithValue(
                                "@x",
                                locationLayout.X);

                            command.Parameters.AddWithValue(
                                "@y",
                                locationLayout.Y);

                            command.Parameters.AddWithValue(
                                "@width",
                                locationLayout.Width);

                            command.Parameters.AddWithValue(
                                "@height",
                                locationLayout.Height);

                            command.Parameters.AddWithValue(
                                "@isCollapsed",
                                locationLayout.IsCollapsed
                                    ? 1
                                    : 0);

                            command.Parameters.AddWithValue(
                                "@isLocked",
                                locationLayout.IsLocked
                                    ? 1
                                    : 0);

                            command.ExecuteNonQuery();
                        }
                    });
            }
        }

        private static MapViewportLayout LoadViewport(
            System.Data.SQLite.SQLiteConnection connection,
            Guid mapId)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    default_zoom,
    default_pan_x,
    default_pan_y
FROM maps
WHERE id = @mapId;";

                command.Parameters.AddWithValue(
                    "@mapId",
                    mapId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return new MapViewportLayout(
                            1.0,
                            0.0,
                            0.0);
                    }

                    return new MapViewportLayout(
                        reader.GetDouble(0),
                        reader.GetDouble(1),
                        reader.GetDouble(2));
                }
            }
        }

        private static IReadOnlyList<MapDeviceLayout>
            LoadDevices(
                System.Data.SQLite.SQLiteConnection connection,
                Guid mapId)
        {
            var result =
                new List<MapDeviceLayout>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    device_id,
    x,
    y,
    is_locked
FROM map_device_layout
WHERE map_id = @mapId
ORDER BY device_id;";

                command.Parameters.AddWithValue(
                    "@mapId",
                    mapId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new MapDeviceLayout(
                                Guid.Parse(
                                    reader.GetString(0)),
                                reader.GetDouble(1),
                                reader.GetDouble(2),
                                reader.GetInt32(3) != 0));
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<MapLocationLayout>
            LoadLocations(
                System.Data.SQLite.SQLiteConnection connection,
                Guid mapId)
        {
            var result =
                new List<MapLocationLayout>();

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    location_id,
    x,
    y,
    width,
    height,
    is_collapsed,
    is_locked
FROM map_location_layout
WHERE map_id = @mapId
ORDER BY location_id;";

                command.Parameters.AddWithValue(
                    "@mapId",
                    mapId.ToString("D"));

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new MapLocationLayout(
                                Guid.Parse(
                                    reader.GetString(0)),
                                reader.GetDouble(1),
                                reader.GetDouble(2),
                                reader.GetDouble(3),
                                reader.GetDouble(4),
                                reader.GetInt32(5) != 0,
                                reader.GetInt32(6) != 0));
                    }
                }
            }

            return result;
        }

        private static void EnsureMap(
            System.Data.SQLite.SQLiteConnection connection,
            Guid mapId)
        {
            using (var command =
                connection.CreateCommand())
            {
                var now =
                    UtcNowText();

                command.CommandText = @"
INSERT OR IGNORE INTO maps
(
    id,
    name,
    default_zoom,
    default_pan_x,
    default_pan_y,
    created_at_utc,
    updated_at_utc
)
VALUES
(
    @mapId,
    @name,
    1.0,
    0.0,
    0.0,
    @createdUtc,
    @updatedUtc
);";

                command.Parameters.AddWithValue(
                    "@mapId",
                    mapId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@name",
                    MapLayoutScope.PhysicalTopologyMapName);

                command.Parameters.AddWithValue(
                    "@createdUtc",
                    now);

                command.Parameters.AddWithValue(
                    "@updatedUtc",
                    now);

                command.ExecuteNonQuery();
            }
        }

        private static string UtcNowText()
        {
            return DateTime.UtcNow.ToString(
                "o",
                CultureInfo.InvariantCulture);
        }

        private static void ValidateMapId(
            Guid mapId)
        {
            if (mapId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Map id is required.",
                    nameof(mapId));
            }
        }
    }
}
