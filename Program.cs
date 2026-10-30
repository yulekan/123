using System;
using System.Linq;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Diagnostics;
using PPEAccounting.Forms;

namespace PPEAccounting
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Диагностический прогон бизнес-логики без интерфейса:
            // PPEAccounting.exe --selftest
            if (HasSwitch(args, "--selftest"))
                return SelfTest.Run();

            // Съёмка экранов для документации: PPEAccounting.exe --screenshots
            if (HasSwitch(args, "--screenshots"))
                return ScreenshotRunner.Run();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (sender, eventArgs) => ShowFatal(eventArgs.Exception);
            AppDomain.CurrentDomain.UnhandledException +=
                (sender, eventArgs) => ShowFatal(eventArgs.ExceptionObject as Exception);

            try
            {
                string migrationNote;
                var dbPath = DbPathResolver.Resolve(out migrationNote);
                var database = new Database(dbPath);

                // --empty отключает демонстрационные данные (для чистой установки).
                SchemaMigrator.Migrate(database, seedDemoData: !HasSwitch(args, "--empty"));

                if (migrationNote != null)
                {
                    MessageBox.Show(migrationNote, "Перенос базы данных",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                var services = new AppServices(database);

                // Вход в систему не требуется: программа рассчитана на одно рабочее
                // место специалиста по охране труда.
                Application.Run(new MainForm(services));

                return 0;
            }
            catch (Exception exception)
            {
                ShowFatal(exception);
                return 1;
            }
        }

        private static bool HasSwitch(string[] args, string value)
        {
            return args != null && args.Any(argument => string.Equals(argument, value, StringComparison.OrdinalIgnoreCase));
        }

        private static void ShowFatal(Exception exception)
        {
            var details = exception == null ? "Неизвестная ошибка." : exception.ToString();

            MessageBox.Show(details, "Непредвиденная ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
