using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.1) — default service. Pipeline:
    /// staged News items → team mentions (matcher) → team's matches → per-match
    /// aggregation → NewsIntelligenceSnapshot. Read-only over staging (no Processed flip).
    /// </summary>
    public sealed class NewsIntelligenceService : INewsIntelligenceService
    {
        private const int NewsBatchTake = 500;

        private readonly IStagedSourceReader _stagedReader;
        private readonly INewsMatcher _matcher;
        private readonly INewsClassifier _classifier;
        private readonly INewsImpactEngine _impactEngine;
        private readonly INewsIntelligenceRepository _repository;
        private readonly ILogger<NewsIntelligenceService> _logger;

        public NewsIntelligenceService(
            IStagedSourceReader stagedReader,
            INewsMatcher matcher,
            INewsClassifier classifier,
            INewsImpactEngine impactEngine,
            INewsIntelligenceRepository repository,
            ILogger<NewsIntelligenceService> logger)
        {
            _stagedReader = stagedReader;
            _matcher = matcher;
            _classifier = classifier;
            _impactEngine = impactEngine;
            _repository = repository;
            _logger = logger;
        }

        public async Task<int> BuildAsync(DateTime fromUtc, CancellationToken ct = default)
        {
            var news = await _stagedReader.GetPendingByCategoryAsync(SourceCategory.News, NewsBatchTake, ct);
            if (news.Count == 0)
            {
                _logger.LogDebug("[NEWS INTEL] no pending news.");
                return 0;
            }

            // Aggregate per match.
            var agg = new Dictionary<int, MatchNewsAgg>();
            // Cache team→matches to avoid repeated lookups.
            var teamMatchCache = new Dictionary<int, IReadOnlyList<TeamMatchRef>>();

            foreach (var item in news)
            {
                var mentions = _matcher.DetectMentions(item);
                if (mentions.Count == 0) continue;

                // R.10.2 — one deterministic category per news item.
                var classification = _classifier.Classify(item);

                // Matches this news links to (dedup per news item).
                var linkedMatches = new Dictionary<int, string>();

                foreach (var mention in mentions)
                {
                    if (!teamMatchCache.TryGetValue(mention.TeamId, out var matches))
                    {
                        matches = await _repository.GetMatchesByTeamFromAsync(mention.TeamId, fromUtc, ct);
                        teamMatchCache[mention.TeamId] = matches;
                    }

                    foreach (var m in matches)
                        linkedMatches[m.MatchId] = m.League;
                }

                foreach (var (matchId, league) in linkedMatches)
                {
                    if (!agg.TryGetValue(matchId, out var a))
                    {
                        a = new MatchNewsAgg();
                        agg[matchId] = a;
                    }

                    a.NewsCount++; // one news item counted once per match
                    a.Categories[classification.Category] =
                        a.Categories.GetValueOrDefault(classification.Category) + 1;
                    foreach (var mention in mentions)
                        a.Teams.Add(mention.TeamName);
                    if (!string.IsNullOrWhiteSpace(league))
                        a.Leagues.Add(league);
                    if (a.LastNewsAtUtc is null || item.NormalizedAtUtc > a.LastNewsAtUtc)
                        a.LastNewsAtUtc = item.NormalizedAtUtc;
                }
            }

            var now = DateTime.UtcNow;
            foreach (var (matchId, a) in agg)
            {
                // R.10.3 — deterministic news impact from the category breakdown.
                var impact = _impactEngine.Evaluate(a.Categories);

                await _repository.UpsertAsync(new NewsIntelligenceSnapshot
                {
                    MatchId = matchId,
                    NewsCount = a.NewsCount,
                    MentionedTeams = JsonSerializer.Serialize(a.Teams.OrderBy(x => x).ToList()),
                    MentionedLeagues = JsonSerializer.Serialize(a.Leagues.OrderBy(x => x).ToList()),
                    LastNewsAtUtc = a.LastNewsAtUtc,
                    CategoryBreakdown = JsonSerializer.Serialize(
                        a.Categories.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)),
                    ImpactScore = impact.Score.Value,
                    ImpactLevel = impact.Score.Level,
                    GeneratedAtUtc = now
                }, ct);
            }
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[NEWS INTEL] processed {News} news → {Matches} match snapshot(s).",
                news.Count, agg.Count);

            return agg.Count;
        }

        private sealed class MatchNewsAgg
        {
            public int NewsCount;
            public readonly HashSet<string> Teams = new();
            public readonly HashSet<string> Leagues = new();
            public readonly Dictionary<NewsCategory, int> Categories = new();
            public DateTime? LastNewsAtUtc;
        }
    }
}
