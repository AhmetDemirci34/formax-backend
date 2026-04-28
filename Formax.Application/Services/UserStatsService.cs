using Formax.Application.Interfaces;
using Formax.Domain.Entities;

public class UserStatsService : IUserStatsService
{
    private readonly IUserStatsRepository _repo;

    public UserStatsService(IUserStatsRepository repo)
    {
        _repo = repo;
    }

    public async Task UpdateStats(int userId, bool isWin) // 🔥 FIX
    {
        var stats = await _repo.Get(userId);

        if (stats == null)
        {
            stats = new UserStats
            {
                UserId = userId // 🔥 FIX
            };

            await _repo.Add(stats);
        }

        stats.Total++;

        if (isWin)
        {
            stats.Win++;
            stats.CurrentStreak++;

            if (stats.CurrentStreak > stats.BestStreak)
                stats.BestStreak = stats.CurrentStreak;
        }
        else
        {
            stats.Lose++;
            stats.CurrentStreak = 0;
        }

        stats.Level = CalculateLevel(stats);

        await _repo.Update(stats);
    }

    private int CalculateLevel(UserStats stats)
    {
        if (stats.Win >= 50) return 5;
        if (stats.Win >= 30) return 4;
        if (stats.Win >= 15) return 3;
        if (stats.Win >= 5) return 2;
        return 1;
    }
}