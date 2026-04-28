using Formax.Application.Live;

namespace Formax.Application.Interfaces
{
    public interface IMatchEventRepository
    {
        Task AddAsync(MatchEvent matchEvent);
        Task<List<MatchEvent>> GetByMatchAsync(int matchId);
    }
}
