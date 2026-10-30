using System;
using System.Data.SQLite;

namespace PPEAccounting.Data
{
    /// <summary>
    /// Низкоуровневые операции, которые можно выполнять на уже открытом соединении
    /// (в том числе внутри транзакции, переданной сервисом).
    /// </summary>
    internal static class SqlExtensions
    {
        public static int Exec(this SQLiteConnection connection, SQLiteTransaction transaction, string sql, params SQLiteParameter[] parameters)
        {
            using (var command = new SQLiteCommand(sql, connection, transaction))
            {
                AddParameters(command, parameters);
                return command.ExecuteNonQuery();
            }
        }

        public static object Scalar(this SQLiteConnection connection, SQLiteTransaction transaction, string sql, params SQLiteParameter[] parameters)
        {
            using (var command = new SQLiteCommand(sql, connection, transaction))
            {
                AddParameters(command, parameters);
                return command.ExecuteScalar();
            }
        }

        public static int Insert(this SQLiteConnection connection, SQLiteTransaction transaction, string sql, params SQLiteParameter[] parameters)
        {
            Exec(connection, transaction, sql, parameters);
            return checked((int)connection.LastInsertRowId);
        }

        public static T QuerySingle<T>(this SQLiteConnection connection, SQLiteTransaction transaction, string sql,
            Func<SQLiteDataReader, T> map, params SQLiteParameter[] parameters)
            where T : class
        {
            using (var command = new SQLiteCommand(sql, connection, transaction))
            {
                AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                    return reader.Read() ? map(reader) : null;
            }
        }

        internal static void AddParameters(SQLiteCommand command, SQLiteParameter[] parameters)
        {
            if (parameters == null)
                return;

            foreach (var parameter in parameters)
            {
                if (parameter != null)
                    command.Parameters.Add(parameter);
            }
        }
    }
}
