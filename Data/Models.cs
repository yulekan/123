using System;
using System.ComponentModel;

namespace PPEAccounting.Data
{
    // Модели строк для таблиц. DisplayName задаёт заголовок колонки в DataGridView,
    // Browsable(false) скрывает служебные идентификаторы.

    public sealed class EmployeeRow
    {
        [Browsable(false)] public int Id { get; set; }
        [DisplayName("ФИО")] public string FullName { get; set; }
        [Browsable(false)] public int PositionId { get; set; }
        [DisplayName("Должность")] public string PositionName { get; set; }
        [DisplayName("Цех")] public string DepartmentName { get; set; }
        [DisplayName("Дата приёма")] public DateTime HireDate { get; set; }
        [DisplayName("Активен")] public bool IsActive { get; set; }
    }

    public sealed class PpeItemRow
    {
        [Browsable(false)] public int Id { get; set; }
        [DisplayName("Наименование")] public string Name { get; set; }
        [DisplayName("Ед. изм.")] public string Unit { get; set; }
        [DisplayName("Остаток")] public int StockQty { get; set; }
        [DisplayName("Минимум")] public int MinStock { get; set; }
        [Browsable(false)] public int? SupplierId { get; set; }
        [DisplayName("Поставщик")] public string SupplierName { get; set; }

        [Browsable(false)]
        public bool NeedsSupply
        {
            get { return StockQty <= MinStock; }
        }
    }

    public sealed class NormRow
    {
        [Browsable(false)] public int Id { get; set; }
        [Browsable(false)] public int ItemId { get; set; }
        [DisplayName("Вид СИЗ")] public string ItemName { get; set; }
        [Browsable(false)] public int PositionId { get; set; }
        [DisplayName("Должность")] public string PositionName { get; set; }
        [DisplayName("Кол-во в год")] public int QtyPerYear { get; set; }
        [DisplayName("Срок носки, мес.")] public int WearPeriodMonths { get; set; }
    }

    public sealed class IssueRow
    {
        [Browsable(false)] public int Id { get; set; }
        [Browsable(false)] public int EmployeeId { get; set; }
        [DisplayName("Сотрудник")] public string EmployeeName { get; set; }
        [DisplayName("Должность")] public string PositionName { get; set; }
        [DisplayName("Наименование СИЗ")] public string ItemName { get; set; }
        [DisplayName("Дата выдачи")] public DateTime IssueDate { get; set; }
        [DisplayName("Кол-во")] public int Qty { get; set; }
        [DisplayName("Дата возврата")] public DateTime? ReturnDate { get; set; }
        [DisplayName("Статус")] public string Status { get; set; }
        [DisplayName("Получено")] public DateTime? ReceivedDate { get; set; }

        [Browsable(false)]
        public bool AwaitingReceipt
        {
            get { return ReceivedDate == null && Status == IssueStatus.Issued; }
        }
    }

    /// <summary>Запись журнала действий: что и когда было сделано.</summary>
    public sealed class AuditRow
    {
        [Browsable(false)] public int Id { get; set; }
        [DisplayName("Дата и время")] public DateTime CreatedAt { get; set; }
        [DisplayName("Действие")] public string Action { get; set; }
        [DisplayName("Подробности")] public string Details { get; set; }
    }

    /// <summary>Выдача в разрезе личной карточки работника.</summary>
    public sealed class EmployeePpeRow
    {
        public int IssueId { get; set; }
        public DateTime IssueDate { get; set; }
        public string ItemName { get; set; }
        public string Unit { get; set; }
        public int Qty { get; set; }
        public DateTime? ReturnDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string Status { get; set; }
        public int WearPeriodMonths { get; set; }

        public DateTime? ValidUntil
        {
            get { return WearPeriodMonths > 0 ? IssueDate.AddMonths(WearPeriodMonths) : (DateTime?)null; }
        }

        public bool Expired
        {
            get { return ValidUntil.HasValue && ValidUntil.Value.Date < DateTime.Today && Status == IssueStatus.Issued; }
        }
    }

    /// <summary>Обеспеченность работника по одной норме: положено, выдано, остаток лимита.</summary>
    public sealed class NormCoverageRow
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string PositionName { get; set; }
        public string ItemName { get; set; }
        public int QtyPerYear { get; set; }
        public int WearPeriodMonths { get; set; }
        public int Issued { get; set; }
        public DateTime? LastIssueDate { get; set; }

        public int Remaining
        {
            get { return Math.Max(0, QtyPerYear - Issued); }
        }

        public DateTime? ValidUntil
        {
            get { return LastIssueDate.HasValue && WearPeriodMonths > 0
                ? LastIssueDate.Value.AddMonths(WearPeriodMonths)
                : (DateTime?)null; }
        }

        public bool Expired
        {
            get { return ValidUntil.HasValue && ValidUntil.Value.Date < DateTime.Today; }
        }
    }

    /// <summary>Элемент выпадающего списка: Id + отображаемое имя.</summary>
    public sealed class LookupItem
    {
        public LookupItem(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
