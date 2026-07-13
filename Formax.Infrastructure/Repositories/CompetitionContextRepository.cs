using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class CompetitionContextRepository : ICompetitionContextRepository
    {
        private readonly FormaxDbContext _context;

        public CompetitionContextRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // ── Synchronous reads ──────────────────────────────────────────────────

        public CompetitionContext? GetByMatchId(int matchId)
            => _context.CompetitionContexts.FirstOrDefault(x => x.MatchId == matchId);

        // ── Async writes ───────────────────────────────────────────────────────

        public async Task UpsertAsync(CompetitionContext context, CancellationToken ct = default)
        {
            var existing = await _context.CompetitionContexts
                .FirstOrDefaultAsync(x => x.MatchId == context.MatchId, ct);

            if (existing == null)
            {
                _context.CompetitionContexts.Add(context);
            }
            else
            {
                existing.CompetitionType = context.CompetitionType;
                existing.StageName = context.StageName;
                existing.ContextHeadline = context.ContextHeadline;
                existing.ContextSummary = context.ContextSummary;
                existing.BracketJson = context.BracketJson;
                existing.UpdatedAt = context.UpdatedAt;
                _context.CompetitionContexts.Update(existing);
            }
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
