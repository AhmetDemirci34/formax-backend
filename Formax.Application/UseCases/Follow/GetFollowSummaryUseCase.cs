using Formax.Application.DTOs.Follow;
using Formax.Application.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Follow
{
    /// <summary>
    /// GET /api/follow/summary — kullanıcının takım/maç/lig takip sayımları.
    /// Tüm sayımlar gerçek depolardan gelir (uydurma yok).
    /// </summary>
    public class GetFollowSummaryUseCase
    {
        private readonly IUserTeamFollowRepository _teams;
        private readonly IUserMatchFollowRepository _matches;
        private readonly IUserLeagueFollowRepository _leagues;

        public GetFollowSummaryUseCase(
            IUserTeamFollowRepository teams,
            IUserMatchFollowRepository matches,
            IUserLeagueFollowRepository leagues)
        {
            _teams = teams;
            _matches = matches;
            _leagues = leagues;
        }

        public async Task<FollowSummaryDto> ExecuteAsync(int userId)
        {
            var teams = await _teams.GetActiveByUserAsync(userId);
            var matches = await _matches.GetByUserAsync(userId);
            var leaguesCount = await _leagues.CountActiveByUserAsync(userId);

            return new FollowSummaryDto
            {
                TeamsCount = teams.Count,
                MatchesCount = matches.Count,
                LeaguesCount = leaguesCount
            };
        }
    }
}
