using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

/// <summary>
/// Persistence layer for MatchSocialFeedItem (NABIZ engine).
///
/// Ranking formula: RelevanceScore * 0.6 + FreshnessScore * 0.4
///   FreshnessScore = 1 - (hoursOld / 48)  clamped to [0, 1]
/// Items older than 48 h are excluded.
/// </summary>
public class NabizFeedRepository : INabizFeedRepository
{
    private readonly FormaxDbContext _context;

    public NabizFeedRepository(FormaxDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public bool HashExists(string contentHash)
        => _context.MatchSocialFeedItems
                   .AsNoTracking()
                   .Any(x => x.ContentHash == contentHash);

    /// <inheritdoc />
    public Task AddAsync(MatchSocialFeedItem item, CancellationToken ct = default)
    {
        _context.MatchSocialFeedItems.Add(item);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public List<MatchSocialFeedItem> GetForMatch(int matchId, int limit = 20)
    {
        var cutoff = DateTime.UtcNow.AddHours(-48);

        return _context.MatchSocialFeedItems
            .AsNoTracking()
            .Where(x => x.MatchId == matchId && x.PublishedAt >= cutoff)
            .AsEnumerable()
            .Select(x =>
            {
                // compute combined score in memory (no SQL translation required)
                var hoursOld      = (DateTime.UtcNow - x.PublishedAt).TotalHours;
                var freshness     = Math.Max(0.0, 1.0 - hoursOld / 48.0);
                var combined      = x.RelevanceScore * 0.6 + freshness * 0.4;
                return (Item: x, Score: combined);
            })
            .OrderByDescending(t => t.Score)
            .Take(limit)
            .Select(t => t.Item)
            .ToList();
    }

    /// <inheritdoc />
    public Task TrimOldItemsAsync(DateTime cutoff, CancellationToken ct = default)
    {
        // ExecuteDeleteAsync (EF 7+) — avoids loading rows into memory
        return _context.MatchSocialFeedItems
                       .Where(x => x.PublishedAt < cutoff)
                       .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
