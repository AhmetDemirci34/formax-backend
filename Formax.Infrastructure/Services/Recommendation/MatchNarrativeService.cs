using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class MatchNarrativeService
    {
        private readonly FormaxDbContext _db;

        public MatchNarrativeService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task<MatchNarrative> GenerateNarrative(
            int matchId,
            string homeTeam,
            string awayTeam,
            string league,
            double heat,
            double trendScore)
        {
            var existing = await _db.MatchNarratives
                .FirstOrDefaultAsync(x => x.MatchId == matchId);

            if (existing != null)
                return existing;

            var title = $"{homeTeam} vs {awayTeam}";

            var reason = BuildReason(heat, trendScore);

            var story =
                $"{homeTeam} ile {awayTeam} karşılaşıyor. " +
                $"Lig: {league}. {reason}";

            var narrative = new MatchNarrative
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                Title = title,
                Story = story,
                Reason = reason,
                NarrativeScore = CalculateNarrativeScore(heat, trendScore),
                GeneratedAt = DateTime.UtcNow
            };

            _db.MatchNarratives.Add(narrative);

            await _db.SaveChangesAsync();

            return narrative;
        }

        private string BuildReason(double heat, double trend)
        {
            if (trend > 10)
                return "Bu maç şu anda platformda hızla trend oluyor.";

            if (heat > 7)
                return "İki takım da formda ve yüksek tempolu bir maç bekleniyor.";

            return "Bu karşılaşma keşfedilmeye değer bir mücadele sunuyor.";
        }

        private double CalculateNarrativeScore(double heat, double trend)
        {
            return (heat * 0.6) + (trend * 0.4);
        }
    }
}