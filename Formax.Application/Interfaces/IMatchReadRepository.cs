using Formax.Application.DTOs.Matches;
using Formax.Domain.Entities;
using System.Linq;

namespace Formax.Application.Interfaces
{
    public interface IMatchReadRepository
    {
        IQueryable<Match> Query();

        Match? GetById(int id);

        List<Match> GetUpcomingMatches(DateTime from, DateTime to);

        Task<IReadOnlyList<MatchListItemDto>> GetMatchListAsync();

        Task<IReadOnlyList<MatchListItemDto>> GetMatchListByIdsAsync(IEnumerable<int> ids);

        // 🔥 NEW
        List<Match> GetRecentMatchesForTeam(int teamId, int count = 5);

        // 🔥 NEW
        List<Match> GetHeadToHeadMatches(
            int homeTeamId,
            int awayTeamId,
            int count = 5
        );
    }
}