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
    /// Radar News Intelligence (R.10.1) — EF Core persistence for news snapshots and
    /// the team→matches lookup.
    /// </summary>
    public sealed class NewsIntelligenceRepository : INewsIntelligenceRepository
    {
        private readonly FormaxDbContext _context;

        public NewsIntelligenceRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<NewsIntelligenceSnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default)
            => await _context.NewsIntelligenceSnapshots
                .FirstOrDefaultAsync(x => x.MatchId == matchId, ct);

        public async Task UpsertAsync(NewsIntelligenceSnapshot snapshot, CancellationToken ct = default)
        {
            var existing = await _context.NewsIntelligenceSnapshots
                .FirstOrDefaultAsync(x => x.MatchId == snapshot.MatchId, ct);

            if (existing is null)
            {
                _context.NewsIntelligenceSnapshots.Add(snapshot);
                return;
            }

            existing.NewsCount = snapshot.NewsCount;
            existing.MentionedTeams = snapshot.MentionedTeams;
            existing.MentionedLeagues = snapshot.MentionedLeagues;
            existing.LastNewsAtUtc = snapshot.LastNewsAtUtc;
            existing.CategoryBreakdown = snapshot.CategoryBreakdown;
            existing.ImpactScore = snapshot.ImpactScore;
            existing.ImpactLevel = snapshot.ImpactLevel;
            existing.GeneratedAtUtc = snapshot.GeneratedAtUtc;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        public async Task<IReadOnlyList<TeamMatchRef>> GetMatchesByTeamFromAsync(
            int teamId, DateTime fromUtc, CancellationToken ct = default)
            => await _context.Matches
                .Where(m => m.MatchDate >= fromUtc && (m.HomeTeamId == teamId || m.AwayTeamId == teamId))
                .OrderBy(m => m.MatchDate)
                .Select(m => new TeamMatchRef { MatchId = m.Id, League = m.League })
                .ToListAsync(ct);
    }
}
