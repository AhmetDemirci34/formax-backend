using System;
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
    /// Radar Match Intelligence (R.9.1) — EF Core persistence for intelligence snapshots
    /// and the read access used to build match profiles.
    /// </summary>
    public sealed class MatchIntelligenceRepository : IMatchIntelligenceRepository
    {
        private readonly FormaxDbContext _context;

        public MatchIntelligenceRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<MatchIntelligenceSnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default)
            => await _context.MatchIntelligenceSnapshots
                .FirstOrDefaultAsync(x => x.MatchId == matchId, ct);

        public async Task UpsertAsync(MatchIntelligenceSnapshot snapshot, CancellationToken ct = default)
        {
            var existing = await _context.MatchIntelligenceSnapshots
                .FirstOrDefaultAsync(x => x.MatchId == snapshot.MatchId, ct);

            if (existing is null)
            {
                _context.MatchIntelligenceSnapshots.Add(snapshot);
                return;
            }

            existing.Status = snapshot.Status;
            existing.PrimarySignalType = snapshot.PrimarySignalType;
            existing.SignalCount = snapshot.SignalCount;
            existing.SignalsJson = snapshot.SignalsJson;
            existing.Summary = snapshot.Summary;
            existing.ImportanceScore = snapshot.ImportanceScore;
            existing.ImportanceLevel = snapshot.ImportanceLevel;
            existing.NewsImpactScore = snapshot.NewsImpactScore;
            existing.NewsImpactLevel = snapshot.NewsImpactLevel;
            existing.SyntheticSignalScore = snapshot.SyntheticSignalScore;
            existing.SyntheticSignalLevel = snapshot.SyntheticSignalLevel;
            existing.SyntheticDirection = snapshot.SyntheticDirection;
            existing.GeneratedAtUtc = snapshot.GeneratedAtUtc;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        public async Task<Match?> GetMatchWithTeamsAsync(int matchId, CancellationToken ct = default)
            => await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

        public async Task<IReadOnlyList<int>> GetMatchIdsFromAsync(DateTime fromUtc, CancellationToken ct = default)
            => await _context.Matches
                .Where(m => m.MatchDate >= fromUtc)
                .OrderBy(m => m.MatchDate)
                .Select(m => m.Id)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<Match>> GetRecentFinishedByTeamAsync(
            int teamId, DateTime beforeUtc, int take, CancellationToken ct = default)
            => await _context.Matches
                .Where(m => m.Status == "Finished"
                            && m.MatchDate < beforeUtc
                            && (m.HomeTeamId == teamId || m.AwayTeamId == teamId))
                .OrderByDescending(m => m.MatchDate)
                .Take(take)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<Match>> GetH2HFinishedAsync(
            int teamAId, int teamBId, DateTime beforeUtc, int take, CancellationToken ct = default)
            => await _context.Matches
                .Where(m => m.Status == "Finished"
                            && m.MatchDate < beforeUtc
                            && ((m.HomeTeamId == teamAId && m.AwayTeamId == teamBId)
                                || (m.HomeTeamId == teamBId && m.AwayTeamId == teamAId)))
                .OrderByDescending(m => m.MatchDate)
                .Take(take)
                .ToListAsync(ct);
    }
}
