using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.4) — default enricher. Reads staged source items
    /// related to the match's two teams and maps them into enrichment facts
    /// (News / InternalSignal / SourceMetadata), then attaches the result to the
    /// context. Read-only bridge: it never mutates staging.
    ///
    /// With no real collectors yet, staging is empty, so the result is empty — the
    /// bridge is live, the data is not.
    /// </summary>
    public sealed class MatchContextEnricher : IMatchContextEnricher
    {
        private const int PerTeamTake = 10;

        private readonly IStagedSourceReader _reader;
        private readonly INewsIntelligenceRepository _newsRepository;
        private readonly Odds.ISyntheticOddsEngine _syntheticOdds;
        private readonly ILogger<MatchContextEnricher> _logger;

        public MatchContextEnricher(
            IStagedSourceReader reader,
            INewsIntelligenceRepository newsRepository,
            Odds.ISyntheticOddsEngine syntheticOdds,
            ILogger<MatchContextEnricher> logger)
        {
            _reader = reader;
            _newsRepository = newsRepository;
            _syntheticOdds = syntheticOdds;
            _logger = logger;
        }

        public async Task<ContextEnrichmentResult> EnrichAsync(
            MatchContextData context, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var items = new List<ContextEnrichmentItem>();

            await CollectForTeamAsync(context.HomeTeamId, items, ct);
            await CollectForTeamAsync(context.AwayTeamId, items, ct);

            var result = items.Count == 0
                ? ContextEnrichmentResult.Empty(now)
                : new ContextEnrichmentResult { Items = items, EnrichedAtUtc = now };

            // Attach to the context so downstream (signal engine, snapshot) can read it.
            context.Enrichment = result;

            // R.10.4 — feed News Intelligence into the context.
            var news = await _newsRepository.GetByMatchIdAsync(context.MatchId, ct);
            if (news is not null)
            {
                context.NewsImpactScore = news.ImpactScore;
                context.NewsImpactLevel = news.ImpactLevel;
                context.NewsCount = news.NewsCount;
            }

            // R.11.4 — synthetic odds signal from internal scores (no real odds).
            // Uses the builder's importance (available now), news impact, and an interest
            // proxy from internal behaviour signals.
            var interestProxy = System.Math.Clamp((result.InternalSignalCount) * 20.0, 0, 100);
            var synth = _syntheticOdds.Evaluate(
                context.MatchId,
                interestProxy,
                context.NewsImpactScore,
                context.Importance.ImportanceScore);
            context.SyntheticSignalScore = synth.SignalScore;
            context.SyntheticSignalLevel = synth.Level;
            context.SyntheticDirection = synth.Direction;

            if (result.HasEnrichment)
            {
                _logger.LogDebug(
                    "[MATCH ENRICH] match {Id}: news={News} internal={Internal} meta={Meta}",
                    context.MatchId, result.NewsCount, result.InternalSignalCount, result.MetadataCount);
            }

            return result;
        }

        private async Task CollectForTeamAsync(
            int teamId, List<ContextEnrichmentItem> items, CancellationToken ct)
        {
            if (teamId <= 0) return;

            var staged = await _reader.GetPendingByTeamAsync(teamId, PerTeamTake, ct);
            foreach (var s in staged)
                items.Add(Map(s, teamId));
        }

        private static ContextEnrichmentItem Map(StagedSourceItem s, int teamId)
            => new()
            {
                Source = s.Category switch
                {
                    SourceCategory.News => ContextEnrichmentSource.News,
                    SourceCategory.Behavior => ContextEnrichmentSource.InternalSignal,
                    _ => ContextEnrichmentSource.SourceMetadata
                },
                SourceKey = s.SourceKey,
                RelatedTeamId = teamId,
                CanonicalName = s.CanonicalName,
                Payload = s.Payload,
                NormalizedAtUtc = s.NormalizedAtUtc
            };
    }
}
