using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation;

public class RecommendationStatService
{
    private readonly FormaxDbContext _db;

    public RecommendationStatService(FormaxDbContext db)
    {
        _db = db;
    }

    public async Task RegisterImpression(int matchId)
    {
        var stat = await GetOrCreate(matchId);

        stat.Impressions++;
        stat.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Batch impression (perf): feed'in TÜM kartları için tek SELECT + tek SaveChanges.
    /// Eskiden controller kart-başına RegisterImpression çağırıyordu → kart başına
    /// 1 SELECT + 1 SaveChanges. Sayaç artışları birebir aynıdır.
    /// </summary>
    public async Task RegisterImpressions(IEnumerable<int> matchIds)
    {
        var ids = matchIds?.Distinct().ToList();
        if (ids is null || ids.Count == 0) return;

        var existing = await _db.MatchRecommendationStats
            .Where(x => ids.Contains(x.MatchId))
            .ToListAsync();

        var byMatch = existing
            .GroupBy(x => x.MatchId)
            .ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.UtcNow;

        foreach (var matchId in ids)
        {
            if (!byMatch.TryGetValue(matchId, out var stat))
            {
                stat = new MatchRecommendationStat
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    Impressions = 0,
                    Clicks = 0,
                    Dwells = 0,
                    Skips = 0,
                    Follows = 0,
                    RewardScore = 0,
                    UpdatedAt = now
                };
                _db.MatchRecommendationStats.Add(stat);
            }

            stat.Impressions++;
            stat.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task RegisterClick(int matchId)
    {
        var stat = await GetOrCreate(matchId);

        stat.Clicks++;
        stat.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task RegisterSkip(int matchId)
    {
        var stat = await GetOrCreate(matchId);

        stat.Skips++;
        stat.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task RegisterDwell(int matchId, int dwellSeconds)
    {
        var stat = await GetOrCreate(matchId);

        stat.Dwells += dwellSeconds;
        stat.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task RegisterFollow(int matchId)
    {
        var stat = await GetOrCreate(matchId);

        stat.Follows++;
        stat.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    private async Task<MatchRecommendationStat> GetOrCreate(int matchId)
    {
        var stat = await _db.MatchRecommendationStats
            .FirstOrDefaultAsync(x => x.MatchId == matchId);

        if (stat != null)
            return stat;

        stat = new MatchRecommendationStat
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            Impressions = 0,
            Clicks = 0,
            Dwells = 0,
            Skips = 0,
            Follows = 0,
            RewardScore = 0,
            UpdatedAt = DateTime.UtcNow
        };

        _db.MatchRecommendationStats.Add(stat);

        return stat;
    }
}