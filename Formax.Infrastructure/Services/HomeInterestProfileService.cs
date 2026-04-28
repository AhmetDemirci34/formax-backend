using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Services
{
    /// <summary>
    /// FAZ 6.2 — Interest Engine derinleştirme.
    /// DB'deki interest skorlarını decay uygulayarak radar için okunabilir hale getirir.
    /// </summary>
    public sealed class HomeInterestProfileService : IHomeInterestProfileService
    {
        private readonly FormaxDbContext _db;

        public HomeInterestProfileService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task<HomeInterestProfileDto> BuildAsync(int? userId, DateTime utcNow)
        {
            if (!userId.HasValue)
            {
                return new HomeInterestProfileDto();
            }

            var rows = await _db.UserInterestScores
                .Where(x => x.UserId == userId.Value)
                .ToListAsync();

            var team = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var league = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var content = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var decayed = ApplyDecay(row.Score, row.Layer, row.LastEventAtUtc, utcNow);
                if (decayed <= 0 || string.IsNullOrWhiteSpace(row.Key))
                    continue;

                if (row.Layer.Equals("Team", StringComparison.OrdinalIgnoreCase))
                {
                    team[row.Key] = decayed;
                }
                else if (row.Layer.Equals("League", StringComparison.OrdinalIgnoreCase))
                {
                    league[row.Key] = decayed;
                }
                else if (row.Layer.Equals("ContentType", StringComparison.OrdinalIgnoreCase))
                {
                    content[row.Key] = decayed;
                }
            }

            return new HomeInterestProfileDto
            {
                TeamScores = team,
                LeagueScores = league,
                ContentScores = content
            };
        }

        private static int ApplyDecay(int score, string layer, DateTime lastEventAtUtc, DateTime utcNow)
        {
            var hours = Math.Max(0, (utcNow - lastEventAtUtc).TotalHours);
            var hourlyDecay = layer?.ToUpperInvariant() switch
            {
                "TEAM" => 0.28,
                "LEAGUE" => 0.18,
                "CONTENTTYPE" => 0.40,
                _ => 0.30
            };

            var decayed = (int)Math.Round(score - (hours * hourlyDecay), MidpointRounding.AwayFromZero);
            return Clamp(decayed);
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
    }
}
