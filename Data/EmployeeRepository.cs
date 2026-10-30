using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace PPEAccounting.Data
{
    public interface IEmployeeRepository
    {
        IReadOnlyList<EmployeeRow> GetAll();
        void Add(string fullName, int positionId, DateTime hireDate);
        void Update(int id, string fullName, int positionId, bool isActive);
        bool HasIssues(int employeeId);
        void Delete(int employeeId);
    }

    public sealed class EmployeeRepository : IEmployeeRepository
    {
        private const string SelectSql = @"
SELECT e.IdEmployee, e.FullName, p.IdPosition, p.Name, d.Name, e.HireDate, e.IsActive
FROM Employee e
JOIN Position p ON p.IdPosition = e.IdPosition
JOIN Department d ON d.IdDepartment = p.IdDepartment
ORDER BY e.IsActive DESC, e.FullName";

        private readonly Database _database;

        public EmployeeRepository(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public IReadOnlyList<EmployeeRow> GetAll()
        {
            return _database.QueryList(SelectSql, reader => new EmployeeRow
            {
                Id = reader.GetInt32(0),
                FullName = reader.GetString(1),
                PositionId = reader.GetInt32(2),
                PositionName = reader.GetString(3),
                DepartmentName = reader.GetString(4),
                HireDate = SqlDate.Parse(reader.GetString(5)),
                IsActive = reader.GetInt32(6) != 0
            });
        }

        public void Add(string fullName, int positionId, DateTime hireDate)
        {
            _database.Execute(
                "INSERT INTO Employee(FullName, IdPosition, HireDate, IsActive) VALUES(@name, @position, @hired, 1)",
                new SQLiteParameter("@name", fullName),
                new SQLiteParameter("@position", positionId),
                new SQLiteParameter("@hired", SqlDate.ToText(hireDate)));
        }

        public void Update(int id, string fullName, int positionId, bool isActive)
        {
            var affected = _database.Execute(
                "UPDATE Employee SET FullName = @name, IdPosition = @position, IsActive = @active WHERE IdEmployee = @id",
                new SQLiteParameter("@name", fullName),
                new SQLiteParameter("@position", positionId),
                new SQLiteParameter("@active", isActive ? 1 : 0),
                new SQLiteParameter("@id", id));

            if (affected == 0)
                throw new InvalidOperationException("Сотрудник не найден — возможно, запись уже удалена.");
        }

        public bool HasIssues(int employeeId)
        {
            var exists = _database.Scalar(
                "SELECT EXISTS(SELECT 1 FROM PPEIssue WHERE IdEmployee = @id)",
                new SQLiteParameter("@id", employeeId));

            return SqlValue.ToInt(exists) != 0;
        }

        public void Delete(int employeeId)
        {
            // Жёсткое удаление допустимо только для «пустой» карточки.
            // Сотрудников с историей выдач помечают неактивными (IsActive = 0).
            if (HasIssues(employeeId))
                throw new InvalidOperationException(
                    "У сотрудника есть записи о выдаче СИЗ, удалить его нельзя.\n" +
                    "Снимите галочку «Активен» в карточке — история сохранится.");

            _database.Execute("DELETE FROM Employee WHERE IdEmployee = @id", new SQLiteParameter("@id", employeeId));
        }
    }
}
