using Formax.Application.Interfaces;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Leagues
{
    public class FollowLeagueUseCase
    {
        private readonly IUserLeagueFollowRepository _follows;

        public FollowLeagueUseCase(IUserLeagueFollowRepository follows)
        {
            _follows = follows;
        }

        public Task ExecuteAsync(int userId, int leagueId)
            => _follows.FollowAsync(userId, leagueId);
    }
}
