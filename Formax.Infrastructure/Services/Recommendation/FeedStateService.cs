using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class FeedStateService
    {
        private readonly FormaxDbContext _db;

        public FeedStateService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task UpdateState(int matchId, bool clicked)
        {
            var state = await _db.MatchFeedStates
                .FirstOrDefaultAsync(x => x.MatchId == matchId);

            if (state == null)
            {
                state = new MatchFeedState
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId
                };

                _db.MatchFeedStates.Add(state);
            }

            state.Impressions++;

            if (clicked)
                state.Clicks++;

            state.Score = CalculateScore(state);

            state.State = CalculateState(state);

            state.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private double CalculateScore(MatchFeedState state)
        {
            if (state.Impressions == 0)
                return 0;

            return (double)state.Clicks / state.Impressions;
        }

        private string CalculateState(MatchFeedState state)
        {
            if (state.Impressions < 50)
                return "testing";

            if (state.Score > 0.30)
                return "hot";

            if (state.Score > 0.15)
                return "learning";

            if (state.Score > 0.05)
                return "cooling";

            return "dead";
        }
    }
}
