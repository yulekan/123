using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace PPEAccounting.Data
{
    /// <summary>
    /// Единственная точка доступа к файлу базы данных.
    /// Соединения короткоживущие (открываются на операцию), транзакции — через <see cref="InTransaction{T}"/>.
    /// </summary>
    public sealed class Database
    {
        private readonly string _connectionString;

        public Database(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("Не задан путь к файлу базы данных.", nameof(dbPath));

            DbPath = Path.GetFullPath(dbPath);

            var directory = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            _connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = DbPath,
                Version = 3,
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal,
                BusyTimeout = 5000
            }.ToString();
        }

        /// <summary>Полный путь к файлу базы (показывается пользователю).</summary>
        public string DbPath { get; }

        public SQLiteConnection Open()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>Выполняет запрос и возвращает число затронутых строк.</summary>
        public int Execute(string sql, params SQLiteParameter[] parameters)
        {
            using (var connection = Open())
                return connection.Exec(null, sql, parameters);
        }

        public object Scalar(string sql, params SQLiteParameter[] parameters)
        {
            using (var connection = Open())
                return connection.Scalar(null, sql, parameters);
        }

        /// <summary>
        /// INSERT с возвратом Id новой строки. Значение читается на том же соединении,
        /// иначе SQLite вернёт 0 (last_insert_rowid() привязан к соединению).
        /// </summary>
        public int Insert(string sql, params SQLiteParameter[] parameters)
        {
            using (var connection = Open())
                return connection.Insert(null, sql, parameters);
        }

        public List<T> QueryList<T>(string sql, Func<SQLiteDataReader, T> map, params SQLiteParameter[] parameters)
        {
            var result = new List<T>();

            using (var connection = Open())
            using (var command = new SQLiteCommand(sql, connection))
            {
                SqlExtensions.AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(map(reader));
                }
            }

            return result;
        }

        public T QuerySingle<T>(string sql, Func<SQLiteDataReader, T> map, params SQLiteParameter[] parameters)
            where T : class
        {
            using (var connection = Open())
                return connection.QuerySingle(null, sql, map, parameters);
        }

        public void InTransaction(Action<SQLiteConnection, SQLiteTransaction> work)
        {
            InTransaction((connection, transaction) =>
            {
                work(connection, transaction);
                return true;
            });
        }

        /// <summary>
        /// Выполняет работу в транзакции: изменения фиксируются только при успехе,
        /// при исключении SQLite откатывает всё автоматически.
        /// </summary>
        public T InTransaction<T>(Func<SQLiteConnection, SQLiteTransaction, T> work)
        {
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            {
                var result = work(connection, transaction);
                transaction.Commit();
                return result;
            }
        }
    }
}
