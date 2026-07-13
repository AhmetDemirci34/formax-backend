using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchLiveStatsRepository : IMatchLiveStatsRepository
    {
        private readonly FormaxDbContext _context;

        public MatchLiveStatsRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public MatchLiveStats? GetByMatchId(int matchId)
            => _context.MatchLiveStats.FirstOrDefault(x => x.MatchId == matchId);

        public List<MatchLiveStats> GetByMatchIds(IEnumerable<int> matchIds)
        {
            var ids = matchIds.ToHashSet();
            return _context.MatchLiveStats
                .Where(x => ids.Contains(x.MatchId))
                .ToList();
        }

        public async Task UpsertAsync(MatchLiveStats stats, CancellationToken ct = default)
        {
            var existing = await _context.MatchLiveStats
                .FirstOrDefaultAsync(x => x.MatchId == stats.MatchId, ct);

            if (existing == null)
            {
                _context.MatchLiveStats.Add(stats);
            }
            else
            {
                existing.HomeScore = stats.HomeScore;
                existing.AwayScore = stats.AwayScore;
                existing.Minute = stats.Minute;
                existing.Phase = stats.Phase;
                existing.PossessionHome = stats.PossessionHome;
                existing.PossessionAway = stats.PossessionAway;
                existing.ShotsHome = stats.ShotsHome;
                existing.ShotsAway = stats.ShotsAway;
                existing.ShotsOnTargetHome = stats.ShotsOnTargetHome;
                existing.ShotsOnTargetAway = stats.ShotsOnTargetAway;
                existing.CornersHome = stats.CornersHome;
                existing.CornersAway = stats.CornersAway;
                existing.FoulsHome = stats.FoulsHome;
                existing.FoulsAway = stats.FoulsAway;
                existing.OffsidesHome = stats.OffsidesHome;
                existing.OffsidesAway = stats.OffsidesAway;
                existing.YellowHome = stats.YellowHome;
                existing.YellowAway = stats.YellowAway;
                existing.RedHome = stats.RedHome;
                existing.RedAway = stats.RedAway;
                existing.DangerousAttacksHome = stats.DangerousAttacksHome;
                existing.DangerousAttacksAway = stats.DangerousAttacksAway;
                existing.XgHome = stats.XgHome;
                existing.XgAway = stats.XgAway;
                existing.UpdatedAt = stats.UpdatedAt;
                _context.MatchLiveStats.Update(existing);
            }
        }

        /// <summary>
        /// No-query overload: reuses an EF-tracked entity already loaded by
        /// <see cref="GetByMatchIds"/> so no second SELECT is issued.
        /// </summary>
        public Task UpsertAsync(
            MatchLiveStats incoming,
            MatchLiveStats? existingTracked,
            CancellationToken ct = default)
        {
            if (existingTracked == null)
            {
                // First row for this match — just add it.
                _context.MatchLiveStats.Add(incoming);
            }
            else
            {
                // existingTracked is already change-tracked by the same DbContext
                // (it was loaded via GetByMatchIds in the same scope).
                // Mutating its properties is enough — SaveChangesAsync will detect
                // the diff and issue a single UPDATE.  No explicit Update() call needed.
                existingTracked.HomeScore             = incoming.HomeScore;
                existingTracked.AwayScore             = incoming.AwayScore;
                existingTracked.Minute                = incoming.Minute;
                existingTracked.Phase                 = incoming.Phase;
                existingTracked.PossessionHome        = incoming.PossessionHome;
                existingTracked.PossessionAway        = incoming.PossessionAway;
                existingTracked.ShotsHome             = incoming.ShotsHome;
                existingTracked.ShotsAway             = incoming.ShotsAway;
                existingTracked.ShotsOnTargetHome     = incoming.ShotsOnTargetHome;
                existingTracked.ShotsOnTargetAway     = incoming.ShotsOnTargetAway;
                existingTracked.CornersHome           = incoming.CornersHome;
                existingTracked.CornersAway           = incoming.CornersAway;
                existingTracked.FoulsHome             = incoming.FoulsHome;
                existingTracked.FoulsAway             = incoming.FoulsAway;
                existingTracked.OffsidesHome          = incoming.OffsidesHome;
                existingTracked.OffsidesAway          = incoming.OffsidesAway;
                existingTracked.YellowHome            = incoming.YellowHome;
                existingTracked.YellowAway            = incoming.YellowAway;
                existingTracked.RedHome               = incoming.RedHome;
                existingTracked.RedAway               = incoming.RedAway;
                existingTracked.DangerousAttacksHome  = incoming.DangerousAttacksHome;
                existingTracked.DangerousAttacksAway  = incoming.DangerousAttacksAway;
                existingTracked.XgHome                = incoming.XgHome;
                existingTracked.XgAway                = incoming.XgAway;
                existingTracked.UpdatedAt             = incoming.UpdatedAt;
            }
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
