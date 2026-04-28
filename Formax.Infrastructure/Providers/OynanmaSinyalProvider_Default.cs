using System;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Abstractions;
using Formax.Application.Services.Intelligence;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Providers
{
    public sealed class OynanmaSinyalProvider_Default : IOynanmaSinyalProvider
    {
        private readonly IAppDbContext _context;
        private readonly IExternalTrendProvider _external;
        private readonly GlobalTrendService _global;

        public OynanmaSinyalProvider_Default(
            IAppDbContext context,
            IExternalTrendProvider external,
            GlobalTrendService global)
        {
            _context = context;
            _external = external;
            _global = global;
        }

        public async Task<OynanmaSinyalleri?> GetAsync(int matchId)
        {
            var now = DateTime.UtcNow;

            var actions = await _context.UserActions
                .Where(x => x.MatchId == matchId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();

            double score = 0;

            foreach (var a in actions)
            {
                var baseScore = a.ActionType == 1 ? 1 : -1;

                var ageMin = (now - a.CreatedAt).TotalMinutes;
                var timeWeight = Math.Max(0.3, 1 - (ageMin / 30));

                var oddsWeight = Math.Clamp(a.Odds / 2.0, 0.7, 1.3);

                score += baseScore * timeWeight * oddsWeight;
            }

            var cappedScore = Math.Clamp(score, -7, 7);      // 🔥 cap düşürüldü
            var userScaled = Math.Tanh(cappedScore / 7.0);   // 🔥 daha yavaş büyüme
            var userIntensity = 50 + (userScaled * 50);

            if (actions.Count < 3)
            {
                userIntensity = 50 + ((userIntensity - 50) * 0.5);
            }

            // 🔥 EXTERNAL
            var marketMomentum = await _external.GetMarketMomentum(matchId);
            var externalIntensity = 50 + ((marketMomentum - 0.5) * 100);

            // 🔥 GLOBAL
            var dummy = new Formax.Application.DTOs.Home.HomeRadarMatchDto
            {
                MatchId = matchId,
                PlayRate = 50,
                RadarScore = 50
            };

            var globalScore = await _global.GetGlobalScore(dummy);
            var globalIntensity = 50 + ((globalScore - 0.5) * 100);

            // 🔥 FINAL WEIGHT (Phase 7 uyumlu)
            var final =
                (userIntensity * 0.6) +
                (globalIntensity * 0.2) +
                (externalIntensity * 0.2);

            var intensity = (int)Math.Clamp(final, 0, 100);

            string side = "Denge";

            if (intensity > 55)
                side = "Home";
            else if (intensity < 45)
                side = "Away";

            return new OynanmaSinyalleri
            {
                Side = side,
                Intensity = intensity,
                OddsMove = (int)externalIntensity,
                MediaTrend = (int)(marketMomentum * 100),
                LastUpdatedAtUtc = now
            };
        }
    }
}