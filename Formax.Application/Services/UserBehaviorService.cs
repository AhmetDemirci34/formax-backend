using Formax.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

public class UserBehaviorService
{
    private readonly IAppDbContext _context;

    public UserBehaviorService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<double> GetUserPlayBias(int userId)
    {
        var actions = await _context.UserActions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        if (!actions.Any())
            return 0.5;

        var playCount = actions.Count(x => x.ActionType == 1);
        var total = actions.Count;

        var ratio = (double)playCount / total;

        return Math.Round(ratio, 2);
    }
}