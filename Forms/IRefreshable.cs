namespace PPEAccounting.Forms
{
    /// <summary>Форма, умеющая перечитать свои данные при повторном открытии.</summary>
    public interface IRefreshable
    {
        void Reload();
    }
}
