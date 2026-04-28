using Formax.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

public class SessionBehaviorService
{
    private readonly IAppDbContext _context;

    public SessionBehaviorService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<SessionState> GetSessionState(int userId)
    {
        var recent = await _context.UserActions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        if (!recent.Any())
        {
            return new SessionState
            {
                Mode = "NORMAL",
                PlayRatio = 0.5
            };
        }

        var play = recent.Count(x => x.ActionType == 1);
        var skip = recent.Count(x => x.ActionType == 0);

        var total = play + skip == 0 ? 1 : play + skip;
        var ratio = (double)play / total;

        string mode;

        if (ratio > 0.7)
            mode = "AGGRESSIVE";
        else if (ratio < 0.3)
            mode = "CAUTIOUS";
        else
            mode = "NORMAL";

        return new SessionState
        {
            Mode = mode,
            PlayRatio = Math.Round(ratio, 2)
        };
    }
}

public class SessionState
{
    public string Mode { get; set; } = "NORMAL";
    public double PlayRatio { get; set; }
}
