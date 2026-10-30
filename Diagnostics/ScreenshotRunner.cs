using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Forms;
using PPEAccounting.Reports;
using PPEAccounting.Services;

namespace PPEAccounting.Diagnostics
{
    /// <summary>
    /// Сценарий съёмки экранов для документации: поднимает временную базу с демо-данными,
    /// проходит по разделам и сохраняет PNG. Запуск: PPEAccounting.exe --screenshots
    /// Результат: %TEMP%\ppe-shots (файл index.txt со списком снимков).
    /// </summary>
    internal static class ScreenshotRunner
    {
        public static int Run()
        {
            var output = Path.Combine(Path.GetTempPath(), "ppe-shots");

            if (Directory.Exists(output))
                Directory.Delete(output, true);

            Directory.CreateDirectory(output);

            var dbPath = Path.Combine(Path.GetTempPath(), "ppe-shots-" + Guid.NewGuid().ToString("N") + ".db");

            try
            {
                var database = new Database(dbPath);
                SchemaMigrator.Migrate(database, seedDemoData: true);

                var services = new AppServices(database);

                SeedActivity(services);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                CaptureMain(services, output);
                CaptureReports(services, output);

                File.WriteAllLines(Path.Combine(output, "index.txt"),
                    new[]
                    {
                        "01-main.png — главное окно",
                        "02-employees.png — сотрудники",
                        "03-stock.png — склад СИЗ",
                        "04-journal.png — журнал выдачи",
                        "05-reports.png — отчёты",
                        "06-audit.png — журнал действий",
                        "07-report-card.png — личная карточка учёта СИЗ",
                        "08-report-compliance.png — отчёт о соблюдении норм",
                        "09-report-stock.png — ведомость остатков"
                    });

                return 0;
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(output, "error.txt"), exception.ToString());
                return 1;
            }
            finally
            {
                try
                {
                    foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
                        if (File.Exists(dbPath + suffix))
                            File.Delete(dbPath + suffix);
                }
                catch (IOException)
                {
                    // Временный файл останется до следующей уборки.
                }
            }
        }

        /// <summary>Демонстрационные выдачи: без них отчёты и журнал выглядят пустыми.</summary>
        private static void SeedActivity(AppServices services)
        {
            var items = services.Items.GetAll().ToDictionary(row => row.Name, row => row.Id);
            var employees = services.Employees.GetAll().ToDictionary(row => row.FullName, row => row.Id);

            Issue(services, employees["Иванов Иван Иванович"], items["Халат х/б"], 1, -5, true);
            Issue(services, employees["Иванов Иван Иванович"], items["Перчатки резиновые"], 6, -2, true);
            Issue(services, employees["Иванов Иван Иванович"], items["Каска защитная"], 1, -10, true);
            Issue(services, employees["Иванов Иван Иванович"], items["Фартук прорезиненный"], 1, -1, false);

            Issue(services, employees["Петров Пётр Петрович"], items["Халат х/б"], 1, -8, true);
            Issue(services, employees["Петров Пётр Петрович"], items["Перчатки резиновые"], 12, -3, true);
            Issue(services, employees["Петров Пётр Петрович"], items["Каска защитная"], 1, -14, true);

            Issue(services, employees["Сидоров Сидор Сидорович"], items["Сапоги резиновые"], 1, -20, true);
            Issue(services, employees["Сидоров Сидор Сидорович"], items["Перчатки резиновые"], 12, -1, true);
            Issue(services, employees["Сидоров Сидор Сидорович"], items["Каска защитная"], 1, -6, true);

            Issue(services, employees["Козлова Анна Сергеевна"], items["Халат х/б"], 1, -12, true);
            Issue(services, employees["Козлова Анна Сергеевна"], items["Каска защитная"], 1, -3, true);

            // Записи журнала действий: при работе через интерфейс они создаются
            // формами, а при автоматической съёмке экранов — явно.
            services.Audit.Write("Выдача СИЗ", "Иванов Иван Иванович — Халат х/б × 1");
            services.Audit.Write("Подтверждение получения", "Иванов Иван Иванович — Халат х/б");
            services.Audit.Write("Выдача СИЗ", "Петров Пётр Петрович — Перчатки резиновые × 12");
            services.Audit.Write("Возврат СИЗ", "Сидоров Сидор Сидорович — Каска защитная");
            services.Audit.Write("Выдача СИЗ", "Козлова Анна Сергеевна — Халат х/б × 1");
        }

        private static void Issue(AppServices services, int employeeId, int itemId, int qty, int monthsAgo, bool confirm)
        {
            var date = DateTime.Today.AddMonths(monthsAgo);
            var result = services.IssueService.Issue(employeeId, itemId, qty, date);

            if (result.Success && confirm)
                services.IssueService.ConfirmReceipt(result.Id, date.AddDays(1));
        }

        private static void CaptureMain(AppServices services, string output)
        {
            using (var form = new MainForm(services))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(10, 10);
                form.Show();
                Pump(600);

                Capture(form, Path.Combine(output, "01-main.png"));

                Open(form, "Сотрудники", output, "02-employees.png");
                Open(form, "Склад СИЗ", output, "03-stock.png");
                Open(form, "Журнал выдачи", output, "04-journal.png");
                Open(form, "Отчёты", output, "05-reports.png");
                Open(form, "Журнал действий", output, "06-audit.png");
            }
        }

        private static void CaptureReports(AppServices services, string output)
        {
            var employeeId = services.Lookups.GetActiveEmployees().First().Id;

            Save(ReportFactory.PersonalCard(services, employeeId), Path.Combine(output, "07-report-card.png"));
            Save(ReportFactory.Compliance(services, null), Path.Combine(output, "08-report-compliance.png"));
            Save(ReportFactory.StockSheet(services), Path.Combine(output, "09-report-stock.png"));
        }

        private static void Save(ReportDocument document, string path)
        {
            using (var page = document.RenderFirstPage())
                page.Save(path, ImageFormat.Png);
        }

        private static void Open(Form main, string section, string output, string fileName)
        {
            var button = main.Controls.OfType<Panel>()
                .SelectMany(panel => panel.Controls.OfType<FlowLayoutPanel>())
                .SelectMany(flow => flow.Controls.OfType<Button>())
                .FirstOrDefault(item => item.Text == section);

            if (button == null)
                return;

            button.PerformClick();
            Pump(500);

            // Снимаем фокус с таблицы: в рабочем окне он обычно на кнопке меню,
            // а сфокусированная таблица рисует «текущую» ячейку системным синим.
            main.ActiveControl = null;
            Pump(150);

            Capture(main, Path.Combine(output, fileName));
        }

        private static void Capture(Form form, string path)
        {
            // PrintWindow снимает окно так, как его рисует сама Windows:
            // DrawToBitmap иногда иначе изображает «текущую» ячейку таблицы.
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var hdc = graphics.GetHdc();
                    try
                    {
                        PrintWindow(form.Handle, hdc, RenderFullContent);
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdc);
                    }
                }

                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private const uint RenderFullContent = 0x00000002;

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        private static void Pump(int milliseconds)
        {
            var until = DateTime.Now.AddMilliseconds(milliseconds);

            while (DateTime.Now < until)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }
    }
}
