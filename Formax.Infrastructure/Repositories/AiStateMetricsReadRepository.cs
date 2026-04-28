using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.States;
using Formax.Infrastructure.Data;
using Formax.Application.Interfaces.Repositories;

namespace Formax.Infrastructure.Repositories
{
    public class AiStateMetricsReadRepository : IAiStateMetricsReadRepository
    {
        private readonly FormaxDbContext _context;

        public AiStateMetricsReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // -------------------------
        // TOTAL
        // -------------------------
        public Task<int> TotalAsync()
        {
            return _context.StateTransitionLogs.CountAsync();
        }

        public Task<int> CountTotalSinceAsync(DateTime sinceUtc)
        {
            return _context.StateTransitionLogs
                .CountAsync(x => x.CreatedAt >= sinceUtc);
        }

        // -------------------------
        // STATE BASED
        // -------------------------
        public Task<int> CountByStateAsync(AIUxState state)
        {
            return _context.StateTransitionLogs
                .CountAsync(x => x.ToState == state);
        }

        public Task<int> CountByStateSinceAsync(AIUxState state, DateTime sinceUtc)
        {
            return _context.StateTransitionLogs
                .CountAsync(x =>
                    x.ToState == state &&
                    x.CreatedAt >= sinceUtc);
        }
    }
}
