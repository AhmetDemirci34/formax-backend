using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// Radar Match Intelligence (R.9.1) — EF Core persistence for intelligence snapshots
    /// and the read access used to build match profiles.
    /// </summary>
    public sealed class MatchIntelligenceRepository : IMatchIntelligenceRepository
    {
        private readonly FormaxDbContext _context;

        /// <summary>Kilitli müsabaka kapsamı — Radar/Feed/Commentary de aynı kapıdan geçer.</summary>
        private readonly HashSet<int> _allowedLeagues;

        public MatchIntelligenceRepository(FormaxDbContext context, IConfiguration config)
        {
            _context = context;
            _allowedLeagues = CoveragePolicy.LeagueAllowList(config);
        }

        private IQueryable<Match> InScope(IQueryable<Match> source)
            => _allowedLeagues.Count == 0
                ? source
                : source.Where(m => _allowedLeagues.Contains(m.LeagueId));

        public async Task<MatchIntelligenceSnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default)
            => await _context.MatchIntelligenceSnapshots
                .FirstOrDefaultAsync(x => x.MatchId == matchId, ct);

        public async Task<IReadOnlyDictionary<int, MatchIntelligenceSnapshot>> GetByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default)
        {
            if (matchIds is null || matchIds.Count == 0)
                return new Dictionary<int, MatchIntelligenceSnapshot>();

            var rows = await _context.MatchIntelligenceSnapshots
                .AsNoTracking()
                .Where(x => matchIds.Contains(x.MatchId))
                .ToListAsync(ct);

            return rows
                .GroupBy(x => x.MatchId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        public async Task<IReadOnlyDictionary<int, MatchIntelligenceFeedRow>> GetFeedRowsFromAsync(
            DateTime fromUtc, CancellationToken ct = default)
        {
            // Yalnız pencere içindeki maçların snapshot'ı, yalnız iki alan (SignalsJson ÇEKİLMEZ).
            var rows = await (
                    from s in _context.MatchIntelligenceSnapshots.AsNoTracking()
                    join m in InScope(_context.Matches.AsNoTracking()) on s.MatchId equals m.Id
                    where m.MatchDate >= fromUtc
                    orderby m.MatchDate
                    select new MatchIntelligenceFeedRow(
                        s.MatchId,
                        s.ImportanceScore,
                        s.PrimarySignalType.ToString()))
                .ToListAsync(ct);

            return rows
                .GroupBy(x => x.MatchId)
                .ToDictionary(g => g.Key, g => g.First());
        }

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
            => await InScope(_context.Matches
                    .Include(m => m.HomeTeam)
                    .Include(m => m.AwayTeam))
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

        public async Task<IReadOnlyList<int>> GetMatchIdsFromAsync(DateTime fromUtc, CancellationToken ct = default)
            => await InScope(_context.Matches)
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
