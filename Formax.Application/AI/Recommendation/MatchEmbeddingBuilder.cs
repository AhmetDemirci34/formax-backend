using Formax.Application.Services.Recommendation;

namespace Formax.Application.AI.Recommendation;

public class MatchEmbedding
{
    public int TeamA { get; set; }
    public int TeamB { get; set; }
    public int LeagueId { get; set; }
}

public class MatchEmbeddingBuilder
{
    public MatchEmbedding Build(RankingInput m)
    {
        return new MatchEmbedding
        {
            TeamA = m.HomeTeamName.GetHashCode(),
            TeamB = m.AwayTeamName.GetHashCode(),
            LeagueId = m.LeagueName.GetHashCode()
        };
    }
}