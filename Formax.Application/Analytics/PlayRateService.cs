using Formax.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

public class PlayRateService
{
    private readonly IAppDbContext _context;

    public PlayRateService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<int> Calculate(int matchId)
    {
        var actions = await _context.UserActions
            .Where(x => x.MatchId == matchId)
            .ToListAsync();

        if (!actions.Any())
            return 50;

        var play = actions.Count(x => x.ActionType == 1);
        var total = actions.Count;

        var rate = (int)((double)play / total * 100);

        return Math.Clamp(rate, 0, 100);
    }
}