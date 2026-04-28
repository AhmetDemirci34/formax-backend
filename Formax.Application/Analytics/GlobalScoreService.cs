using Formax.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

public class GlobalScoreService
{
    private readonly IAppDbContext _context;

    public GlobalScoreService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<double> Calculate(int matchId)
    {
        var actions = await _context.UserActions
            .Where(x => x.MatchId == matchId)
            .ToListAsync();

        // 🔥 fallback
        if (!actions.Any())
            return 0.2;

        var now = DateTime.UtcNow;

        double weightedPlay = 0;
        double weightedSkip = 0;

        foreach (var action in actions)
        {
            // 🔥 saat farkı
            var hours = (now - action.CreatedAt).TotalHours;

            // 🔥 TIME DECAY (24 saat yarı ömür)
            var decay = Math.Exp(-hours / 24);

            if (action.ActionType == 1)
                weightedPlay += decay;

            else if (action.ActionType == 0)
                weightedSkip += decay;
        }

        var total = weightedPlay + weightedSkip;

        // 🔥 güvenlik fallback
        if (total == 0)
            return 0.2;

        var raw = (weightedPlay - weightedSkip) / total;

        // 🔥 normalize (0–1)
        var normalized = (raw + 1) / 2;

        return Math.Round(normalized, 2);
    }
}