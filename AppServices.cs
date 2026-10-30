using System;
using PPEAccounting.Data;
using PPEAccounting.Services;

namespace PPEAccounting
{
    /// <summary>
    /// Точка сборки приложения: здесь создаются зависимости и передаются в формы.
    /// Благодаря этому формы не открывают соединения сами, а сервисы можно
    /// протестировать на отдельной базе.
    /// </summary>
    public sealed class AppServices
    {
        public AppServices(Database database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            Database = database;
            Employees = new EmployeeRepository(database);
            Items = new PpeItemRepository(database);
            Norms = new NormRepository(database);
            Lookups = new LookupRepository(database);
            IssueService = new IssueService(database);
            StockService = new StockService(database);

            Audit = new AuditService(new AuditRepository(database));
            Reports = new ReportService(database, Items);
        }

        public Database Database { get; }
        public IEmployeeRepository Employees { get; }
        public IPpeItemRepository Items { get; }
        public INormRepository Norms { get; }
        public ILookupRepository Lookups { get; }
        public IIssueService IssueService { get; }
        public IStockService StockService { get; }
        public AuditService Audit { get; }
        public ReportService Reports { get; }
    }
}
