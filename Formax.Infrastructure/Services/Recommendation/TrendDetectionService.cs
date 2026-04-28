using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class TrendDetectionService
    {
        private readonly FormaxDbContext _db;

        public TrendDetectionService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task RegisterTrendEvent(
            int matchId,
            bool clicked,
            int dwellSeconds,
            bool goal)
        {
            var trend = await _db.MatchTrendStats
                .FirstOrDefaultAsync(x => x.MatchId == matchId);

            if (trend == null)
            {
                trend = new MatchTrendStat
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId
                };

                _db.MatchTrendStats.Add(trend);
            }

            trend.Impressions++;

            if (clicked)
                trend.Clicks++;

            trend.Dwells += dwellSeconds;

            if (goal)
                trend.Goals++;

            trend.TrendScore = CalculateTrendScore(trend);

            trend.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private double CalculateTrendScore(MatchTrendStat trend)
        {
            double score =
                (trend.Clicks * 0.6) +
                (trend.Dwells * 0.02) +
                (trend.Goals * 5);

            return score;
        }
    }
}