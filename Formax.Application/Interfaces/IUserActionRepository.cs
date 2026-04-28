using Formax.Domain.Entities;

namespace Formax.Application.Interfaces.Repositories;

public interface IUserActionRepository
{
    Task AddAsync(UserAction action);
    Task<List<UserAction>> GetByUserIdAsync(int userId);
    Task<List<UserAction>> GetByMatch(int userId, int matchId);
}