using System;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.1) — reads staged news items, links them to matches
    /// via team mentions, and writes per-match news snapshots. Deterministic; no AI,
    /// no feed, no commentary.
    /// </summary>
    public interface INewsIntelligenceService
    {
        /// <summary>Process pending news and rebuild snapshots for matches from the
        /// cutoff onward. Returns the number of match snapshots written.</summary>
        Task<int> BuildAsync(DateTime fromUtc, CancellationToken ct = default);
    }
}
