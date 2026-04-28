using Formax.Domain.Entities;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.AI;

public class SelfLearningTrainerService
{
    private readonly IFeedInteractionRepository _repository;

    public SelfLearningTrainerService(IFeedInteractionRepository repository)
    {
        _repository = repository;
    }

    public async Task<double> CalculateGlobalCTR()
    {
        var impressions = await _repository.CountByEventTypeAsync(FeedSignalType.Impression);
        var clicks = await _repository.CountByEventTypeAsync(FeedSignalType.Click);

        if (impressions == 0)
            return 0;

        return (double)clicks / impressions;
    }
}