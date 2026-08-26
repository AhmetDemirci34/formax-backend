using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.1) — assembles <see cref="FeedInsight"/>s from existing Radar
    /// snapshots (match intelligence + news + commentary). Filters out matches whose
    /// commentary visibility is Hidden. Produces no new intelligence or scores.
    /// </summary>
    public interface IFeedInsightBuilder
    {
        Task<IReadOnlyList<FeedInsight>> BuildAsync(DateTime fromUtc, CancellationToken ct = default);

        /// <summary>
        /// Yalnız verilen maçlar için insight kurar (feed zaten adaylarını biliyor).
        /// Tüm 120 günlük pencereyi taramak yerine kullanılır; çıktı alanları aynıdır.
        /// </summary>
        Task<IReadOnlyList<FeedInsight>> BuildByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default);
    }
}
