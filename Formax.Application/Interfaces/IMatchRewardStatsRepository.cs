using System.Threading.Tasks;
using Formax.Domain.Entities;


namespace Formax.Application.Interfaces
{
    public interface IMatchRewardStatsRepository
    {
        Task<MatchRewardStats?> GetAsync(int matchId);

        Task IncrementImpressionAsync(int matchId);

        Task IncrementClickAsync(int matchId);

        Task IncrementOpenAsync(int matchId);

        Task IncrementFollowAsync(int matchId);
        Task IncrementSkipAsync(int matchId);
        Task<List<MatchRewardStats>> GetAllAsync();
       

    }
}
