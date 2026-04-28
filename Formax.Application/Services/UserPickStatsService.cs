using Formax.Domain.Entities;

public class UserPickStatsService
{
    private readonly IUserPickStatsRepository _repo;

    public UserPickStatsService(IUserPickStatsRepository repo)
    {
        _repo = repo;
    }

    public async Task Update(string userId, string pickLabel, bool isWin)
    {
        var stat = await _repo.Get(userId, pickLabel);

        if (stat == null)
        {
            stat = new UserPickStats
            {
                UserId = userId,
                PickLabel = pickLabel
            };

            await _repo.Add(stat);
        }

        stat.Total++;

        if (isWin)
            stat.Win++;

        await _repo.Update(stat);
    }
}