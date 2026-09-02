using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Observations
{
    public sealed class SqliteObservationStore
        : IObservationStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteObservationStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void SaveSnmp(SnmpObservation snmpObservation)
        {
            if (snmpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(snmpObservation));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var transaction =
                connection.BeginTransaction())
            {
                SaveObservation(
                    connection,
                    transaction,
                    snmpObservation.Observation);

                SaveVarbinds(
                    connection,
                    transaction,
                    snmpObservation);

                transaction.Commit();
            }
        }

        public SnmpObservation GetSnmp(Guid observationId)
        {
            if (observationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Observation id is required.",
                    nameof(observationId));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                Observation observation;

                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    observation_kind,
    source_address,
    captured_utc
FROM observations
WHERE observation_id = @id;";

                    command.Parameters.AddWithValue(
                        "@id",
                        observationId.ToString("D"));

                    using (var reader =
                        command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        observation =
                            new Observation(
                                observationId,
                                (ObservationKind)Enum.Parse(
                                    typeof(ObservationKind),
                                    reader.GetString(0)),
                                reader.GetString(1),
                                DateTime.Parse(
                                    reader.GetString(2),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind));
                    }
                }

                return new SnmpObservation(
                    observation,
                    LoadVarbinds(
                        connection,
                        observationId));
            }
        }

        private static void SaveObservation(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            Observation observation)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = @"
INSERT INTO observations
(
    observation_id,
    observation_kind,
    source_address,
    captured_utc
)
VALUES
(
    @id,
    @kind,
    @sourceAddress,
    @capturedUtc
);";

                command.Parameters.AddWithValue(
                    "@id",
                    observation.Id.ToString("D"));
                command.Parameters.AddWithValue(
                    "@kind",
                    observation.Kind.ToString());
                command.Parameters.AddWithValue(
                    "@sourceAddress",
                    observation.SourceAddress);
                command.Parameters.AddWithValue(
                    "@capturedUtc",
                    observation.CapturedUtc.ToString(
                        "o",
                        CultureInfo.InvariantCulture));

                command.ExecuteNonQuery();
            }
        }

        private static void SaveVarbinds(
            System.Data.SQLite.SQLiteConnection connection,
            System.Data.SQLite.SQLiteTransaction transaction,
            SnmpObservation snmpObservation)
        {
            for (var index = 0;
                index < snmpObservation.Variables.Count;
                index++)
            {
                var variable =
                    snmpObservation.Variables[index];

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
INSERT INTO snmp_varbinds
(
    observation_id,
    sequence_no,
    oid,
    type_code,
    display_value,
    encoded_value
)
VALUES
(
    @observationId,
    @sequence,
    @oid,
    @typeCode,
    @displayValue,
    @encodedValue
);";

                    command.Parameters.AddWithValue(
                        "@observationId",
                        snmpObservation.Observation.Id
                            .ToString("D"));
                    command.Parameters.AddWithValue(
                        "@sequence",
                        index);
                    command.Parameters.AddWithValue(
                        "@oid",
                        variable.Oid);
                    command.Parameters.AddWithValue(
                        "@typeCode",
                        variable.TypeCode);
                    command.Parameters.AddWithValue(
                        "@displayValue",
                        (object)variable.DisplayValue ??
                        DBNull.Value);
                    command.Parameters.AddWithValue(
                        "@encodedValue",
                        variable.GetEncodedValue());

                    command.ExecuteNonQuery();
                }
            }
        }

        private static IReadOnlyList<SnmpVariable> LoadVarbinds(
            System.Data.SQLite.SQLiteConnection connection,
            Guid observationId)
        {
            var result =
                new List<SnmpVariable>();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    oid,
    type_code,
    display_value,
    encoded_value
FROM snmp_varbinds
WHERE observation_id = @id
ORDER BY sequence_no;";

                command.Parameters.AddWithValue(
                    "@id",
                    observationId.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            new SnmpVariable(
                                reader.GetString(0),
                                reader.GetInt32(1),
                                reader.IsDBNull(2)
                                    ? null
                                    : reader.GetString(2),
                                (byte[])reader.GetValue(3)));
                    }
                }
            }

            return result;
        }
    }
}
