using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchPredictionSignalRepository : IMatchPredictionSignalRepository
    {
        private readonly FormaxDbContext _context;

        public MatchPredictionSignalRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public MatchPredictionSignal? GetByMatchId(int matchId)
            => _context.MatchPredictionSignals
                .AsNoTracking()
                .FirstOrDefault(x => x.MatchId == matchId);

        public async Task UpsertAsync(MatchPredictionSignal signal, CancellationToken ct = default)
        {
            var existing = await _context.MatchPredictionSignals
                .FirstOrDefaultAsync(x => x.MatchId == signal.MatchId, ct);

            if (existing == null)
            {
                _context.MatchPredictionSignals.Add(signal);
                return;
            }

            existing.ExternalMatchId     = signal.ExternalMatchId;
            existing.PercentHome         = signal.PercentHome;
            existing.PercentDraw         = signal.PercentDraw;
            existing.PercentAway         = signal.PercentAway;
            existing.WinnerName          = signal.WinnerName;
            existing.WinnerSide          = signal.WinnerSide;
            existing.WinOrDraw           = signal.WinOrDraw;
            existing.Advice              = signal.Advice;
            existing.UnderOver           = signal.UnderOver;
            existing.ComparisonTotalHome = signal.ComparisonTotalHome;
            existing.ComparisonTotalAway = signal.ComparisonTotalAway;
            existing.UpdatedAt           = signal.UpdatedAt;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
