using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class TeamSeasonStatisticRepository : ITeamSeasonStatisticRepository
    {
        private readonly FormaxDbContext _context;

        public TeamSeasonStatisticRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public TeamSeasonStatistic? GetByTeam(int leagueId, int seasonYear, int teamExternalId)
            => _context.TeamSeasonStatistics
                .AsNoTracking()
                .FirstOrDefault(x =>
                    x.LeagueId == leagueId &&
                    x.SeasonYear == seasonYear &&
                    x.TeamId == teamExternalId);

        public async Task UpsertAsync(TeamSeasonStatistic stat, CancellationToken ct = default)
        {
            var existing = await _context.TeamSeasonStatistics
                .FirstOrDefaultAsync(x =>
                    x.LeagueId == stat.LeagueId &&
                    x.SeasonYear == stat.SeasonYear &&
                    x.TeamId == stat.TeamId, ct);

            if (existing == null)
            {
                _context.TeamSeasonStatistics.Add(stat);
                return;
            }

            existing.TeamName             = stat.TeamName;
            existing.PlayedTotal          = stat.PlayedTotal;
            existing.PlayedHome           = stat.PlayedHome;
            existing.PlayedAway           = stat.PlayedAway;
            existing.WinsTotal            = stat.WinsTotal;
            existing.DrawsTotal           = stat.DrawsTotal;
            existing.LosesTotal           = stat.LosesTotal;
            existing.GoalsForAvgTotal     = stat.GoalsForAvgTotal;
            existing.GoalsForAvgHome      = stat.GoalsForAvgHome;
            existing.GoalsForAvgAway      = stat.GoalsForAvgAway;
            existing.GoalsAgainstAvgTotal = stat.GoalsAgainstAvgTotal;
            existing.GoalsAgainstAvgHome  = stat.GoalsAgainstAvgHome;
            existing.GoalsAgainstAvgAway  = stat.GoalsAgainstAvgAway;
            existing.CleanSheetTotal      = stat.CleanSheetTotal;
            existing.FailedToScoreTotal   = stat.FailedToScoreTotal;
            existing.Form                 = stat.Form;
            existing.UpdatedAt            = stat.UpdatedAt;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
