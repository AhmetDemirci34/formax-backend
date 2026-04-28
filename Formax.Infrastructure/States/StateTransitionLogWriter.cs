using Formax.Application.States;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.States
{
    public class StateTransitionLogWriter : IStateTransitionLogWriter
    {
        private readonly FormaxDbContext _db;

        public StateTransitionLogWriter(FormaxDbContext db)
        {
            _db = db;
        }

        public void Write(StateTransitionLog log)
        {
            _db.StateTransitionLogs.Add(log);
            _db.SaveChanges();
        }
    }
}
