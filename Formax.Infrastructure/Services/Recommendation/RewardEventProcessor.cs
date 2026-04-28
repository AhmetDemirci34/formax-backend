using Formax.Application.Interfaces;
using Formax.Application.Services.Recommendation;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Services.Recommendation;

public class RewardEventProcessor : IRewardEventProcessor
{
    private readonly FormaxDbContext _db;
    private readonly RewardCalculator _calculator;
    private readonly IMatchRewardStatsRepository _rewardStatsRepo;

    public RewardEventProcessor(
        FormaxDbContext db,
        RewardCalculator calculator,
        IMatchRewardStatsRepository rewardStatsRepo)
    {
        _db = db;
        _calculator = calculator;
        _rewardStatsRepo = rewardStatsRepo;
    }

    public async Task ProcessEvent(
        int matchId,
        string eventType,
        int dwellSeconds = 0)
    {
        // 🔥 FIX (NULL SAFE)
        eventType = (eventType ?? "").ToLowerInvariant();

        var stat = await _db.MatchRecommendationStats
            .FirstOrDefaultAsync(x => x.MatchId == matchId);

        if (stat == null)
        {
            stat = new MatchRecommendationStat
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UpdatedAt = DateTime.UtcNow
            };

            _db.MatchRecommendationStats.Add(stat);
        }

        // 🔥 GLOBAL IMPRESSION
        stat.Impressions++;
        await _rewardStatsRepo.IncrementImpressionAsync(matchId);

        switch (eventType)
        {
            case "impression":
                stat.Impressions++;
                await _rewardStatsRepo.IncrementImpressionAsync(matchId);
                break;

            case "click":
                stat.Clicks++;
                await _rewardStatsRepo.IncrementClickAsync(matchId);
                break;

            case "skip":
                stat.Skips++;
                await _rewardStatsRepo.IncrementSkipAsync(matchId);
                break;

            case "follow":
                stat.Follows++;
                await _rewardStatsRepo.IncrementFollowAsync(matchId);
                break;

            case "dwell":
                stat.Dwells += dwellSeconds;
                await _rewardStatsRepo.IncrementOpenAsync(matchId);
                break;
        }

        double ctr = stat.Impressions > 0
            ? (double)stat.Clicks / stat.Impressions
            : 0;

        double skipRate = stat.Impressions > 0
            ? (double)stat.Skips / stat.Impressions
            : 0;

        ctr = Math.Clamp(ctr, 0, 1);
        skipRate = Math.Clamp(skipRate, 0, 1);

        var reward = _calculator.CalculateFinalScore(
            ctr,
            skipRate,
            eventType == "click",
            eventType == "follow"
        );

        stat.LastReward = reward;
        stat.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}