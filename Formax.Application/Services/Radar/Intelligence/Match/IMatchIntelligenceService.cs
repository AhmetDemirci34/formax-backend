using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.1) — builds importance-signal snapshots for matches.
    /// Pipeline: MatchProfile → MatchSignal → MatchIntelligenceSnapshot. Hardcoded rules
    /// in this sprint. No feed, no AI, no notifications.
    /// </summary>
    public interface IMatchIntelligenceService
    {
        /// <summary>Build and persist the intelligence snapshot for one match.</summary>
        Task<MatchIntelligenceSnapshot?> BuildForMatchAsync(int matchId, CancellationToken ct = default);

        /// <summary>Build snapshots for all matches from the cutoff onward. Returns count built.</summary>
        Task<int> BuildUpcomingAsync(System.DateTime fromUtc, CancellationToken ct = default);
    }
}
