using Formax.Domain.Entities;

public interface IUserPickRepository
{
    Task<List<UserPick>> GetByMatchId(int matchId);
    Task Update(UserPick pick);
    Task Add(UserPick pick);
    Task SaveChanges();
}
