using Formax.Domain.Entities;
using Formax.Infrastructure;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Repositories
{
    public class StateTransitionLogReadRepository
    {
        private readonly FormaxDbContext _context;

        public StateTransitionLogReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public Task<List<StateTransitionLog>> GetLastAsync(int take = 100)
        {
            return _context.StateTransitionLogs
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();
        }
    }
}
