using System;
using System.Globalization;
using NetLoom.Application.Observations;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Observations
{
    public sealed class
        SqliteObservationDeviceBindingStore :
        IObservationDeviceBindingStore
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteObservationDeviceBindingStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public void Bind(
            Guid observationId,
            Guid deviceId)
        {
            if (observationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id is required.",
                    nameof(observationId));
            }

            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Device id is required.",
                    nameof(deviceId));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        using (var check =
                            connection.CreateCommand())
                        {
                            check.CommandText = @"
SELECT device_id
FROM observation_device_bindings
WHERE observation_id = @observationId;";

                            check.Parameters.AddWithValue(
                                "@observationId",
                                observationId.ToString("D"));

                            var existing =
                                check.ExecuteScalar();

                            if (existing != null &&
                                existing != DBNull.Value)
                            {
                                var existingDeviceId =
                                    Guid.Parse(
                                        Convert.ToString(
                                            existing,
                                            CultureInfo
                                                .InvariantCulture));

                                if (existingDeviceId !=
                                    deviceId)
                                {
                                    throw new
                                        InvalidOperationException(
                                            "Observation is already bound to another DeviceId.");
                                }

                                return;
                            }
                        }

                        using (var insert =
                            connection.CreateCommand())
                        {
                            insert.CommandText = @"
INSERT INTO observation_device_bindings
(
    observation_id,
    device_id
)
VALUES
(
    @observationId,
    @deviceId
);";

                            insert.Parameters.AddWithValue(
                                "@observationId",
                                observationId.ToString("D"));

                            insert.Parameters.AddWithValue(
                                "@deviceId",
                                deviceId.ToString("D"));

                            insert.ExecuteNonQuery();
                        }
                    });
            }
        }

        public Guid? GetDeviceId(
            Guid observationId)
        {
            if (observationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id is required.",
                    nameof(observationId));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT device_id
FROM observation_device_bindings
WHERE observation_id = @observationId;";

                command.Parameters.AddWithValue(
                    "@observationId",
                    observationId.ToString("D"));

                var value =
                    command.ExecuteScalar();

                if (value == null ||
                    value == DBNull.Value)
                {
                    return null;
                }

                return Guid.Parse(
                    Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture));
            }
        }
    }
}