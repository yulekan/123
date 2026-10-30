using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace PPEAccounting.Data
{
    /// <summary>Журнал действий: только добавление записей, изменение и удаление запрещены.</summary>
    public sealed class AuditRepository
    {
        private readonly Database _database;

        public AuditRepository(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public void Write(string action, string details)
        {
            _database.Execute(@"
INSERT INTO AuditLog(Action, Details, CreatedAt)
VALUES(@action, @details, @created)",
                new SQLiteParameter("@action", action ?? string.Empty),
                new SQLiteParameter("@details", details ?? string.Empty),
                new SQLiteParameter("@created", SqlDate.ToText(DateTime.Now)));
        }

        public IReadOnlyList<AuditRow> GetRecent(int limit)
        {
            return _database.QueryList(@"
SELECT IdAudit, CreatedAt, Action, Details
FROM AuditLog
ORDER BY IdAudit DESC
LIMIT @limit",
                reader => new AuditRow
                {
                    Id = reader.GetInt32(0),
                    CreatedAt = SqlDate.Parse(reader.GetString(1)),
                    Action = reader.GetString(2),
                    Details = reader.GetString(3)
                },
                new SQLiteParameter("@limit", limit));
        }

        public int Count()
        {
            return SqlValue.ToInt(_database.Scalar("SELECT COUNT(*) FROM AuditLog"));
        }
    }
}
