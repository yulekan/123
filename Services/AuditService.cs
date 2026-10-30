using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PPEAccounting.Data;

namespace PPEAccounting.Services
{
    /// <summary>Журнал действий. Записи только добавляются: изменить историю нельзя.</summary>
    public sealed class AuditService
    {
        private readonly AuditRepository _repository;

        public AuditService(AuditRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Write(string action, string details)
        {
            _repository.Write(action, details);
        }

        public IReadOnlyList<AuditRow> GetRecent(int limit)
        {
            return _repository.GetRecent(limit);
        }

        public int Count()
        {
            return _repository.Count();
        }
    }
}
