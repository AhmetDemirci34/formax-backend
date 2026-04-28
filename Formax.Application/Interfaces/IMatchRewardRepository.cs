using Formax.Domain.Entities;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IMatchRewardRepository
    {
        Task UpdateAsync(int matchId, int weight);

        Task<MatchRewardStats?> GetByMatchIdAsync(int matchId);

        Task<List<MatchRewardStats>> GetAllAsync();
    }
}
