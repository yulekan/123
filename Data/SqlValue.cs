using System;
using System.Globalization;

namespace PPEAccounting.Data
{
    /// <summary>
    /// Приведение значений из SQLite. В отличие от прямого Convert.ToInt32,
    /// корректно обрабатывает DBNull (иначе InvalidCastException).
    /// </summary>
    public static class SqlValue
    {
        public static int ToInt(object value, int fallback = 0)
        {
            return value == null || value == DBNull.Value
                ? fallback
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static int? ToIntOrNull(object value)
        {
            return value == null || value == DBNull.Value
                ? (int?)null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static string ToTextOrNull(object value)
        {
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Даты в БД хранятся строками ISO 8601 (yyyy-MM-dd) — так их можно сравнивать лексикографически.
    /// Формат и культура фиксированы, чтобы не зависеть от региональных настроек.
    /// </summary>
    public static class SqlDate
    {
        public const string Format = "yyyy-MM-dd";

        public static string ToText(DateTime value)
        {
            return value.ToString(Format, CultureInfo.InvariantCulture);
        }

        public static string ToTextOrNull(DateTime? value)
        {
            return value.HasValue ? ToText(value.Value) : null;
        }

        public static DateTime Parse(string value)
        {
            return DateTime.ParseExact(value, Format, CultureInfo.InvariantCulture);
        }

        public static DateTime? ParseOrNull(object value)
        {
            var text = SqlValue.ToTextOrNull(value);
            return string.IsNullOrEmpty(text) ? (DateTime?)null : Parse(text);
        }
    }
}
