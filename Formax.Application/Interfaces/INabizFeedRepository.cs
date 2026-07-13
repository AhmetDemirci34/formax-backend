using Formax.Domain.Entities;

namespace Formax.Application.Interfaces;

public interface INabizFeedRepository
{
    /// <summary>
    /// Returns true if an item with this ContentHash already exists in the DB.
    /// Used for fast deduplication before persistence.
    /// </summary>
    bool HashExists(string contentHash);

    /// <summary>Persist a new feed item (does not save — call SaveChangesAsync after batch).</summary>
    Task AddAsync(MatchSocialFeedItem item, CancellationToken ct = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> items for a match, ranked by
    /// combined relevance (60 %) + freshness (40 %), published within the last 48 h.
    /// Returns an empty list cleanly when no content is available.
    /// </summary>
    List<MatchSocialFeedItem> GetForMatch(int matchId, int limit = 20);

    /// <summary>
    /// Remove items older than <paramref name="cutoff"/> to keep the table lean.
    /// Call once per ingestion cycle (does not save — call SaveChangesAsync after).
    /// </summary>
    Task TrimOldItemsAsync(DateTime cutoff, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
