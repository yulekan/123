namespace PPEAccounting.Data
{
    /// <summary>Статусы выдачи. Раньше строки были размазаны по коду и БД.</summary>
    public static class IssueStatus
    {
        public const string Issued = "Выдана";
        public const string Returned = "Возвращена";
    }
}
