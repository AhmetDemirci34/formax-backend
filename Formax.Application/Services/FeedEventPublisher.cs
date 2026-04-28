using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Application.Services.Intelligence;

public class FeedEventPublisher : IFeedEventPublisher
{
    private readonly UserWeightLearningService _weightService;
    private readonly IRewardEventProcessor _rewardProcessor;

    public FeedEventPublisher(
        UserWeightLearningService weightService,
        IRewardEventProcessor rewardProcessor)
    {
        _weightService = weightService;
        _rewardProcessor = rewardProcessor;
    }

    public async Task PublishAsync(FeedInteractionEvent evt)
    {
        // 🔥 GLOBAL TREND WRITE
        await _rewardProcessor.ProcessEvent(
        evt.MatchId,
        evt.SignalType.ToString(),
        (int)(evt.DwellTimeSeconds ?? 0)
        );

        // 🔥 PERSONAL LEARNING
        await _weightService.Update(evt.UserId, evt.Reward);
    }
}