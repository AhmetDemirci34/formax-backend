using Formax.Application.Interfaces;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Leagues
{
    public class UnfollowLeagueUseCase
    {
        private readonly IUserLeagueFollowRepository _follows;

        public UnfollowLeagueUseCase(IUserLeagueFollowRepository follows)
        {
            _follows = follows;
        }

        public Task ExecuteAsync(int userId, int leagueId)
            => _follows.UnfollowAsync(userId, leagueId);
    }
}
