using System;
using System.Data.SQLite;
using System.IO;

namespace NetLoom.Persistence.Sqlite.Database
{
    public sealed class SqliteConnectionFactory
    {
        private readonly string _databasePath;

        public SqliteConnectionFactory(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(databasePath));
            }

            _databasePath = Path.GetFullPath(databasePath);
        }

        public string DatabasePath
        {
            get { return _databasePath; }
        }

        public SQLiteConnection OpenConnection()
        {
            var directory = Path.GetDirectoryName(_databasePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connection = new SQLiteConnection(
                "Data Source=" + _databasePath + ";Foreign Keys=True;");

            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON;";
                command.ExecuteNonQuery();
            }

            return connection;
        }
    }
}
