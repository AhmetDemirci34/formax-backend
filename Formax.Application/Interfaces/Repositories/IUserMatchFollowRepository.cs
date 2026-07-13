using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Domain.Entities;

public interface IUserMatchFollowRepository
{
    Task<bool> ExistsAsync(int userId, int matchId);
    Task AddAsync(UserMatchFollow follow);
    Task RemoveAsync(int userId, int matchId);
    Task<List<UserMatchFollow>> GetByUserAsync(int userId);
    Task<List<UserMatchFollow>> GetByMatch(int matchId);

    /// <summary>Synchronous follower count — safe to call from synchronous use cases.</summary>
    int CountByMatchId(int matchId);
}
