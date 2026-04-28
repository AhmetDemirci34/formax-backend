using Formax.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Formax.Application.Services.Recommendation;

public interface ITrendService
{
    Task<double> GetTrendScore(int matchId);
}

public class TrendService : ITrendService
{
    private readonly IAppDbContext _context;

    private readonly Dictionary<int, (double score, DateTime time)> _cache = new();
    private const int CACHE_SECONDS = 30;

    public TrendService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<double> GetTrendScore(int matchId)
    {
        // CACHE
        if (_cache.TryGetValue(matchId, out var cached))
        {
            if ((DateTime.UtcNow - cached.time).TotalSeconds < CACHE_SECONDS)
                return cached.score;
        }

        var actions = await _context.UserActions
            .Where(x => x.MatchId == matchId)
            .ToListAsync();

        if (!actions.Any())
            return Cache(matchId, 0.3); // fallback

        var play = actions.Count(x => x.ActionType == 1);
        var total = actions.Count;

        double playRate = (double)play / total;

        // 🔥 TREND DELTA (basit versiyon)
        double trendDelta = playRate > 0.6 ? 0.2 : 0;

        double trend = playRate + trendDelta;

        // clamp
        trend = Math.Clamp(trend, 0.0, 1.0);

        return Cache(matchId, Math.Round(trend, 2));
    }

    private double Cache(int matchId, double score)
    {
        _cache[matchId] = (score, DateTime.UtcNow);
        return score;
    }
}