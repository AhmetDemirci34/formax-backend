using Formax.Application.Interfaces;
using Formax.Application.Services.Nabiz;
using Formax.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Infrastructure.BackgroundJobs;

/// <summary>
/// Sprint 4 — NABIZ ingestion background job.
///
/// Cadence: every 15 minutes.
/// Flow:
///   1. Fetch all RSS items via INabizFeedFetcher
///   2. Trim DB items older than 48 h
///   3. For each raw item:
///      a. Compute SHA-256 content hash (Source + Headline + PublishedAt)
///      b. Skip if hash already exists (deduplication)
///      c. Run relevance engine against upcoming/live matches (±24 h window)
///      d. Persist item (MatchId may be null if no match qualifies)
///   4. Save all changes in one transaction
/// </summary>
public class NabizIngestionJob : BackgroundService
{
    private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NabizIngestionJob> _logger;

    public NabizIngestionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<NabizIngestionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[NABIZ] Ingestion job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NABIZ] Ingestion cycle failed — will retry in {Delay}.", LoopDelay);
            }

            await Task.Delay(LoopDelay, stoppingToken);
        }

        _logger.LogInformation("[NABIZ] Ingestion job stopped.");
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var fetcher         = scope.ServiceProvider.GetRequiredService<INabizFeedFetcher>();
        var feedRepo        = scope.ServiceProvider.GetRequiredService<INabizFeedRepository>();
        var matchReadRepo   = scope.ServiceProvider.GetRequiredService<IMatchReadRepository>();
        var teamReadRepo    = scope.ServiceProvider.GetRequiredService<ITeamReadRepository>();
        var relevanceEngine = scope.ServiceProvider.GetRequiredService<NabizRelevanceEngine>();

        // 1. Fetch raw items from all configured RSS sources
        var rawItems = await fetcher.FetchAllAsync(ct);

        _logger.LogDebug("[NABIZ] Fetched {Count} raw item(s).", rawItems.Count);

        if (rawItems.Count == 0)
            return;

        // 2. Trim items older than 48 h (housekeeping)
        var cutoff = DateTime.UtcNow.AddHours(-48);
        await feedRepo.TrimOldItemsAsync(cutoff, ct);

        // 3. Load candidate matches: upcoming or live within ±24 h window
        var windowStart = DateTime.UtcNow.AddHours(-3);    // include recently finished
        var windowEnd   = DateTime.UtcNow.AddHours(24);    // include tomorrow's fixtures
        var candidates  = matchReadRepo.GetUpcomingMatches(windowStart, windowEnd);

        // Build teamId → teamName lookup for relevance engine
        var teamIds   = candidates.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).Distinct();
        var teamNames = teamIds.ToDictionary(
            id => id,
            id => teamReadRepo.GetById(id)?.Name ?? string.Empty);

        // 4. Ingest each item
        var added = 0;
        var skipped = 0;

        // Intra-batch dedup guard. HashExists() only queries the DB, so it cannot
        // see items already staged (AddAsync) but not yet committed in THIS cycle.
        // A raw batch that contains the same article twice (repeated RSS entries,
        // or the same story syndicated across two sources) would pass HashExists
        // for both, stage two rows with an identical ContentHash, and blow up the
        // whole cycle at SaveChangesAsync with SqlException 2601 (unique index
        // IX_MatchSocialFeedItems_ContentHash) — rolling back every item and
        // leaving the table permanently empty. Track hashes seen this cycle too.
        var seenHashes = new HashSet<string>();

        foreach (var raw in rawItems)
        {
            ct.ThrowIfCancellationRequested();

            // a. Compute deduplication hash.
            // PATCH: SourceUrl is the stable canonical identifier for an article.
            // Using Headline + PublishedAt was brittle: headline corrections
            // (common in SEO publishing) would regenerate the hash and create
            // duplicate rows for the same article.
            // Fallback to Headline + PublishedAt only when no URL is available.
            var hashInput = !string.IsNullOrWhiteSpace(raw.SourceUrl)
                ? $"{raw.Source}|{raw.SourceUrl}"
                : $"{raw.Source}|{raw.Headline}|{raw.PublishedAt:O}";
            var hash = ComputeSha256(hashInput);

            // b. Skip duplicates — already in DB, or already staged this cycle.
            if (feedRepo.HashExists(hash) || !seenHashes.Add(hash))
            {
                skipped++;
                continue;
            }

            // c. Relevance engine
            var (matchId, relevanceScore) =
                relevanceEngine.FindBestMatch(raw, candidates, teamNames);

            // d. Persist — items with no match link are still stored (relevanceScore = 0)
            //    They can be surfaced in a "general news" feed in Phase B.
            var entity = new MatchSocialFeedItem
            {
                MatchId        = matchId,
                TeamId         = null,            // Phase B: derive from relevance result
                Source         = raw.Source,
                SourceType     = raw.SourceType,
                Author         = raw.Author,
                AuthorVerified = raw.AuthorVerified,
                Headline       = raw.Headline,
                Summary        = raw.Summary,
                ImageUrl       = raw.ImageUrl,
                SourceUrl      = raw.SourceUrl,
                PublishedAt    = raw.PublishedAt,
                SentimentScore = null,            // Phase B: ML sentiment
                RelevanceScore = relevanceScore,
                ContentHash    = hash,
                CreatedAt      = DateTime.UtcNow
            };

            await feedRepo.AddAsync(entity, ct);
            added++;
        }

        // 5. Commit
        await feedRepo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[NABIZ] Cycle complete — added {Added}, skipped {Skipped} (duplicates) out of {Total} raw item(s).",
            added, skipped, rawItems.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
