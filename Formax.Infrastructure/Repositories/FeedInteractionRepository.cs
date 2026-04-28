using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class FeedInteractionRepository : IFeedInteractionRepository
{
    private readonly FormaxDbContext _context;

    public FeedInteractionRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(FeedInteractionEvent entity)
    {
        _context.FeedInteractionEvents.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<double> GetCtrAsync(double minScore, double maxScore)
    {
        var data = await _context.FeedInteractionEvents
            .Where(x => x.ScoreAtServe >= minScore && x.ScoreAtServe < maxScore)
            .ToListAsync();

        var impressions = data.Count;
        var clicks = data.Count(x => x.SignalType == FeedSignalType.Click);

        if (impressions == 0) return 0;

        return (double)clicks / impressions;
    }

    public async Task<double> GetSkipRateAsync(double minScore, double maxScore)
    {
        var data = await _context.FeedInteractionEvents
            .Where(x => x.ScoreAtServe >= minScore && x.ScoreAtServe < maxScore)
            .ToListAsync();

        var impressions = data.Count;
        var skips = data.Count(x => x.SignalType == FeedSignalType.Skip);

        if (impressions == 0) return 0;

        return (double)skips / impressions;
    }

    public async Task<List<ScoreBucketResult>> GetScoreBucketsAsync()
    {
        var buckets = new List<(double min, double max)>
        {
            (0, 0.2),
            (0.2, 0.4),
            (0.4, 0.6),
            (0.6, 0.8),
            (0.8, 1.0)
        };

        var result = new List<ScoreBucketResult>();

        foreach (var b in buckets)
        {
            var data = await _context.FeedInteractionEvents
                .Where(x => x.ScoreAtServe >= b.min && x.ScoreAtServe < b.max)
                .ToListAsync();

            var bucket = new ScoreBucketResult
            {
                MinScore = b.min,
                MaxScore = b.max,
                Impressions = data.Count,
                Clicks = data.Count(x => x.SignalType == FeedSignalType.Click),
                Skips = data.Count(x => x.SignalType == FeedSignalType.Skip)
            };

            result.Add(bucket);
        }

        return result;
    }

    public async Task<int> CountByEventTypeAsync(FeedSignalType type)
    {
        return await _context.FeedInteractionEvents
            .CountAsync(x => x.SignalType == type);
    }

    public async Task<List<FeedInteractionEvent>> GetAllAsync()
    {
        return await _context.FeedInteractionEvents
            .AsNoTracking()
            .ToListAsync();
    }
}