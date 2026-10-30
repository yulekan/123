using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace PPEAccounting.Data
{
    public interface ILookupRepository
    {
        IReadOnlyList<LookupItem> GetActiveEmployees();
        IReadOnlyList<LookupItem> GetDepartments();
        IReadOnlyList<LookupItem> GetItems();
        IReadOnlyList<LookupItem> GetPositions();
        IReadOnlyList<LookupItem> GetSupplierNames();

        /// <summary>Возвращает Id поставщика по имени, создавая запись при необходимости.</summary>
        int GetOrCreateSupplier(string name);

        /// <summary>
        /// Возвращает Id должности по названию и подразделению, создавая запись при необходимости.
        /// Одинаковые название и подразделение считаются одной должностью.
        /// </summary>
        int GetOrCreatePosition(string name, int departmentId);
    }

    public sealed class LookupRepository : ILookupRepository
    {
        private readonly Database _database;

        public LookupRepository(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public IReadOnlyList<LookupItem> GetActiveEmployees()
        {
            return _database.QueryList(
                "SELECT IdEmployee, FullName FROM Employee WHERE IsActive = 1 ORDER BY FullName",
                reader => new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }

        public IReadOnlyList<LookupItem> GetDepartments()
        {
            return _database.QueryList(
                "SELECT IdDepartment, Name FROM Department ORDER BY Name",
                reader => new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }

        public IReadOnlyList<LookupItem> GetItems()
        {
            return _database.QueryList(
                "SELECT IdPPEItem, Name || ' (' || Unit || ')' FROM PPEItem ORDER BY Name",
                reader => new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }

        public IReadOnlyList<LookupItem> GetPositions()
        {
            return _database.QueryList(@"
SELECT p.IdPosition, p.Name || ' (' || d.Name || ')'
FROM Position p
JOIN Department d ON d.IdDepartment = p.IdDepartment
ORDER BY p.Name",
                reader => new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }

        public IReadOnlyList<LookupItem> GetSupplierNames()
        {
            return _database.QueryList(
                "SELECT IdSupplier, Name FROM Supplier ORDER BY Name",
                reader => new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }

        public int GetOrCreateSupplier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Не задано имя поставщика.", nameof(name));

            var trimmed = name.Trim();

            return _database.InTransaction((connection, transaction) =>
            {
                var existing = connection.Scalar(transaction,
                    "SELECT IdSupplier FROM Supplier WHERE Name = @name LIMIT 1",
                    new SQLiteParameter("@name", trimmed));

                if (existing != null && existing != DBNull.Value)
                    return Convert.ToInt32(existing);

                return connection.Insert(transaction,
                    "INSERT INTO Supplier(Name) VALUES(@name)",
                    new SQLiteParameter("@name", trimmed));
            });
        }

        public int GetOrCreatePosition(string name, int departmentId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Не задано название должности.", nameof(name));

            if (departmentId <= 0)
                throw new ArgumentException("Не выбрано подразделение.", nameof(departmentId));

            var trimmed = name.Trim();

            return _database.InTransaction((connection, transaction) =>
            {
                var existing = connection.Scalar(transaction,
                    "SELECT IdPosition FROM Position WHERE Name = @name AND IdDepartment = @department LIMIT 1",
                    new SQLiteParameter("@name", trimmed),
                    new SQLiteParameter("@department", departmentId));

                if (existing != null && existing != DBNull.Value)
                    return Convert.ToInt32(existing);

                return connection.Insert(transaction,
                    "INSERT INTO Position(Name, IdDepartment) VALUES(@name, @department)",
                    new SQLiteParameter("@name", trimmed),
                    new SQLiteParameter("@department", departmentId));
            });
        }
    }
}
