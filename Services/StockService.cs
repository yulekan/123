using System;
using System.Data.SQLite;
using PPEAccounting.Data;

namespace PPEAccounting.Services
{
    public interface IStockService
    {
        OperationResult RegisterSupply(int ppeItemId, DateTime date, int qty, decimal price, int supplierId);
    }

    /// <summary>
    /// Приход СИЗ на склад. Раньше остаток правился вручную в карточке товара,
    /// из-за чего он расходился с историей; таблица Supply вообще не использовалась.
    /// </summary>
    public sealed class StockService : IStockService
    {
        private readonly Database _database;

        public StockService(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public OperationResult RegisterSupply(int ppeItemId, DateTime date, int qty, decimal price, int supplierId)
        {
            if (qty <= 0)
                return OperationResult.Fail("Количество должно быть больше нуля.");

            if (price < 0)
                return OperationResult.Fail("Цена не может быть отрицательной.");

            if (date.Date > DateTime.Today)
                return OperationResult.Fail("Дата прихода не может быть в будущем.");

            return _database.InTransaction((connection, transaction) =>
            {
                var supplierExists = SqlValue.ToInt(connection.Scalar(transaction,
                    "SELECT EXISTS(SELECT 1 FROM Supplier WHERE IdSupplier = @id)",
                    new SQLiteParameter("@id", supplierId))) != 0;

                if (!supplierExists)
                    return OperationResult.Fail("Поставщик не найден.");

                var affected = connection.Exec(transaction,
                    "UPDATE PPEItem SET StockQty = StockQty + @qty WHERE IdPPEItem = @item",
                    new SQLiteParameter("@qty", qty),
                    new SQLiteParameter("@item", ppeItemId));

                if (affected == 0)
                    return OperationResult.Fail("Вид СИЗ не найден.");

                connection.Insert(transaction, @"
INSERT INTO Supply(IdSupplier, IdPPEItem, SupplyDate, Qty, Price)
VALUES(@supplier, @item, @date, @qty, @price)",
                    new SQLiteParameter("@supplier", supplierId),
                    new SQLiteParameter("@item", ppeItemId),
                    new SQLiteParameter("@date", SqlDate.ToText(date)),
                    new SQLiteParameter("@qty", qty),
                    new SQLiteParameter("@price", price));

                return OperationResult.Ok("Приход оформлен: +" + qty + ".");
            });
        }
    }
}
