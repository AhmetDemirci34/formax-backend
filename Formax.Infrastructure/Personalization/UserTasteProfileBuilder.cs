using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Personalization;

public class UserTasteProfileBuilder : IUserTasteProfileBuilder
{
    private readonly FormaxDbContext _context;

    public UserTasteProfileBuilder(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<UserTasteProfile> Build(int userId)
    {
        var actions = await _context.UserActions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        if (!actions.Any())
        {
            return new UserTasteProfile
            {
                UserId = userId,
                RiskLevel = 0.5,
                TrendAffinity = 0.5,
                ValueSeeking = 0.5
            };
        }

        // 🔥 INT SYSTEM (0 = SKIP, 1 = PLAY)
        var play = actions.Count(x => x.ActionType == 1);
        var skip = actions.Count(x => x.ActionType == 0);

        var total = play + skip;

        if (total == 0)
            total = 1;

        // 🔥 USER PROFILE CALC

        // risk = ne kadar oynuyor
        var risk = (double)play / total;

        // trend affinity = aktiflik (şimdilik play oranı ile)
        var trend = (double)play / total;

        // value seeking = risk + ters davranış dengesi
        var value = (double)(play - skip + total) / (2 * total);

        return new UserTasteProfile
        {
            UserId = userId,
            RiskLevel = Math.Round(risk, 2),
            TrendAffinity = Math.Round(trend, 2),
            ValueSeeking = Math.Round(value, 2)
        };
    }
}