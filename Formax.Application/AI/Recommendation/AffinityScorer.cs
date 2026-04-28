using Formax.Domain.ValueObjects;

namespace Formax.Application.AI.Recommendation
{
    public class AffinityScorer
    {
        public double Score(UserEmbeddingVector user, MatchEmbedding match)
        {
            double score = 0;

            if (user.TeamWeights.TryGetValue(match.TeamA, out var t1))
                score += t1;

            if (user.TeamWeights.TryGetValue(match.TeamB, out var t2))
                score += t2;

            if (user.LeagueWeights.TryGetValue(match.LeagueId, out var l))
                score += l;

            return score;
        }
    }
}
