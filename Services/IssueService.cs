using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PPEAccounting.Data;

namespace PPEAccounting.Services
{
    /// <summary>Сведения о норме и остатке лимита для конкретной пары «сотрудник + СИЗ».</summary>
    public sealed class NormAvailability
    {
        public bool EmployeeFound { get; set; }
        public string PositionName { get; set; } = string.Empty;
        public bool HasNorm { get; set; }
        public int QtyPerYear { get; set; }
        public int WearPeriodMonths { get; set; }
        public int AlreadyIssued { get; set; }
        public DateTime? LastIssueDate { get; set; }

        public int Remaining
        {
            get { return Math.Max(0, QtyPerYear - AlreadyIssued); }
        }
    }

    public interface IIssueService
    {
        IReadOnlyList<IssueRow> GetJournal();
        IReadOnlyList<EmployeePpeRow> GetEmployeeIssues(int employeeId);
        NormAvailability GetAvailability(int employeeId, int ppeItemId, DateTime onDate);
        OperationResult Issue(int employeeId, int ppeItemId, int qty, DateTime date);
        OperationResult Return(int issueId, DateTime date);
        OperationResult ConfirmReceipt(int issueId, DateTime date);
    }

    /// <summary>
    /// Бизнес-правила выдачи и возврата СИЗ.
    /// Все изменения остатка и журнала выполняются в одной транзакции: либо
    /// применяются оба изменения, либо ни одного.
    /// </summary>
    public sealed class IssueService : IIssueService
    {
        /// <summary>Лимит нормы считается за скользящие 12 месяцев (а не за календарный год).</summary>
        private const int NormWindowMonths = 12;

        private const string JournalSql = @"
SELECT iss.IdIssue, e.FullName, pos.Name, i.Name, iss.IssueDate, iss.Qty, iss.ReturnDate, iss.Status,
       iss.ReceivedDate, iss.IdEmployee
FROM PPEIssue iss
JOIN Employee e   ON e.IdEmployee = iss.IdEmployee
JOIN Position pos ON pos.IdPosition = e.IdPosition
JOIN PPEItem i    ON i.IdPPEItem = iss.IdPPEItem
ORDER BY iss.IssueDate DESC, iss.IdIssue DESC";

        private const string EmployeeJournalSql = @"
SELECT iss.IdIssue, iss.IssueDate, i.Name, i.Unit, iss.Qty, iss.ReturnDate, iss.ReceivedDate,
       iss.Status, IFNULL(n.WearPeriodMonths, 0)
FROM PPEIssue iss
JOIN PPEItem i        ON i.IdPPEItem = iss.IdPPEItem
LEFT JOIN IssueNorm n ON n.IdNorm = iss.IdNorm
WHERE iss.IdEmployee = @employee
ORDER BY iss.IssueDate DESC, iss.IdIssue DESC";

        private const string UsageSql = @"
SELECT IFNULL(SUM(Qty), 0)
FROM PPEIssue
WHERE IdEmployee = @employee
  AND IdPPEItem = @item
  AND ReturnDate IS NULL
  AND IssueDate > @from
  AND IssueDate <= @to";

        private const string NormSql = @"
SELECT n.IdNorm, pos.Name, n.QtyPerYear, n.WearPeriodMonths
FROM Employee e
JOIN Position pos ON pos.IdPosition = e.IdPosition
LEFT JOIN IssueNorm n ON n.IdPosition = e.IdPosition AND n.IdPPEItem = @item
WHERE e.IdEmployee = @employee";

        private readonly Database _database;

        public IssueService(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public IReadOnlyList<IssueRow> GetJournal()
        {
            return _database.QueryList(JournalSql, reader => new IssueRow
            {
                Id = reader.GetInt32(0),
                EmployeeName = reader.GetString(1),
                PositionName = reader.GetString(2),
                ItemName = reader.GetString(3),
                IssueDate = SqlDate.Parse(reader.GetString(4)),
                Qty = reader.GetInt32(5),
                ReturnDate = SqlDate.ParseOrNull(reader.GetValue(6)),
                Status = reader.GetString(7),
                ReceivedDate = SqlDate.ParseOrNull(reader.GetValue(8)),
                EmployeeId = reader.GetInt32(9)
            });
        }

        public IReadOnlyList<EmployeePpeRow> GetEmployeeIssues(int employeeId)
        {
            return _database.QueryList(EmployeeJournalSql, reader => new EmployeePpeRow
            {
                IssueId = reader.GetInt32(0),
                IssueDate = SqlDate.Parse(reader.GetString(1)),
                ItemName = reader.GetString(2),
                Unit = reader.GetString(3),
                Qty = reader.GetInt32(4),
                ReturnDate = SqlDate.ParseOrNull(reader.GetValue(5)),
                ReceivedDate = SqlDate.ParseOrNull(reader.GetValue(6)),
                Status = reader.GetString(7),
                WearPeriodMonths = reader.GetInt32(8)
            },
                new SQLiteParameter("@employee", employeeId));
        }

        public NormAvailability GetAvailability(int employeeId, int ppeItemId, DateTime onDate)
        {
            using (var connection = _database.Open())
            {
                var norm = ReadNorm(connection, null, employeeId, ppeItemId);
                if (norm == null)
                    return new NormAvailability { EmployeeFound = false };

                return new NormAvailability
                {
                    EmployeeFound = true,
                    PositionName = norm.PositionName,
                    HasNorm = norm.NormId.HasValue,
                    QtyPerYear = norm.QtyPerYear,
                    WearPeriodMonths = norm.WearPeriodMonths,
                    AlreadyIssued = ReadUsage(connection, null, employeeId, ppeItemId, onDate),
                    LastIssueDate = ReadLastIssueDate(connection, null, employeeId, ppeItemId)
                };
            }
        }

        public OperationResult Issue(int employeeId, int ppeItemId, int qty, DateTime date)
        {
            if (qty <= 0)
                return OperationResult.Fail("Количество должно быть больше нуля.");

            if (date.Date > DateTime.Today)
                return OperationResult.Fail("Дата выдачи не может быть в будущем.");


            return _database.InTransaction((connection, transaction) =>
            {
                var norm = ReadNorm(connection, transaction, employeeId, ppeItemId);
                if (norm == null)
                    return OperationResult.Fail("Сотрудник не найден.");

                if (!norm.NormId.HasValue)
                    return OperationResult.Fail(
                        "Для должности «" + norm.PositionName + "» норма на этот вид СИЗ не предусмотрена.");

                var already = ReadUsage(connection, transaction, employeeId, ppeItemId, date);
                var limit = norm.QtyPerYear;

                if (already + qty > limit)
                    return OperationResult.Fail(
                        "Превышена норма выдачи: за последние 12 месяцев выдано " + already +
                        " из " + limit + ", запрошено ещё " + qty + ".");

                // Проверка и списание остатка одним запросом: остаток не может уйти в минус
                // даже при одновременной работе двух копий программы.
                var stockAffected = connection.Exec(transaction,
                    "UPDATE PPEItem SET StockQty = StockQty - @qty WHERE IdPPEItem = @id AND StockQty >= @qty",
                    new SQLiteParameter("@qty", qty),
                    new SQLiteParameter("@id", ppeItemId));

                if (stockAffected == 0)
                {
                    var stock = ReadStock(connection, transaction, ppeItemId);
                    return OperationResult.Fail(
                        stock.HasValue
                            ? "Недостаточно на складе: остаток " + stock.Value + "."
                            : "Вид СИЗ не найден.");
                }

                var issueId = connection.Insert(transaction, @"
INSERT INTO PPEIssue(IdEmployee, IdPPEItem, IdNorm, IssueDate, Qty, Status)
VALUES(@employee, @item, @norm, @date, @qty, @status)",
                    new SQLiteParameter("@employee", employeeId),
                    new SQLiteParameter("@item", ppeItemId),
                    new SQLiteParameter("@norm", (object)norm.NormId.Value),
                    new SQLiteParameter("@date", SqlDate.ToText(date)),
                    new SQLiteParameter("@qty", qty),
                    new SQLiteParameter("@status", IssueStatus.Issued));

                return OperationResult.Ok("Выдача оформлена.", issueId);
            });
        }

        public OperationResult Return(int issueId, DateTime date)
        {
            if (date.Date > DateTime.Today)
                return OperationResult.Fail("Дата возврата не может быть в будущем.");

            return _database.InTransaction((connection, transaction) =>
            {
                var issue = connection.QuerySingle(transaction,
                    "SELECT IdPPEItem, Qty, Status FROM PPEIssue WHERE IdIssue = @id",
                    reader => new IssueRecord
                    {
                        ItemId = reader.GetInt32(0),
                        Qty = reader.GetInt32(1),
                        Status = reader.GetString(2)
                    },
                    new SQLiteParameter("@id", issueId));

                if (issue == null)
                    return OperationResult.Fail("Запись о выдаче не найдена.");

                if (issue.Status == IssueStatus.Returned)
                    return OperationResult.Fail("Эта запись уже возвращена.");

                var affected = connection.Exec(transaction, @"
UPDATE PPEIssue
SET ReturnDate = @date, Status = @returned
WHERE IdIssue = @id AND Status = @issued",
                    new SQLiteParameter("@date", SqlDate.ToText(date)),
                    new SQLiteParameter("@returned", IssueStatus.Returned),
                    new SQLiteParameter("@id", issueId),
                    new SQLiteParameter("@issued", IssueStatus.Issued));

                if (affected == 0)
                    return OperationResult.Fail("Запись уже изменена, обновите журнал.");

                // Возврат обязан вернуть СИЗ на склад, иначе остатки «тают».
                connection.Exec(transaction,
                    "UPDATE PPEItem SET StockQty = StockQty + @qty WHERE IdPPEItem = @item",
                    new SQLiteParameter("@qty", issue.Qty),
                    new SQLiteParameter("@item", issue.ItemId));

                return OperationResult.Ok("Возврат оформлен.");
            });
        }

        /// <summary>
        /// Подтверждение получения СИЗ работником. Подтвердить может только владелец записи,
        /// только по выданной и ещё не подтверждённой позиции.
        /// </summary>
        public OperationResult ConfirmReceipt(int issueId, DateTime date)
        {
            if (date.Date > DateTime.Today)
                return OperationResult.Fail("Дата подтверждения не может быть в будущем.");

            var affected = _database.Execute(@"
UPDATE PPEIssue
SET ReceivedDate = @date
WHERE IdIssue = @id
  AND Status = @issued
  AND ReceivedDate IS NULL",
                new SQLiteParameter("@date", SqlDate.ToText(date)),
                new SQLiteParameter("@id", issueId),
                new SQLiteParameter("@issued", IssueStatus.Issued));

            if (affected == 0)
                return OperationResult.Fail("Запись не найдена, возвращена или получение уже подтверждено.");

            return OperationResult.Ok("Получение подтверждено.");
        }

        private sealed class NormRecord
        {
            public int? NormId { get; set; }
            public string PositionName { get; set; }
            public int QtyPerYear { get; set; }
            public int WearPeriodMonths { get; set; }
        }

        private sealed class IssueRecord
        {
            public int ItemId { get; set; }
            public int Qty { get; set; }
            public string Status { get; set; }
        }

        private static NormRecord ReadNorm(SQLiteConnection connection, SQLiteTransaction transaction,
            int employeeId, int ppeItemId)
        {
            return connection.QuerySingle(transaction, NormSql, reader => new NormRecord
            {
                NormId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0),
                PositionName = reader.GetString(1),
                QtyPerYear = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                WearPeriodMonths = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
            },
                new SQLiteParameter("@item", ppeItemId),
                new SQLiteParameter("@employee", employeeId));
        }

        private static int ReadUsage(SQLiteConnection connection, SQLiteTransaction transaction,
            int employeeId, int ppeItemId, DateTime onDate)
        {
            var from = onDate.Date.AddMonths(-NormWindowMonths);
            var to = onDate.Date;

            return SqlValue.ToInt(connection.Scalar(transaction, UsageSql,
                new SQLiteParameter("@employee", employeeId),
                new SQLiteParameter("@item", ppeItemId),
                new SQLiteParameter("@from", SqlDate.ToText(from)),
                new SQLiteParameter("@to", SqlDate.ToText(to))));
        }

        private static DateTime? ReadLastIssueDate(SQLiteConnection connection, SQLiteTransaction transaction,
            int employeeId, int ppeItemId)
        {
            var value = connection.Scalar(transaction,
                "SELECT MAX(IssueDate) FROM PPEIssue WHERE IdEmployee = @employee AND IdPPEItem = @item",
                new SQLiteParameter("@employee", employeeId),
                new SQLiteParameter("@item", ppeItemId));

            return SqlDate.ParseOrNull(value);
        }

        private static int? ReadStock(SQLiteConnection connection, SQLiteTransaction transaction, int ppeItemId)
        {
            var value = connection.Scalar(transaction,
                "SELECT StockQty FROM PPEItem WHERE IdPPEItem = @id",
                new SQLiteParameter("@id", ppeItemId));

            return SqlValue.ToIntOrNull(value);
        }
    }
}
