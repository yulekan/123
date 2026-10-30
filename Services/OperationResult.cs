namespace PPEAccounting.Services
{
    /// <summary>Результат операции для показа пользователю. Ошибка — это данные, а не исключение.</summary>
    public sealed class OperationResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public int Id { get; private set; }

        public static OperationResult Ok(string message, int id = 0)
        {
            return new OperationResult { Success = true, Message = message, Id = id };
        }

        public static OperationResult Fail(string message)
        {
            return new OperationResult { Message = message };
        }
    }
}
