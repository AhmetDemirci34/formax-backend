using Formax.Domain.Entities;

public interface IUserStatsRepository
{
    Task<UserStats?> Get(string userId);
    Task Add(UserStats stats);
    Task Update(UserStats stats);
    Task<UserStats?> Get(int userId);
}
