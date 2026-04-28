using System.Collections.Generic;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Recommendation;

public class FeedAssembler
{
    private readonly FeedLogService _logService;

    public FeedAssembler(FeedLogService logService)
    {
        _logService = logService;
    }

    public List<FeedItemDto> BuildFeed(int userId, List<RankedMatchResult> rankedMatches)
    {
        var feed = new List<FeedItemDto>();

        for (int i = 0; i < rankedMatches.Count; i++)
        {
            var m = rankedMatches[i];

            var position = i + 1;

            var decisionType =
                position == 1 ? "HOT" :
                position <= 3 ? "RELEVANT" :
                "DISCOVERY";

            var item = new FeedItemDto
            {
                MatchId = m.MatchId,
                Reason = "Trending on Formax",
                Story = "This match is gaining attention on the platform.",
                Score = m.Score
            };

            feed.Add(item);

            // 🔥 LOG (non-blocking)
            _ = _logService.LogAsync(new FeedScoreLog
            {
                UserId = userId,
                MatchId = m.MatchId,

                FinalScore = m.Score,

                RankPosition = position,
                DecisionType = decisionType,

                // sonraki fazda doldurulacak
                WeightSnapshot = ""
            });
        }

        return feed;
    }
}

public class FeedItemDto
{
    public int MatchId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Story { get; set; } = string.Empty;

    public double Score { get; set; }
}