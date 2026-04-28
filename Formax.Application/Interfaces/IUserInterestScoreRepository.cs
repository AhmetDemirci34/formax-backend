using Formax.Domain.Entities;

public interface IUserInterestScoreRepository
{
    // 🔥 CORE (NEW SYSTEM)
    Task<UserInterestScore?> GetAsync(int userId, string layer, string key);

    Task<List<UserInterestScore>> GetByUser(int userId);

    Task UpsertAsync(UserInterestScore entity);

    // 🔥 LEGACY SUPPORT (korunuyor)
    Task UpsertAsync(int userId, string layer, string key, int score, DateTime updatedAtUtc);

    Task UpsertDeltaAsync(int userId, string layer, string key, int delta, DateTime updatedAtUtc);
}