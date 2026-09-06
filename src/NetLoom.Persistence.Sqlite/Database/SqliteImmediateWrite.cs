using System;
using System.Data.SQLite;

namespace NetLoom.Persistence.Sqlite.Database
{
    internal static class SqliteImmediateWrite
    {
        public static void Execute(
            SQLiteConnection connection,
            Action action)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(
                    nameof(connection));
            }

            if (action == null)
            {
                throw new ArgumentNullException(
                    nameof(action));
            }

            Execute<object>(
                connection,
                () =>
                {
                    action();
                    return null;
                });
        }

        public static T Execute<T>(
            SQLiteConnection connection,
            Func<T> action)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(
                    nameof(connection));
            }

            if (action == null)
            {
                throw new ArgumentNullException(
                    nameof(action));
            }

            ExecuteNonQuery(
                connection,
                "BEGIN IMMEDIATE;");

            try
            {
                var result =
                    action();

                ExecuteNonQuery(
                    connection,
                    "COMMIT;");

                return result;
            }
            catch
            {
                TryRollback(connection);
                throw;
            }
        }

        private static void TryRollback(
            SQLiteConnection connection)
        {
            try
            {
                ExecuteNonQuery(
                    connection,
                    "ROLLBACK;");
            }
            catch (SQLiteException)
            {
            }
        }

        private static void ExecuteNonQuery(
            SQLiteConnection connection,
            string commandText)
        {
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText =
                    commandText;

                command.ExecuteNonQuery();
            }
        }
    }
}
