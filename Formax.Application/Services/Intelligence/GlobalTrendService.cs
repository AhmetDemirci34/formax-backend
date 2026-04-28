using Formax.Application.Abstractions;
using Formax.Application.DTOs.Home;
using Microsoft.EntityFrameworkCore;

namespace Formax.Application.Services.Intelligence;

public class GlobalTrendService
{
    private readonly IAppDbContext _context;

    public GlobalTrendService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<double> GetGlobalScore(HomeRadarMatchDto match)
    {
        var now = DateTime.UtcNow;

        var actions = await _context.UserActions
            .Where(x => x.MatchId == match.MatchId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();

        double baseScore;

        if (!actions.Any())
        {
            var play = match.PlayRate / 100.0;
            var radar = match.RadarScore / 100.0;

            baseScore = 0.45 + ((play + radar) * 0.1);
        }
        else
        {
            double score = 0;

            foreach (var a in actions)
            {
                var actionScore = a.ActionType == 1 ? 1 : -1;

                var ageMin = (now - a.CreatedAt).TotalMinutes;

                var timeWeight = Math.Exp(-ageMin / 60.0);

                score += actionScore * timeWeight;
            }

            var scaled = Math.Tanh(score / 3.0);

            baseScore = 0.5 + (scaled * 0.5);
        }

        // 🔥 DOĞRU AMPLIFICATION (linear boost)
        var boosted = baseScore + ((baseScore - 0.5) * 0.8);

        return Math.Round(Math.Clamp(boosted, 0.0, 1.0), 3);
    }
}