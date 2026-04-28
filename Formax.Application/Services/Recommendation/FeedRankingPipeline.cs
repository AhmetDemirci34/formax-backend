using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Recommendation
{
    public class FeedRankingPipeline
    {
        private readonly RankingEngineV2 _ranking;

        public FeedRankingPipeline()
        {
            _ranking = new RankingEngineV2();
        }

        public Task<List<RecommendationCardDto>> Rank(int userId, List<RecommendationCardDto> matches)
        {
            var result = _ranking.Rank(userId, matches);
            return Task.FromResult(result);
        }
    }
}