using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Domain.Entities;

public class UnfollowMatchUseCase
{
    private readonly IUserMatchFollowRepository _repository;

    public UnfollowMatchUseCase(IUserMatchFollowRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(int userId, int matchId)
        => await _repository.RemoveAsync(userId, matchId);
}

