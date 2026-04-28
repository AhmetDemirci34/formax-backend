using Formax.Domain.Entities;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.AI;

public class TrainerMetricsCalculator
{
    private readonly IFeedInteractionRepository _repository;

    public TrainerMetricsCalculator(IFeedInteractionRepository repository)
    {
        _repository = repository;
    }

    public async Task<double> CalculateCTR()
    {
        var impressions = await _repository.CountByEventTypeAsync(FeedSignalType.Impression);
        var clicks = await _repository.CountByEventTypeAsync(FeedSignalType.Click);

        if (impressions == 0)
            return 0;

        return (double)clicks / impressions;
    }

    public async Task<double> CalculateSkipRate()
    {
        var impressions = await _repository.CountByEventTypeAsync(FeedSignalType.Impression);
        var skips = await _repository.CountByEventTypeAsync(FeedSignalType.Skip);

        if (impressions == 0)
            return 0;

        return (double)skips / impressions;
    }

    public async Task<double> CalculateFollowRate()
    {
        var impressions = await _repository.CountByEventTypeAsync(FeedSignalType.Impression);
        var follows = await _repository.CountByEventTypeAsync(FeedSignalType.Follow);

        if (impressions == 0)
            return 0;

        return (double)follows / impressions;
    }
}