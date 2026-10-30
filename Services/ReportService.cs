using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PPEAccounting.Data;

namespace PPEAccounting.Services
{
    /// <summary>
    /// Данные для печатных отчётов. Формы только выбирают параметры и передают
    /// готовые строки в построитель документа, поэтому расчёты можно проверить тестом.
    /// </summary>
    public sealed class ReportService
    {
        private const int NormWindowMonths = 12;

        private const string CoverageSql = @"
SELECT e.IdEmployee, e.FullName, pos.Name, i.Name, n.QtyPerYear, n.WearPeriodMonths,
       IFNULL((SELECT SUM(iss.Qty)
               FROM PPEIssue iss
               WHERE iss.IdEmployee = e.IdEmployee
                 AND iss.IdPPEItem = n.IdPPEItem
                 AND iss.ReturnDate IS NULL
                 AND iss.IssueDate > @from
                 AND iss.IssueDate <= @to), 0),
       (SELECT MAX(iss.IssueDate)
        FROM PPEIssue iss
        WHERE iss.IdEmployee = e.IdEmployee
          AND iss.IdPPEItem = n.IdPPEItem)
FROM Employee e
JOIN Position pos ON pos.IdPosition = e.IdPosition
JOIN IssueNorm n  ON n.IdPosition = e.IdPosition
JOIN PPEItem i    ON i.IdPPEItem = n.IdPPEItem
WHERE e.IsActive = 1
  AND (@employee IS NULL OR e.IdEmployee = @employee)
  AND (@department IS NULL OR pos.IdDepartment = @department)
ORDER BY e.FullName, i.Name";

        private const string JournalSql = @"
SELECT iss.IdIssue, e.FullName, pos.Name, i.Name, iss.IssueDate, iss.Qty, iss.ReturnDate, iss.Status,
       iss.ReceivedDate, iss.IdEmployee
FROM PPEIssue iss
JOIN Employee e   ON e.IdEmployee = iss.IdEmployee
JOIN Position pos ON pos.IdPosition = e.IdPosition
JOIN PPEItem i    ON i.IdPPEItem = iss.IdPPEItem
WHERE iss.IssueDate >= @from
  AND iss.IssueDate <= @to
  AND (@department IS NULL OR pos.IdDepartment = @department)
ORDER BY iss.IssueDate, iss.IdIssue";

        private readonly Database _database;
        private readonly IPpeItemRepository _items;

        public ReportService(Database database, IPpeItemRepository items)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        /// <summary>Обеспеченность работника по нормам: положено, выдано за 12 месяцев, остаток.</summary>
        public IReadOnlyList<NormCoverageRow> GetCoverage(int? employeeId, int? departmentId, DateTime onDate)
        {
            var from = onDate.Date.AddMonths(-NormWindowMonths);

            return _database.QueryList(CoverageSql, reader => new NormCoverageRow
            {
                EmployeeId = reader.GetInt32(0),
                EmployeeName = reader.GetString(1),
                PositionName = reader.GetString(2),
                ItemName = reader.GetString(3),
                QtyPerYear = reader.GetInt32(4),
                WearPeriodMonths = reader.GetInt32(5),
                Issued = reader.GetInt32(6),
                LastIssueDate = SqlDate.ParseOrNull(reader.GetValue(7))
            },
                new SQLiteParameter("@from", SqlDate.ToText(from)),
                new SQLiteParameter("@to", SqlDate.ToText(onDate.Date)),
                new SQLiteParameter("@employee", (object)employeeId ?? DBNull.Value),
                new SQLiteParameter("@department", (object)departmentId ?? DBNull.Value));
        }

        public IReadOnlyList<IssueRow> GetJournal(DateTime from, DateTime to, int? departmentId)
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
            },
                new SQLiteParameter("@from", SqlDate.ToText(from.Date)),
                new SQLiteParameter("@to", SqlDate.ToText(to.Date)),
                new SQLiteParameter("@department", (object)departmentId ?? DBNull.Value));
        }

        public IReadOnlyList<PpeItemRow> GetStock()
        {
            return _items.GetAll();
        }

        public IReadOnlyList<LookupItem> GetDepartments()
        {
            return _database.QueryList(
                "SELECT IdDepartment, Name FROM Department ORDER BY Name",
                reader => new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }
    }
}
