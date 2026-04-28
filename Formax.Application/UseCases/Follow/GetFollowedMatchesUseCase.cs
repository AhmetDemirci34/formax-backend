using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Domain.Entities;

public class GetFollowedMatchesUseCase
{
    private readonly IUserMatchFollowRepository _repository;

    public GetFollowedMatchesUseCase(IUserMatchFollowRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<int>> ExecuteAsync(int userId)
        => (await _repository.GetByUserAsync(userId))
            .Select(x => x.MatchId)
            .ToList();
}
