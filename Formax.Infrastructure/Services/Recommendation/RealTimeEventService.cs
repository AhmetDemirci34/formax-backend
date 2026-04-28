using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class RealTimeEventService
    {
        private readonly FormaxDbContext _db;

        public RealTimeEventService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task RegisterEvent(
            int matchId,
            string eventType,
            int minute,
            string? team,
            string? player)
        {
            var liveEvent = new MatchLiveEvent
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                EventType = eventType,
                Minute = minute,
                Team = team,
                Player = player,
                ImpactScore = CalculateImpact(eventType, minute),
                CreatedAt = DateTime.UtcNow
            };

            _db.MatchLiveEvents.Add(liveEvent);

            await _db.SaveChangesAsync();
        }

        private double CalculateImpact(string eventType, int minute)
        {
            double baseScore = eventType switch
            {
                "goal" => 10,
                "red_card" => 7,
                "penalty" => 6,
                "big_chance" => 4,
                _ => 2
            };

            double lateBoost = minute > 75 ? 1.5 : 1.0;

            return baseScore * lateBoost;
        }
    }
}
