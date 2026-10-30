using System;
using System.IO;

namespace PPEAccounting.Data
{
    /// <summary>
    /// Выбирает место хранения файла БД. Раньше база лежала рядом с exe (bin\Debug) и
    /// терялась при пересборке, а в Program Files была бы недоступна для записи.
    /// </summary>
    public static class DbPathResolver
    {
        public static string Resolve(out string migrationNote)
        {
            migrationNote = null;

            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PPEAccounting");

            Directory.CreateDirectory(dataDirectory);

            var target = Path.Combine(dataDirectory, "ppe.db");
            var legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ppe.db");

            if (!File.Exists(target) && File.Exists(legacy))
            {
                File.Copy(legacy, target);
                migrationNote = "База данных перенесена из папки программы:\n" + legacy;
            }

            return target;
        }
    }
}
