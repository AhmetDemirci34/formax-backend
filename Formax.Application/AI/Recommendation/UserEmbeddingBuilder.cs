using System;
using System.Threading.Tasks;
using Formax.Domain.ValueObjects;
using Formax.Application.Interfaces;

namespace Formax.Application.AI.Recommendation;

public class UserEmbeddingBuilder
{
    private readonly IUserInterestScoreRepository _repo;

    public UserEmbeddingBuilder(IUserInterestScoreRepository repo)
    {
        _repo = repo;
    }

    public async Task<UserEmbeddingVector> Build(int userId)
    {
        var scores = await _repo.GetByUser(userId);

        var vector = new UserEmbeddingVector();

        foreach (var s in scores)
        {
            // 🔥 string → int hash
            var id = s.Key.GetHashCode();

            if (s.Layer == "Team")
                vector.TeamWeights[id] = s.Score;

            if (s.Layer == "League")
                vector.LeagueWeights[id] = s.Score;
        }

        return vector;
    }
}