using Formax.Application.DTOs;
using Formax.Application.Abstractions;
using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Formax.Application.AI.Learning;

public class UserLearningService
{
    private readonly IAppDbContext _db;

    public UserLearningService(IAppDbContext db)
    {
        _db = db;
    }

    // 🔥 USER ACTION → PROFILE UPDATE
    public async Task UpdateUserProfile(UserActionDto dto)
    {
        int userId = dto.UserId;

        // USER
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
        {
            user = new User
            {
                Id = userId
            };

            _db.Users.Add(user);
        }

        // USER STATS
        var stats = await _db.UserStats
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (stats == null)
        {
            stats = new UserStats
            {
                UserId = userId,
                PlayCount = 0,
                PassCount = 0,
                RiskPreference = 0,
                SafePreference = 0,
                Total = 0
            };

            _db.UserStats.Add(stats);
        }

        // 🔥 ACTION TYPE LOGIC
        // 1 = PLAY (risk)
        // 0 = PASS (safe)

        if (dto.ActionType == 1)
        {
            stats.PlayCount++;
            stats.RiskPreference += 1;
        }
        else if (dto.ActionType == 0)
        {
            stats.PassCount++;
            stats.SafePreference += 1;
        }

        stats.Total++;

        await _db.SaveChangesAsync();
    }

    // 🔥 USER TYPE (CRITICAL)
    public string GetUserType(int userId)
    {
        var stats = _db.UserStats
            .FirstOrDefault(x => x.UserId == userId);

        if (stats == null || stats.Total == 0)
            return "SAFE"; // default

        var ratio = (double)stats.PlayCount / stats.Total;

        if (ratio > 0.65)
            return "RISK";

        if (ratio < 0.35)
            return "SAFE";

        return "BALANCED";
    }
}