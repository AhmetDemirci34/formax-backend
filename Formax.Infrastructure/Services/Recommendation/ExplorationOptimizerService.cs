using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class ExplorationOptimizerService
    {
        private readonly FormaxDbContext _db;

        public ExplorationOptimizerService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task<double> CalculateExplorationBoost(int matchId)
        {
            var stat = await _db.MatchExplorationStats
                .FirstOrDefaultAsync(x => x.MatchId == matchId);

            if (stat == null)
            {
                stat = new MatchExplorationStat
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId
                };

                _db.MatchExplorationStats.Add(stat);

                await _db.SaveChangesAsync();

                return 2.0;
            }

            if (stat.ExplorationImpressions == 0)
                return 1.5;

            double ctr =
                (double)stat.ExplorationClicks /
                stat.ExplorationImpressions;

            double boost =
                1 + (ctr * 2);

            return boost;
        }

        public async Task RegisterExplorationEvent(
            int matchId,
            bool clicked)
        {
            var stat = await _db.MatchExplorationStats
                .FirstOrDefaultAsync(x => x.MatchId == matchId);

            if (stat == null)
                return;

            stat.ExplorationImpressions++;

            if (clicked)
                stat.ExplorationClicks++;

            stat.ExplorationScore =
                stat.ExplorationClicks /
                (double)Math.Max(stat.ExplorationImpressions, 1);

            stat.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
    }
}
