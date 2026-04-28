using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Follow
{
    public class GetUsersFollowingMatchUseCase
    {
        private readonly IUserMatchFollowRepository _repository;

        public GetUsersFollowingMatchUseCase(IUserMatchFollowRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<int>> ExecuteAsync(int matchId)
        {
            var follows = await _repository.GetByMatch(matchId);

            return follows
                .Select(x => x.UserId)
                .ToList();
        }
    }
}
