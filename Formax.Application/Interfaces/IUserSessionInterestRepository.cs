using Formax.Domain.Entities;

public interface IUserSessionInterestRepository
{
    Task<UserSessionInterest?> GetAsync(int userId, string? league, string? team);
    Task UpsertAsync(UserSessionInterest entity);
}
