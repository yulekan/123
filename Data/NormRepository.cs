using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace PPEAccounting.Data
{
    public interface INormRepository
    {
        IReadOnlyList<NormRow> GetAll();
        void Add(int itemId, int positionId, int qtyPerYear, int wearPeriodMonths);
        void Update(int id, int itemId, int positionId, int qtyPerYear, int wearPeriodMonths);
        bool Exists(int itemId, int positionId, int? exceptId);
        void Delete(int id);
    }

    public sealed class NormRepository : INormRepository
    {
        private const string SelectSql = @"
SELECT n.IdNorm, n.IdPPEItem, i.Name, n.IdPosition, pos.Name, n.QtyPerYear, n.WearPeriodMonths
FROM IssueNorm n
JOIN PPEItem i  ON i.IdPPEItem = n.IdPPEItem
JOIN Position pos ON pos.IdPosition = n.IdPosition
ORDER BY i.Name, pos.Name";

        private readonly Database _database;

        public NormRepository(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public IReadOnlyList<NormRow> GetAll()
        {
            return _database.QueryList(SelectSql, reader => new NormRow
            {
                Id = reader.GetInt32(0),
                ItemId = reader.GetInt32(1),
                ItemName = reader.GetString(2),
                PositionId = reader.GetInt32(3),
                PositionName = reader.GetString(4),
                QtyPerYear = reader.GetInt32(5),
                WearPeriodMonths = reader.GetInt32(6)
            });
        }

        public void Add(int itemId, int positionId, int qtyPerYear, int wearPeriodMonths)
        {
            if (Exists(itemId, positionId, null))
                throw new InvalidOperationException("Норма для этой пары «вид СИЗ + должность» уже задана. Измените существующую строку.");

            Insert(itemId, positionId, qtyPerYear, wearPeriodMonths);
        }

        public void Update(int id, int itemId, int positionId, int qtyPerYear, int wearPeriodMonths)
        {
            if (Exists(itemId, positionId, id))
                throw new InvalidOperationException("Такая норма уже есть — выберите другую должность или вид СИЗ.");

            var affected = _database.Execute(
                "UPDATE IssueNorm SET IdPPEItem = @item, IdPosition = @position, QtyPerYear = @qty, WearPeriodMonths = @months WHERE IdNorm = @id",
                new SQLiteParameter("@item", itemId),
                new SQLiteParameter("@position", positionId),
                new SQLiteParameter("@qty", qtyPerYear),
                new SQLiteParameter("@months", wearPeriodMonths),
                new SQLiteParameter("@id", id));

            if (affected == 0)
                throw new InvalidOperationException("Норма не найдена — возможно, запись уже удалена.");
        }

        public bool Exists(int itemId, int positionId, int? exceptId)
        {
            var exists = _database.Scalar(
                "SELECT EXISTS(SELECT 1 FROM IssueNorm WHERE IdPPEItem = @item AND IdPosition = @position AND IdNorm <> @except)",
                new SQLiteParameter("@item", itemId),
                new SQLiteParameter("@position", positionId),
                new SQLiteParameter("@except", exceptId ?? 0));

            return SqlValue.ToInt(exists) != 0;
        }

        public void Delete(int id)
        {
            try
            {
                _database.Execute("DELETE FROM IssueNorm WHERE IdNorm = @id", new SQLiteParameter("@id", id));
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                throw new InvalidOperationException(
                    "По этой норме уже есть выдачи СИЗ, удалить её нельзя. " +
                    "Измените норму или удалите связанные записи журнала.", ex);
            }
        }

        private void Insert(int itemId, int positionId, int qtyPerYear, int wearPeriodMonths)
        {
            try
            {
                _database.Execute(
                    "INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths) VALUES(@item, @position, @qty, @months)",
                    new SQLiteParameter("@item", itemId),
                    new SQLiteParameter("@position", positionId),
                    new SQLiteParameter("@qty", qtyPerYear),
                    new SQLiteParameter("@months", wearPeriodMonths));
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                // Страховка на случай гонки с уникальным индексом UX_IssueNorm_Item_Position.
                throw new InvalidOperationException("Норма для этой пары «вид СИЗ + должность» уже задана.", ex);
            }
        }
    }
}
