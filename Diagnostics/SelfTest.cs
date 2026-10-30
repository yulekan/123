using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Forms;
using PPEAccounting.Reports;
using PPEAccounting.Services;
using PPEAccounting.Ui;

namespace PPEAccounting.Diagnostics
{
    /// <summary>
    /// Дымовой тест бизнес-логики на временной базе: нормы, остатки, возврат, приход,
    /// вход по паролю, роли, подтверждение получения и печатные отчёты.
    /// Запуск: PPEAccounting.exe --selftest
    /// Результат пишется в %TEMP%\ppe-selftest.log, код возврата 0 — успех.
    /// </summary>
    internal static class SelfTest
    {
        public static int Run()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), "ppe-selftest-" + Guid.NewGuid().ToString("N") + ".db");
            var logPath = Path.Combine(Path.GetTempPath(), "ppe-selftest.log");
            var failures = new List<string>();

            using (var log = new StreamWriter(logPath, false, Encoding.UTF8))
            {
                log.WriteLine("Дымовой тест: " + DateTime.Now.ToString("u"));

                try
                {
                    RunScenarios(dbPath, failures, log);
                }
                catch (Exception exception)
                {
                    failures.Add("Исключение: " + exception.Message);
                    log.WriteLine(exception);
                }

                log.WriteLine();
                log.WriteLine(failures.Count == 0 ? "РЕЗУЛЬТАТ: OK" : "РЕЗУЛЬТАТ: FAIL (" + failures.Count + ")");

                foreach (var failure in failures)
                    log.WriteLine(" - " + failure);
            }

            Cleanup(dbPath);

            return failures.Count == 0 ? 0 : 1;
        }

        private static void RunScenarios(string dbPath, List<string> failures, TextWriter log)
        {
            var database = new Database(dbPath);
            SchemaMigrator.Migrate(database, seedDemoData: true);

            var services = new AppServices(database);

            Check(failures, log, services.Employees.GetAll().Count >= 4, "Демонстрационные сотрудники загружены");
            Check(failures, log, services.Items.GetAll().Count >= 5, "Номенклатура СИЗ загружена");

            Check(failures, log,
                SqlValue.ToInt(database.Scalar("PRAGMA user_version")) == SchemaMigrator.CurrentVersion,
                "Схема базы обновлена до версии " + SchemaMigrator.CurrentVersion);
            Check(failures, log,
                SqlValue.ToInt(database.Scalar(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'User'")) == 0,
                "Таблица учётных записей удалена из схемы");
            Check(failures, log,
                SqlValue.ToInt(database.Scalar(
                    "SELECT COUNT(*) FROM pragma_table_info('PPEIssue') WHERE name = 'IdIssuer'")) == 0,
                "Журнал выдачи не хранит ссылку на пользователя");
            Check(failures, log,
                SqlValue.ToInt(database.Scalar(
                    "SELECT COUNT(*) FROM pragma_table_info('AuditLog') WHERE name = 'UserName'")) == 0,
                "Журнал действий не привязан к учётной записи");

            var employee = services.Lookups.GetActiveEmployees().First();
            var gown = services.Lookups.GetItems().First(item => item.Name.StartsWith("Халат"));
            var gloves = services.Lookups.GetItems().First(item => item.Name.StartsWith("Перчатки"));

            var stockStart = services.Items.GetStock(gown.Id);
            Check(failures, log, stockStart == 50, "Начальный остаток халатов = 50");

            var availability = services.IssueService.GetAvailability(employee.Id, gown.Id, DateTime.Today);
            Check(failures, log, availability.HasNorm && availability.QtyPerYear == 2, "Норма по халатам найдена (2 в год)");
            Check(failures, log, availability.Remaining == 2, "Доступно к выдаче 2 единицы");

            // 1. Обычная выдача.
            var first = services.IssueService.Issue(employee.Id, gown.Id, 1, DateTime.Today);
            Check(failures, log, first.Success && first.Id > 0, "Выдача №1 оформлена и вернула корректный Id");
            Check(failures, log, services.Items.GetStock(gown.Id) == stockStart - 1, "Остаток уменьшен на 1");

            // 2. Вторая выдача в пределах нормы.
            var second = services.IssueService.Issue(employee.Id, gown.Id, 1, DateTime.Today);
            Check(failures, log, second.Success, "Выдача №2 в пределах нормы разрешена");

            // 3. Третья выдача сверх нормы.
            var third = services.IssueService.Issue(employee.Id, gown.Id, 1, DateTime.Today);
            Check(failures, log, !third.Success, "Выдача №3 сверх нормы отклонена");
            Check(failures, log, services.Items.GetStock(gown.Id) == stockStart - 2, "Отказ не изменил остаток");

            // 4. Возврат возвращает остаток и освобождает лимит.
            var returned = services.IssueService.Return(first.Id, DateTime.Today);
            Check(failures, log, returned.Success, "Возврат оформлен");
            Check(failures, log, services.Items.GetStock(gown.Id) == stockStart - 1, "Возврат вернул единицу на склад");
            Check(failures, log, !services.IssueService.Return(first.Id, DateTime.Today).Success, "Повторный возврат отклонён");

            var afterReturn = services.IssueService.Issue(employee.Id, gown.Id, 1, DateTime.Today);
            Check(failures, log, afterReturn.Success, "После возврата лимит снова доступен");

            // 5. Выдача сверх остатка (норма позволяет).
            database.Execute("UPDATE PPEItem SET StockQty = 5 WHERE IdPPEItem = @id",
                new SQLiteParameter("@id", gloves.Id));

            var overStock = services.IssueService.Issue(employee.Id, gloves.Id, 6, DateTime.Today);
            Check(failures, log, !overStock.Success, "Выдача сверх остатка отклонена");
            Check(failures, log, services.Items.GetStock(gloves.Id) == 5, "Остаток не изменился после отказа");

            // 6. Приход.
            var supplierId = services.Lookups.GetOrCreateSupplier("ООО «СпецСнаб»");
            var supply = services.StockService.RegisterSupply(gloves.Id, DateTime.Today, 10, 12.5m, supplierId);
            Check(failures, log, supply.Success, "Приход оформлен");
            Check(failures, log, services.Items.GetStock(gloves.Id) == 15, "Приход увеличил остаток на 10");

            // 7. Журнал.
            var journal = services.IssueService.GetJournal();
            Check(failures, log, journal.Count == 3, "В журнале 3 записи (2 выдачи + 1 после возврата)");
            Check(failures, log, journal.Count(row => row.Status == IssueStatus.Returned) == 1,
                "Одна запись имеет статус «Возвращена»");

            // 8. Уникальность норм. Демонстрационные данные покрывают большинство пар
            // «вид СИЗ + должность», поэтому сначала ищем свободную пару.
            var existingNorms = services.Norms.GetAll();
            var positions = services.Lookups.GetPositions();
            var freeItem = services.Lookups.GetItems()
                .First(item => positions.Any(position => !existingNorms.Any(norm =>
                    norm.ItemId == item.Id && norm.PositionId == position.Id)));
            var freePosition = positions
                .First(position => !existingNorms.Any(norm =>
                    norm.ItemId == freeItem.Id && norm.PositionId == position.Id));

            services.Norms.Add(freeItem.Id, freePosition.Id, 1, 24);

            var duplicateRejected = false;
            try
            {
                services.Norms.Add(freeItem.Id, freePosition.Id, 2, 12);
            }
            catch (InvalidOperationException)
            {
                duplicateRejected = true;
            }

            Check(failures, log, duplicateRejected, "Повторная норма для той же пары отклонена");

            // 9. Удаление используемого СИЗ запрещено.
            var deleteRejected = false;
            try
            {
                services.Items.Delete(gown.Id);
            }
            catch (InvalidOperationException)
            {
                deleteRejected = true;
            }

            Check(failures, log, deleteRejected, "Удаление СИЗ с историей выдач заблокировано");

            CheckReceipt(services, failures, log);
            CheckReports(services, failures, log);
            CheckForms(services, failures, log);
            CheckGrid(failures, log, services);
        }

        private static void CheckReceipt(AppServices services, List<string> failures, TextWriter log)
        {
            var target = services.IssueService.GetJournal()
                .FirstOrDefault(row => row.Status == IssueStatus.Issued && row.ReceivedDate == null);

            if (target == null)
            {
                Check(failures, log, false, "Нет выдачи для проверки подтверждения получения");
                return;
            }

            var confirmed = services.IssueService.ConfirmReceipt(target.Id, DateTime.Today);
            Check(failures, log, confirmed.Success, "Получение СИЗ подтверждено");

            var repeated = services.IssueService.ConfirmReceipt(target.Id, DateTime.Today);
            Check(failures, log, !repeated.Success, "Повторное подтверждение отклонено");

            var stored = services.IssueService.GetJournal().First(row => row.Id == target.Id);
            Check(failures, log, stored.ReceivedDate.HasValue, "Дата получения сохранена в журнале");
        }

        private static void CheckReports(AppServices services, List<string> failures, TextWriter log)
        {
            var employee = services.Lookups.GetActiveEmployees().First();
            var from = DateTime.Today.AddMonths(-12);
            var to = DateTime.Today;

            var card = ReportFactory.PersonalCard(services, employee.Id);
            Check(failures, log, card.RenderText().Contains("ЛИЧНАЯ КАРТОЧКА УЧЁТА СИЗ"),
                "Личная карточка учёта СИЗ формируется");
            Check(failures, log, card.RowCount > 0, "В личной карточке есть строки выдач");

            var sheet = ReportFactory.IssueSheet(services, from, to, null);
            Check(failures, log, sheet.RowCount > 0, "Ведомость выдачи содержит записи за период");

            var stock = ReportFactory.StockSheet(services);
            Check(failures, log, stock.RowCount == services.Items.GetAll().Count,
                "Ведомость остатков содержит все позиции склада");

            var compliance = ReportFactory.Compliance(services, null);
            Check(failures, log, compliance.RowCount > 0, "Отчёт о соблюдении норм содержит строки");

            var journal = ReportFactory.Journal(services, from, to, null);
            Check(failures, log, journal.RowCount == services.Reports.GetJournal(from, to, null).Count,
                "Журнал в отчёте совпадает с журналом в базе");
        }

        /// <summary>Формы должны конструироваться и читать данные без исключений.</summary>
        private static void CheckForms(AppServices services, List<string> failures, TextWriter log)
        {
            var forms = new List<Form>();

            try
            {
                forms.Add(new MainForm(services));
                forms.Add(new EmployeesForm(services.Employees, services.Lookups));
                forms.Add(new PpeItemsForm(services.Items, services.Lookups, services.StockService));
                forms.Add(new NormsForm(services.Norms, services.Lookups));
                forms.Add(new IssueForm(services.IssueService, services.Lookups, services.Audit));
                forms.Add(new ReportsForm(services));
                forms.Add(new AuditForm(services));

                Check(failures, log, true, "Все формы создаются без исключений");
            }
            catch (Exception exception)
            {
                Check(failures, log, false, "Создание форм: " + exception.Message);
            }
            finally
            {
                foreach (var form in forms)
                    form.Dispose();
            }
        }

        /// <summary>Проверяет, что служебные колонки скрыты, а заголовки берутся из DisplayName.</summary>
        private static void CheckGrid(List<string> failures, TextWriter log, AppServices services)
        {
            using (var host = new Form())
            using (var grid = UiFactory.CreateGrid())
            {
                host.Controls.Add(grid);
                host.CreateControl();

                UiFactory.Bind(grid, services.Employees.GetAll(), row => row.Id, null);

                log.WriteLine("Колонки: " + string.Join("; ", grid.Columns.Cast<DataGridViewColumn>()
                    .Select(column => column.Name + "='" + column.HeaderText + "'")));

                Check(failures, log, grid.Columns.Count > 0, "Таблица получила колонки из модели");
                Check(failures, log, ((IBindingList)grid.DataSource).SupportsSorting,
                    "Таблицы поддерживают сортировку по клику на заголовок");
                Check(failures, log, !grid.Columns.Contains("Id"), "Служебный Id не попадает в таблицу");
                Check(failures, log,
                    grid.Columns.Contains("FullName") && grid.Columns["FullName"].HeaderText == "ФИО",
                    "Заголовок колонки берётся из DisplayName");
            }
        }

        private static void Check(List<string> failures, TextWriter log, bool condition, string description)
        {
            log.WriteLine((condition ? "OK   " : "FAIL ") + description);

            if (!condition)
                failures.Add(description);
        }

        private static void Cleanup(string dbPath)
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                try
                {
                    var path = dbPath + suffix;
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (IOException)
                {
                    // Временный файл останется до следующей уборки — на результат не влияет.
                }
            }
        }
    }
}
