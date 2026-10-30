using System;
using System.Collections.Generic;
using System.Linq;
using PPEAccounting.Data;

namespace PPEAccounting.Reports
{
    /// <summary>
    /// Пять печатных форм. Каждая собирает документ из уже подготовленных данных,
    /// поэтому печать не обращается к базе повторно.
    /// </summary>
    internal static class ReportFactory
    {
        public static ReportDocument PersonalCard(AppServices services, int employeeId)
        {
            var employee = services.Employees.GetAll().FirstOrDefault(row => row.Id == employeeId);
            if (employee == null)
                throw new InvalidOperationException("Сотрудник не найден.");

            var document = new ReportDocument("ЛИЧНАЯ КАРТОЧКА УЧЁТА СИЗ");
            document.AddTitle("ЛИЧНАЯ КАРТОЧКА УЧЁТА СИЗ");
            document.AddSubtitle(
                "Форма разработана в соответствии с Инструкцией о порядке обеспечения работников " +
                "средствами индивидуальной защиты (постановление Минтруда и соцзащиты Республики " +
                "Беларусь от 30.12.2008 № 209)");

            document.AddInfo("Фамилия, имя, отчество", employee.FullName);
            document.AddInfo("Должность", employee.PositionName);
            document.AddInfo("Подразделение", employee.DepartmentName);
            document.AddInfo("Дата приёма на работу", employee.HireDate.ToString("dd.MM.yyyy"));
            document.AddInfo("Табельный номер", employee.Id.ToString());
            document.AddInfo("Дата открытия карточки", DateTime.Today.ToString("dd.MM.yyyy"));

            var coverage = services.Reports.GetCoverage(employeeId, null, DateTime.Today)
                .ToDictionary(row => row.ItemName, row => row);

            var issues = services.IssueService.GetEmployeeIssues(employeeId);

            var rows = new List<string[]>();
            var number = 1;

            foreach (var issue in issues.OrderBy(row => row.IssueDate))
            {
                NormCoverageRow norm;
                coverage.TryGetValue(issue.ItemName, out norm);

                var normText = norm == null
                    ? "—"
                    : norm.QtyPerYear + " в год, " + norm.WearPeriodMonths + " мес.";

                rows.Add(new[]
                {
                    number.ToString(),
                    issue.ItemName,
                    issue.Unit,
                    normText,
                    issue.Qty.ToString(),
                    issue.IssueDate.ToString("dd.MM.yyyy"),
                    issue.ValidUntil.HasValue ? issue.ValidUntil.Value.ToString("dd.MM.yyyy") : "—",
                    issue.ReturnDate.HasValue ? "возвращено" : string.Empty
                });

                number++;
            }

            document.AddTable(
                new[] { "№", "Наименование СИЗ", "Ед.", "Норма выдачи", "Выдано", "Дата выдачи", "Срок носки до", "Отметка" },
                new[] { 0.04f, 0.26f, 0.07f, 0.13f, 0.08f, 0.11f, 0.12f, 0.19f },
                rows);

            document.AddNote("Срок носки исчисляется с даты выдачи. Отметка «возвращено» означает, что СИЗ сданы на склад.");

            document.AddSignature("Специалист по охране труда", "Работник");

            return document;
        }

        public static ReportDocument IssueSheet(AppServices services, DateTime from, DateTime to, int? departmentId)
        {
            var document = new ReportDocument("ВЕДОМОСТЬ ВЫДАЧИ СИЗ");
            document.AddTitle("ВЕДОМОСТЬ ВЫДАЧИ СИЗ");
            document.AddSubtitle("За период с " + from.ToString("dd.MM.yyyy") + " по " + to.ToString("dd.MM.yyyy"));

            var department = DepartmentName(services, departmentId);
            document.AddInfo("Подразделение", department);
            document.AddInfo("Дата составления", DateTime.Today.ToString("dd.MM.yyyy"));

            var journal = services.Reports.GetJournal(from, to, departmentId);
            var units = services.Items.GetAll().ToDictionary(row => row.Name, row => row.Unit);

            var rows = new List<string[]>();
            var number = 1;

            foreach (var issue in journal)
            {
                string unit;
                units.TryGetValue(issue.ItemName, out unit);

                rows.Add(new[]
                {
                    number.ToString(),
                    issue.IssueDate.ToString("dd.MM.yyyy"),
                    issue.EmployeeName,
                    issue.PositionName,
                    issue.ItemName,
                    unit ?? "шт",
                    issue.Qty.ToString(),
                    string.Empty
                });

                number++;
            }

            document.AddTable(
                new[] { "№", "Дата", "Сотрудник", "Должность", "Наименование СИЗ", "Ед.", "Кол-во", "Подпись" },
                new[] { 0.04f, 0.10f, 0.20f, 0.16f, 0.20f, 0.06f, 0.08f, 0.16f },
                rows);

            document.AddNote("Всего выдано позиций: " + journal.Count + ".");

            document.AddSignature("Выдал (специалист по охране труда)", "Получил (работник)");

            return document;
        }

        public static ReportDocument StockSheet(AppServices services)
        {
            var document = new ReportDocument("ВЕДОМОСТЬ ОСТАТКОВ СИЗ");
            document.AddTitle("ВЕДОМОСТЬ ОСТАТКОВ СИЗ НА СКЛАДЕ");
            document.AddInfo("Дата составления", DateTime.Today.ToString("dd.MM.yyyy"));

            var items = services.Reports.GetStock();

            var rows = items.Select((item, index) => new[]
            {
                (index + 1).ToString(),
                item.Name,
                item.Unit,
                item.StockQty.ToString(),
                item.MinStock.ToString(),
                (item.StockQty - item.MinStock).ToString(),
                string.IsNullOrEmpty(item.SupplierName) ? "—" : item.SupplierName
            }).ToList();

            document.AddTable(
                new[] { "№", "Наименование СИЗ", "Ед.", "Остаток", "Минимум", "Отклонение", "Поставщик" },
                new[] { 0.04f, 0.30f, 0.07f, 0.09f, 0.09f, 0.11f, 0.30f },
                rows);

            var shortage = items.Count(item => item.NeedsSupply);
            document.AddNote("Позиций с остатком не выше минимального: " + shortage + ". " +
                             "Отрицательное отклонение означает необходимость пополнения склада.");

            document.AddSignature("Специалист по охране труда", "Материально ответственное лицо");

            return document;
        }

        public static ReportDocument Compliance(AppServices services, int? departmentId)
        {
            var document = new ReportDocument("ОТЧЁТ О СОБЛЮДЕНИИ НОРМ ВЫДАЧИ");
            document.AddTitle("ОТЧЁТ О СОБЛЮДЕНИИ НОРМ ВЫДАЧИ СИЗ");
            document.AddSubtitle("Норма контролируется за скользящие 12 месяцев");

            document.AddInfo("Подразделение", DepartmentName(services, departmentId));
            document.AddInfo("Дата составления", DateTime.Today.ToString("dd.MM.yyyy"));

            var coverage = services.Reports.GetCoverage(null, departmentId, DateTime.Today);

            var rows = coverage.Select(row => new[]
            {
                row.EmployeeName,
                row.PositionName,
                row.ItemName,
                row.QtyPerYear.ToString(),
                row.Issued.ToString(),
                row.Remaining.ToString(),
                row.ValidUntil.HasValue ? row.ValidUntil.Value.ToString("dd.MM.yyyy") : "—",
                row.Expired ? "да" : string.Empty
            }).ToList();

            document.AddTable(
                new[] { "Сотрудник", "Должность", "СИЗ", "Норма", "Выдано", "Остаток", "Срок носки до", "Просрочено" },
                new[] { 0.18f, 0.15f, 0.18f, 0.08f, 0.08f, 0.08f, 0.12f, 0.13f },
                rows);

            var employees = coverage.Select(row => row.EmployeeId).Distinct().Count();
            var expired = coverage.Count(row => row.Expired);
            var remaining = coverage.Count(row => row.Remaining > 0);

            document.AddNote("Сотрудников в отчёте: " + employees + ". Позиций с остатком лимита: " + remaining +
                             ". Позиций с истёкшим сроком носки: " + expired + ".");

            document.AddSignature("Специалист по охране труда", "Руководитель подразделения");

            return document;
        }

        public static ReportDocument Journal(AppServices services, DateTime from, DateTime to, int? departmentId)
        {
            var document = new ReportDocument("ЖУРНАЛ ВЫДАЧИ И ВОЗВРАТА СИЗ");
            document.AddTitle("ЖУРНАЛ ВЫДАЧИ И ВОЗВРАТА СИЗ");
            document.AddSubtitle("За период с " + from.ToString("dd.MM.yyyy") + " по " + to.ToString("dd.MM.yyyy"));

            document.AddInfo("Подразделение", DepartmentName(services, departmentId));
            document.AddInfo("Дата составления", DateTime.Today.ToString("dd.MM.yyyy"));

            var journal = services.Reports.GetJournal(from, to, departmentId);

            var rows = journal.Select((issue, index) => new[]
            {
                (index + 1).ToString(),
                issue.IssueDate.ToString("dd.MM.yyyy"),
                issue.EmployeeName,
                issue.ItemName,
                issue.Qty.ToString(),
                issue.ReturnDate.HasValue ? issue.ReturnDate.Value.ToString("dd.MM.yyyy") : "—",
                issue.Status,
                issue.ReceivedDate.HasValue ? issue.ReceivedDate.Value.ToString("dd.MM.yyyy") : "нет"
            }).ToList();

            document.AddTable(
                new[] { "№", "Дата", "Сотрудник", "СИЗ", "Кол-во", "Возврат", "Статус", "Получено" },
                new[] { 0.04f, 0.10f, 0.23f, 0.22f, 0.07f, 0.11f, 0.12f, 0.11f },
                rows);

            var returned = journal.Count(row => row.Status == IssueStatus.Returned);
            document.AddNote("Всего записей: " + journal.Count + ", из них возвращено: " + returned + ".");

            document.AddSignature("Специалист по охране труда", "Материально ответственное лицо");

            return document;
        }

        private static string DepartmentName(AppServices services, int? departmentId)
        {
            if (!departmentId.HasValue)
                return "все подразделения";

            var department = services.Reports.GetDepartments()
                .FirstOrDefault(row => row.Id == departmentId.Value);

            return department == null ? "все подразделения" : department.Name;
        }
    }
}
