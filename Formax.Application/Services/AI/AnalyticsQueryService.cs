using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.DTOs.Analytics;
using Formax.Domain.Entities;

namespace Formax.Application.Services.AI
{
    public class AnalyticsQueryService
    {
        private readonly IFeedInteractionRepository _interactionRepository;

        public AnalyticsQueryService(
            IFeedInteractionRepository interactionRepository)
        {
            _interactionRepository = interactionRepository;
        }

        public async Task<TrainerMetricsResult> GetGlobalMetricsAsync()
        {
            var events = await _interactionRepository.GetAllAsync();

            var impressionMatchIds = events
                .Where(x => x.EventType != null &&
                    x.EventType.Equals("impression", System.StringComparison.OrdinalIgnoreCase))
                .Select(x => x.MatchId)
                .Distinct()
                .ToHashSet();

            var clickMatchIds = events
                .Where(x => x.EventType != null &&
                    x.EventType.Equals("click", System.StringComparison.OrdinalIgnoreCase))
                .Select(x => x.MatchId)
                .Distinct()
                .ToHashSet();

            var skipMatchIds = events
                .Where(x => x.EventType != null &&
                    x.EventType.Equals("skip", System.StringComparison.OrdinalIgnoreCase))
                .Select(x => x.MatchId)
                .Distinct()
                .ToHashSet();

            var followMatchIds = events
                .Where(x => x.EventType != null &&
                    x.EventType.Equals("follow", System.StringComparison.OrdinalIgnoreCase))
                .Select(x => x.MatchId)
                .Distinct()
                .ToHashSet();

            var impressions = impressionMatchIds.Count;

            var clicks = clickMatchIds.Count(id => impressionMatchIds.Contains(id));

            // 🔥 RULE: click varsa skip sayılmaz
            var skips = skipMatchIds
                .Where(id => impressionMatchIds.Contains(id) && !clickMatchIds.Contains(id))
                .Count();

            var follows = followMatchIds.Count(id => impressionMatchIds.Contains(id));

            return new TrainerMetricsResult
            {
                TotalImpressions = impressions,
                TotalClicks = clicks,
                TotalSkips = skips,
                TotalFollows = follows,

                CTR = impressions == 0 ? 0 : (double)clicks / impressions,
                SkipRate = impressions == 0 ? 0 : (double)skips / impressions,
                FollowRate = impressions == 0 ? 0 : (double)follows / impressions
            };
        }

        public async Task<List<MatchPerformanceDto>> GetTopMatchesAsync(int top = 50)
        {
            var events = await _interactionRepository.GetAllAsync();

            return events
                .GroupBy(x => x.MatchId)
                .Select(g =>
                {
                    var hasImpression = g.Any(x =>
                        x.EventType != null &&
                        x.EventType.Equals("impression", System.StringComparison.OrdinalIgnoreCase));

                    var hasClick = g.Any(x =>
                        x.EventType != null &&
                        x.EventType.Equals("click", System.StringComparison.OrdinalIgnoreCase));

                    var hasSkip = g.Any(x =>
                        x.EventType != null &&
                        x.EventType.Equals("skip", System.StringComparison.OrdinalIgnoreCase));

                    var hasFollow = g.Any(x =>
                        x.EventType != null &&
                        x.EventType.Equals("follow", System.StringComparison.OrdinalIgnoreCase));

                    var impressions = hasImpression ? 1 : 0;

                    // 🔥 RULE: click varsa skip yok sayılır
                    var clicks = hasClick ? 1 : 0;
                    var skips = (!hasClick && hasSkip) ? 1 : 0;
                    var follows = hasFollow ? 1 : 0;

                    return new MatchPerformanceDto
                    {
                        MatchId = g.Key,

                        Impressions = impressions,
                        Clicks = impressions == 1 ? clicks : 0,
                        Skips = impressions == 1 ? skips : 0,
                        Follows = impressions == 1 ? follows : 0,

                        CTR = impressions == 0 ? 0 : (double)clicks / impressions,
                        SkipRate = impressions == 0 ? 0 : (double)skips / impressions
                    };
                })
                .OrderByDescending(x => x.CTR)
                .Take(top)
                .ToList();
        }
    }
}