using System;
using System.Globalization;
using NetLoom.Application.Observations;
using NetLoom.Domain.Observations;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Observations
{
    public sealed class SqliteObservationRetentionStore
        : IObservationRetentionStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteObservationRetentionStore(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public int DeleteOlderThan(
            DateTime cutoffUtc,
            int maxObservations)
        {
            if (cutoffUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Retention cutoff must be UTC.",
                    nameof(cutoffUtc));
            }

            if (maxObservations <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxObservations));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                var deleted = 0;

                while (deleted < maxObservations)
                {
                    var deletedOne =
                        SqliteImmediateWrite.Execute(
                            connection,
                            () => DeleteOldestOne(
                                connection,
                                cutoffUtc));

                    if (deletedOne == 0)
                    {
                        break;
                    }

                    deleted +=
                        deletedOne;
                }

                return deleted;
            }
        }

        private static int DeleteOldestOne(
            System.Data.SQLite.SQLiteConnection connection,
            DateTime cutoffUtc)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
DELETE FROM observations
WHERE observation_id =
(
    SELECT observation_id
    FROM observations
    WHERE captured_utc < @cutoffUtc
      AND observation_kind <> @manualKind
    ORDER BY
        captured_utc,
        observation_id
    LIMIT 1
);";

                command.Parameters.AddWithValue(
                    "@cutoffUtc",
                    cutoffUtc.ToString(
                        "o",
                        CultureInfo.InvariantCulture));

                command.Parameters.AddWithValue(
                    "@manualKind",
                    ObservationKind.Manual.ToString());

                return command.ExecuteNonQuery();
            }
        }
    }
}
