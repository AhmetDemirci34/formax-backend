using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.1) — default builder. For each match with intelligence, reads its
    /// commentary and emits a <see cref="FeedInsight"/> unless the commentary is Hidden.
    /// Pure assembly: it copies existing fields, computes nothing new.
    ///
    /// PERF (MVP freeze): eskiden maç-başına 3 ayrı sorgu atılıyordu (intelligence +
    /// commentary + kullanılmayan news) → 120 günlük pencerede ~13.6k maç × 3 ≈ 41k
    /// round-trip. Artık iki toplu sorgu (intelligence + commentary) ile aynı çıktı
    /// üretilir. Filtreleme/alan eşlemesi DEĞİŞMEDİ.
    /// </summary>
    public sealed class FeedInsightBuilder : IFeedInsightBuilder
    {
        private readonly IMatchIntelligenceRepository _matchRepo;
        private readonly ICommentaryRepository _commentaryRepo;
        private readonly ILogger<FeedInsightBuilder> _logger;

        public FeedInsightBuilder(
            IMatchIntelligenceRepository matchRepo,
            ICommentaryRepository commentaryRepo,
            ILogger<FeedInsightBuilder> logger)
        {
            _matchRepo = matchRepo;
            _commentaryRepo = commentaryRepo;
            _logger = logger;
        }

        public async Task<IReadOnlyList<FeedInsight>> BuildAsync(DateTime fromUtc, CancellationToken ct = default)
        {
            // Pencerede intelligence'ı OLAN maçlar zaten tek sorguda (hafif projeksiyonla) gelir;
            // intelligence'ı olmayan maçlar eskiden de eleniyordu → çıktı kümesi aynı.
            var intelligenceById = await _matchRepo.GetFeedRowsFromAsync(fromUtc, ct);
            var ids = intelligenceById.Keys.ToList();

            var commentaryById = await _commentaryRepo.GetByMatchIdsAsync(ids, ct);

            var insights = new List<FeedInsight>();

            foreach (var id in ids)
            {
                if (!commentaryById.TryGetValue(id, out var commentary)) continue;
                if (commentary.Visibility == CommentaryVisibility.Hidden) continue;

                var intelligence = intelligenceById[id];

                insights.Add(new FeedInsight
                {
                    MatchId = id,
                    Headline = commentary.Headline,
                    Summary = commentary.Summary,
                    ImportanceScore = intelligence.ImportanceScore,
                    PrimarySignal = intelligence.PrimarySignal,
                    CommentaryTone = commentary.Tone,
                    Visibility = commentary.Visibility
                });
            }

            _logger.LogInformation(
                "[FEED INSIGHT] built {Count} feed insight(s) from {Total} match(es).",
                insights.Count, ids.Count);

            return insights;
        }

        public async Task<IReadOnlyList<FeedInsight>> BuildByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default)
        {
            if (matchIds is null || matchIds.Count == 0)
                return Array.Empty<FeedInsight>();

            return await AssembleAsync(matchIds.ToList(), ct);
        }

        // Tek montaj yolu — hem tam pencere hem id-bazlı çağrı buradan geçer.
        private async Task<List<FeedInsight>> AssembleAsync(
            IReadOnlyList<int> ids, CancellationToken ct)
        {
            var insights = new List<FeedInsight>();
            if (ids.Count == 0) return insights;

            var intelligenceById = await _matchRepo.GetByMatchIdsAsync(ids, ct);
            var commentaryById = await _commentaryRepo.GetByMatchIdsAsync(ids, ct);

            foreach (var id in ids)
            {
                if (!intelligenceById.TryGetValue(id, out var intelligence)) continue;
                if (!commentaryById.TryGetValue(id, out var commentary)) continue;
                if (commentary.Visibility == CommentaryVisibility.Hidden) continue;

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

            return insights;
        }
    }
}
