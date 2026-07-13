using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.1) — default builder. For each match with intelligence, reads its
    /// commentary (and news for context) and emits a <see cref="FeedInsight"/> unless the
    /// commentary is Hidden. Pure assembly: it copies existing fields, computes nothing new.
    /// </summary>
    public sealed class FeedInsightBuilder : IFeedInsightBuilder
    {
        private readonly IMatchIntelligenceRepository _matchRepo;
        private readonly INewsIntelligenceRepository _newsRepo;
        private readonly ICommentaryRepository _commentaryRepo;
        private readonly ILogger<FeedInsightBuilder> _logger;

        public FeedInsightBuilder(
            IMatchIntelligenceRepository matchRepo,
            INewsIntelligenceRepository newsRepo,
            ICommentaryRepository commentaryRepo,
            ILogger<FeedInsightBuilder> logger)
        {
            _matchRepo = matchRepo;
            _newsRepo = newsRepo;
            _commentaryRepo = commentaryRepo;
            _logger = logger;
        }

        public async Task<IReadOnlyList<FeedInsight>> BuildAsync(DateTime fromUtc, CancellationToken ct = default)
        {
            var ids = await _matchRepo.GetMatchIdsFromAsync(fromUtc, ct);
            var insights = new List<FeedInsight>();

            foreach (var id in ids)
            {
                var intelligence = await _matchRepo.GetByMatchIdAsync(id, ct);
                if (intelligence is null) continue;

                var commentary = await _commentaryRepo.GetByMatchIdAsync(id, ct);
                if (commentary is null || commentary.Visibility == CommentaryVisibility.Hidden)
                    continue;

                // News is read as declared input (context); not part of the output fields.
                _ = await _newsRepo.GetByMatchIdAsync(id, ct);

                insights.Add(new FeedInsight
                {
                    MatchId = id,
                    Headline = commentary.Headline,
                    Summary = commentary.Summary,
                    ImportanceScore = intelligence.ImportanceScore,
                    PrimarySignal = intelligence.PrimarySignalType.ToString(),
                    CommentaryTone = commentary.Tone,
                    Visibility = commentary.Visibility
                });
            }

            _logger.LogInformation(
                "[FEED INSIGHT] built {Count} feed insight(s) from {Total} match(es).",
                insights.Count, ids.Count);

            return insights;
        }
    }
}
