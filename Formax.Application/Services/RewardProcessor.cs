using Formax.Application.Interfaces;

namespace Formax.Application.Services;

public class RewardProcessor : IRewardProcessor
{
    public Task ProcessEvent(int matchId, string eventType, int dwellTime)
    {
        return Task.CompletedTask;
    }
}
