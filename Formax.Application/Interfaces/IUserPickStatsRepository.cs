using Formax.Domain.Entities;

public interface IUserPickStatsRepository
{
    Task<UserPickStats?> Get(string userId, string pickLabel);
    Task Add(UserPickStats stats);
    Task Update(UserPickStats stats);
}
