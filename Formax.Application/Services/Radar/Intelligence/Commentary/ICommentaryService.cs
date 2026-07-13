using System;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.Radar.Intelligence.Commentary
{
    /// <summary>
    /// Radar Commentary (R.12.1) — builds and persists deterministic commentary for
    /// matches that have intelligence snapshots. No AI, no feed, no notifications.
    /// </summary>
    public interface ICommentaryService
    {
        /// <summary>Build commentary for matches from the cutoff onward. Returns count written.</summary>
        Task<int> BuildAsync(DateTime fromUtc, CancellationToken ct = default);
    }
}
