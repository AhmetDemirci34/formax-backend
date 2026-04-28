using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Teams
{
    public class SetMyTeamsUseCase
    {
        private readonly IUserTeamFollowRepository _follows;

        public SetMyTeamsUseCase(IUserTeamFollowRepository follows)
        {
            _follows = follows;
        }

        public async Task ExecuteAsync(int userId, List<int> teamIds)
        {
            teamIds ??= new List<int>();
            await _follows.SetUserTeamsSyncAsync(userId, teamIds);
        }
    }
}
