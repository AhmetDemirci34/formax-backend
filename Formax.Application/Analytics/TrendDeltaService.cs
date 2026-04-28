using Formax.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

public class TrendDeltaService
{
    private readonly IAppDbContext _context;

    public TrendDeltaService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<int> Calculate(int matchId)
    {
        var now = DateTime.UtcNow;

        var last1h = now.AddHours(-1);
        var prev1h = now.AddHours(-2);

        var recent = await _context.UserActions
            .Where(x => x.MatchId == matchId && x.CreatedAt >= last1h)
            .ToListAsync();

        var previous = await _context.UserActions
            .Where(x => x.MatchId == matchId && x.CreatedAt >= prev1h && x.CreatedAt < last1h)
            .ToListAsync();

        if (!recent.Any())
            return 0;

        int recentPlay = recent.Count(x => x.ActionType == 1);
        int prevPlay = previous.Count(x => x.ActionType == 1);

        int recentTotal = recent.Count;
        int prevTotal = previous.Count;

        double recentRate = recentTotal == 0 ? 0 : (double)recentPlay / recentTotal;
        double prevRate = prevTotal == 0 ? 0 : (double)prevPlay / prevTotal;

        var delta = (recentRate - prevRate) * 100;

        return (int)Math.Round(Math.Clamp(delta, -20, 20));
    }
}