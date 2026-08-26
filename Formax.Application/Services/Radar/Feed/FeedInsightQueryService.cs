using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.2) — default query service. Uses the builder (which already
    /// excludes Hidden), orders by ImportanceScore DESC, takes the first N, and projects
    /// to DTOs.
    /// </summary>
    public sealed class FeedInsightQueryService : IFeedInsightQueryService
    {
        private const int DefaultLookbackDays = 120;

        private readonly IFeedInsightBuilder _builder;

        public FeedInsightQueryService(IFeedInsightBuilder builder)
        {
            _builder = builder;
        }

        public async Task<IReadOnlyList<FeedInsightDto>> GetFeedAsync(int limit = 50, CancellationToken ct = default)
        {
            if (limit <= 0) limit = 50;

            var insights = await _builder.BuildAsync(DateTime.UtcNow.AddDays(-DefaultLookbackDays), ct);

            return Project(insights, limit);
        }

        public async Task<IReadOnlyList<FeedInsightDto>> GetFeedByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default)
        {
            if (matchIds is null || matchIds.Count == 0)
                return Array.Empty<FeedInsightDto>();

            var insights = await _builder.BuildByMatchIdsAsync(matchIds, ct);

            return Project(insights, insights.Count);
        }

        // Ortak sıralama + projeksiyon (davranış birebir korunur).
        private static List<FeedInsightDto> Project(IReadOnlyList<FeedInsight> insights, int limit)
            => insights
                .OrderByDescending(i => i.ImportanceScore)
                .ThenBy(i => i.MatchId)
                .Take(limit)
                .Select(i => new FeedInsightDto
                {
                    MatchId = i.MatchId,
                    Headline = i.Headline,
                    Summary = i.Summary,
                    ImportanceScore = i.ImportanceScore,
                    PrimarySignal = i.PrimarySignal,
                    CommentaryTone = i.CommentaryTone.ToString(),
                    Visibility = i.Visibility.ToString()
                })
                .ToList();
    }
}
