using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IInterestAggregationService
    {
        Task AggregateMatchEventAsync(
            int userId,
            int matchId,
            int weight);

        Task AggregateTeamFollowAsync(
            int userId,
            int teamId,
            int weight);

        Task AggregateLeagueFollowAsync(
            int userId,
            string leagueName,
            int weight);
    }
}