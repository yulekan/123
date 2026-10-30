using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace PPEAccounting.Data
{
    public interface IPpeItemRepository
    {
        IReadOnlyList<PpeItemRow> GetAll();
        void Add(string name, string unit, int initialStock, int minStock, int? supplierId);
        void Update(int id, string name, string unit, int minStock, int? supplierId);
        int GetStock(int id);
        bool IsInUse(int id);
        void Delete(int id);
    }

    public sealed class PpeItemRepository : IPpeItemRepository
    {
        private const string SelectSql = @"
SELECT i.IdPPEItem, i.Name, i.Unit, i.StockQty, i.MinStock, i.IdSupplier, IFNULL(s.Name, '')
FROM PPEItem i
LEFT JOIN Supplier s ON s.IdSupplier = i.IdSupplier
ORDER BY i.Name";

        private readonly Database _database;

        public PpeItemRepository(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public IReadOnlyList<PpeItemRow> GetAll()
        {
            return _database.QueryList(SelectSql, reader => new PpeItemRow
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Unit = reader.GetString(2),
                StockQty = reader.GetInt32(3),
                MinStock = reader.GetInt32(4),
                SupplierId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                SupplierName = reader.GetString(6)
            });
        }

        public void Add(string name, string unit, int initialStock, int minStock, int? supplierId)
        {
            _database.Execute(
                "INSERT INTO PPEItem(Name, Unit, StockQty, MinStock, IdSupplier) VALUES(@name, @unit, @stock, @min, @supplier)",
                new SQLiteParameter("@name", name),
                new SQLiteParameter("@unit", unit),
                new SQLiteParameter("@stock", initialStock),
                new SQLiteParameter("@min", minStock),
                new SQLiteParameter("@supplier", (object)supplierId ?? DBNull.Value));
        }

        /// <summary>Остаток здесь намеренно не меняется: только через приход, выдачу и возврат.</summary>
        public void Update(int id, string name, string unit, int minStock, int? supplierId)
        {
            var affected = _database.Execute(
                "UPDATE PPEItem SET Name = @name, Unit = @unit, MinStock = @min, IdSupplier = @supplier WHERE IdPPEItem = @id",
                new SQLiteParameter("@name", name),
                new SQLiteParameter("@unit", unit),
                new SQLiteParameter("@min", minStock),
                new SQLiteParameter("@supplier", (object)supplierId ?? DBNull.Value),
                new SQLiteParameter("@id", id));

            if (affected == 0)
                throw new InvalidOperationException("Вид СИЗ не найден — возможно, запись уже удалена.");
        }

        public int GetStock(int id)
        {
            return SqlValue.ToInt(_database.Scalar(
                "SELECT StockQty FROM PPEItem WHERE IdPPEItem = @id",
                new SQLiteParameter("@id", id)));
        }

        public bool IsInUse(int id)
        {
            var exists = _database.Scalar(@"
SELECT EXISTS(SELECT 1 FROM PPEIssue WHERE IdPPEItem = @id)
    OR EXISTS(SELECT 1 FROM IssueNorm WHERE IdPPEItem = @id)
    OR EXISTS(SELECT 1 FROM Supply   WHERE IdPPEItem = @id)",
                new SQLiteParameter("@id", id));

            return SqlValue.ToInt(exists) != 0;
        }

        public void Delete(int id)
        {
            if (IsInUse(id))
                throw new InvalidOperationException(
                    "Этот вид СИЗ уже используется в нормах, выдачах или приходах, удалить его нельзя.");

            _database.Execute("DELETE FROM PPEItem WHERE IdPPEItem = @id", new SQLiteParameter("@id", id));
        }
    }
}
