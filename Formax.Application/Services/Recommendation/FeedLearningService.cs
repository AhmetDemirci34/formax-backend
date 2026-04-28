using Formax.Domain.Entities;
using Formax.Application.Interfaces;
using Formax.Application.Services.Intelligence;

namespace Formax.Application.Services.Recommendation;

public class FeedLearningService
{
    private readonly AdaptiveInterestLearningService _interestService;
    private readonly IMatchRewardStatsRepository _matchRewardStatsRepository;
    private readonly SessionMemoryService _sessionMemory;
    private readonly RewardCalculator _rewardCalculator;
    private readonly UserWeightLearningService _weightService;
    private readonly TasteLearningService _tasteService;

    public FeedLearningService(
        AdaptiveInterestLearningService interestService,
        IMatchRewardStatsRepository matchRewardStatsRepository,
        SessionMemoryService sessionMemory,
        UserWeightLearningService weightService,
        TasteLearningService tasteService)
    {
        _interestService = interestService;
        _matchRewardStatsRepository = matchRewardStatsRepository;
        _sessionMemory = sessionMemory;
        _weightService = weightService;
        _tasteService = tasteService;
        _rewardCalculator = new RewardCalculator();
    }

    public async Task ProcessAsync(FeedInteractionEvent e)
    {
        var multi = _rewardCalculator.CalculateMulti(e);
        e.Reward = multi.Total;

        switch (e.SignalType)
        {
            case FeedSignalType.Click:
                await _matchRewardStatsRepository.IncrementClickAsync(e.MatchId);
                break;

            case FeedSignalType.Follow:
                await _matchRewardStatsRepository.IncrementFollowAsync(e.MatchId);
                break;

            case FeedSignalType.Skip:
                await _matchRewardStatsRepository.IncrementSkipAsync(e.MatchId);
                break;
        }

        await _interestService.UpdateFromEvent(e.UserId, e, e.Reward);
        await _weightService.UpdateFromEvent(e.UserId, e);
        await _tasteService.UpdateFromEvent(e);

        await _sessionMemory.UpdateFromEvent(
            e.UserId,
            null,
            null,
            e.SignalType.ToString()
        );
    }

    public async Task RegisterClick(int userId, int matchId)
    {
        await ProcessAsync(new FeedInteractionEvent
        {
            UserId = userId,
            MatchId = matchId,
            SignalType = FeedSignalType.Click,
            EventType = "Click"
        });
    }

    public async Task RegisterSkip(int userId, int matchId)
    {
        await ProcessAsync(new FeedInteractionEvent
        {
            UserId = userId,
            MatchId = matchId,
            SignalType = FeedSignalType.Skip,
            EventType = "Skip"
        });
    }

    public async Task RegisterFollow(int userId, int matchId)
    {
        await ProcessAsync(new FeedInteractionEvent
        {
            UserId = userId,
            MatchId = matchId,
            SignalType = FeedSignalType.Follow,
            EventType = "Follow"
        });
    }

    public async Task RegisterImpression(int userId, int matchId)
    {
        await ProcessAsync(new FeedInteractionEvent
        {
            UserId = userId,
            MatchId = matchId,
            SignalType = FeedSignalType.Impression
        });
    }

    public async Task RegisterDwell(int userId, int matchId, double dwellSeconds)
    {
        await ProcessAsync(new FeedInteractionEvent
        {
            UserId = userId,
            MatchId = matchId,
            SignalType = FeedSignalType.Dwell,
            DwellTimeSeconds = dwellSeconds
        });
    }
}