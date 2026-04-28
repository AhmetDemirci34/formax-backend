using Formax.Application.DTOs.User;
using Formax.Application.Interfaces.Repositories;

namespace Formax.Application.Services.Intelligence;

public class UserProfileEngine
{
    private readonly IUserActionRepository _repo;

    public UserProfileEngine(IUserActionRepository repo)
    {
        _repo = repo;
    }

    public async Task<UserProfileDto> Build(int userId)
    {
        var actions = await _repo.GetByUserIdAsync(userId);

        if (actions.Count == 0)
        {
            return new UserProfileDto
            {
                RiskLevel = 0.5,
                AvgViewTime = 0,
                LikeRate = 0,
                SkipRate = 0,
                EngagementScore = 0
            };
        }

        var total = actions.Count;

        var likes = actions.Count(x => x.ActionType == 1);
        var skips = actions.Count(x => x.ActionType == -1);

        var avgView = actions.Average(x => x.ViewDurationMs);

        // 🔥 RISK: yüksek odds seviyor mu
        var highOdds = actions.Where(x => x.Odds >= 2.0).Count();
        var riskLevel = (double)highOdds / total;

        // 🔥 engagement
        var engaged = actions.Count(x => x.ViewDurationMs > 5000 || x.OpenedDetail || x.Followed);
        var engagement = (double)engaged / total;

        return new UserProfileDto
        {
            RiskLevel = Math.Clamp(riskLevel, 0, 1),
            AvgViewTime = avgView,
            LikeRate = (double)likes / total,
            SkipRate = (double)skips / total,
            EngagementScore = engagement
        };
    }
}