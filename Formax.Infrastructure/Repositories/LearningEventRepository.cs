using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// Radar Learning (R.14.1) — EF Core persistence for normalized learning events.
    /// </summary>
    public sealed class LearningEventRepository : ILearningEventRepository
    {
        private readonly FormaxDbContext _context;

        public LearningEventRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(LearningEvent learningEvent, CancellationToken ct = default)
            => await _context.LearningEvents.AddAsync(learningEvent, ct);

        public async Task<IReadOnlyList<LearningEvent>> GetByUserAsync(int userId, int take = 100, CancellationToken ct = default)
            => await _context.LearningEvents
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.OccurredAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
