using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.2) — builds the enriched <see cref="MatchContextData"/>
    /// for a match from existing DB data (match, teams, recent results, H2H). Read-only;
    /// no feed, no AI, no notifications.
    /// </summary>
    public interface IMatchContextBuilder
    {
        Task<MatchContextData?> BuildAsync(int matchId, CancellationToken ct = default);
    }
}
