using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Domain.Entities;

public class FollowMatchUseCase
{
    private readonly IUserMatchFollowRepository _repository;

    public FollowMatchUseCase(IUserMatchFollowRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(int userId, int matchId)
    {
        if (await _repository.ExistsAsync(userId, matchId))
            return;

        await _repository.AddAsync(new UserMatchFollow
        {
            UserId = userId,
            MatchId = matchId
        });
    }
}

