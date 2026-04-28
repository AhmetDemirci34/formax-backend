using Formax.Application.AI.SelfAudit;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.AI.SelfAudit
{
    public class AISelfInvalidationLogWriter : IAISelfInvalidationLogWriter
    {
        private readonly FormaxDbContext _db;

        public AISelfInvalidationLogWriter(FormaxDbContext db)
        {
            _db = db;
        }

        public void Write(AISelfInvalidationLog log)
        {
            _db.AISelfInvalidationLogs.Add(log);
            _db.SaveChanges();
        }
    }
}
